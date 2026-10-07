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
        private Color32[] maskUploadPixels;
        private long uploadedMaskRevision = -1;
        private readonly CoinSurfaceBrushStroke surfaceStroke = new();

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

        public float Polish { get; private set; }

        /// <summary>
        /// B 타입 오염 또는 광택이 실제로 변경됐을 때 발생합니다.
        /// </summary>
        public event Action CleaningApplied;

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
            if (!isActiveAndEnabled || SurfaceMask == null || !surfacePointer.TryGetToolUseContact(out var contact))
            {
                BreakSurfaceContact();
                return false;
            }
            return ApplySurfaceContact(contact, deltaTime);
        }

        private bool ApplySurfaceContact(CoinSurfaceContact contact, float deltaTime)
        {
            var tool = toolSelection.CurrentTool;
            if (tool == null)
            {
                BreakSurfaceContact();
                return false;
            }
            var changed = surfaceStroke.Apply(SurfaceMask, contact, surfacePointer.SurfaceLocalToWorldMatrix,
                surfacePointer.SurfaceLocalBounds, tool, tool.BrushRadius, tool.SurfaceDirtRemovalPerSecond, deltaTime);
            if (!surfaceStroke.LastFrameAccepted) return false;
            if (changed) UploadSurfaceMasks();

            // 전역 광택은 S2-03까지 종전 방식을 유지합니다. 지역 광택을 여기서 선행하지 않습니다.
            if (Polish < 1f && tool.PolishGainPerSecond > 0f)
            {
                var polishEfficiency = 1f - SurfaceDirt;
                if (polishEfficiency > 0f)
                {
                    var previousPolish = Polish;
                    Polish = Mathf.Min(1f, Polish + tool.PolishGainPerSecond * polishEfficiency * deltaTime);
                    changed |= Polish != previousPolish;
                }
            }

            if (changed)
            {
                ApplyVisuals();
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
            Polish = initialPolish;
            ApplyVisuals();
        }

        private void EnsureSurfaceMasks()
        {
            if (SurfaceMask != null && FrontDirtMask != null && BackDirtMask != null &&
                SurfaceMask.Resolution == surfaceMaskResolution && SurfaceMask.InitialIntensity == initialSurfaceDirt &&
                SurfaceMask.Pattern == surfaceDirtPattern) return;
            ReleaseSurfaceMasks();
            try
            {
                SurfaceMask = new CoinSurfaceMaskData(surfaceMaskResolution, initialSurfaceDirt, surfaceDirtPattern);
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
                    0, 0, SurfaceMask.IsValidTexel(i) ? (byte)255 : (byte)0);
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
            if (!material.HasProperty(BaseColorId) || !material.HasProperty(SmoothnessId) || !material.HasProperty(MetallicId))
            {
                Debug.LogError("CoinSurfaceContamination requires a URP Lit material with Base Color, Smoothness, and Metallic properties.", this);
                return false;
            }

            return true;
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
            // S2-03의 지역 셰이더 전까지 전체 동전에 집계 기반 임시 색상/광택을 적용합니다.
            var cleanSurfaceRatio = 1f - SurfaceDirt;
            var baseSmoothness = Mathf.Lerp(dirtySmoothness, cleanSmoothness, cleanSurfaceRatio);

            coinRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, Color.Lerp(dirtColor, cleanBaseColor, cleanSurfaceRatio));
            propertyBlock.SetFloat(SmoothnessId, Mathf.Lerp(baseSmoothness, maximumSmoothness, Polish));
            propertyBlock.SetFloat(MetallicId, Mathf.Lerp(cleanMetallic * 0.35f, cleanMetallic, cleanSurfaceRatio));
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
