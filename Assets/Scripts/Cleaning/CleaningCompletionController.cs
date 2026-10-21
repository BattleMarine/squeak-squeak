using System;
using UnityEngine;

namespace SqueakSqueak.Cleaning
{
    /// <summary>
    /// 청소 판의 완료 조건을 판정하고 결과 상태로 한 번만 전환합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CleaningCompletionController : MonoBehaviour
    {
        [SerializeField]
        private CleaningProgressTracker progressTracker;

        [SerializeField]
        private CoinSurfacePointer surfacePointer;

        [SerializeField]
        private CleaningResultPanel resultPanel;

        [SerializeField]
        private ObjectDirtCleaner objectDirtCleaner;

        [SerializeField]
        private CoinSurfaceContamination surfaceContamination;

        [SerializeField]
        private CleaningToolSelection toolSelection;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("완료에 필요한 A 타입 오염 제거율입니다.")]
        private float requiredObjectDirtRemovalRatio = 0.9f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("완료에 필요한 B 타입 표면 오염 제거율입니다.")]
        private float requiredSurfaceDirtRemovalRatio = 0.8f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("완료에 필요한 광택도입니다.")]
        private float requiredPolish = 0.7f;

        public bool IsCompleted { get; private set; }
        public bool EvaluationAllowed { get; private set; } = true;
        public string StageId { get; private set; }
        public long RoundId { get; private set; }
        private Func<bool> restartHandler;
        // 표시/UI는 판정에 쓰이는 값을 읽습니다. 별도 목표/완료 상태를 소유하지 않습니다.
        public float RequiredObjectDirtRemovalRatio => requiredObjectDirtRemovalRatio;
        public float RequiredSurfaceDirtRemovalRatio => requiredSurfaceDirtRemovalRatio;
        public float RequiredPolish => requiredPolish;
        /// <summary>결과 상태 전환이 끝난 판당1회 알림입니다. 완료 판정 권한은 이 컴포넌트에만 있습니다.</summary>
        public event Action RoundCompleted;
        /// <summary>재시작 승인 직후, 청소/뷰 초기화 전에 표현 취소·Transform 복원용 읽기 알림입니다.</summary>
        public event Action RoundRestarting;
        /// <summary>전체 초기 상태와 입력 복원이 끝난 재시작1회 알림입니다.</summary>
        public event Action RoundRestarted;
        /// <summary>단계 적용/포기 때 표현 취소용 알림입니다. 완료 판 재시작과 구분합니다.</summary>
        public event Action RoundPreparing;
        public event Action<string, long> RoundCompletedIdentified;

        private void Awake()
        {
            if (progressTracker != null &&
                surfacePointer != null &&
                resultPanel != null &&
                objectDirtCleaner != null &&
                surfaceContamination != null &&
                toolSelection != null)
            {
                return;
            }

            Debug.LogError(
                "CleaningCompletionController requires progress, pointer, result, object dirt, surface contamination, and tool selection references.",
                this);
            enabled = false;
        }

        private void Update()
        {
            TryCompleteIfEligible();
        }

        /// <summary>
        /// 세 완료 조건을 동시에 만족할 때만 완료 상태로 전환합니다.
        /// 이미 완료된 판에서는 다시 전환하지 않습니다.
        /// </summary>
        public bool TryCompleteIfEligible()
        {
            if (!isActiveAndEnabled || !EvaluationAllowed || IsCompleted ||
                progressTracker.ObjectDirtRemovalRatio < requiredObjectDirtRemovalRatio ||
                progressTracker.SurfaceDirtRemovalRatio < requiredSurfaceDirtRemovalRatio ||
                surfaceContamination.SideDirtRemovalRatio < requiredSurfaceDirtRemovalRatio ||
                progressTracker.Polish < requiredPolish)
            {
                return false;
            }

            IsCompleted = true;
            var completedStageId = StageId;
            var completedRoundId = RoundId;
            progressTracker.StopTiming();
            surfacePointer.enabled = false;
            resultPanel.Show(progressTracker.Cleanliness, progressTracker.Polish, progressTracker.ElapsedSeconds);
            RoundCompleted?.Invoke();
            RoundCompletedIdentified?.Invoke(completedStageId, completedRoundId);
            return true;
        }

        /// <summary>
        /// 완료된 청소 판을 초기 상태로 되돌리고 다음 판의 입력을 다시 활성화합니다.
        /// </summary>
        public bool RestartRound()
        {
            if (!IsCompleted)
            {
                return false;
            }

            if (restartHandler != null) return restartHandler();

            PrepareRound(true);
            ResetRoundState();
            ResumeRound();
            NotifyRoundRestarted();
            return true;
        }

        public void SetRestartHandler(Func<bool> handler) => restartHandler = handler;

        public void SetEvaluationAllowed(bool allowed)
        {
            EvaluationAllowed = allowed;
            if (surfacePointer != null) surfacePointer.enabled = allowed && !IsCompleted;
        }

        /// <summary>완료 플래그 우회 없이 모든 새 판 경로가 쓰는 명시적인 준비 단계입니다.</summary>
        public void PrepareRound(bool isCompletedRetry = false)
        {
            SetEvaluationAllowed(false);
            if (isCompletedRetry) RoundRestarting?.Invoke();
            else RoundPreparing?.Invoke();
        }

        public void ConfigureRound(string stageId, long roundId, float requiredA, float requiredB, float polish)
        {
            if (EvaluationAllowed) throw new InvalidOperationException("Block evaluation before configuring a round.");
            if (string.IsNullOrEmpty(stageId) || roundId <= 0 || !ValidRatio(requiredA) || !ValidRatio(requiredB) || !ValidRatio(polish))
                throw new ArgumentException("Invalid round identity or goals.");
            StageId = stageId; RoundId = roundId;
            requiredObjectDirtRemovalRatio = requiredA;
            requiredSurfaceDirtRemovalRatio = requiredB;
            requiredPolish = polish;
        }

        public bool TryCompleteRound(string stageId, long roundId) =>
            StageId == stageId && RoundId == roundId && TryCompleteIfEligible();

        /// <summary>입력을 닫은 채 상태만 복원합니다. 마지막 ResumeRound 전에는 완료 판정하지 않습니다.</summary>
        public void ResetRoundState()
        {
            if (EvaluationAllowed) throw new InvalidOperationException("Prepare the round first.");
            objectDirtCleaner.ResetAllDirt();
            surfaceContamination.ResetSurface();
            progressTracker.ResetProgress();
            toolSelection.ResetToDefaultTool();
            resultPanel.Hide();
            IsCompleted = false;
            surfacePointer.ResetViewToInitialState();
        }

        public void ResumeRound() => SetEvaluationAllowed(true);
        public void NotifyRoundRestarted() => RoundRestarted?.Invoke();
        private static bool ValidRatio(float value) => float.IsFinite(value) && value >= 0 && value <= 1;

        private void OnValidate()
        {
            requiredObjectDirtRemovalRatio = Mathf.Clamp01(requiredObjectDirtRemovalRatio);
            requiredSurfaceDirtRemovalRatio = Mathf.Clamp01(requiredSurfaceDirtRemovalRatio);
            requiredPolish = Mathf.Clamp01(requiredPolish);
        }
    }
}
