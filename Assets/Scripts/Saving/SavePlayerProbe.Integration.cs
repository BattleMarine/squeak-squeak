#if UNITY_EDITOR || SQUEAK_SAVE_VALIDATION
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using SqueakSqueak.Cleaning;
using SqueakSqueak.Experiment;

namespace SqueakSqueak.Saving
{
    /// <summary>04b 통합 경계만 추가합니다. 제품 소유권/진행 상태를 주입해 가드를 우회하지 않습니다.</summary>
    public sealed partial class SavePlayerProbe
    {
        private IntegrationFiles integrationFiles;
        private void ReleaseIntegrationGate()=>integrationFiles?.Release();
        private IEnumerator RunIntegration()
        {
            while(!c.SaveReady){if(c.LastError!=null)throw new Exception(c.LastError);yield return null;}
            yield return null;while(c.PersistenceBusy)yield return null;
            var progressPath=Path.Combine(directory,GeneralProgressStore.FileName);
            var identityPath=Path.Combine(directory,"integration-identities.txt");
            var baselinePath=Path.Combine(directory,"integration-progress.bin");
            var progress=File.ReadAllBytes(phase=="integration-seed"?progressPath:baselinePath);
            AssertIntegrationProgress(progress,"startup",true);
            var session=(CoinSaveSession)IntegrationField("saveSession");
            var store=new CompletedCoinStore(directory,session.Definitions);
            var primary=Path.Combine(directory,CompletedCoinStore.FileName);
            var events=0;c.ResultStored+=_=>events++;

            if(phase=="integration-reload")
            {
                Check(c.StoredResults.Count==2&&!c.IsActive&&c.PendingResultId==null&&!tracker.HasStarted,"restart loads two results, not unfinished work/dialog");
                var identities=File.ReadAllLines(identityPath);
                Check(identities.SequenceEqual(c.StoredResults.Select(x=>x.ResultId+" "+Digest(x))),"both result identities/time/arrays exact across restart");
                foreach(var result in c.StoredResults.ToArray())
                {
                    Check(c.ViewStoredResult(result.ResultId)&&c.State==ExperimentState.Viewing,"restart -> read-only result "+result.ResultId);
                    CheckMasks(result);var revision=surface.SurfaceMask.Revision;var elapsed=tracker.ElapsedSeconds;
                    Check(!c.RequestStore(c.RoundId)&&!c.SelectTool(2)&&!surface.ApplyCleaning(1)&&!surface.ApplyDroplet(default,.2f,Time.frameCount),"restored view rejects work/store");
                    yield return null;
                    Check(surface.SurfaceMask.Revision==revision&&tracker.ElapsedSeconds==elapsed&&events==0,"observation never re-confirms results");
                }
                Check(c.RequestLeave()&&!c.IsActive,"observation returns to original normal mode");
                yield return null;while(c.PersistenceBusy)yield return null;
                AssertIntegrationProgress(progress,"restart after viewing both",true);
                Check(store.Load().Data.results.Select(x=>x.resultId).Distinct().Count()==2,"restart file has exactly two unique results");
                Finish("PASS integration-reload checks="+checks.Count);yield break;
            }

            Check(c.StoredResults.Count==0&&c.RequestCoin(0),"begin fresh M1 experiment");
            Check(c.SelectTool(0)&&c.SelectTool(1)&&c.SelectTool(2),"all experimental tools accessible without ownership grant");
            Check(surface.ApplyDroplet(FinalizationContact(CoinSurfaceFace.Front),.2f,Time.frameCount),"actual experimental treatment used");
            PulseFinalizationClock(1);Complete(0);var roundA=c.RoundId;
            Click("보관 확인");var confirm=IntegrationButton("확인");confirm.onClick.Invoke();confirm.onClick.Invoke();
            Check(c.State==ExperimentState.Saving&&!c.ConfirmDialog(roundA)&&!c.RetryStore()&&!c.RequestStore(roundA),"double UI event and duplicate store APIs cannot create another candidate");
            while(c.PersistenceBusy)yield return null;
            var a=c.CurrentResult;var aDigest=Digest(a);var aBytes=File.ReadAllBytes(primary);
            Check(c.State==ExperimentState.Stored&&events==1&&c.StoredResults.Count==1&&store.Load().Data.results.Length==1,"first result confirmed once in memory/file/event");
            AssertIntegrationProgress(progress,"first stored",false);

            Check(c.RequestCoin(0)&&c.RoundId!=roundA&&!c.ConfirmDialog(roundA),"new round rejects old confirmation API");
            Complete(1);var roundB=c.RoundId;integrationFiles=new IntegrationFiles();c.UseValidationFiles(integrationFiles);
            Click("보관 확인");confirm=IntegrationButton("확인");confirm.onClick.Invoke();confirm.onClick.Invoke();
            var idB=c.PendingResultId;
            while(!integrationFiles.Replaced.IsSet)yield return null;
            var committed=store.Load();var committedBytes=File.ReadAllBytes(primary);
            Check(committed.HasData&&committed.Data.results.Length==2&&committed.Data.results.Any(x=>x.resultId==idB),"actual atomic replacement contains candidate B");
            Check(File.ReadAllBytes(primary+".bak").SequenceEqual(aBytes),"actual replacement preserves previous A backup");
            Check(c.State==ExperimentState.Saving&&c.StoredResults.Count==1&&events==1&&c.CurrentResult==null,"replacement alone does not confirm candidate");
            Check(!c.ConfirmDialog(roundB)&&!c.RetryStore()&&!c.RequestCoin(0)&&!c.ViewStoredResult(a.ResultId),"busy replacement rejects duplicate/new round/view APIs");
            var task=(Task<CoinSaveCommit>)IntegrationField("storeTask");c.enabled=false;integrationFiles.ReleaseReplacement.Set();
            while(!task.IsCompleted)yield return null;
            Check(c.State==ExperimentState.Saving&&c.StoredResults.Count==1&&events==1,"disabled owner cannot apply late worker completion");
            c.enabled=true;while(c.PersistenceBusy)yield return null;
            Check(c.State==ExperimentState.SaveFailed&&c.PendingResultId==idB&&events==1&&!c.CancelFailedStore()&&!c.RequestCoin(0),"post-replace uncertainty retains same ID and prevents cancel/new ID escape");
            Click("종료 확인");Click("취소");
            Check(c.State==ExperimentState.SaveFailed&&ButtonText("같은 결과 재시도")&&c.PendingResultId==idB,"quit cancel restores uncertain same-ID retry action");
            integrationFiles.HoldRetryRead=true;var retry=IntegrationButton("같은 결과 재시도");retry.onClick.Invoke();retry.onClick.Invoke();
            Check(c.State==ExperimentState.Saving&&c.PendingResultId==idB&&!c.RetryStore(),"double UI retry/API keeps one same-ID attempt");
            while(!integrationFiles.RetryRead.IsSet)yield return null;
            yield return new WaitForSecondsRealtime(.3f);
            Check(c.StoredResults.Count==1&&events==1&&c.CurrentResult==null,"retry readback wait cannot prematurely confirm candidate");
            integrationFiles.ReleaseRetryRead.Set();while(c.PersistenceBusy)yield return null;
            Check(c.State==ExperimentState.Stored&&c.CurrentResult.ResultId==idB&&c.StoredResults.Count==2&&events==2,"same ID reconciliation confirms candidate exactly once");
            Check(File.ReadAllBytes(primary).SequenceEqual(committedBytes)&&File.ReadAllBytes(primary+".bak").SequenceEqual(aBytes),"already-saved retry does not rewrite primary or backup");
            Check(store.Load().Data.results.Select(x=>x.resultId).Distinct().Count()==2&&Digest(c.StoredResults[0])==aDigest,"two unique IDs and original A unchanged");
            confirm.onClick.Invoke();retry.onClick.Invoke();
            Check(!c.ConfirmDialog(roundB)&&!c.RetryStore()&&!c.RequestStore(roundB)&&events==2,"late old UI/API requests cannot re-confirm Stored");
            AssertIntegrationProgress(progress,"after uncertain reconciliation",false);

            Check(c.ViewStoredResult(a.ResultId)&&Digest(c.CurrentResult)==aDigest,"stored B -> earlier result A view");CheckMasks(a);
            Check(c.RequestCoin(0)&&c.CurrentResult==null&&tracker.ElapsedSeconds==0&&!tracker.HasStarted,"earlier view -> new independent work");
            FinalizationRub();PulseFinalizationClock(1);
            Check(!c.ViewStoredResult(a.ResultId)&&!c.RequestLibrary(),"active unfinished work cannot silently enter library/view");
            // RequestLibrary's notice is not a state transition; explicit discard owns the confirmation.
            Check(c.RequestLeave(),"unfinished round explicitly requests discard");var leaving=FinalizationSample();var unfinishedRound=c.RoundId;
            Check(c.CancelDialog(unfinishedRound)&&c.RoundId==unfinishedRound&&SameFinalizationArrays(leaving,FinalizationSample())&&tracker.ElapsedSeconds==leaving.ElapsedSeconds,"discard cancel preserves unfinished round/surface/time");
            Check(c.RequestLeave()&&c.ConfirmDialog(c.RoundId)&&!c.IsActive,"explicit discard returns to normal mode without storing work");
            yield return null;while(c.PersistenceBusy)yield return null;
            AssertIntegrationProgress(progress,"normal mode after discard",true);
            Check(c.RequestLibrary(),"normal mode opens completed library");
            var entry=FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.GetComponentInChildren<Text>()?.text.StartsWith("01. ",StringComparison.Ordinal)==true);
            entry.onClick.Invoke();entry.onClick.Invoke();
            Check(c.State==ExperimentState.Viewing&&c.CurrentResult.ResultId==a.ResultId&&events==2,"actual library double event only views A; no result event");CheckMasks(a);
            AssertIntegrationProgress(progress,"library observation",false);
            Check(c.StoredResults.Count==2&&store.Load().Data.results.Length==2&&File.ReadAllBytes(primary).SequenceEqual(committedBytes),"view/new/discard flow never appends or changes saved results");
            File.WriteAllLines(identityPath,c.StoredResults.Select(x=>x.ResultId+" "+Digest(x)));File.WriteAllBytes(baselinePath,progress);
            Finish("PASS integration-seed checks="+checks.Count+"; actual UI callbacks/IO/frames + synthetic completion/rub; not physical double-click test");
        }
        private object IntegrationField(string name)=>typeof(CleaningExperimentController).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c);
        private static Button IntegrationButton(string text)=>FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.interactable&&b.GetComponentInChildren<Text>()?.text==text);
        private void AssertIntegrationProgress(byte[] baseline,string when,bool normal)
        {
            Check(File.ReadAllBytes(Path.Combine(directory,GeneralProgressStore.FileName)).SequenceEqual(baseline),"general file bytes unchanged: "+when);
            var read=new GeneralProgressStore(directory,new GeneralProgressSaveDefinition(stage.Catalog)).Load();
            Check(read.HasData&&read.Data.ownedToolIds.SequenceEqual(stage.Catalog.InitiallyOwnedToolIds)&&read.Data.completedStageIds.Length==0&&read.Data.selectedStageId==stage.Catalog.Stages[0].Id,"general DTO ownership/completion/selection unchanged: "+when);
            Check(stage.Catalog.Tools.All(x=>stage.Access.Owns(x.Id)==stage.Catalog.InitiallyOwnedToolIds.Contains(x.Id))&&stage.Catalog.Stages.All(x=>!stage.Access.Completed(x.Id))&&!stage.Access.CanEnter(stage.Catalog.Stages[1],out _),"general owner has no experimental ownership/completion/unlock leak: "+when);
            if(normal)
            {
                var selection=FindFirstObjectByType<CleaningToolSelection>();var locked=stage.Catalog.Tools.First(x=>!stage.Access.Owns(x.Id));
                Check(stage.enabled&&selection.AvailableToolCount==stage.Catalog.InitiallyOwnedToolIds.Count&&!selection.TrySelect(locked.Definition),"normal mode still denies locked tool API: "+when);
            }
        }
        private sealed class IntegrationFiles:SaveFileOperations
        {
            internal readonly ManualResetEventSlim Replaced=new(),ReleaseReplacement=new(),RetryRead=new(),ReleaseRetryRead=new();
            internal volatile bool HoldRetryRead;private bool fired;
            internal void Release(){ReleaseReplacement.Set();ReleaseRetryRead.Set();}
            public override void Replace(string temporary,string primary,string previous)
            {
                base.Replace(temporary,primary,previous);Replaced.Set();
                if(!ReleaseReplacement.Wait(TimeSpan.FromSeconds(30)))throw new IOException("Integration replacement gate timeout");
                if(!fired){fired=true;throw new IOException("Injected immediately after actual atomic replacement");}
            }
            public override byte[] Read(string path)
            {
                if(HoldRetryRead&&Path.GetFileName(path)==CompletedCoinStore.FileName)
                {RetryRead.Set();if(!ReleaseRetryRead.Wait(TimeSpan.FromSeconds(30)))throw new IOException("Integration retry read gate timeout");}
                return base.Read(path);
            }
        }
    }
}
#endif
