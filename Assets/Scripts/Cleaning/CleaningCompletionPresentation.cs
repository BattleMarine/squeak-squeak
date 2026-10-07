using UnityEngine;
using UnityEngine.UI;

namespace SqueakSqueak.Cleaning
{
    public enum CompletionPresentationPhase { Idle, Showing, Moving, Stored }

    /// <summary>완료 알림을 실제 동전1개의 감상/보관으로 표현합니다. 청소/결과 수치는 변경하지 않습니다.</summary>
    [DisallowMultipleComponent]
    public sealed class CleaningCompletionPresentation : MonoBehaviour
    {
        [SerializeField] private CleaningCompletionController completion;
        [SerializeField] private Transform coin;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private GameObject storageTray;
        [SerializeField] private Text storageStatusText;
        [SerializeField] private GameObject toolPanel;
        [SerializeField] private GameObject controlsGuide;
        [SerializeField] private CleaningAudioFeedback audioFeedback;
        [SerializeField] private CleaningVisualFeedback visualFeedback;
        [SerializeField, Min(.1f), Tooltip("완료 동전을 감상하는 시간(초, unscaled)입니다.")] private float showSeconds = 1.4f;
        [SerializeField, Min(.1f), Tooltip("트레이로 이동하는 시간(초, unscaled)입니다.")] private float moveSeconds = 1.1f;
        [SerializeField, Tooltip("감상 위치(월드)입니다.")] private Vector3 showPosition = new(-.35f, 1.2f, 0f);
        [SerializeField, Tooltip("트레이의 동전 중심 위치(월드)입니다.")] private Vector3 storedPosition = new(2.6f, 1.16f, -1.3f);
        [SerializeField, Range(.1f, 1f), Tooltip("보관 동전의 초기 localScale 대비 배율입니다.")] private float storedScaleRatio = .38f;
        [SerializeField, Min(.1f), Tooltip("감상/보관 중 Orthographic Size(월드 반높이)입니다.")] private float displayZoom = 2f;

        private Transform initialParent;
        private Vector3 initialLocalPosition, initialLocalScale;
        private Quaternion initialLocalRotation, initialWorldRotation;
        private float initialZoom, elapsed;
        private bool initialized, subscribed, initialToolPanelActive, initialControlsGuideActive;
        public CompletionPresentationPhase Phase { get; private set; }

        private void Awake()
        {
            if (completion == null || coin == null || gameplayCamera == null || storageTray == null || storageStatusText == null || toolPanel == null || controlsGuide == null ||
                audioFeedback == null || visualFeedback == null || !gameplayCamera.orthographic)
            { Debug.LogError("CleaningCompletionPresentation requires completion, original coin/camera, tray/status and feedback references.", this); enabled = false; return; }
            initialParent = coin.parent; initialLocalPosition = coin.localPosition; initialLocalRotation = coin.localRotation;
            initialLocalScale = coin.localScale; initialWorldRotation = coin.rotation; initialZoom = gameplayCamera.orthographicSize;
            initialToolPanelActive = toolPanel.activeSelf; initialControlsGuideActive = controlsGuide.activeSelf;
            initialized = true; storageTray.SetActive(false);
        }

        private void OnEnable()
        {
            if (!initialized || subscribed) return;
            completion.RoundCompleted += OnCompleted;
            completion.RoundRestarting += CancelAndRestore;
            subscribed = true;
            // 완료 중 재활성화는 알림/감상 재생 없이 현재 결과를 보관 위치로만 표현합니다.
            if (completion.IsCompleted) { ShowCoin(); SetStored(); }
        }

        private void OnDisable()
        {
            if (subscribed)
            {
                if (completion != null) { completion.RoundCompleted -= OnCompleted; completion.RoundRestarting -= CancelAndRestore; }
                subscribed = false;
            }
            CancelAndRestore();
        }

        private void OnDestroy() => CancelAndRestore();

        private void OnCompleted()
        {
            if (!initialized || !isActiveAndEnabled || !completion.IsCompleted || Phase != CompletionPresentationPhase.Idle) return;
            visualFeedback.StopFeedback();
            ShowCoin(); elapsed = 0f; Phase = CompletionPresentationPhase.Showing;
            storageStatusText.text = "완성된 동전을 잠깐 감상하세요";
        }

        private void ShowCoin()
        {
            coin.SetPositionAndRotation(showPosition, initialWorldRotation);
            coin.localScale = initialLocalScale; gameplayCamera.orthographicSize = displayZoom;
            storageTray.SetActive(true); toolPanel.SetActive(false); controlsGuide.SetActive(false);
        }

        private void Update() => AdvancePresentation(Time.unscaledDeltaTime);

        // 합성 dt 검사는 Editor 전용입니다. 런타임 진입점은 완료 소유자의 알림뿐입니다.
        private void AdvancePresentation(float deltaTime)
        {
            if (Phase == CompletionPresentationPhase.Idle || Phase == CompletionPresentationPhase.Stored) return;
            if (!completion.IsCompleted) { CancelAndRestore(); return; }
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f) return;
            elapsed += deltaTime;
            if (Phase == CompletionPresentationPhase.Showing)
            {
                if (elapsed < showSeconds) return;
                elapsed -= showSeconds; Phase = CompletionPresentationPhase.Moving;
                storageStatusText.text = "트레이로 옮기는 중";
            }
            var t = Mathf.Clamp01(elapsed / moveSeconds);
            // 결과 카드를 가로지르지 않도록 먼저 아래로 축소 이동한 뒤 트레이로 옮깁니다.
            var waypoint = new Vector3(showPosition.x, storedPosition.y, storedPosition.z);
            var leg = Mathf.SmoothStep(0f, 1f, t <= .5f ? t * 2f : (t - .5f) * 2f);
            coin.SetPositionAndRotation(t <= .5f ? Vector3.Lerp(showPosition, waypoint, leg) : Vector3.Lerp(waypoint, storedPosition, leg), initialWorldRotation);
            coin.localScale = initialLocalScale * (t <= .5f ? Mathf.Lerp(1f, storedScaleRatio, leg) : storedScaleRatio);
            if (elapsed >= moveSeconds) SetStored();
        }

        private void SetStored()
        {
            coin.SetPositionAndRotation(storedPosition, initialWorldRotation);
            coin.localScale = initialLocalScale * storedScaleRatio; elapsed = 0f; Phase = CompletionPresentationPhase.Stored;
            storageStatusText.text = "트레이 보관 완료 · 새 청소 가능";
        }

        private void CancelAndRestore()
        {
            var wasPresenting = Phase != CompletionPresentationPhase.Idle;
            elapsed = 0f; Phase = CompletionPresentationPhase.Idle;
            if (!initialized || !wasPresenting) return;
            if (audioFeedback != null) audioFeedback.StopAudio();
            if (visualFeedback != null) visualFeedback.StopFeedback();
            if (coin != null)
            {
                coin.SetParent(initialParent, false);
                coin.localPosition = initialLocalPosition; coin.localRotation = initialLocalRotation; coin.localScale = initialLocalScale;
            }
            if (gameplayCamera != null) gameplayCamera.orthographicSize = initialZoom;
            if (storageTray != null) storageTray.SetActive(false);
            if (toolPanel != null) toolPanel.SetActive(initialToolPanelActive);
            if (controlsGuide != null) controlsGuide.SetActive(initialControlsGuideActive);
            if (storageStatusText != null) storageStatusText.text = "";
        }

        private void OnValidate()
        {
            showSeconds = Mathf.Max(.1f, showSeconds); moveSeconds = Mathf.Max(.1f, moveSeconds);
            storedScaleRatio = Mathf.Clamp(storedScaleRatio, .1f, 1f); displayZoom = Mathf.Max(.1f, displayZoom);
        }
    }
}
