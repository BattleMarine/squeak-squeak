using UnityEngine;
using UnityEngine.UI;

namespace SqueakSqueak.Cleaning
{
    /// <summary>
    /// 완료된 청소 판의 결과 수치를 표시하는 UI 전용 컴포넌트입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CleaningResultPanel : MonoBehaviour
    {
        [SerializeField]
        private GameObject resultRoot;

        [SerializeField]
        private Text cleanlinessText;

        [SerializeField]
        private Text polishText;

        [SerializeField]
        private Text completionTimeText;

        private void Awake()
        {
            if (resultRoot != null && cleanlinessText != null && polishText != null && completionTimeText != null)
            {
                Hide();
                return;
            }

            Debug.LogError("CleaningResultPanel requires result root and all result Text references.", this);
            enabled = false;
        }

        public void Show(float cleanliness, float polish, float elapsedSeconds)
        {
            resultRoot.SetActive(true);
            cleanlinessText.text = $"청결도: {ToPercent(cleanliness)}%";
            polishText.text = $"광택도: {ToPercent(polish)}%";

            var wholeSeconds = Mathf.Max(0, Mathf.FloorToInt(elapsedSeconds));
            completionTimeText.text = $"완료 시간: {wholeSeconds / 60:00}:{wholeSeconds % 60:00}";
        }

        public void Hide()
        {
            resultRoot.SetActive(false);
        }

        private static int ToPercent(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f);
        }
    }
}
