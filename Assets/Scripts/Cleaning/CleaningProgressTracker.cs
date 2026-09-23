using UnityEngine;

namespace SqueakSqueak.Cleaning
{
    /// <summary>
    /// 현재 청소 판의 진행률과 경과 시간을 기존 청소 상태에서 계산합니다.
    /// 이 컴포넌트는 오염 수치를 소유하거나 변경하지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CleaningProgressTracker : MonoBehaviour
    {
        [SerializeField]
        private CoinSurfacePointer surfacePointer;

        [SerializeField]
        private ObjectDirtCleaner objectDirtCleaner;

        [SerializeField]
        private CoinSurfaceContamination surfaceContamination;

        private bool isApplicationFocused = true;
        private bool isTimingStopped;

        public bool HasStarted { get; private set; }

        public float ElapsedSeconds { get; private set; }

        public float ObjectDirtRemovalRatio => objectDirtCleaner == null
            ? 0f
            : Mathf.Clamp01(objectDirtCleaner.RemovalRatio);

        public float SurfaceDirtRemovalRatio => surfaceContamination == null
            ? 0f
            : Mathf.Clamp01(surfaceContamination.SurfaceDirtRemovalRatio);

        public float Polish => surfaceContamination == null
            ? 0f
            : Mathf.Clamp01(surfaceContamination.Polish);

        public float Cleanliness => Mathf.Clamp01((ObjectDirtRemovalRatio + SurfaceDirtRemovalRatio) * 0.5f);

        public bool IsTimingStopped => isTimingStopped;

        private void Awake()
        {
            if (surfacePointer != null && objectDirtCleaner != null && surfaceContamination != null)
            {
                return;
            }

            Debug.LogError(
                "CleaningProgressTracker requires Coin Surface Pointer, Object Dirt Cleaner, and Surface Contamination references.",
                this);
            enabled = false;
        }

        private void Update()
        {
            if (isTimingStopped)
            {
                return;
            }

            if (!HasStarted && surfacePointer.TryGetToolUseSurfaceHit(out _))
            {
                HasStarted = true;
            }

            if (HasStarted && isApplicationFocused)
            {
                ElapsedSeconds += Time.unscaledDeltaTime;
            }
        }

        /// <summary>
        /// 완료 결과에 표시할 시간이 더 늘어나지 않도록 현재 판의 타이머를 멈춥니다.
        /// </summary>
        public void StopTiming()
        {
            isTimingStopped = true;
        }

        /// <summary>
        /// 새 청소 판을 위해 타이머 상태만 초기화합니다.
        /// 오염·광택 상태는 각 소유 컴포넌트가 복원합니다.
        /// </summary>
        public void ResetProgress()
        {
            HasStarted = false;
            ElapsedSeconds = 0f;
            isTimingStopped = false;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            isApplicationFocused = hasFocus;
        }
    }
}
