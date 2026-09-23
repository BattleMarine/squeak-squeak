using UnityEngine;
using UnityEngine.UI;

namespace SqueakSqueak.Cleaning
{
    /// <summary>
    /// 결과 화면의 다시 시작 버튼을 현재 청소 판의 재시작 경로에 연결합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CleaningRestartButton : MonoBehaviour
    {
        [SerializeField]
        private CleaningCompletionController completionController;

        [SerializeField]
        private Button button;

        private void Awake()
        {
            if (completionController == null || button == null)
            {
                Debug.LogError("CleaningRestartButton requires completion controller and Button references.", this);
                enabled = false;
                return;
            }

            button.onClick.AddListener(RestartRound);
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(RestartRound);
            }
        }

        private void RestartRound()
        {
            completionController.RestartRound();
        }
    }
}
