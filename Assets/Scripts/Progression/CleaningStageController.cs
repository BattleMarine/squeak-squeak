using System;
using System.Collections.Generic;
using UnityEngine;
using SqueakSqueak.Cleaning;

namespace SqueakSqueak.Progression
{
    public enum CleaningStageState { Selecting, Preparing, Cleaning, Result, AbandonConfirmation }

    /// <summary>단계/판/전환 상태의 단일 소유자입니다. 진행 거래·저장·상점·최종 UI는 담당하지 않습니다.</summary>
    [DisallowMultipleComponent]
    public sealed class CleaningStageController : MonoBehaviour
    {
        [SerializeField] private ProgressionCatalog catalog;
        [SerializeField] private string initialStageId = "stage-01";
        [SerializeField] private CleaningCompletionController completion;
        [SerializeField] private CleaningProgressTracker progress;
        [SerializeField] private CoinSurfacePointer pointer;
        [SerializeField] private ObjectDirtCleaner objectCleaner;
        [SerializeField] private CoinSurfaceContamination surface;
        [SerializeField] private CleaningToolSelection selection;
        [SerializeField] private CleaningResultPanel resultPanel;
        [SerializeField] private MeshFilter coinMesh;
        [SerializeField] private MeshCollider coinCollider;
        [SerializeField] private Renderer coinRenderer;
        [SerializeField] private Transform coinRoot;
        [SerializeField, Tooltip("원본 고정 A 배치를 런타임에 숨깁니다. 원본 에셋/씬 오염은 삭제하지 않습니다.")]
        private GameObject originalDirtRoot;
        [SerializeField] private CleaningAudioFeedback audioFeedback;
        [SerializeField] private CleaningVisualFeedback visualFeedback;

        private StageAccessContext access;
        private GameObject generatedDirtRoot;
        private ObjectDirt[] currentDirt = Array.Empty<ObjectDirt>();
        private bool initialized, subscribed;
        public CleaningStageState State { get; private set; } = CleaningStageState.Selecting;
        public StageDefinition CurrentStage { get; private set; }
        public string StageId => CurrentStage != null ? CurrentStage.Id : null;
        public long RoundId { get; private set; }
        public string LastError { get; private set; }
        public int ActiveDirtCount => currentDirt.Length;
        public event Action<CleaningStageState> StateChanged;
        public event Action<string, long> StageRoundCompleted;

        private void Start()
        {
            if (!TryInitializeRuntime())
            { Debug.LogError("Stage initialization failed: " + LastError, this); enabled = false; }
        }

        private bool TryInitializeRuntime()
        {
            if (!DependenciesValid()) return false;
            initialized = true;
            BlockProcessing();
            if (!InitializeAccess()) return false;
            Subscribe();
            return TryStartStage(initialStageId, RoundId);
        }

        private bool InitializeAccess()
        {
            // 입력을 닫은 뒤 검증합니다. 잘못된 초기 소유 ID가 생성자에서 예외를 내도 판은 열리지 않습니다.
            BlockProcessing();
            try
            {
                var issues = ProgressionCatalogValidator.Validate(catalog);
                if (issues.Count > 0) return Fail(issues[0]);
                var candidate = new StageAccessContext(catalog.InitiallyOwnedToolIds, Array.Empty<string>());
                if (!candidate.Validate(catalog, out var error)) return Fail(error);
                access = candidate;
                return true;
            }
            catch (Exception exception) { return Fail("초기 접근 정보 오류: " + exception.Message); }
        }

        private bool DependenciesValid()
        {
            if (catalog != null && completion != null && progress != null && pointer != null && objectCleaner != null &&
                surface != null && selection != null && resultPanel != null && coinMesh != null && coinCollider != null &&
                coinRenderer != null && coinRoot != null && originalDirtRoot != null && audioFeedback != null && visualFeedback != null &&
                coinMesh.transform == coinCollider.transform && coinMesh.transform == coinRenderer.transform && coinMesh.transform.IsChildOf(coinRoot)) return true;
            LastError = "단계 실행에 필요한 참조가 누락되거나 동전 참조가 일치하지 않습니다.";
            // 설정 오류에서도 옛 전역 목표로 청소가 진행되지 않게 닫습니다.
            if (completion != null) completion.SetEvaluationAllowed(false);
            if (pointer != null) pointer.enabled = false;
            if (objectCleaner != null) objectCleaner.SetProcessingAllowed(false);
            if (surface != null) surface.SetProcessingAllowed(false);
            if (progress != null) progress.SetTimingPaused(true);
            return false;
        }

        private void Subscribe()
        {
            if (subscribed) return;
            completion.RoundCompletedIdentified += OnRoundCompleted;
            completion.SetRestartHandler(RestartCurrentRound);
            subscribed = true;
        }

        private void OnEnable()
        {
            if (!initialized) return;
            Subscribe();
            if (State == CleaningStageState.Cleaning) ResumeProcessing();
        }

        private void OnDisable()
        {
            if (!initialized) return;
            BlockProcessing();
            if (subscribed && completion != null)
            { completion.RoundCompletedIdentified -= OnRoundCompleted; }
            subscribed = false;
        }

        private void OnDestroy()
        {
            if (completion != null && initialized) completion.SetRestartHandler(null);
            if (objectCleaner != null && initialized) objectCleaner.ReplaceDirt(Array.Empty<ObjectDirt>());
            ReleaseDirtRoot(generatedDirtRoot);
        }

        /// <summary>후속 진행 소유자가 조회한 접근 스냅샷을 주입합니다. 완료/구매 기록을 자체 생성하지 않습니다.</summary>
        public bool TrySetAccessContext(StageAccessContext context)
        {
            if (!initialized || !isActiveAndEnabled || (State != CleaningStageState.Selecting && State != CleaningStageState.Result))
                return Fail("선택/결과 상태에서만 접근 정보를 갱신할 수 있습니다.");
            if (context == null) return Fail("접근 정보가 없습니다.");
            var issues = ProgressionCatalogValidator.Validate(catalog);
            if (issues.Count > 0) return Fail(issues[0]);
            if (!context.Validate(catalog, out var error)) return Fail(error);
            access = context;
            LastError = null;
            return true;
        }

        /// <summary>기대 판 ID를 확인해 이전 판의 늦은 전환 명령을 거부합니다.</summary>
        public bool TryStartStage(string stageId, long expectedRoundId)
        {
            if (!initialized || !isActiveAndEnabled || expectedRoundId != RoundId ||
                (State != CleaningStageState.Selecting && State != CleaningStageState.Result))
                return Fail("현재 상태/판에서는 단계를 시작할 수 없습니다.");
            var issues = ProgressionCatalogValidator.Validate(catalog);
            if (issues.Count > 0) return Fail(issues[0]);
            if (!access.Validate(catalog, out var accessError)) return Fail(accessError);
            StageDefinition stage = null;
            foreach (var candidate in catalog.Stages) if (candidate.Id == stageId) { stage = candidate; break; }
            if (stage == null) return Fail("정의되지 않은 단계: " + stageId);
            if (!access.CanEnter(stage, out var error)) return Fail(error);
            if (!CoinSurfaceMapper.TryGetSupportedBounds(stage.Coin.SurfaceMesh, out var bounds)) return Fail("지원되지 않는 동전 Mesh입니다.");
            if (RoundId == long.MaxValue) return Fail("판 ID가 한계에 도달했습니다.");
            var tools = new List<CleaningToolDefinition>();
            var unavailable = new Dictionary<CleaningToolDefinition, string>();
            foreach (var tool in catalog.Tools)
            {
                if (!access.Owns(tool.Id)) unavailable[tool.Definition] = "미보유 도구";
                else
                {
                    var allowed = false;
                    foreach (var id in stage.AllowedToolIds) if (id == tool.Id) { allowed = true; break; }
                    if (!allowed) unavailable[tool.Definition] = "이 단계 사용 불가";
                }
            }
            foreach (var id in stage.AllowedToolIds)
                if (access.Owns(id)) foreach (var tool in catalog.Tools) if (tool.Id == id) tools.Add(tool.Definition);
            var retry = State == CleaningStageState.Result && CurrentStage == stage;
            GameObject preparedRoot = null;
            try
            {
                // 할당/구성 실패는 입력 차단 이전에도 기존 결과에 영향을 주지 않습니다.
                preparedRoot = new GameObject("StageDirt");
                preparedRoot.SetActive(false);
                var preparedDirt = CreateDirt(preparedRoot, stage);
                BlockProcessing();
                SetState(CleaningStageState.Preparing);
                // 실패한 준비 판 ID도 소비합니다. 나중에 같은 ID로 다른 판이 열리지 않습니다.
                var nextRound = RoundId + 1;
                RoundId = nextRound;
                completion.ConfigureRound(stage.Id, nextRound, stage.RequiredA, stage.RequiredB, stage.RequiredPolish);
                completion.PrepareRound(retry);
                pointer.ResetViewToInitialState();
                originalDirtRoot.SetActive(false);
                coinMesh.sharedMesh = stage.Coin.SurfaceMesh;
                coinCollider.sharedMesh = stage.Coin.SurfaceMesh;
                coinRenderer.sharedMaterial = stage.Coin.SurfaceMaterial;
                if (!pointer.RefreshSurfaceGeometry()) throw new InvalidOperationException("Coin pointer refresh failed.");
                surface.ConfigureStage(stage.InitialSurfaceDirt, stage.InitialPolish, stage.DirtPattern, stage.Coin.BaseColor);
                objectCleaner.SetReactionProfile(stage.Coin.ReactionProfile);
                surface.SetReactionProfile(stage.Coin.ReactionProfile);
                PositionDirt(preparedRoot, preparedDirt, stage, bounds);
                objectCleaner.ReplaceDirt(preparedDirt);
                selection.ConfigureAvailableTools(tools.ToArray(), unavailable);
                completion.ResetRoundState();
                ReleaseDirtRoot(generatedDirtRoot);
                generatedDirtRoot = preparedRoot;
                preparedRoot = null;
                currentDirt = preparedDirt;
                CurrentStage = stage;
                LastError = null;
                coinRenderer.enabled = true;
                generatedDirtRoot.SetActive(true);
                // 타이머/입력은 모든 설정/초기화가 끝난 뒤에만 재개합니다.
                SetState(CleaningStageState.Cleaning);
                if (State == CleaningStageState.Cleaning) ResumeProcessing();
                if (retry) completion.NotifyRoundRestarted();
                return true;
            }
            catch (Exception exception)
            {
                ReleaseDirtRoot(preparedRoot);
                if (State == CleaningStageState.Preparing || State == CleaningStageState.Cleaning)
                {
                    BlockProcessing();
                    objectCleaner.ReplaceDirt(Array.Empty<ObjectDirt>());
                    ReleaseDirtRoot(generatedDirtRoot);
                    generatedDirtRoot = null; currentDirt = Array.Empty<ObjectDirt>(); CurrentStage = null;
                    coinRenderer.enabled = false;
                    resultPanel.Hide();
                    SetState(CleaningStageState.Selecting);
                }
                return Fail("단계 적용 실패: " + exception.Message);
            }
        }

        private ObjectDirt[] CreateDirt(GameObject root, StageDefinition stage)
        {
            var dirt = new ObjectDirt[stage.ObjectDirt.Count];
            for (var i = 0; i < dirt.Length; i++)
            {
                var go = new GameObject("Dirt_" + (i + 1));
                go.SetActive(false); go.layer = 2; go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = catalog.ObjectDirtMesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = catalog.ObjectDirtMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                dirt[i] = go.AddComponent<ObjectDirt>();
            }
            return dirt;
        }

        private void PositionDirt(GameObject root, ObjectDirt[] dirt, StageDefinition stage, Bounds bounds)
        {
            root.transform.SetParent(coinRoot, false);
            var rootScale = coinRoot.lossyScale;
            if (rootScale.x <= 0 || rootScale.y <= 0 || rootScale.z <= 0) throw new InvalidOperationException("Positive coin scale required.");
            for (var i = 0; i < dirt.Length; i++)
            {
                var placement = stage.ObjectDirt[i];
                var sign = placement.Face == CoinSurfaceFace.Front ? 1f : -1f;
                var local = bounds.center + new Vector3((placement.Uv.x * 2 - 1) * bounds.extents.x,
                    sign * bounds.extents.y, (placement.Uv.y * 2 - 1) * bounds.extents.x);
                var world = coinMesh.transform.TransformPoint(local) + coinMesh.transform.up * sign * placement.WorldRadius * .35f;
                dirt[i].transform.localPosition = coinRoot.InverseTransformPoint(world);
                var diameter = placement.WorldRadius * 2;
                dirt[i].Configure(placement.Durability, new Vector3(diameter / rootScale.x, diameter * .35f / rootScale.y, diameter / rootScale.z));
                dirt[i].gameObject.SetActive(true); // 비활성 부모 아래이므로 Awake는 새 판 설정 뒤 실행됩니다.
            }
        }

        public bool RestartCurrentRound() => State == CleaningStageState.Result && completion.IsCompleted &&
            TryStartStage(StageId, RoundId);

        public bool RequestAbandon(long expectedRoundId)
        {
            if (!Matches(expectedRoundId, CleaningStageState.Cleaning)) return Fail("포기 확인을 열 수 없습니다.");
            BlockProcessing(); SetState(CleaningStageState.AbandonConfirmation); return true;
        }

        public bool CancelAbandon(long expectedRoundId)
        {
            if (!Matches(expectedRoundId, CleaningStageState.AbandonConfirmation)) return Fail("포기를 취소할 수 없습니다.");
            LastError = null; SetState(CleaningStageState.Cleaning);
            if (State == CleaningStageState.Cleaning) ResumeProcessing();
            return true;
        }

        public bool ConfirmAbandon(long expectedRoundId)
        {
            if (!Matches(expectedRoundId, CleaningStageState.AbandonConfirmation)) return Fail("포기를 확정할 수 없습니다.");
            completion.PrepareRound();
            objectCleaner.ReplaceDirt(Array.Empty<ObjectDirt>());
            ReleaseDirtRoot(generatedDirtRoot); generatedDirtRoot = null; currentDirt = Array.Empty<ObjectDirt>();
            pointer.ResetViewToInitialState();
            progress.ResetProgress(); progress.SetTimingPaused(true);
            resultPanel.Hide(); CurrentStage = null; coinRenderer.enabled = false;
            LastError = null; SetState(CleaningStageState.Selecting); return true;
        }

        private void OnRoundCompleted(string stageId, long roundId)
        {
            if (!Matches(roundId, CleaningStageState.Cleaning) || StageId != stageId) return;
            objectCleaner.SetProcessingAllowed(false); surface.SetProcessingAllowed(false); progress.SetTimingPaused(true);
            SetState(CleaningStageState.Result);
            StageRoundCompleted?.Invoke(stageId, roundId);
        }

        private bool Matches(long id, CleaningStageState state) => initialized && isActiveAndEnabled && id == RoundId && State == state;
        private void BlockProcessing()
        {
            completion.SetEvaluationAllowed(false); pointer.enabled = false;
            objectCleaner.SetProcessingAllowed(false); surface.SetProcessingAllowed(false);
            progress.SetTimingPaused(true); audioFeedback.StopAudio(); visualFeedback.StopFeedback();
        }
        private void ResumeProcessing()
        {
            progress.SetTimingPaused(false); objectCleaner.SetProcessingAllowed(true); surface.SetProcessingAllowed(true); completion.ResumeRound();
        }
        private void SetState(CleaningStageState state) { State = state; StateChanged?.Invoke(state); }
        private bool Fail(string error) { LastError = error; return false; }
        private static void ReleaseDirtRoot(GameObject root)
        {
            if (root == null) return;
            root.SetActive(false);
            if (Application.isPlaying) Destroy(root); else DestroyImmediate(root);
        }
    }
}
