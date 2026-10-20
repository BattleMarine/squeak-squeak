#if UNITY_EDITOR || SQUEAK_SAVE_VALIDATION
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;
using SqueakSqueak.Cleaning;
using SqueakSqueak.Experiment;

namespace SqueakSqueak.Saving
{
    // 04a 경계 전용. 정식 Player에는 기존 probe와 함께 컴파일되지 않습니다.
    public sealed partial class SavePlayerProbe
    {
        private FinalizationFiles finalizationFiles;
        private void ReleaseFinalizationGate()=>finalizationFiles?.Release();
        private void OnDestroy(){ReleaseFinalizationGate();ReleaseIntegrationGate();}

        private IEnumerator RunFinalization()
        {
            while(!c.SaveReady){if(c.LastError!=null)throw new Exception(c.LastError);yield return null;}
            while(c.PersistenceBusy)yield return null;
            var identityPath=Path.Combine(directory,"finalization-identity.txt");
            if(phase=="finalization-reload")
            {
                Check(c.StoredResults.Count==1,"one final result loaded in fresh domain/process");
                var value=c.StoredResults[0];
                Check(File.ReadAllText(identityPath)==value.ResultId+" "+Digest(value),"persisted ID/time/all arrays bit-exact after restart");
                Check(c.ViewStoredResult(value.ResultId)&&c.State==ExperimentState.Viewing,"restored final result opens read-only");
                CheckMasks(value);var elapsed=tracker.ElapsedSeconds;var revision=surface.SurfaceMask.Revision;
                PulseFinalizationClock(20);
                Check(!surface.ApplyCleaning(1)&&!surface.ApplyDroplet(default,.2f,Time.frameCount)&&!c.SelectTool(2),"reloaded result blocks clean/drop/tool mutation");
                yield return new WaitForSecondsRealtime(.3f);
                Check(tracker.ElapsedSeconds==elapsed&&surface.SurfaceMask.Revision==revision,"reloaded observation excludes time and surface changes");
                Check(c.RequestCoin(0)&&surface.SurfaceMask.CurrentDirtSum==surface.SurfaceMask.InitialDirtSum&&tracker.ElapsedSeconds==0,"reloaded view -> initial new round/reset clock");
                FinalizationRub();PulseFinalizationClock(1);
                Check(File.ReadAllText(identityPath)==value.ResultId+" "+Digest(value),"new round cannot mutate loaded snapshot");
                Finish("PASS finalization-reload checks="+checks.Count);yield break;
            }

            Check(c.StoredResults.Count==0&&c.RequestCoin(0),"fresh finalization round");
            var round=c.RoundId;var goals=0;var storedEvents=0;
            c.GoalReached+=_=>goals++;c.ResultStored+=_=>storedEvents++;
            Check(!tracker.HasStarted&&tracker.ElapsedSeconds==0,"no timing before valid work");
            Check(c.SelectTool(2)&&surface.ApplyDroplet(FinalizationContact(CoinSurfaceFace.Front),.2f,Time.frameCount),"valid product droplet starts work");
            Check(tracker.HasStarted,"treatment starts existing tracker");PulseFinalizationClock(2);
            foreach(var dirt in c.Dirt)dirt.ApplyCleaning(100);
            foreach(var face in new[]{CoinSurfaceFace.Front,CoinSurfaceFace.Back})
                for(var i=0;i<surface.SurfaceMask.TexelCount;i++)
                    surface.SurfaceMask.TrySetDirt(face,i,surface.SurfaceMask.GetDirt(face,i)*.04f);
            surface.UploadSurfaceMasks();c.EvaluateGoal();
            var goal=FinalizationSample();
            Check(c.State==ExperimentState.Ready&&goals==1&&c.CurrentResult==null&&c.PendingResultId==null&&storedEvents==0,"goal notification is not final result");
            Check(surface.ProcessingAllowed&&!tracker.IsTimingPaused&&!tracker.IsTimingStopped,"goal preserves work and timer");
            yield return null;
            Check(c.SelectTool(2)&&surface.ApplyDroplet(FinalizationContact(CoinSurfaceFace.Back),.2f,Time.frameCount),"post-goal product droplet allowed");
            FinalizationRub();PulseFinalizationClock(3);
            Check(!SameFinalizationArrays(goal,FinalizationSample())&&tracker.ElapsedSeconds>goal.ElapsedSeconds+2,"post-goal arrays and focused work time advance");

            Check(c.RequestStore(round)&&c.PendingResultId==null,"opening confirmation pauses without snapshot");
            var modal=FinalizationSample();var modalStart=Time.realtimeSinceStartupAsDouble;var modalFrame=Time.frameCount;
            PulseFinalizationClock(20);yield return new WaitForSecondsRealtime(.3f);
            Check(tracker.IsTimingPaused&&tracker.ElapsedSeconds==modal.ElapsedSeconds&&Time.frameCount>modalFrame&&Time.realtimeSinceStartupAsDouble>modalStart+.2,"confirmation real frames and focused synthetic wait excluded");
            Check(!surface.ApplyDroplet(default,.2f,Time.frameCount)&&!surface.ApplyCleaning(1),"confirmation blocks treatment and cleaning");
            Check(c.CancelDialog(round)&&c.RoundId==round&&c.State==ExperimentState.Ready&&SameFinalizationArrays(modal,FinalizationSample())&&tracker.ElapsedSeconds==modal.ElapsedSeconds,"confirmation cancel preserves round/arrays/time");
            PulseFinalizationClock(2);FinalizationRub();

            finalizationFiles=new FinalizationFiles(true);c.UseValidationFiles(finalizationFiles);
            Check(c.RequestStore(round)&&c.ConfirmDialog(round),"precommit failure candidate begins");
            var cancelledId=c.PendingResultId;var failed=FinalizationSample();
            while(!finalizationFiles.WriteEntered.IsSet)yield return null;
            PulseFinalizationClock(20);yield return new WaitForSecondsRealtime(.3f);
            Check(c.State==ExperimentState.Saving&&c.CurrentResult==null&&storedEvents==0&&tracker.ElapsedSeconds==failed.ElapsedSeconds,"write wait is paused and not Stored");
            finalizationFiles.WriteRelease.Set();while(c.PersistenceBusy)yield return null;
            Check(c.State==ExperimentState.SaveFailed&&c.PendingResultId==cancelledId&&!File.Exists(Path.Combine(directory,CompletedCoinStore.FileName)),"known precommit failure keeps candidate without file");
            PulseFinalizationClock(20);yield return new WaitForSecondsRealtime(.3f);
            Check(tracker.ElapsedSeconds==failed.ElapsedSeconds&&SameFinalizationArrays(failed,FinalizationSample()),"error wait excludes time and preserves surface");
            Check(c.CancelFailedStore()&&c.RoundId==round&&c.PendingResultId==null&&c.State==ExperimentState.Ready&&tracker.ElapsedSeconds==failed.ElapsedSeconds,"safe failure cancel resumes same work/time");
            yield return null;
            Check(c.SelectTool(2)&&surface.ApplyDroplet(FinalizationContact(CoinSurfaceFace.Back),.2f,Time.frameCount),"failure cancel allows further product treatment");
            FinalizationRub();PulseFinalizationClock(4);

            finalizationFiles=new FinalizationFiles(false);c.UseValidationFiles(finalizationFiles);
            Check(c.RequestStore(round),"latest final confirmation opens");
            var confirmed=FinalizationSample();PulseFinalizationClock(20);
            Check(c.ConfirmDialog(round)&&c.PendingResultId!=cancelledId,"new final confirmation freezes latest state with new ID");
            var finalId=c.PendingResultId;
            while(!finalizationFiles.WriteEntered.IsSet)yield return null;
            Check(confirmed.ElapsedSeconds>failed.ElapsedSeconds+3&&!SameFinalizationArrays(confirmed,failed),"final candidate differs from cancelled candidate");
            Check(!SameFinalizationArrays(goal,confirmed)&&confirmed.ElapsedSeconds>goal.ElapsedSeconds+8,"final candidate differs from goal-time arrays/time");
            Check(!surface.ApplyCleaning(1)&&!surface.ApplyDroplet(default,.2f,Time.frameCount)&&!c.SelectTool(0),"saving blocks all product mutations");
            // 시험 원본을 직접 바꿔 후보의 배열 소유권을 공격합니다. 제품 입력 우회는 이 probe 안에만 있습니다.
            foreach(var face in new[]{CoinSurfaceFace.Front,CoinSurfaceFace.Back})
                for(var i=0;i<surface.SurfaceMask.TexelCount;i++)surface.SurfaceMask.TrySetDirt(face,i,0);
            surface.UploadSurfaceMasks();
            finalizationFiles.WriteRelease.Set();while(!finalizationFiles.ReadbackEntered.IsSet)yield return null;
            var readbackFrame=Time.frameCount;PulseFinalizationClock(20);yield return new WaitForSecondsRealtime(.3f);
            Check(File.Exists(Path.Combine(directory,CompletedCoinStore.FileName))&&c.State==ExperimentState.Saving&&storedEvents==0&&c.StoredResults.Count==0&&c.CurrentResult==null,"file commit alone is not Stored before same-ID readback");
            Check(Time.frameCount>readbackFrame&&tracker.ElapsedSeconds==confirmed.ElapsedSeconds,"readback wait advances frames but not work time");
            finalizationFiles.ReadbackRelease.Set();while(c.PersistenceBusy)yield return null;
            Check(c.State==ExperimentState.Stored&&storedEvents==1&&c.StoredResults.Count==1&&c.CurrentResult.ResultId==finalId,"readback success confirms result/event");
            var result=c.CurrentResult;var digest=Digest(result);
            Check(SameFinalizationArrays(result,confirmed)&&result.ElapsedSeconds==confirmed.ElapsedSeconds,"final saved snapshot owns exact confirmation arrays/time");
            Check(result.A.All(x=>x==0)&&result.FrontPolish.All(x=>x==0)&&result.BackPolish.All(x=>x==0),"A complete and no unapproved polish");
            Check(c.StoredResults[0].ResultId==result.ResultId&&Digest(c.StoredResults[0])==digest,"actual file readback and pending snapshot identical");
            Check(tracker.IsTimingStopped&&tracker.IsTimingPaused&&pointer.enabled&&!surface.ProcessingAllowed,"stored result allows observation only");
            PulseFinalizationClock(20);yield return new WaitForSecondsRealtime(.3f);
            Check(tracker.ElapsedSeconds==confirmed.ElapsedSeconds&&Digest(result)==digest,"Stored time and snapshot immutable");
            Check(c.ViewStoredResult(result.ResultId),"view committed final surface after adversarial live mutation");CheckMasks(result);
            Check(c.RequestCoin(0)&&c.RoundId>round&&tracker.ElapsedSeconds==0&&!tracker.HasStarted,"next round independently resets goal/result/time");
            Check(surface.SurfaceMask.CurrentDirtSum==surface.SurfaceMask.InitialDirtSum&&c.Dirt.All(x=>x.CurrentDurability>0)&&c.CurrentResult==null&&c.PendingResultId==null,"next round resets surface/A/result without requiring a new reusable mask object");
            FinalizationRub();PulseFinalizationClock(1);
            Check(Digest(result)==digest&&Digest(c.StoredResults[0])==digest,"actual new-round mutation cannot rewrite immutable final result");
            File.WriteAllText(identityPath,result.ResultId+" "+digest);
            checks.Add("samples goalSeconds="+goal.ElapsedSeconds.ToString("R")+" modalSeconds="+modal.ElapsedSeconds.ToString("R")+" failedSeconds="+failed.ElapsedSeconds.ToString("R")+" finalSeconds="+result.ElapsedSeconds.ToString("R")+" changedFrontDirt="+goal.FrontDirt.Where((v,i)=>v!=result.FrontDirt[i]).Count()+" changedBackSoftened="+goal.BackSoftened.Where((v,i)=>v!=result.BackSoftened[i]).Count()+" resultId="+result.ResultId);
            Finish("PASS finalization-seed checks="+checks.Count+"; synthetic accepted rub/focus clock + actual product droplet/worker IO/frames; no physical input claim");
        }

        private ExperimentSnapshot FinalizationSample()=>new(c.RoundId,c.CurrentCoin.Id,c.CurrentCoin.ReactionProfile.Id,"probe-only-sample",surface.SurfaceMask,c.Dirt.Select(x=>x.CurrentDurability).ToArray(),tracker.ElapsedSeconds);
        private static bool SameFinalizationArrays(ExperimentSnapshot a,ExperimentSnapshot b)=>a.A.SequenceEqual(b.A)&&a.FrontDirt.SequenceEqual(b.FrontDirt)&&a.BackDirt.SequenceEqual(b.BackDirt)&&a.FrontPolish.SequenceEqual(b.FrontPolish)&&a.BackPolish.SequenceEqual(b.BackPolish)&&a.FrontSoftened.SequenceEqual(b.FrontSoftened)&&a.BackSoftened.SequenceEqual(b.BackSoftened);
        private CoinSurfaceContact FinalizationContact(CoinSurfaceFace face)
        {
            var m=surface.SurfaceMask;var n=m.Resolution;
            var i=Enumerable.Range(0,m.TexelCount).Where(x=>m.GetDirt(face,x)>0&&!m.IsSoftened(face,x)).OrderBy(x=>Math.Abs(x%n-n/2)+Math.Abs(x/n-n/2)).First();
            var uv=new Vector2((i%n+.5f)/n,(i/n+.5f)/n);var bounds=pointer.SurfaceLocalBounds;var sign=face==CoinSurfaceFace.Front?1f:-1f;
            var local=bounds.center+new Vector3((uv.x*2-1)*bounds.extents.x,sign*bounds.extents.y,(uv.y*2-1)*bounds.extents.x);
            var matrix=pointer.SurfaceLocalToWorldMatrix;
            return new CoinSurfaceContact(face,uv,local,matrix.MultiplyPoint3x4(local),matrix.inverse.transpose.MultiplyVector(Vector3.up*sign).normalized);
        }
        private void FinalizationRub()
        {
            Check(c.SelectTool(1)&&surface.ProcessingAllowed,"additional accepted cloth rub tool/work allowed");
            var start=FinalizationContact(CoinSurfaceFace.Front);var bounds=pointer.SurfaceLocalBounds;var matrix=pointer.SurfaceLocalToWorldMatrix;
            var local=start.LocalPosition+Vector3.right*bounds.extents.x*.08f;
            Check(CoinSurfaceMapper.TryMapPoint(matrix,bounds,matrix.MultiplyPoint3x4(local),start.WorldNormal,out var end),"synthetic rub end maps to valid surface");
            var sample=new CleaningRubSample(start,end,.08f,Time.frameCount+100000);
            var method=typeof(CoinSurfaceContamination).GetMethod("ApplyRubSample",BindingFlags.Instance|BindingFlags.NonPublic);
            Check((bool)method.Invoke(surface,new object[]{sample}),"existing approved cloth rub path changes surface");
        }
        private void PulseFinalizationClock(double seconds)
        {
            // batchmode의 실제 포커스 여부와 무관한 결정론적 경계 검사. 제품 타이머 가드는 변경하지 않습니다.
            var type=typeof(CleaningProgressTracker);var flags=BindingFlags.Instance|BindingFlags.NonPublic;var now=Time.realtimeSinceStartupAsDouble;
            type.GetField("previousTimingTimestamp",flags).SetValue(tracker,now-seconds);
            type.GetField("wasTimingFocused",flags).SetValue(tracker,true);
            type.GetMethod("AdvanceTiming",flags).Invoke(tracker,new object[]{now,true});
        }
        private sealed class FinalizationFiles:SaveFileOperations
        {
            internal readonly ManualResetEventSlim WriteEntered=new(),WriteRelease=new(),ReadbackEntered=new(),ReadbackRelease=new();
            private readonly bool failWrite;private bool committed;
            internal FinalizationFiles(bool fail)=>failWrite=fail;
            internal void Release(){WriteRelease.Set();ReadbackRelease.Set();}
            public override void WriteNew(string path,byte[] bytes)
            {
                WriteEntered.Set();if(!WriteRelease.Wait(TimeSpan.FromSeconds(30)))throw new IOException("Probe write gate timeout");
                if(failWrite)throw new IOException("Probe known precommit failure");base.WriteNew(path,bytes);
            }
            public override void CommitNew(string temporary,string primary){base.CommitNew(temporary,primary);committed=true;}
            public override void Replace(string temporary,string primary,string previous){base.Replace(temporary,primary,previous);committed=true;}
            public override byte[] Read(string path)
            {
                if(committed&&Path.GetFileName(path)==CompletedCoinStore.FileName)
                {ReadbackEntered.Set();if(!ReadbackRelease.Wait(TimeSpan.FromSeconds(30)))throw new IOException("Probe readback gate timeout");}
                return base.Read(path);
            }
        }
    }
}
#endif
