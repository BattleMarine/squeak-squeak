using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using SqueakSqueak.Progression;
using SqueakSqueak.Saving;

namespace SqueakSqueak.Experiment
{
    // 기존 판 소유자의 저장 경계. 완료 Task는 이 소유자의 Update에서만 화면/메모리에 적용합니다.
    public sealed partial class CleaningExperimentController
    {
        public const string EditorSaveOverrideKey="SqueakSqueak.SaveValidationDirectory";
        private CoinSaveSession saveSession;
        private Task<CoinSaveLoad> loadTask;
        private Task<CoinSaveCommit> storeTask;
        private Task<SaveWriteResult> progressTask;
        private ExperimentSnapshot pendingSnapshot;
        private SavedCoinDto pendingSave;
        private GeneralProgressDto pendingProgress;
        private long progressRevision,storeRound;
        private bool persistenceReady,generalDirty,generalFailed,quitAfterOperation,storeUncertain;
        public bool SaveReady=>persistenceReady&&!generalFailed;
        public bool PersistenceBusy=>loadTask!=null||storeTask!=null||progressTask!=null;
        public string PendingResultId=>pendingSnapshot?.ResultId;
        public string SaveDirectory=>saveSession?.DirectoryPath;

        private void Awake()
        {
            // 모든 Awake가 끝난 뒤 Stage.Start가 실행되므로 초기 판보다 로드가 먼저입니다.
            if(legacyStage!=null)legacyStage.enabled=false;
            if(audioFeedback!=null)originalAudio=audioFeedback.enabled;
            if(visualFeedback!=null)originalVisual=visualFeedback.enabled;
            SetWork(false);
        }
        private void BeginPersistence()
        {
            maximumStoredResults=Math.Min(maximumStoredResults,CoinSaveLimits.MaximumResults);
            legacyStage.StateChanged+=OnGeneralStateChanged;legacyStage.AccessChanged+=OnAccessChanged;
            try
            {
                var path=Path.Combine(Application.persistentDataPath,"SaveData","v1");
                #if UNITY_EDITOR
                var isolated=UnityEditor.SessionState.GetString(EditorSaveOverrideKey,"");
                if(!string.IsNullOrEmpty(isolated))path=ValidateIsolatedPath(isolated);
                #elif SQUEAK_SAVE_VALIDATION
                path=SavePlayerProbe.IsolatedDirectory;
                #endif
                var definitions=new CoinSaveDefinitionCatalog(definition,new CoinSaveRenderContract(CoinSaveSession.RenderRevision,
                    surface.SaveDirtColor,surface.SaveDirtySmoothness,surface.SaveMaximumSmoothness));
                saveSession=new CoinSaveSession(path,definitions,new GeneralProgressSaveDefinition(legacyStage.Catalog));
                RetryLoad();
            }
            catch(Exception e){ShowLoadError(e.Message);}
        }
        #if UNITY_EDITOR || SQUEAK_SAVE_VALIDATION
        internal void ApproveValidationQuit()=>quitApproved=true;
        internal void UseValidationFiles(SaveFileOperations files)
        {
            if(!SaveReady||PersistenceBusy)throw new InvalidOperationException("Idle isolated validation session required.");
            ValidateIsolatedPath(SaveDirectory);
            saveSession=new CoinSaveSession(SaveDirectory,saveSession.Definitions,new GeneralProgressSaveDefinition(legacyStage.Catalog),files);
        }
        public static string ValidateIsolatedPath(string path)
        {
            var full=Path.GetFullPath(path);var marker=Path.DirectorySeparatorChar+".utmp"+Path.DirectorySeparatorChar;
            if(!full.Contains(marker,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Validation saves require a dedicated .utmp child directory.");
            return full;
        }
        #endif
        public bool RetryLoad()
        {
            if(!isActiveAndEnabled||saveSession==null||PersistenceBusy||persistenceReady)return false;
            LastError=null;view.ShowSystemDialog("보관함과 일반 진행을 읽고 있습니다…",null,null);
            loadTask=SaveOperationLifetime.Run(saveSession.Load);return true;
        }
        private void PollPersistence()
        {
            if(loadTask?.IsCompleted==true)
            {
                var task=loadTask;loadTask=null;
                if(task.IsFaulted){ShowLoadError(task.Exception.GetBaseException().Message);return;}
                var value=task.Result;
                if(!value.Succeeded){ShowLoadError(value.Message);return;}
                stored.Clear();stored.AddRange(value.Results);progressRevision=value.ProgressRevision;
                legacyStage.enabled=true;
                if(!legacyStage.TryInitializeFromAccess(new StageAccessContext(value.Progress.ownedToolIds,value.Progress.completedStageIds),value.Progress.selectedStageId))
                {legacyStage.enabled=false;ShowLoadError(legacyStage.LastError);return;}
                persistenceReady=true;view.HideDialog();RestoreInput();RefreshView();
                if(value.Message!=null)ShowNotice(value.Message);
            }
            if(storeTask?.IsCompleted==true)
            {
                var task=storeTask;storeTask=null;
                if(storeRound!=RoundId||State!=ExperimentState.Saving)return; // 늦은 이전 판 완료는 적용하지 않습니다.
                var value=task.IsFaulted?new CoinSaveCommit{Write=new SaveWriteResult(SaveWriteStatus.Indeterminate,task.Exception.GetBaseException().Message)}:task.Result;
                if(value.Write.Succeeded)
                {
                    stored.Clear();stored.AddRange(value.Results);CurrentResult=pendingSnapshot;pendingSnapshot=null;pendingSave=null;
                    tracker.StopTiming();State=ExperimentState.Stored;view.HideDialog();RestoreInput();RefreshView();ResultStored?.Invoke(CurrentResult);
                }
                else
                {
                    State=ExperimentState.SaveFailed;storeUncertain|=value.Write.Status==SaveWriteStatus.Indeterminate;
                    LastError=value.Write.Status+" / "+value.Write.Message;ShowStoreError();RefreshView();
                }
            }
            if(progressTask?.IsCompleted==true)
            {
                var task=progressTask;progressTask=null;
                var value=task.IsFaulted?new SaveWriteResult(SaveWriteStatus.Indeterminate,task.Exception.GetBaseException().Message):task.Result;
                if(value.Succeeded){var wasFailed=generalFailed;progressRevision=value.Revision;pendingProgress=null;generalFailed=false;if(wasFailed){LastError=null;view.HideDialog();RestoreInput();}}
                else
                {
                    generalFailed=true;LastError=value.Status+" / "+value.Message;SetWork(false);legacyStage.enabled=false;
                    ShowGeneralError();
                }
            }
            if(SaveReady&&generalDirty&&progressTask==null&&!IsActive&&legacyStage.StageId!=null)
            {
                generalDirty=false;pendingProgress=CaptureProgress();RetryProgress();
            }
            if(quitAfterOperation&&!PersistenceBusy){quitAfterOperation=false;view.HideDialog();RequestQuit();}
        }
        private void ShowLoadError(string message)
        {
            LastError=message;SetWork(false);legacyStage.enabled=false;
            view.ShowSystemDialog("저장 자료를 열 수 없습니다. 자동 초기화하지 않았습니다.\n"+message,()=>RetryLoad(),ConfirmAppQuit,"재시도","종료");
        }
        private void ShowGeneralError()=>view.ShowSystemDialog("일반 진행 저장 실패. 원본은 유지됩니다.\n"+LastError,()=>RetryProgress(),()=>RequestQuit(),"재시도","종료 확인");
        private void RestoreErrorDialog()
        {
            if(!persistenceReady)ShowLoadError(LastError);
            else if(generalFailed)ShowGeneralError();
            else if(State==ExperimentState.SaveFailed)ShowStoreError();
        }
        private bool BeginStore()
        {
            if(!SaveReady||PersistenceBusy)return false;
            if(stored.Count>=maximumStoredResults){LastError="보관함이 가득 찼습니다. 기존 결과는 유지됩니다.";view.ShowDialog(LastError+"\n취소하여 작업으로 돌아갈 수 있습니다.");return false;}
            try
            {
                var a=new float[dirt.Length];for(var i=0;i<a.Length;i++)a[i]=dirt[i].CurrentDurability;
                pendingSnapshot=new ExperimentSnapshot(RoundId,CurrentCoin.Id,CurrentCoin.ReactionProfile.Id,definition.LayoutId,surface.SurfaceMask,a,tracker.ElapsedSeconds);
                pendingSave=SavedCoinDto.FromSnapshot(pendingSnapshot,saveSession.Definitions);storeUncertain=false;
                return RetryStore();
            }
            catch(Exception e){LastError=e.Message;view.ShowDialog("보관할 수 없습니다.\n"+e.Message);return false;}
        }
        public bool RetryStore()
        {
            if(!isActiveAndEnabled||!SaveReady||PersistenceBusy||pendingSave==null||
                (State!=ExperimentState.StoreConfirmation&&State!=ExperimentState.SaveFailed))return false;
            State=ExperimentState.Saving;storeRound=RoundId;SetWork(false);view.ShowSystemDialog("현재 결과를 보관하고 다시 확인하고 있습니다…",null,null);
            var session=saveSession;var candidate=pendingSave;storeTask=SaveOperationLifetime.Run(()=>session.Store(candidate));RefreshView();return true;
        }
        public bool CancelFailedStore()
        {
            if(!isActiveAndEnabled||State!=ExperimentState.SaveFailed||storeUncertain||PersistenceBusy)return false;
            pendingSnapshot=null;pendingSave=null;LastError=null;State=ExperimentState.Ready;view.HideDialog();RestoreInput();RefreshView();return true;
        }
        private void ShowStoreError()=>view.ShowSystemDialog("보관 실패. 현재 판과 시간은 유지됩니다.\n"+LastError+
            (storeUncertain?"\n파일 반영 여부 불명: 같은 결과로 재조회/재시도해야 합니다.":"\n취소한 뒤 더 닦으면 새 결과로 보관합니다."),
            ()=>RetryStore(),storeUncertain?()=>RequestQuit():()=>CancelFailedStore(),"같은 결과 재시도",storeUncertain?"종료 확인":"청소로 돌아가기");

        public bool RequestLibrary()
        {
            if(!isActiveAndEnabled||!SaveReady||PersistenceBusy)return false;
            if(IsActive&&State!=ExperimentState.Stored&&State!=ExperimentState.Viewing&&State!=ExperimentState.Failed)
            {ShowNotice("미보관 작업 중에는 열람할 수 없습니다. 먼저 보관하거나 ‘실험 종료’로 명시적으로 폐기해 주세요.");return false;}
            if(!IsActive&&legacyStage.State==CleaningStageState.Cleaning&&tracker.HasStarted)
            {ShowNotice("미보관 일반 작업 중에는 열람할 수 없습니다. 먼저 완료하거나 명시적으로 폐기해 주세요.");return false;}
            SetWork(false);if(!IsActive)legacyStage.enabled=false;
            view.ShowLibrary(stored,()=>{view.HideLibrary();RestoreInput();});return true;
        }
        public bool ViewStoredResult(string resultId)
        {
            if(!isActiveAndEnabled||!SaveReady||PersistenceBusy)return false;
            if(IsActive&&State!=ExperimentState.Stored&&State!=ExperimentState.Viewing&&State!=ExperimentState.Failed)return false;
            if(!IsActive&&legacyStage.State==CleaningStageState.Cleaning&&tracker.HasStarted)return false;
            var value=stored.Find(x=>x.ResultId==resultId);if(value==null)return false;
            var index=-1;for(var i=0;i<definition.Coins.Count;i++)if(definition.Coins[i].Id==value.CoinId)index=i;
            if(index<0)return false;
            // 진입 가드는 미보관 판 폐기를 허용하지 않습니다. 보관/새 판에서만 기존 준비 경로를 재사용합니다.
            if(!IsActive)legacyStage.enabled=true;
            var entered=IsActive?StartCoin(index):Enter(index);if(!entered)return false;
            SetWork(false);State=ExperimentState.Viewing;CurrentResult=value;goalAnnounced=true;
            try
            {
                surface.RestoreMask(saveSession.RestoreMask(value));
                for(var i=0;i<dirt.Length;i++)dirt[i].ApplyCleaning(definition.Placements[i].Durability-value.A[i]);
                tracker.StopTiming();view.HideLibrary();view.HideDialog();RestoreInput();RefreshView();return true;
            }
            catch(Exception e){Fail("열람 복원 실패: "+e.Message);return false;}
        }
        private void OnGeneralStateChanged(CleaningStageState state){if(persistenceReady&&(state==CleaningStageState.Cleaning||state==CleaningStageState.Result))generalDirty=true;}
        private void OnAccessChanged(){if(persistenceReady)generalDirty=true;}
        private GeneralProgressDto CaptureProgress()=>new(){formatVersion=1,catalogId=legacyStage.Catalog.Id,selectedStageId=legacyStage.StageId,
            ownedToolIds=legacyStage.Catalog.Tools.Where(t=>legacyStage.Access.Owns(t.Id)).Select(t=>t.Id).ToArray(),
            completedStageIds=legacyStage.Catalog.Stages.Where(s=>legacyStage.Access.Completed(s.Id)).Select(s=>s.Id).ToArray()};
        private bool RetryProgress()
        {
            if(!isActiveAndEnabled||pendingProgress==null||PersistenceBusy)return false;
            var candidate=pendingProgress;var session=saveSession;var revision=progressRevision;
            if(generalFailed)view.ShowSystemDialog("일반 진행 저장을 재시도합니다…",null,null);
            progressTask=SaveOperationLifetime.Run(()=>session.StoreProgress(candidate,revision));return true;
        }
        private void RestoreInput()
        {
            if(view!=null&&view.HasBlockingOverlay){SetWork(false);if(!IsActive)legacyStage.enabled=false;return;}
            if(!SaveReady){SetWork(false);return;}
            if(!IsActive){audioFeedback.enabled=originalAudio;visualFeedback.enabled=originalVisual;legacyStage.enabled=true;return;}
            SetWork(State==ExperimentState.Cleaning||State==ExperimentState.Ready,State==ExperimentState.Stored||State==ExperimentState.Viewing);
        }
        private void ConfirmAppQuit()
        {
            if(PersistenceBusy){quitAfterOperation=true;return;}
            quitApproved=true;view.HideDialog();
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
            #else
            Application.Quit();
            #endif
        }
        private void DisposePersistence()
        {
            if(legacyStage!=null){legacyStage.StateChanged-=OnGeneralStateChanged;legacyStage.AccessChanged-=OnAccessChanged;}
            // 작업을 취소하지 않으며 이 소유자를 캡처한 continuation도 없습니다. 새 씬은 Lifetime tail 뒤 로드합니다.
        }
    }
}
