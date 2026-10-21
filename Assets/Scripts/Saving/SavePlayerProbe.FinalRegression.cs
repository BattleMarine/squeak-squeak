#if UNITY_EDITOR || SQUEAK_SAVE_VALIDATION
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using SqueakSqueak.Cleaning;
using SqueakSqueak.Experiment;

namespace SqueakSqueak.Saving
{
    // S3-07 통합 공백만 확인합니다. 기존 손상/경계/물리 입력 검사를 대체하지 않습니다.
    public sealed partial class SavePlayerProbe
    {
        private IEnumerator RunFinalRegression()
        {
            while(!c.SaveReady){if(c.LastError!=null)throw new InvalidOperationException(c.LastError);yield return null;}
            while(c.PersistenceBusy)yield return null;
            var identity=Path.Combine(directory,"final-identities.txt");var baseline=Path.Combine(directory,"final-general.bin");
            var general=File.ReadAllBytes(phase=="final-seed"?Path.Combine(directory,GeneralProgressStore.FileName):baseline);
            var events=0;c.ResultStored+=_=>events++;
            CheckFinalReferences();AssertIntegrationProgress(general,"final startup",true);
            if(phase=="final-reload")
            {
                Check(!c.IsActive&&c.PendingResultId==null&&!tracker.HasStarted&&tracker.ElapsedSeconds==0,"restart does not restore unfinished work/dialog/time");
                Check(c.StoredResults.Count==2&&c.StoredResults.Select(x=>x.CoinId).Distinct().Count()==2,"restart restores M1 and M2 only");
                Check(File.ReadAllLines(identity).SequenceEqual(c.StoredResults.Select(x=>x.ResultId+" "+Digest(x))),"both result IDs/final time/all arrays bit-exact after restart");
                foreach(var value in c.StoredResults.ToArray())
                {
                    Check(c.ViewStoredResult(value.ResultId)&&c.State==ExperimentState.Viewing,"restart opens readonly "+value.CoinId);CheckMasks(value);
                    var revision=surface.SurfaceMask.Revision;var seconds=tracker.ElapsedSeconds;
                    Check(!surface.ApplyCleaning(1)&&!surface.ApplyDroplet(default,.2f,Time.frameCount)&&!c.SelectTool(2)&&!c.RequestStore(c.RoundId),"restored observation rejects mutation/store "+value.CoinId);
                    yield return null;Check(surface.SurfaceMask.Revision==revision&&tracker.ElapsedSeconds==seconds&&events==0,"observation changes no data/time/event "+value.CoinId);
                }
                Check(c.RequestLeave()&&!c.IsActive,"restored observation returns normal");yield return null;while(c.PersistenceBusy)yield return null;
                AssertIntegrationProgress(general,"restart complete",true);CheckFinalReferences();
                Finish("PASS final-reload checks="+checks.Count+"; fresh process/domain, exact saved M1/M2; unfinished work absent");yield break;
            }

            Check(c.StoredResults.Count==0,"fresh final seed library");
            for(var n=0;n<2;n++)
            {
                Check(c.RequestCoin(n)&&c.State==ExperimentState.Cleaning,"new representative coin "+n);yield return null;
                FinalRegressionRub(CoinSurfaceFace.Front);PulseFinalizationClock(1);
                foreach(var dirt in c.Dirt)dirt.ApplyCleaning(100);
                foreach(var face in new[]{CoinSurfaceFace.Front,CoinSurfaceFace.Back,CoinSurfaceFace.Side})for(var i=0;i<surface.SurfaceMask.GetTexelCount(face);i++)surface.SurfaceMask.TrySetDirt(face,i,surface.SurfaceMask.GetDirt(face,i)*.04f);
                surface.UploadSurfaceMasks();c.EvaluateGoal();var goal=FinalizationSample();
                Check(c.State==ExperimentState.Ready&&c.CurrentResult==null&&surface.ProcessingAllowed&&!tracker.IsTimingPaused,"goal is not stored and permits continued work "+n);
                yield return null;FinalRegressionRub(CoinSurfaceFace.Back);PulseFinalizationClock(2);
                Check(!SameFinalizationArrays(goal,FinalizationSample())&&tracker.ElapsedSeconds>goal.ElapsedSeconds+1,"postgoal treatment/rub/time advance "+n);
                Click("보관 확인");var confirmed=FinalizationSample();Check(tracker.IsTimingPaused&&!pointer.enabled,"confirmation pauses "+n);
                Click("확인");var id=c.PendingResultId;while(c.PersistenceBusy)yield return null;
                Check(c.State==ExperimentState.Stored&&events==n+1&&c.StoredResults.Count==n+1&&c.CurrentResult.ResultId==id,"save memory/file/event confirm once "+n);
                Check(SameFinalizationArrays(confirmed,c.CurrentResult)&&confirmed.ElapsedSeconds==c.CurrentResult.ElapsedSeconds,"saved final state matches confirmation "+n);
                var value=c.CurrentResult;Check(c.ViewStoredResult(value.ResultId),"stored -> readonly "+n);CheckMasks(value);
                Check(!surface.ProcessingAllowed&&tracker.IsTimingPaused&&pointer.enabled,"readonly permits view not cleaning "+n);
            }
            var saved=c.StoredResults.Select(x=>x.ResultId+" "+Digest(x)).ToArray();
            var primary=File.ReadAllBytes(Path.Combine(directory,CompletedCoinStore.FileName));
            Check(c.RequestLeave()&&!c.IsActive,"stored -> normal before repeated work");yield return null;while(c.PersistenceBusy)yield return null;
            Check(c.RequestLibrary(),"warm library close button once");Click("목록 닫기");yield return null;yield return null;
            var resources=FinalResourceCounts();checks.Add("resource baseline "+resources);var ownerId=c.GetInstanceID();var surfaceId=surface.GetInstanceID();var pointerId=pointer.GetInstanceID();
            Check(Resources.FindObjectsOfTypeAll<Texture2D>().Count(x=>x.name=="SqueakSqueak.BMask.Front")==1&&Resources.FindObjectsOfTypeAll<Texture2D>().Count(x=>x.name=="SqueakSqueak.BMask.Back")==1&&
                Resources.FindObjectsOfTypeAll<Material>().Count(x=>x.name=="RB.ScannerOutline")==1&&Resources.FindObjectsOfTypeAll<AudioClip>().Count(x=>x.name=="RB.Droplet")==1,
                "named resource positive baseline: SqueakSqueak.BMask.Front/Back=2 RB.ScannerOutline=1 RB.Droplet=1");
            for(var n=0;n<6;n++)
            {
                Check(c.RequestCoin(n%2),"repeat entry "+n);yield return null;FinalRegressionRub(CoinSurfaceFace.Front);
                Check(c.ToggleScanner()&&c.ToggleScanner(),"repeat scanner "+n);
                Check(!c.RequestLibrary()&&tracker.IsTimingPaused&&!pointer.enabled,"repeat notice blocks work "+n);Click("확인");
                Check(pointer.enabled&&!tracker.IsTimingPaused,"repeat notice restores work "+n);
                Check(c.RequestLeave()&&c.CancelDialog(c.RoundId)&&c.State==ExperimentState.Cleaning,"repeat discard cancel "+n);
                Check(c.RequestLeave()&&c.ConfirmDialog(c.RoundId)&&!c.IsActive,"repeat explicit discard "+n);
                yield return null;yield return null;while(c.PersistenceBusy)yield return null;
                Check(FinalResourceCounts()==resources,"same-state resource counts stable "+n+": "+FinalResourceCounts());
                Check(c.GetInstanceID()==ownerId&&surface.GetInstanceID()==surfaceId&&pointer.GetInstanceID()==pointerId,"core instance references stable "+n);CheckFinalReferences();
            }
            Check(saved.SequenceEqual(c.StoredResults.Select(x=>x.ResultId+" "+Digest(x)))&&File.ReadAllBytes(Path.Combine(directory,CompletedCoinStore.FileName)).SequenceEqual(primary)&&events==2,"repeat work never mutates/appends stored results/events");
            AssertIntegrationProgress(general,"after six repeats",true);
            File.WriteAllLines(identity,saved);File.WriteAllBytes(baseline,general);
            Check(c.RequestCoin(0),"last unfinished round starts");yield return null;FinalRegressionRub(CoinSurfaceFace.Front);
            Check(tracker.HasStarted&&c.State==ExperimentState.Cleaning&&c.PendingResultId==null,"unfinished work exists only in memory before quit");
            Check(c.RequestQuit()&&c.State==ExperimentState.QuitConfirmation&&tracker.IsTimingPaused&&!pointer.enabled,"unfinished work product quit warning pauses");
            Check(FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None).Any(x=>x.name=="RB.DialogText"&&x.text.Contains("미보관")),"quit explicitly warns unfinished work will not save");
            // PASS는 아래 선행 검사 결과입니다. 제품 종료 확인 콜백 후 실제 Edit/프로세스 exit는 외부에서 확인합니다.
            Finish("PASS final-seed checks="+checks.Count+"; M1/M2 + six repeats; synthetic approved rub/completion/clock, actual product APIs/IO/UI; product quit confirmation follows",false);
            Click("확인");
        }
        private void FinalRegressionRub(CoinSurfaceFace face)
        {
            var start=FinalizationContact(face);var before=surface.SurfaceMask.CurrentDirtSum;
            Check(c.SelectTool(2)&&surface.ApplyDroplet(start,.2f,Time.frameCount),"representative product droplet "+c.CurrentCoin.Id+"/"+face);
            Check(c.SelectTool(1),"representative cloth selected");var bounds=pointer.SurfaceLocalBounds;var matrix=pointer.SurfaceLocalToWorldMatrix;
            var local=start.LocalPosition+Vector3.right*bounds.extents.x*.04f;
            Check(CoinSurfaceMapper.TryMapPoint(matrix,bounds,matrix.MultiplyPoint3x4(local),start.WorldNormal,out var end),"approved representative rub endpoint maps");
            var sample=new CleaningRubSample(start,end,.08f,Time.frameCount+100000);
            Check((bool)typeof(CoinSurfaceContamination).GetMethod("ApplyRubSample",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(surface,new object[]{sample})&&surface.SurfaceMask.CurrentDirtSum<before,"approved representative rub changes B "+c.CurrentCoin.Id+"/"+face);
        }
        private void CheckFinalReferences()
        {
            var objects=c.gameObject.scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
            Check(objects.All(x=>x.GetComponents<Component>().All(v=>v!=null)),"scene has no missing scripts");
            Check(c!=null&&pointer!=null&&surface!=null&&tracker!=null&&stage!=null&&pointer.GameplayCamera!=null&&surface.FrontDirtMask!=null&&surface.BackDirtMask!=null,"core runtime references and both mask textures valid");
        }
        private string FinalResourceCounts()
        {
            var roots=c.gameObject.scene.GetRootGameObjects();var objects=roots.SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
            int Count<T>()where T:Component=>objects.Sum(x=>x.GetComponents<T>().Length);
            return "objects="+objects.Length+" dirt="+Count<ObjectDirt>()+" lines="+Count<LineRenderer>()+" particles="+Count<ParticleSystem>()+" audio="+Count<AudioSource>()+
                " masks="+Resources.FindObjectsOfTypeAll<Texture2D>().Count(x=>x.name.StartsWith("SqueakSqueak.BMask.",StringComparison.Ordinal))+
                " scannerMaterials="+Resources.FindObjectsOfTypeAll<Material>().Count(x=>x.name=="RB.ScannerOutline")+
                " dropClips="+Resources.FindObjectsOfTypeAll<AudioClip>().Count(x=>x.name=="RB.Droplet");
        }
    }
}
#endif
