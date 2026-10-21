using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SqueakSqueak.Cleaning;
using SqueakSqueak.Progression;

namespace SqueakSqueak.Experiment
{
    public enum ExperimentState { Inactive, Preparing, Cleaning, Ready, StoreConfirmation, Stored, DiscardConfirmation, QuitConfirmation, Failed, Saving, SaveFailed, Viewing }

    /// <summary>명시적으로 진입한 실험 판의 단일 상태 소유자. 기존 단계 소유자는 실험 동안만 비활성화합니다.</summary>
    [DisallowMultipleComponent]
    public sealed partial class CleaningExperimentController : MonoBehaviour
    {
        [SerializeField] private CleaningExperimentDefinition definition;
        [SerializeField] private CleaningStageController legacyStage;
        [SerializeField] private CleaningCompletionController completion;
        [SerializeField] private CleaningProgressTracker tracker;
        [SerializeField] private CoinSurfacePointer pointer;
        [SerializeField] private ObjectDirtCleaner cleaner;
        [SerializeField] private CoinSurfaceContamination surface;
        [SerializeField] private CleaningToolSelection selection;
        [SerializeField] private CleaningAudioFeedback audioFeedback;
        [SerializeField] private CleaningVisualFeedback visualFeedback;
        [SerializeField] private Transform coinRoot;
        [SerializeField] private MeshFilter coinMesh;
        [SerializeField] private MeshCollider coinCollider;
        [SerializeField] private Renderer coinRenderer;
        [SerializeField] private Canvas canvas;
        [SerializeField] private Font uiFont;
        [SerializeField] private GameObject[] normalUi = Array.Empty<GameObject>();
        [SerializeField, Min(1), Tooltip("보관 상한(파일 형식 최대16). 초과 시 기존 결과를 버리지 않습니다.")]
        private int maximumStoredResults = 16;

        private readonly List<ExperimentSnapshot> stored = new();
        private System.Collections.ObjectModel.ReadOnlyCollection<ExperimentSnapshot> storedView;
        private CleaningExperimentView view;
        private ObjectDirt[] dirt = Array.Empty<ObjectDirt>();
        private LineRenderer[] outlines = Array.Empty<LineRenderer>();
        private GameObject dirtRoot;
        private Material outlineMaterial;
        private AudioSource treatmentAudio;
        private AudioClip dropClip;
        private bool[] originalUi;
        private bool originalAudio, originalVisual, goalAnnounced, quitApproved;
        private string originalStageId;
        private int coinIndex, pendingCoin;
        private bool pendingLeave, pendingEntry;
        private ExperimentState resumeState;
        private long lastDropFrame = -1;
        private int lastPercent = -1, lastSecond = -1;
        public ExperimentState State { get; private set; }
        public bool IsActive => State != ExperimentState.Inactive;
        public bool ScannerEnabled => surface != null && surface.ScannerEnabled;
        public long RoundId { get; private set; }
        public long DialogRoundId { get; private set; }
        public string LastError { get; private set; }
        public CoinDefinition CurrentCoin => IsActive && coinIndex >= 0 && coinIndex < definition.Coins.Count ? definition.Coins[coinIndex] : null;
        public ExperimentSnapshot CurrentResult { get; private set; }
        public IReadOnlyList<ExperimentSnapshot> StoredResults => storedView ??= stored.AsReadOnly();
        public IReadOnlyList<ObjectDirt> Dirt => Array.AsReadOnly(dirt);
        public event Action<long> GoalReached;
        public event Action<ExperimentSnapshot> ResultStored;
        public ExperimentProgress Progress => new(IsActive && State != ExperimentState.Preparing && State != ExperimentState.Failed && surface.SurfaceMask != null,
            cleaner.InitialDirtCount, cleaner.RemovedDirtCount, surface.SurfaceMask?.InitialDirtSum ?? 0, surface.SurfaceMask?.CurrentDirtSum ?? 0,
            surface.SurfaceMask?.GetInitialDirtSum(CoinSurfaceFace.Side)??0,surface.SurfaceMask?.GetCurrentDirtSum(CoinSurfaceFace.Side)??0);

        private void Start()
        {
            if (definition == null || legacyStage == null || completion == null || tracker == null || pointer == null || cleaner == null ||
                surface == null || selection == null || audioFeedback == null || visualFeedback == null || coinRoot == null || coinMesh == null ||
                coinCollider == null || coinRenderer == null || canvas == null || uiFont == null)
            { Debug.LogError("Cleaning experiment requires explicit definition/scene/UI references.",this); enabled=false;return; }
            view = new CleaningExperimentView(canvas,uiFont,this);
            surface.TreatmentApplied += OnTreatment;
            selection.ToolSelected += OnToolSelected;
            Application.wantsToQuit += WantsToQuit;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            BeginPersistence();RefreshView();
        }

        private void Update()
        {
            PollPersistence();
            if(!SaveReady)return;
            if (!IsActive || view == null) return;
            if (State == ExperimentState.Cleaning || State == ExperimentState.Ready)
            {
                if (selection.CurrentTool != null && selection.CurrentTool.IsDropper && pointer.TryGetDropContact(out var contact) && lastDropFrame != Time.frameCount)
                { lastDropFrame=Time.frameCount;surface.ApplyDroplet(contact,selection.CurrentTool.BrushRadius,Time.frameCount); }
                EvaluateGoal();
                var percent=goalAnnounced?100:Progress.DisplayPercent;var second=Mathf.FloorToInt(tracker.ElapsedSeconds);
                if(percent!=lastPercent||second!=lastSecond){lastPercent=percent;lastSecond=second;RefreshView();}
            }
        }

        public void EvaluateGoal()
        {
            if(!isActiveAndEnabled)return;
            if (State != ExperimentState.Cleaning && State != ExperimentState.Ready) return;
            var progress=Progress;
            if (!progress.IsValid) { Fail("청소 원본 수치가 유효하지 않습니다.");return; }
            if (!goalAnnounced && progress.IsComplete)
            { goalAnnounced=true;State=ExperimentState.Ready;GoalReached?.Invoke(RoundId);RefreshView(); }
        }

        public bool RequestCoin(int index)
        {
            if(!isActiveAndEnabled||view==null||!SaveReady)return false;
            if (index<0||index>=definition.Coins.Count) return false;
            if (!IsActive)
            {
                if (legacyStage.State != CleaningStageState.Cleaning && legacyStage.State != CleaningStageState.Result && legacyStage.State != CleaningStageState.Selecting) return false;
                var error=definition.Validate();if(error!=null){LastError=error;ShowNotice(error);return false;}
                pendingCoin=index;
                if (legacyStage.State==CleaningStageState.Cleaning && tracker.HasStarted)
                {
                    if(!legacyStage.RequestAbandon(legacyStage.RoundId))return false;
                    pendingEntry=true;resumeState=ExperimentState.Inactive;DialogRoundId=legacyStage.RoundId;
                    view.ShowDialog("현재 작업을 폐기하고 실험을 시작할까요?");
                    return true;
                }
                return Enter(index);
            }
            if (State==ExperimentState.Stored||State==ExperimentState.Viewing||State==ExperimentState.Failed) return StartCoin(index);
            if (State!=ExperimentState.Cleaning&&State!=ExperimentState.Ready) return false;
            pendingCoin=index;pendingLeave=false;resumeState=State;DialogRoundId=RoundId;
            State=ExperimentState.DiscardConfirmation;SetWork(false);view.ShowDialog("미보관 작업을 폐기하고 새 동전을 시작할까요?");return true;
        }

        private bool Enter(int index)
        {
            originalStageId=legacyStage.StageId??"stage-01";
            // 목록/로드에서 일시 차단한 enabled 값을 원래 사용자 설정으로 다시 캡처하지 않습니다.
            // 기존 결과/트레이 연출을 먼저 복원한 뒤 정상 UI 상태를 캡처합니다.
            completion.PrepareRound();
            if (legacyStage.State==CleaningStageState.Cleaning)
            { if(!legacyStage.RequestAbandon(legacyStage.RoundId)||!legacyStage.ConfirmAbandon(legacyStage.RoundId))return false; }
            else if(legacyStage.State==CleaningStageState.AbandonConfirmation)
            { if(!legacyStage.ConfirmAbandon(legacyStage.RoundId))return false; }
            legacyStage.enabled=false;
            originalUi=new bool[normalUi.Length];
            for(var i=0;i<normalUi.Length;i++)if(normalUi[i]!=null){originalUi[i]=normalUi[i].activeSelf;normalUi[i].SetActive(false);}
            return StartCoin(index);
        }

        private bool StartCoin(int index)
        {
            SetWork(false);State=ExperimentState.Preparing;LastError=null;CurrentResult=null;
            try
            {
                var error=definition.Validate();if(error!=null)throw new InvalidOperationException(error);
                if(RoundId==long.MaxValue)throw new InvalidOperationException("실험 판 ID 한계");
                coinIndex=index;RoundId++;goalAnnounced=false;lastDropFrame=-1;lastPercent=lastSecond=-1;
                completion.PrepareRound();pointer.ResetViewToInitialState();
                ReleaseDirt();
                var coin=definition.Coins[index];
                coinMesh.sharedMesh=coin.SurfaceMesh;coinCollider.sharedMesh=coin.SurfaceMesh;coinRenderer.sharedMaterial=coin.SurfaceMaterial;
                if(!pointer.RefreshSurfaceGeometry())throw new InvalidOperationException("실험 원판 접촉 갱신 실패");
                cleaner.SetReactionProfile(coin.ReactionProfile);surface.SetReactionProfile(coin.ReactionProfile);
                surface.ConfigureStage(definition.InitialB,0,CoinSurfaceDirtPattern.FixedPatches,coin.BaseColor);
                CreateDirt();cleaner.ReplaceDirt(dirt);
                var tools=new CleaningToolDefinition[definition.Tools.Count];for(var i=0;i<tools.Length;i++)tools[i]=definition.Tools[i];
                selection.ConfigureAvailableTools(tools);
                completion.ConfigureRound(coin.Id,RoundId,1,.95f,0);completion.ResetRoundState();
                foreach(var ui in normalUi)if(ui!=null)ui.SetActive(false);
                dirtRoot.SetActive(true);coinRenderer.enabled=true;
                EnsureTreatmentAudio();State=ExperimentState.Cleaning;SetWork(true);RefreshView();EvaluateGoal();return true;
            }
            catch(Exception error){Fail(error.Message);return false;}
        }

        private void CreateDirt()
        {
            dirtRoot=new GameObject("RB.Dirt");dirtRoot.SetActive(false);dirtRoot.transform.SetParent(coinRoot,false);
            dirt=new ObjectDirt[definition.Placements.Count];outlines=new LineRenderer[dirt.Length];
            var bounds=coinMesh.sharedMesh.bounds;var scale=coinRoot.lossyScale;
            if(scale.x<=0||scale.y<=0||scale.z<=0)throw new InvalidOperationException("실험 동전 양의 Scale 필요");
            if(outlineMaterial==null){outlineMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="RB.ScannerOutline",hideFlags=HideFlags.DontSave};outlineMaterial.SetColor("_BaseColor",new Color(.05f,.9f,1));}
            for(var i=0;i<dirt.Length;i++)
            {
                var p=definition.Placements[i];var sign=p.Face==CoinSurfaceFace.Front?1f:-1f;
                var go=new GameObject("RB.Dirt_"+i);go.SetActive(false);go.layer=2;go.transform.SetParent(dirtRoot.transform,false);
                var local=bounds.center+new Vector3((p.Uv.x*2-1)*bounds.extents.x,sign*bounds.extents.y,(p.Uv.y*2-1)*bounds.extents.x);
                go.transform.localPosition=coinRoot.InverseTransformPoint(coinMesh.transform.TransformPoint(local)+coinMesh.transform.up*sign*p.WorldRadius*.35f);
                go.transform.localRotation=sign>0?Quaternion.identity:Quaternion.Euler(180,0,0);
                go.AddComponent<MeshFilter>().sharedMesh=definition.DirtMesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=definition.DirtMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;
                dirt[i]=go.AddComponent<ObjectDirt>();var diameter=p.WorldRadius*2;
                dirt[i].Configure(p.Durability,new Vector3(diameter/scale.x,diameter*.35f/scale.y,diameter/scale.z));
                var ring=new GameObject("RB.ScannerRing");ring.layer=2;ring.transform.SetParent(go.transform,false);
                var line=ring.AddComponent<LineRenderer>();line.sharedMaterial=outlineMaterial;line.useWorldSpace=false;line.loop=true;
                line.positionCount=24;line.startWidth=line.endWidth=.012f;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
                for(var j=0;j<24;j++){var angle=j*Mathf.PI*2/24;line.SetPosition(j,new Vector3(Mathf.Cos(angle)*.56f,.6f,Mathf.Sin(angle)*.56f));}
                line.enabled=false;outlines[i]=line;go.SetActive(true);
            }
        }

        public bool SelectTool(int index)
        {
            if (!isActiveAndEnabled||!SaveReady||(State!=ExperimentState.Cleaning&&State!=ExperimentState.Ready)||index<0||index>=definition.Tools.Count) return false;
            return selection.TrySelect(definition.Tools[index]);
        }
        private void OnToolSelected(CleaningToolDefinition _) { if(IsActive)RefreshView(); }
        public bool ToggleScanner()
        {
            if(!isActiveAndEnabled)return false;
            if(!SaveReady||(State!=ExperimentState.Cleaning&&State!=ExperimentState.Ready&&State!=ExperimentState.Stored&&State!=ExperimentState.Viewing))return false;
            surface.SetScanner(!surface.ScannerEnabled);RefreshScannerOutlines(pointer.GameplayCamera);
            RefreshView();return true;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext _, Camera camera) => RefreshScannerOutlines(camera);

        private void RefreshScannerOutlines(Camera camera)
        {
            if(coinMesh==null)return; // 씬 종료에서 참조 대상이 먼저 파괴될 수 있습니다.
            // 깊이 검사만으로는 실루엣 밖으로 나온 반대면 원을 숨길 수 없습니다.
            var show=isActiveAndEnabled&&IsActive&&surface.ScannerEnabled&&camera!=null;
            var normalMatrix=coinMesh.transform.localToWorldMatrix.inverse.transpose;
            for(var i=0;i<outlines.Length;i++)
            {
                var line=outlines[i];if(line==null)continue;
                var sign=definition.Placements[i].Face==CoinSurfaceFace.Front?1f:-1f;
                var normal=normalMatrix.MultiplyVector(Vector3.up*sign).normalized;
                var towardCamera=camera==null?Vector3.zero:(camera.orthographic?-camera.transform.forward:camera.transform.position-dirt[i].transform.position).normalized;
                line.enabled=show&&!dirt[i].IsRemoved&&Vector3.Dot(normal,towardCamera)>.0001f;
            }
        }
        public bool RequestStore(long expectedRound)
        {
            if(!isActiveAndEnabled||!SaveReady||PersistenceBusy)return false;
            EvaluateGoal();
            if(expectedRound!=RoundId||State!=ExperimentState.Ready)return false;
            resumeState=State;State=ExperimentState.StoreConfirmation;DialogRoundId=RoundId;SetWork(false);
            view.ShowDialog("현재 표면을 파일에 보관할까요?\n보관 이후에는 관찰만 할 수 있습니다.");return true;
        }
        public bool RequestLeave()
        {
            if(!isActiveAndEnabled||!SaveReady)return false;
            if(!IsActive)return false;
            if(State==ExperimentState.Stored||State==ExperimentState.Viewing||State==ExperimentState.Failed){Leave();return true;}
            if(State!=ExperimentState.Cleaning&&State!=ExperimentState.Ready)return false;
            pendingLeave=true;pendingEntry=false;resumeState=State;DialogRoundId=RoundId;State=ExperimentState.DiscardConfirmation;
            SetWork(false);view.ShowDialog("미보관 작업을 폐기하고 실험을 종료할까요?");return true;
        }
        public bool CancelDialog(long expectedRound)
        {
            if(!isActiveAndEnabled)return false;
            if(pendingEntry)
            {
                if(expectedRound!=DialogRoundId)return false;
                pendingEntry=false;legacyStage.CancelAbandon(legacyStage.RoundId);view.HideDialog();return true;
            }
            if(expectedRound!=RoundId||expectedRound!=DialogRoundId||!IsDialog(State))return false;
            State=resumeState;if(State!=ExperimentState.SaveFailed&&!generalFailed)LastError=null;
            view.HideDialog();RestoreInput();RefreshView();RestoreErrorDialog();return true;
        }
        public bool ConfirmDialog(long expectedRound)
        {
            if(!isActiveAndEnabled)return false;
            if(pendingEntry)
            { if(expectedRound!=DialogRoundId)return false;pendingEntry=false;view.HideDialog();return Enter(pendingCoin); }
            if(expectedRound!=RoundId||expectedRound!=DialogRoundId||!IsDialog(State))return false;
            if(State==ExperimentState.StoreConfirmation)
            {
                return BeginStore();
            }
            if(State==ExperimentState.QuitConfirmation)
            {
                ConfirmAppQuit();
                return true;
            }
            view.HideDialog();if(pendingLeave)Leave();else StartCoin(pendingCoin);return true;
        }
        public bool RequestQuit()
        {
            if(!isActiveAndEnabled)return false;
            if(PersistenceBusy){quitAfterOperation=true;view.ShowSystemDialog("저장 파일 작업을 마친 뒤 종료를 확인합니다.",null,null);return true;}
            if(!IsActive){SetWork(false);legacyStage.enabled=false;view.ShowSystemDialog("미보관 청소는 저장되지 않습니다.\n보관된 결과와 일반 접근 정보는 유지됩니다. 종료할까요?",ConfirmAppQuit,()=>{view.HideDialog();RestoreInput();RestoreErrorDialog();},"종료","돌아가기");return true;}
            if(!IsActive||IsDialog(State)||State==ExperimentState.Preparing)return false;
            resumeState=State;State=ExperimentState.QuitConfirmation;DialogRoundId=RoundId;SetWork(false);
            view.ShowDialog("미보관 작업은 저장되지 않습니다.\n파일에 보관된 결과는 유지됩니다.\n종료할까요?");return true;
        }
        private bool WantsToQuit(){if(quitApproved&&!PersistenceBusy)return true;RequestQuit();return false;}
        private static bool IsDialog(ExperimentState state)=>state==ExperimentState.StoreConfirmation||state==ExperimentState.DiscardConfirmation||state==ExperimentState.QuitConfirmation;

        private void Leave()
        {
            SetWork(false);surface.SetScanner(false);ReleaseDirt();cleaner.SetReactionProfile(null);surface.SetReactionProfile(null);
            State=ExperimentState.Inactive;
            for(var i=0;i<normalUi.Length;i++)if(normalUi[i]!=null)normalUi[i].SetActive(originalUi[i]);
            audioFeedback.enabled=originalAudio;visualFeedback.enabled=originalVisual;legacyStage.enabled=true;
            if(!legacyStage.TryStartStage(originalStageId,legacyStage.RoundId))LastError=legacyStage.LastError;
            RefreshView();
        }
        private void SetWork(bool work,bool observe=false)
        {
            if(completion!=null)completion.SetEvaluationAllowed(false);
            if(pointer!=null)pointer.enabled=work||observe;if(cleaner!=null)cleaner.SetProcessingAllowed(work);if(surface!=null)surface.SetProcessingAllowed(work);
            if(tracker!=null)tracker.SetTimingPaused(!work);
            if(audioFeedback!=null){audioFeedback.enabled=work&&originalAudio;if(!work)audioFeedback.StopAudio();}
            if(visualFeedback!=null){visualFeedback.enabled=work&&originalVisual;if(!work)visualFeedback.StopFeedback();}
            if(!work&&treatmentAudio!=null)treatmentAudio.Stop();
        }
        private void Fail(string error)
        {
            LastError=error;State=ExperimentState.Failed;SetWork(false);ReleaseDirt();coinRenderer.enabled=false;RefreshView();ShowNotice(error);
        }
        private void ShowNotice(string text)
        {
            SetWork(false);if(!IsActive&&legacyStage!=null)legacyStage.enabled=false;
            view?.ShowNotice(text,RestoreInput);
        }
        private void RefreshView()
        {
            if(view==null)return;
            view.Refresh(IsActive,State,goalAnnounced?100:(IsActive?Progress.DisplayPercent:0),State==ExperimentState.Viewing?CurrentResult.ElapsedSeconds:tracker.ElapsedSeconds,
                stored.Count,maximumStoredResults,CurrentCoin?.DisplayName,selection.CurrentTool,ScannerEnabled,definition.Tools,
                goalAnnounced?1:(IsActive?(float)Progress.Value:0),CurrentResult!=null&&!CurrentResult.HasSide);
        }
        private void OnTreatment(CoinSurfaceContact _)
        {
            if(State!=ExperimentState.Cleaning&&State!=ExperimentState.Ready)return;
            tracker.NotifyTreatmentApplied();EnsureTreatmentAudio();treatmentAudio.PlayOneShot(dropClip,.18f);
        }
        private void EnsureTreatmentAudio()
        {
            if(treatmentAudio!=null)return;
            var go=new GameObject("RB.TreatmentAudio");go.transform.SetParent(transform,false);treatmentAudio=go.AddComponent<AudioSource>();
            treatmentAudio.playOnAwake=false;treatmentAudio.spatialBlend=0;
            const int n=4410;var samples=new float[n];
            for(var i=0;i<n;i++){var t=i/44100f;samples[i]=Mathf.Sin(2*Mathf.PI*(1300*t-3500*t*t))*Mathf.Exp(-t*55)*.5f;}
            dropClip=AudioClip.Create("RB.Droplet",n,1,44100,false);dropClip.SetData(samples,0);
        }
        private void ReleaseDirt()
        {
            cleaner.ReplaceDirt(Array.Empty<ObjectDirt>());
            if(dirtRoot!=null){dirtRoot.SetActive(false);Destroy(dirtRoot);}dirtRoot=null;dirt=Array.Empty<ObjectDirt>();outlines=Array.Empty<LineRenderer>();
        }
        private void OnDestroy()
        {
            Application.wantsToQuit-=WantsToQuit;
            RenderPipelineManager.beginCameraRendering-=OnBeginCameraRendering;
            if(surface!=null)surface.TreatmentApplied-=OnTreatment;if(selection!=null)selection.ToolSelected-=OnToolSelected;
            view?.Dispose();if(dirtRoot!=null)Destroy(dirtRoot);if(outlineMaterial!=null)Destroy(outlineMaterial);
            if(treatmentAudio!=null)Destroy(treatmentAudio.gameObject);if(dropClip!=null)Destroy(dropClip);
            DisposePersistence();
        }
        private void OnDisable()
        {
            if(view!=null){SetWork(false);if(legacyStage!=null)legacyStage.enabled=false;}
            view?.SetSuspended(true);
        }
        private void OnEnable()
        {
            if(view==null)return;
            view.SetSuspended(false);
            RestoreInput();
            RefreshView();
        }
    }
}
