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
            if (IsCompleted ||
                progressTracker.ObjectDirtRemovalRatio < requiredObjectDirtRemovalRatio ||
                progressTracker.SurfaceDirtRemovalRatio < requiredSurfaceDirtRemovalRatio ||
                progressTracker.Polish < requiredPolish)
            {
                return false;
            }

            IsCompleted = true;
            progressTracker.StopTiming();
            surfacePointer.enabled = false;
            resultPanel.Show(progressTracker.Cleanliness, progressTracker.Polish, progressTracker.ElapsedSeconds);
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

            objectDirtCleaner.ResetAllDirt();
            surfaceContamination.ResetSurface();
            progressTracker.ResetProgress();
            toolSelection.ResetToDefaultTool();
            resultPanel.Hide();
            IsCompleted = false;

            if (!surfacePointer.enabled)
            {
                surfacePointer.enabled = true;
            }

            surfacePointer.ResetViewToInitialState();
            return true;
        }

        private void OnValidate()
        {
            requiredObjectDirtRemovalRatio = Mathf.Clamp01(requiredObjectDirtRemovalRatio);
            requiredSurfaceDirtRemovalRatio = Mathf.Clamp01(requiredSurfaceDirtRemovalRatio);
            requiredPolish = Mathf.Clamp01(requiredPolish);
        }
    }
}
