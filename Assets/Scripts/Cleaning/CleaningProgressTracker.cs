using System;
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
        private ObjectDirtCleaner objectDirtCleaner;

        [SerializeField]
        private CoinSurfaceContamination surfaceContamination;

        private bool isTimingStopped;
        private bool isTimingPaused;
        private double previousTimingTimestamp;
        private bool wasTimingFocused;

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
        public float SideDirtRemovalRatio=>surfaceContamination==null?0:surfaceContamination.SideDirtRemovalRatio;

        public float Cleanliness => Mathf.Clamp01((ObjectDirtRemovalRatio + SurfaceDirtRemovalRatio) * 0.5f);

        public bool IsTimingStopped => isTimingStopped;
        public bool IsTimingPaused => isTimingPaused;

        /// <summary>
        /// HUD가 청소 적용 직후의 수치를 즉시 표시할 수 있도록 알립니다.
        /// </summary>
        public event Action ProgressChanged;

        private void Awake()
        {
            if (objectDirtCleaner != null && surfaceContamination != null)
            {
                return;
            }

            Debug.LogError(
                "CleaningProgressTracker requires Object Dirt Cleaner and Surface Contamination references.",
                this);
            enabled = false;
        }

        private void OnEnable()
        {
            previousTimingTimestamp = Time.realtimeSinceStartupAsDouble;
            wasTimingFocused = Application.isFocused;
            objectDirtCleaner.CleaningApplied += HandleCleaningApplied;
            surfaceContamination.CleaningApplied += HandleCleaningApplied;
        }

        private void OnDisable()
        {
            if (objectDirtCleaner != null)
            {
                objectDirtCleaner.CleaningApplied -= HandleCleaningApplied;
            }

            if (surfaceContamination != null)
            {
                surfaceContamination.CleaningApplied -= HandleCleaningApplied;
            }
        }

        private void Update()
        {
            AdvanceTiming(Time.realtimeSinceStartupAsDouble, Application.isFocused);
        }

        private void OnApplicationFocus(bool focused)
        {
            // 마지막 프레임 이후의 기존 포커스 구간까지만 합산합니다.
            // background=false여서 Update가 멈췄어도 복귀한 프레임의 큰 dt를 더하지 않습니다.
            AdvanceTiming(Time.realtimeSinceStartupAsDouble, wasTimingFocused);
            wasTimingFocused = focused;
        }

        private void AdvanceTiming(double now, bool focused)
        {
            var interval = Math.Max(0d, now - previousTimingTimestamp);
            var shouldAccumulate = HasStarted && !isTimingStopped && !isTimingPaused && wasTimingFocused && focused;
            previousTimingTimestamp = now;
            wasTimingFocused = focused;
            if (shouldAccumulate)
            {
                var previousWholeSeconds = Mathf.FloorToInt(ElapsedSeconds);
                ElapsedSeconds += (float)interval;
                if (Mathf.FloorToInt(ElapsedSeconds) != previousWholeSeconds)
                {
                    ProgressChanged?.Invoke();
                }
            }
        }

        /// <summary>
        /// 완료 결과에 표시할 시간이 더 늘어나지 않도록 현재 판의 타이머를 멈춥니다.
        /// </summary>
        public void StopTiming()
        {
            isTimingStopped = true;
        }

        /// <summary>선택/포기 확인 시간은 제외하며 취소 후에도 기존 경과 시간을 보존합니다.</summary>
        public void SetTimingPaused(bool paused)
        {
            if (isTimingPaused == paused) return;
            AdvanceTiming(Time.realtimeSinceStartupAsDouble, Application.isFocused);
            isTimingPaused = paused;
            previousTimingTimestamp = Time.realtimeSinceStartupAsDouble;
            wasTimingFocused = Application.isFocused;
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
            isTimingPaused = false;
            previousTimingTimestamp = Time.realtimeSinceStartupAsDouble;
            wasTimingFocused = Application.isFocused;
            ProgressChanged?.Invoke();
        }

        private void HandleCleaningApplied()
        {
            if (isTimingStopped || isTimingPaused)
            {
                return;
            }

            if (!HasStarted)
            {
                previousTimingTimestamp = Time.realtimeSinceStartupAsDouble;
                wasTimingFocused = Application.isFocused;
                HasStarted = true;
            }
            ProgressChanged?.Invoke();
        }

        /// <summary>첫 유효 풀림도 실제 작업 활동입니다. 스캐너/무효 클릭에서는 호출하지 않습니다.</summary>
        public void NotifyTreatmentApplied() => HandleCleaningApplied();

    }
}
