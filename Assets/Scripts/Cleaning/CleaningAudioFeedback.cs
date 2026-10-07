using UnityEngine;

namespace SqueakSqueak.Cleaning
{
    /// <summary>기존 사용 gate와 읽기 알림을 소리로만 표현합니다. 청소/타이머/완료 상태는 쓰지 않습니다.</summary>
    [DisallowMultipleComponent]
    public sealed class CleaningAudioFeedback : MonoBehaviour
    {
        [SerializeField] private CoinSurfacePointer surfacePointer;
        [SerializeField] private CleaningToolSelection toolSelection;
        [SerializeField] private CoinSurfaceContamination surfaceContamination;
        [SerializeField] private ObjectDirtCleaner objectDirtCleaner;
        [SerializeField] private CleaningCompletionController completion;
        [SerializeField] private AudioSource frictionSource, removalSource, completionSource;
        [SerializeField] private AudioClip dryClothClip, smallBrushClip, polishingClothClip, removalClip, completionClip;
        [SerializeField, Range(0f, 1f), Tooltip("지속 마찰음의 선형 음량입니다.")] private float frictionVolume = .12f;
        [SerializeField, Range(0f, 1f)] private float removalVolume = .22f, completionVolume = .2f;
        [SerializeField, Min(.005f), Tooltip("마찰음 시작/강조 음량을 올리는 시간(초)입니다. 차단은 즉시 Stop합니다.")] private float attackSeconds = .025f;
        private bool subscribed, pendingRemoval, pendingChange, completionPlayed, hasFocus = true;
        private int removalFrame, changeFrame;
        private CleaningToolId removalTool, changedTool;
        private CoinSurfaceFace changedFace;
        private float accentRemaining;

        private void Awake()
        {
            if (surfacePointer == null || toolSelection == null || surfaceContamination == null || objectDirtCleaner == null || completion == null ||
                frictionSource == null || removalSource == null || completionSource == null || frictionSource == removalSource ||
                frictionSource == completionSource || removalSource == completionSource || dryClothClip == null || smallBrushClip == null ||
                polishingClothClip == null || removalClip == null || completionClip == null)
            { Debug.LogError("CleaningAudioFeedback requires source/gate/owner and three distinct sources/five clips.", this); enabled = false; return; }
            StopAudio();
        }

        private void OnEnable()
        {
            if (subscribed || surfacePointer == null || toolSelection == null || surfaceContamination == null || objectDirtCleaner == null || completion == null) return;
            surfacePointer.ContactInvalidated += OnContactInvalidated;
            toolSelection.ToolSelected += OnToolSelected;
            surfaceContamination.SurfaceReset += StopAudio;
            surfaceContamination.SurfaceContactChanged += OnSurfaceChanged;
            objectDirtCleaner.ContactCleaningApplied += OnObjectChanged;
            objectDirtCleaner.DirtRemoved += OnDirtRemoved;
            completion.RoundCompleted += OnCompleted;
            completion.RoundRestarted += OnRestarted;
            hasFocus = Application.isFocused;
            completionPlayed = completion.IsCompleted; // 완료 중 재활성으로 완료음을 다시 만들지 않습니다.
            subscribed = true;
        }

        private void OnDisable()
        {
            if (subscribed)
            {
                if (surfacePointer != null) surfacePointer.ContactInvalidated -= OnContactInvalidated;
                if (toolSelection != null) toolSelection.ToolSelected -= OnToolSelected;
                if (surfaceContamination != null) { surfaceContamination.SurfaceReset -= StopAudio; surfaceContamination.SurfaceContactChanged -= OnSurfaceChanged; }
                if (objectDirtCleaner != null) { objectDirtCleaner.ContactCleaningApplied -= OnObjectChanged; objectDirtCleaner.DirtRemoved -= OnDirtRemoved; }
                if (completion != null) { completion.RoundCompleted -= OnCompleted; completion.RoundRestarted -= OnRestarted; }
                subscribed = false;
            }
            StopAudio();
        }

        private void OnApplicationFocus(bool focused) { hasFocus = focused; if (!focused) StopAudio(); }
        private void OnToolSelected(CleaningToolDefinition _) => StopAudio();
        private void OnContactInvalidated() { StopUseAudio(); if (completion == null || !completion.IsCompleted) StopSource(completionSource); }
        private void OnRestarted() { StopAudio(); completionPlayed = false; }
        private void OnCompleted()
        {
            StopUseAudio();
            if (completionPlayed || !hasFocus || !isActiveAndEnabled || !completion.IsCompleted || !completionSource.isActiveAndEnabled) return;
            completionPlayed = true;
            completionSource.clip = completionClip; completionSource.volume = completionVolume; completionSource.Play();
        }
        private void OnSurfaceChanged(SurfaceCleaningChange change)
        {
            if (!change.Contact.IsCleanable || (!change.DirtChanged && !change.PolishChanged)) return;
            pendingChange = true; changeFrame = Time.frameCount; changedTool = change.Tool; changedFace = change.Contact.Face;
        }
        private void OnObjectChanged(RaycastHit hit)
        {
            if (toolSelection.CurrentTool == null || !CoinSurfaceMapper.TryMapPoint(surfacePointer.SurfaceLocalToWorldMatrix,
                surfacePointer.SurfaceLocalBounds, hit.point, hit.normal, out var contact) || !contact.IsCleanable) return;
            pendingChange = true; changeFrame = Time.frameCount; changedTool = toolSelection.CurrentTool.ToolId; changedFace = contact.Face;
        }
        private void OnDirtRemoved(ObjectDirt _)
        {
            if (toolSelection.CurrentTool == null) return;
            pendingRemoval = true; removalFrame = Time.frameCount; removalTool = toolSelection.CurrentTool.ToolId;
        }
        private void LateUpdate()
        {
            var valid = surfacePointer.TryGetToolUseContact(out var contact);
            AdvanceAudio(Time.unscaledDeltaTime, valid, contact);
        }

        // Editor 검사만 합성 gate/dt를 주입합니다. 런타임에는 공개 입력 우회 API를 두지 않습니다.
        private void AdvanceAudio(float deltaTime, bool validUse, CoinSurfaceContact contact)
        {
            if (!completion.isActiveAndEnabled || !surfaceContamination.isActiveAndEnabled || !objectDirtCleaner.isActiveAndEnabled)
            { StopAudio(); return; }
            var tool = toolSelection.CurrentTool;
            if (!validUse || !hasFocus || !contact.IsCleanable || !surfacePointer.isActiveAndEnabled || completion.IsCompleted || tool == null)
            { StopUseAudio(); return; } // 완료 원샷은 닫힌 사용 gate와 독립적입니다.
            var clip = tool.ToolId == CleaningToolId.SmallBrush ? smallBrushClip : tool.ToolId == CleaningToolId.PolishingCloth ? polishingClothClip : dryClothClip;
            if (frictionSource.clip != clip) { StopSource(frictionSource); frictionSource.clip = clip; }
            if (frictionSource.isActiveAndEnabled && !frictionSource.isPlaying) { frictionSource.volume = 0f; frictionSource.Play(); }
            if (pendingChange && Time.frameCount - changeFrame <= 1 && changedTool == tool.ToolId && changedFace == contact.Face) accentRemaining = .08f;
            else accentRemaining = Mathf.Max(0f, accentRemaining - Mathf.Max(0f, deltaTime));
            pendingChange = false;
            var targetVolume = frictionVolume * (accentRemaining > 0f ? 1.15f : 1f);
            frictionSource.volume = Mathf.MoveTowards(frictionSource.volume, targetVolume, 1.15f * frictionVolume * Mathf.Max(0f, deltaTime) / attackSeconds);
            // A 여러 개 제거는 프레임당 묶음1회. 재생 중인 제거음에 추가 음성을 중첩/예약하지 않습니다.
            if (pendingRemoval && Time.frameCount - removalFrame <= 1 && removalTool == tool.ToolId && contact.Face == CoinSurfaceFace.Front &&
                removalSource.isActiveAndEnabled && !removalSource.isPlaying)
            { removalSource.clip = removalClip; removalSource.volume = removalVolume; removalSource.Play(); }
            pendingRemoval = false;
        }

        private void StopUseAudio()
        {
            pendingChange = pendingRemoval = false; accentRemaining = 0f;
            StopSource(frictionSource); StopSource(removalSource);
        }
        public void StopAudio() { StopUseAudio(); StopSource(completionSource); }
        private static void StopSource(AudioSource source) { if (source == null) return; source.Stop(); source.volume = 0f; }
        private void OnValidate()
        {
            frictionVolume = Mathf.Clamp01(frictionVolume); removalVolume = Mathf.Clamp01(removalVolume); completionVolume = Mathf.Clamp01(completionVolume);
            attackSeconds = Mathf.Max(.005f, attackSeconds);
        }
    }
}
