using UnityEngine;
using UnityEngine.UI;

namespace SqueakSqueak.Cleaning
{
    /// <summary>
    /// 청소 진행 상태를 화면 텍스트로 표시합니다.
    /// HUD는 진행 상태를 변경하지 않고 표시값만 갱신합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CleaningProgressHud : MonoBehaviour
    {
        [SerializeField]
        private CleaningProgressTracker progressTracker;

        [SerializeField]
        private Text objectDirtText;

        [SerializeField]
        private Text surfaceDirtText;

        [SerializeField]
        private Text polishText;

        [SerializeField]
        private Text timerText;

        private int lastObjectDirtPercent = -1;
        private int lastSurfaceDirtPercent = -1;
        private int lastPolishPercent = -1;
        private int lastElapsedSeconds = -1;

        private void Awake()
        {
            if (progressTracker != null && objectDirtText != null && surfaceDirtText != null && polishText != null && timerText != null)
            {
                return;
            }

            Debug.LogError("CleaningProgressHud requires tracker and all progress Text references.", this);
            enabled = false;
        }

        private void OnEnable()
        {
            ResetDisplayedValues();
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            var objectDirtPercent = ToPercent(progressTracker.ObjectDirtRemovalRatio);
            if (objectDirtPercent != lastObjectDirtPercent)
            {
                lastObjectDirtPercent = objectDirtPercent;
                objectDirtText.text = $"A 오염 제거: {objectDirtPercent}%";
            }

            var surfaceDirtPercent = ToPercent(progressTracker.SurfaceDirtRemovalRatio);
            if (surfaceDirtPercent != lastSurfaceDirtPercent)
            {
                lastSurfaceDirtPercent = surfaceDirtPercent;
                surfaceDirtText.text = $"B 표면 제거: {surfaceDirtPercent}%";
            }

            var polishPercent = ToPercent(progressTracker.Polish);
            if (polishPercent != lastPolishPercent)
            {
                lastPolishPercent = polishPercent;
                polishText.text = $"광택: {polishPercent}%";
            }

            var elapsedSeconds = Mathf.FloorToInt(progressTracker.ElapsedSeconds);
            if (elapsedSeconds != lastElapsedSeconds)
            {
                lastElapsedSeconds = elapsedSeconds;
                timerText.text = $"시간: {elapsedSeconds / 60:00}:{elapsedSeconds % 60:00}";
            }
        }

        private void ResetDisplayedValues()
        {
            lastObjectDirtPercent = -1;
            lastSurfaceDirtPercent = -1;
            lastPolishPercent = -1;
            lastElapsedSeconds = -1;
        }

        private static int ToPercent(float ratio)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(ratio) * 100f);
        }
    }
}
