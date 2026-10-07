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
            RoundCompleted?.Invoke();
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

            RoundRestarting?.Invoke();
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
            RoundRestarted?.Invoke();
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
