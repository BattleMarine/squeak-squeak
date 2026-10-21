using UnityEngine;

namespace SqueakSqueak.Cleaning
{
    /// <summary>실제 변경 이벤트를 기존 사용 gate와 대조해 표시합니다. 청소/진행 수치는 쓰지 않습니다.</summary>
    [DisallowMultipleComponent]
    public sealed class CleaningVisualFeedback : MonoBehaviour
    {
        [SerializeField] private CoinSurfacePointer surfacePointer;
        [SerializeField] private CleaningToolSelection toolSelection;
        [SerializeField] private CoinSurfaceContamination surfaceContamination;
        [SerializeField] private ObjectDirtCleaner objectDirtCleaner;
        [SerializeField] private CleaningCompletionController completion;
        [SerializeField] private LineRenderer[] contactLines = new LineRenderer[3];
        [SerializeField] private ParticleSystem removalParticles;
        [SerializeField, Min(.01f)] [Tooltip("마지막 실제 변화 이후 접촉 선이 사라지는 시간(초)입니다.")]
        private float contactLifetime = .12f;
        [SerializeField, Min(0f)] [Tooltip("접촉 선을 표면 법선 방향으로 띄우는 월드 거리입니다.")]
        private float surfaceOffset = .025f;

        private readonly Vector3[] removedLocalPoints = new Vector3[5];
        private readonly CleaningToolId[] removedTools = new CleaningToolId[5];
        private readonly CoinSurfaceFace[] removedFaces = new CoinSurfaceFace[5];
        private int pendingRemovalCount;
        private bool subscribed, pendingChange;
        private SurfaceCleaningChange lastChange;
        private int changeFrame;
        private float contactRemaining, animationTime;
        private CoinSurfaceFace lastUseFace;

        public bool IsContactVisible { get; private set; }
        public int ActiveRemovalParticleCount => removalParticles != null ? removalParticles.particleCount : 0;

        private void Awake()
        {
            if (surfacePointer == null || toolSelection == null || surfaceContamination == null ||
                objectDirtCleaner == null || completion == null || removalParticles == null ||
                contactLines == null || contactLines.Length != 3)
            { Debug.LogError("CleaningVisualFeedback requires source, gate, completion, three lines and removal particles.", this); enabled = false; return; }
            foreach (var line in contactLines)
            {
                if (line == null || line.sharedMaterial == null)
                { Debug.LogError("CleaningVisualFeedback contact material/reference is missing.", this); enabled = false; return; }
                line.useWorldSpace = true; line.positionCount = 3; line.loop = false;
            }
            StopFeedback();
        }

        private void OnEnable()
        {
            if (subscribed || surfacePointer == null || toolSelection == null || surfaceContamination == null || objectDirtCleaner == null) return;
            surfacePointer.ContactInvalidated += StopFeedback;
            toolSelection.ToolSelected += OnToolSelected;
            surfaceContamination.SurfaceContactChanged += OnSurfaceChanged;
            surfaceContamination.SurfaceReset += StopFeedback;
            objectDirtCleaner.ContactCleaningApplied += OnObjectChanged;
            objectDirtCleaner.DirtRemoved += OnDirtRemoved;
            subscribed = true;
        }

        private void OnDisable()
        {
            if (subscribed)
            {
                if (surfacePointer != null) surfacePointer.ContactInvalidated -= StopFeedback;
                if (toolSelection != null) toolSelection.ToolSelected -= OnToolSelected;
                if (surfaceContamination != null)
                { surfaceContamination.SurfaceContactChanged -= OnSurfaceChanged; surfaceContamination.SurfaceReset -= StopFeedback; }
                if (objectDirtCleaner != null)
                { objectDirtCleaner.ContactCleaningApplied -= OnObjectChanged; objectDirtCleaner.DirtRemoved -= OnDirtRemoved; }
                subscribed = false;
            }
            StopFeedback();
        }

        private void OnApplicationFocus(bool focused) { if (!focused) StopFeedback(); }
        private void OnToolSelected(CleaningToolDefinition _) => StopFeedback();

        private void LateUpdate()
        {
            var valid = surfacePointer.TryGetToolUseContact(out var contact);
            AdvanceFeedback(Time.deltaTime, valid, contact);
        }

        private void OnSurfaceChanged(SurfaceCleaningChange change)
        {
            if (!change.Contact.IsCleanable || (!change.DirtChanged && !change.PolishChanged)) return;
            // 두 소유자의 Update 순서는 정하지 않습니다. 같은 프레임/도구/면/접촉 정보만 합칩니다.
            if (pendingChange && changeFrame == Time.frameCount && lastChange.Tool == change.Tool &&
                lastChange.Contact.Face == change.Contact.Face &&
                (lastChange.Contact.LocalPosition - change.Contact.LocalPosition).sqrMagnitude <= 1e-10f)
                change = change.WithFlags(change.DirtChanged || lastChange.DirtChanged, change.PolishChanged || lastChange.PolishChanged);
            lastChange = change; pendingChange = true; changeFrame = Time.frameCount;
        }

        private void OnObjectChanged(RaycastHit hit)
        {
            var matrix = surfacePointer.SurfaceLocalToWorldMatrix;
            if (toolSelection.CurrentTool != null && CoinSurfaceMapper.TryMapPoint(matrix, surfacePointer.SurfaceLocalBounds,
                hit.point, hit.normal, out var contact))
                OnSurfaceChanged(new SurfaceCleaningChange(contact, toolSelection.CurrentTool, true, false));
        }

        private void OnDirtRemoved(ObjectDirt dirt)
        {
            var tool = toolSelection.CurrentTool;
            if (tool == null || pendingRemovalCount == removedLocalPoints.Length) return;
            var matrix = surfacePointer.SurfaceLocalToWorldMatrix;
            var local = matrix.inverse.MultiplyPoint3x4(dirt.transform.position);
            var bounds = surfacePointer.SurfaceLocalBounds;
            var face=local.y>=bounds.center.y?CoinSurfaceFace.Front:CoinSurfaceFace.Back;
            local.y = bounds.center.y + (face==CoinSurfaceFace.Front?1:-1)*bounds.extents.y;
            removedLocalPoints[pendingRemovalCount] = local;
            removedFaces[pendingRemovalCount] = face;
            removedTools[pendingRemovalCount++] = tool.ToolId;
        }

        // Editor 검사는 승인된 합성 접촉으로 이 private 표현 단계만 호출합니다. 런타임 공개 입력 우회 API가 아닙니다.
        private void AdvanceFeedback(float deltaTime, bool validUse, CoinSurfaceContact contact)
        {
            var tool = toolSelection.CurrentTool;
            if (!validUse || !contact.IsCleanable || !surfacePointer.isActiveAndEnabled || !surfaceContamination.isActiveAndEnabled ||
                surfaceContamination.SurfaceMask == null || !objectDirtCleaner.isActiveAndEnabled || completion.IsCompleted || tool == null)
            { StopFeedback(); return; }
            if (lastUseFace != CoinSurfaceFace.Invalid && lastUseFace != contact.Face && removalParticles.particleCount > 0)
                removalParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            lastUseFace = contact.Face;
            animationTime += Mathf.Max(0f, deltaTime);
            if (pendingChange && Time.frameCount - changeFrame <= 1 && lastChange.Tool == tool.ToolId && lastChange.Contact.Face == contact.Face)
                contactRemaining = contactLifetime;
            else contactRemaining = Mathf.Max(0f, contactRemaining - Mathf.Max(0f, deltaTime));
            pendingChange = false;
            var matrix = surfacePointer.SurfaceLocalToWorldMatrix;
            var normal = contact.Face==CoinSurfaceFace.Side?contact.WorldNormal:matrix.inverse.transpose.MultiplyVector(contact.Face == CoinSurfaceFace.Front ? Vector3.up : Vector3.down).normalized;
            var right = matrix.MultiplyVector(contact.Face==CoinSurfaceFace.Side?Vector3.up:Vector3.right).normalized;
            var forward = Vector3.Cross(right, normal).normalized;
            // 단절된 위치/면으로 옛 변화 선을 운반하지 않습니다.
            if (lastChange.Contact.Face != contact.Face ||
                Vector3.Distance(matrix.MultiplyPoint3x4(lastChange.Contact.LocalPosition), contact.WorldPosition) > tool.BrushRadius)
                contactRemaining = 0f;
            IsContactVisible = contactRemaining > 0f;
            if (IsContactVisible) DrawContact(contact.WorldPosition + normal * surfaceOffset, right, forward, tool);
            else foreach (var line in contactLines) line.enabled = false;
            for (var i = 0; i < pendingRemovalCount; i++)
                if (removedTools[i] == tool.ToolId && removedFaces[i] == contact.Face) EmitRemoval(matrix.MultiplyPoint3x4(removedLocalPoints[i]), normal, right, forward, tool.FeedbackToolId);
            pendingRemovalCount = 0;
        }

        private void DrawContact(Vector3 center, Vector3 right, Vector3 forward, CleaningToolDefinition tool)
        {
            var size = tool.BrushRadius * .55f;
            var color = tool.FeedbackToolId == CleaningToolId.SmallBrush ? new Color(1f, .67f, .3f, .8f) :
                tool.FeedbackToolId == CleaningToolId.PolishingCloth && lastChange.PolishChanged ? new Color(1f, .98f, .7f, .95f) :
                new Color(.55f, .9f, 1f, .7f);
            color.a *= Mathf.Clamp01(contactRemaining / contactLifetime);
            for (var i = 0; i < contactLines.Length; i++)
            {
                var line = contactLines[i]; line.enabled = true; line.startColor = line.endColor = color;
                line.widthMultiplier = tool.FeedbackToolId == CleaningToolId.SmallBrush ? .012f : .008f;
                if (tool.FeedbackToolId == CleaningToolId.PolishingCloth && lastChange.PolishChanged)
                {
                    var angle = i * Mathf.PI / 3f;
                    var axis = right * Mathf.Cos(angle) + forward * Mathf.Sin(angle);
                    var length = size * (.65f + .15f * Mathf.Sin(animationTime * 12f));
                    line.SetPosition(0, center - axis * length); line.SetPosition(1, center); line.SetPosition(2, center + axis * length);
                }
                else if (tool.FeedbackToolId == CleaningToolId.SmallBrush)
                {
                    var offset = forward * ((i - 1) * size * .5f);
                    line.SetPosition(0, center + offset - right * size * .55f - forward * size * .3f);
                    line.SetPosition(1, center + offset + forward * size * .25f);
                    line.SetPosition(2, center + offset + right * size * .55f - forward * size * .3f);
                }
                else
                {
                    var offset = forward * ((i - 1) * size * .45f);
                    var bend = forward * (Mathf.Sin(animationTime * 18f + i) * size * .15f);
                    line.SetPosition(0, center + offset - right * size);
                    line.SetPosition(1, center + offset + bend); line.SetPosition(2, center + offset + right * size);
                }
            }
        }

        private void EmitRemoval(Vector3 center, Vector3 normal, Vector3 right, Vector3 forward, CleaningToolId tool)
        {
            if (!removalParticles.isPlaying) removalParticles.Play();
            for (var i = 0; i < 8; i++)
            {
                var angle = i * Mathf.PI * .25f;
                var direction = right * Mathf.Cos(angle) + forward * Mathf.Sin(angle);
                var emit = new ParticleSystem.EmitParams
                {
                    position = center + normal * surfaceOffset,
                    velocity = direction * (tool == CleaningToolId.SmallBrush ? .65f : .35f) + normal * .12f,
                    startLifetime = .32f, startSize = .035f,
                    startColor = tool == CleaningToolId.SmallBrush ? new Color(.65f, .43f, .2f, .85f) : new Color(.8f, .72f, .5f, .7f),
                    randomSeed = (uint)(i + 1)
                };
                removalParticles.Emit(emit, 1);
            }
        }

        public void StopFeedback()
        {
            pendingChange = false; pendingRemovalCount = 0; contactRemaining = 0f; animationTime = 0f;
            lastChange = default; IsContactVisible = false;
            lastUseFace = CoinSurfaceFace.Invalid;
            if (contactLines != null) foreach (var line in contactLines) if (line != null) line.enabled = false;
            if (removalParticles != null && (!removalParticles.isStopped || removalParticles.particleCount > 0))
                removalParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnValidate() { contactLifetime = Mathf.Max(.01f, contactLifetime); surfaceOffset = Mathf.Max(0f, surfaceOffset); }
    }
}
