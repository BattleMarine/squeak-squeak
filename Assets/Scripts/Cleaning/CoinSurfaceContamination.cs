using System;
using UnityEngine;

namespace SqueakSqueak.Cleaning
{
    [DisallowMultipleComponent]
    public sealed class CoinSurfaceContamination : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int FrontMaskId = Shader.PropertyToID("_FrontSurfaceMask");
        private static readonly int BackMaskId = Shader.PropertyToID("_BackSurfaceMask");
        private static readonly int BoundsId = Shader.PropertyToID("_SurfaceBounds");
        private static readonly int HalfHeightId = Shader.PropertyToID("_SurfaceHalfHeight");
        private static readonly int DirtColorId = Shader.PropertyToID("_DirtColor");
        private static readonly int DirtySmoothnessId = Shader.PropertyToID("_DirtySmoothness");
        private static readonly int MaximumSmoothnessId = Shader.PropertyToID("_MaximumSmoothness");

        [SerializeField]
        private CoinSurfacePointer surfacePointer;

        [SerializeField]
        private CleaningToolSelection toolSelection;

        [SerializeField]
        private Renderer coinRenderer;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("새 판 B 마스크 패턴의 초기 강도(0~1)입니다. 판 중 변경은 다음 Reset부터 적용합니다.")]
        private float initialSurfaceDirt = 1f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("새 청소 판이 시작될 때의 광택 수치입니다.")]
        private float initialPolish;

        [SerializeField, Range(CoinSurfaceMaskData.MinimumResolution, CoinSurfaceMaskData.MaximumResolution)]
        [Tooltip("앞면과 뒷면 각각의 B 마스크 한 변 텍셀 수입니다. 기본 256입니다.")]
        private int surfaceMaskResolution = CoinSurfaceMaskData.DefaultResolution;

        [SerializeField]
        [Tooltip("앞뒤 B 마스크의 초기 패턴입니다. 판 중 변경은 다음 Reset부터 적용합니다.")]
        private CoinSurfaceDirtPattern surfaceDirtPattern = CoinSurfaceDirtPattern.FixedPatches;

        [SerializeField]
        [Tooltip("표면 오염이 가득할 때의 동전 색입니다.")]
        private Color dirtColor = new(0.16f, 0.075f, 0.03f, 1f);

        [SerializeField, Range(0f, 1f)]
        private float dirtySmoothness = 0.1f;

        [SerializeField, Range(0f, 1f)]
        private float maximumSmoothness = 0.9f;

        private MaterialPropertyBlock propertyBlock;
        private Color cleanBaseColor;
        private float cleanSmoothness;
        private float cleanMetallic;
        private Bounds surfaceBounds;
        private Color32[] maskUploadPixels;
        private long uploadedMaskRevision = -1;
        private readonly CoinSurfaceBrushStroke surfaceStroke = new();
        private long appliedRubFrame = -1;
        public bool ProcessingAllowed { get; private set; } = true;

        public bool HasSurfaceContactHistory => surfaceStroke.HasPreviousContact;

        // B의 유일한 상태 원본입니다. 텍스처와 표시값은 이 데이터에서 파생됩니다.
        public CoinSurfaceMaskData SurfaceMask { get; private set; }
        public Texture2D FrontDirtMask { get; private set; }
        public Texture2D BackDirtMask { get; private set; }

        /// <summary>유효 앞뒤 텍셀의 잔여 B 면적 평균(0~1)입니다. 제거율과 구분합니다.</summary>
        public float SurfaceDirt => SurfaceMask == null ? 0f :
            Mathf.Clamp01((float)(SurfaceMask.CurrentDirtSum / (2d * SurfaceMask.ValidTexelCount)));

        /// <summary>
        /// 현재 청소 판이 시작할 때의 B 오염량을 기준으로 계산한 제거율입니다.
        /// </summary>
        // 데이터 생성 때 고정된 초기 합을 사용합니다. 설정 필드는 판 중 분모에 관여하지 않습니다.
        // 미초기화/해제 상태는 완료된 빈 마스크(초기 합0)와 다르게 취급합니다.
        public float SurfaceDirtRemovalRatio => SurfaceMask == null ? 0f : SurfaceMask.RemovalRatio;

        /// <summary>유효 양면 지역 광택의 면적 평균입니다. 한 접촉이 다른 영역을 올리지 않습니다.</summary>
        public float Polish => SurfaceMask == null ? 0f : SurfaceMask.MeanPolish;

        /// <summary>
        /// B 타입 오염 또는 광택이 실제로 변경됐을 때 발생합니다.
        /// </summary>
        public event Action CleaningApplied;
        /// <summary>실제 B/광택 변화1회의 접촉·도구·변화 종류입니다. 입력 승인이나 추가 청소를 요청하지 않습니다.</summary>
        public event Action<SurfaceCleaningChange> SurfaceContactChanged;
        /// <summary>ResetSurface가 끝날 때1회 발생합니다. 청소 변화/타이머 시작 이벤트가 아닙니다.</summary>
        public event Action SurfaceReset;

        private void Awake()
        {
            if (!TryResolveDependencies())
            {
                enabled = false;
                return;
            }

            propertyBlock = new MaterialPropertyBlock();
            CaptureCleanMaterialValues();
            ResetSurface();
        }

        private void OnEnable()
        {
            if (surfacePointer != null) surfacePointer.ContactInvalidated += BreakSurfaceContact;
            if (toolSelection != null) toolSelection.ToolSelected += HandleToolSelected;
        }

        private void OnDisable()
        {
            if (surfacePointer != null) surfacePointer.ContactInvalidated -= BreakSurfaceContact;
            if (toolSelection != null) toolSelection.ToolSelected -= HandleToolSelected;
            BreakSurfaceContact();
        }

        private void HandleToolSelected(CleaningToolDefinition _) => BreakSurfaceContact();
        private void BreakSurfaceContact() => surfaceStroke.BreakContact();

        private void Update() => ApplyCleaning(Time.deltaTime);

        /// <summary>위치 없는 호출도 기존 LMB/UI/MMB/포커스 gate를 거칩니다. 전체 B 감소는 하지 않습니다.</summary>
        public bool ApplyCleaning(float deltaTime)
        {
            if (!float.IsFinite(deltaTime) || deltaTime <= 0 || !isActiveAndEnabled || SurfaceMask == null || !surfacePointer.TryGetRubSample(out var sample, out _))
            {
                BreakSurfaceContact();
                return false;
            }
            return ApplyRubSample(sample);
        }

        private bool ApplyRubSample(CleaningRubSample sample)
        {
            if (!ProcessingAllowed || !sample.IsRubbing || sample.FrameId == appliedRubFrame || toolSelection.CurrentTool == null) return false;
            appliedRubFrame = sample.FrameId;
            var tool = toolSelection.CurrentTool;
            var dirtBefore = SurfaceMask.CurrentDirtSum;
            var polishBefore = SurfaceMask.CurrentPolishSum;
            var changed = surfaceStroke.ApplyApprovedSweep(SurfaceMask, sample.Start, sample.End,
                surfacePointer.SurfaceLocalToWorldMatrix, surfacePointer.SurfaceLocalBounds, tool, tool.BrushRadius,
                tool.SurfaceDirtRemovalPerSecond, sample.EffectiveSeconds, tool.PolishGainPerSecond);
            if (changed)
            {
                UploadSurfaceMasks(); ApplyVisuals();
                SurfaceContactChanged?.Invoke(new SurfaceCleaningChange(sample.End, tool,
                    SurfaceMask.CurrentDirtSum != dirtBefore, SurfaceMask.CurrentPolishSum != polishBefore));
                CleaningApplied?.Invoke();
            }
            return changed;
        }

        private bool ApplySurfaceContact(CoinSurfaceContact contact, float deltaTime)
        {
            if (!ProcessingAllowed) { BreakSurfaceContact(); return false; }
            var tool = toolSelection.CurrentTool;
            if (tool == null)
            {
                BreakSurfaceContact();
                return false;
            }
            var dirtBefore = SurfaceMask.CurrentDirtSum;
            var polishBefore = SurfaceMask.CurrentPolishSum;
            var changed = surfaceStroke.Apply(SurfaceMask, contact, surfacePointer.SurfaceLocalToWorldMatrix,
                surfacePointer.SurfaceLocalBounds, tool, tool.BrushRadius, tool.SurfaceDirtRemovalPerSecond, deltaTime, tool.PolishGainPerSecond);
            if (!surfaceStroke.LastFrameAccepted) return false;
            if (changed) UploadSurfaceMasks();

            if (changed)
            {
                ApplyVisuals();
                SurfaceContactChanged?.Invoke(new SurfaceCleaningChange(contact, tool,
                    SurfaceMask.CurrentDirtSum != dirtBefore, SurfaceMask.CurrentPolishSum != polishBefore));
                CleaningApplied?.Invoke();
            }

            return changed;
        }

        public void ResetSurface()
        {
            BreakSurfaceContact();
            if (Application.isPlaying)
            {
                EnsureSurfaceMasks();
                SurfaceMask.Reset();
                UploadSurfaceMasks();
            }
            ApplyVisuals();
            SurfaceReset?.Invoke();
        }

        public void SetProcessingAllowed(bool allowed)
        {
            ProcessingAllowed = allowed;
            if (!allowed) BreakSurfaceContact();
        }

        /// <summary>입력 차단 중 정적 단계 설정만 적용합니다. ResetSurface에서 양면을 새 판으로 만듭니다.</summary>
        public void ConfigureStage(float dirt, float polish, CoinSurfaceDirtPattern pattern, Color color)
        {
            if (ProcessingAllowed) throw new InvalidOperationException("Block processing before configuring a stage.");
            if (!float.IsFinite(dirt) || !float.IsFinite(polish) || dirt < 0 || dirt > 1 || polish < 0 || polish > 1 ||
                !Enum.IsDefined(typeof(CoinSurfaceDirtPattern), pattern)) throw new ArgumentException("Invalid surface settings.");
            if (!TryResolveDependencies()) throw new InvalidOperationException("Unsupported coin surface.");
            initialSurfaceDirt = dirt;
            initialPolish = polish;
            surfaceDirtPattern = pattern;
            propertyBlock ??= new MaterialPropertyBlock();
            CaptureCleanMaterialValues();
            cleanBaseColor = color;
        }

        private void EnsureSurfaceMasks()
        {
            if (SurfaceMask != null && FrontDirtMask != null && BackDirtMask != null &&
                SurfaceMask.Resolution == surfaceMaskResolution && SurfaceMask.InitialIntensity == initialSurfaceDirt &&
                SurfaceMask.Pattern == surfaceDirtPattern && SurfaceMask.InitialPolish == initialPolish) return;
            ReleaseSurfaceMasks();
            try
            {
                SurfaceMask = new CoinSurfaceMaskData(surfaceMaskResolution, initialSurfaceDirt, surfaceDirtPattern, initialPolish);
                maskUploadPixels = new Color32[SurfaceMask.TexelCount];
                FrontDirtMask = CreateDirtMask("Front");
                BackDirtMask = CreateDirtMask("Back");
            }
            catch
            {
                ReleaseSurfaceMasks();
                throw;
            }
        }

        private Texture2D CreateDirtMask(string face) => new(surfaceMaskResolution, surfaceMaskResolution,
            TextureFormat.RGBA32, false, true)
        {
            name = "SqueakSqueak.BMask." + face,
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point
        };

        /// <summary>CPU 변경이 있을 때만 생성된 두 텍스처에 올립니다. GPU를 상태 원본으로 읽지 않습니다.</summary>
        public void UploadSurfaceMasks()
        {
            if (SurfaceMask == null || FrontDirtMask == null || BackDirtMask == null ||
                uploadedMaskRevision == SurfaceMask.Revision) return;
            UploadFace(CoinSurfaceFace.Front, FrontDirtMask);
            UploadFace(CoinSurfaceFace.Back, BackDirtMask);
            uploadedMaskRevision = SurfaceMask.Revision;
        }

        private void UploadFace(CoinSurfaceFace face, Texture2D texture)
        {
            for (var i = 0; i < maskUploadPixels.Length; i++)
                maskUploadPixels[i] = new Color32((byte)Mathf.RoundToInt(SurfaceMask.GetDirt(face, i) * 255f),
                    (byte)Mathf.RoundToInt(SurfaceMask.GetPolish(face, i) * 255f), 0,
                    SurfaceMask.IsValidTexel(i) ? (byte)255 : (byte)0);
            texture.SetPixels32(maskUploadPixels);
            texture.Apply(false, false);
        }

        /// <summary>이 인스턴스가 생성한 런타임 자원만 해제합니다. 다음 Reset에서 재생성할 수 있습니다.</summary>
        public void ReleaseSurfaceMasks()
        {
            BreakSurfaceContact();
            if (FrontDirtMask != null) ReleaseTexture(FrontDirtMask);
            if (BackDirtMask != null) ReleaseTexture(BackDirtMask);
            FrontDirtMask = null;
            BackDirtMask = null;
            SurfaceMask = null;
            maskUploadPixels = null;
            uploadedMaskRevision = -1;
            if (coinRenderer != null && propertyBlock != null)
            {
                coinRenderer.GetPropertyBlock(propertyBlock);
                // MaterialPropertyBlock은 null Texture를 허용하지 않습니다. 소유 자원 참조를 공용 빈 마스크로 교체합니다.
                propertyBlock.SetTexture(FrontMaskId, Texture2D.blackTexture);
                propertyBlock.SetTexture(BackMaskId, Texture2D.blackTexture);
                coinRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private static void ReleaseTexture(Texture2D texture)
        {
            if (Application.isPlaying) Destroy(texture);
            else DestroyImmediate(texture);
        }

        private void OnDestroy() => ReleaseSurfaceMasks();

        private bool TryResolveDependencies()
        {
            if (surfacePointer == null || toolSelection == null || coinRenderer == null || coinRenderer.sharedMaterial == null)
            {
                Debug.LogError("CoinSurfaceContamination requires Coin Surface Pointer, Tool Selection, and Coin Renderer references.", this);
                return false;
            }

            var material = coinRenderer.sharedMaterial;
            foreach (var id in new[] { BaseColorId, SmoothnessId, MetallicId, FrontMaskId, BackMaskId,
                BoundsId, HalfHeightId, DirtColorId, DirtySmoothnessId, MaximumSmoothnessId })
            {
                if (material.HasProperty(id)) continue;
                Debug.LogError("CoinSurfaceContamination requires the dedicated Coin Surface material property contract.", this);
                return false;
            }
            var meshFilter = coinRenderer.GetComponent<MeshFilter>();
            if (meshFilter != null && CoinSurfaceMapper.TryGetSupportedBounds(meshFilter.sharedMesh, out surfaceBounds)) return true;
            Debug.LogError("CoinSurfaceContamination requires the supported Coin surface mesh.", this);
            return false;
        }

        private void CaptureCleanMaterialValues()
        {
            var material = coinRenderer.sharedMaterial;
            cleanBaseColor = material.GetColor(BaseColorId);
            cleanSmoothness = material.GetFloat(SmoothnessId);
            cleanMetallic = material.GetFloat(MetallicId);
        }

        private void ApplyVisuals()
        {
            coinRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, cleanBaseColor);
            propertyBlock.SetFloat(SmoothnessId, cleanSmoothness);
            propertyBlock.SetFloat(MetallicId, cleanMetallic);
            propertyBlock.SetColor(DirtColorId, dirtColor);
            propertyBlock.SetFloat(DirtySmoothnessId, dirtySmoothness);
            propertyBlock.SetFloat(MaximumSmoothnessId, maximumSmoothness);
            propertyBlock.SetVector(BoundsId, new Vector4(surfaceBounds.center.x, surfaceBounds.center.y,
                surfaceBounds.center.z, surfaceBounds.extents.x));
            propertyBlock.SetFloat(HalfHeightId, surfaceBounds.extents.y);
            propertyBlock.SetTexture(FrontMaskId, FrontDirtMask != null ? FrontDirtMask : Texture2D.blackTexture);
            propertyBlock.SetTexture(BackMaskId, BackDirtMask != null ? BackDirtMask : Texture2D.blackTexture);
            coinRenderer.SetPropertyBlock(propertyBlock);
        }

        private void OnValidate()
        {
            surfaceMaskResolution = Mathf.Clamp(surfaceMaskResolution,
                CoinSurfaceMaskData.MinimumResolution, CoinSurfaceMaskData.MaximumResolution);
            initialSurfaceDirt = Mathf.Clamp01(initialSurfaceDirt);
            initialPolish = Mathf.Clamp01(initialPolish);
            dirtySmoothness = Mathf.Clamp01(dirtySmoothness);
            maximumSmoothness = Mathf.Clamp(maximumSmoothness, dirtySmoothness, 1f);
        }
    }
}
