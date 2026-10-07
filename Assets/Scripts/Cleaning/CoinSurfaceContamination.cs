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
        [Tooltip("새 청소 판이 시작될 때의 B 타입 표면 오염 수치입니다.")]
        private float initialSurfaceDirt = 1f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("새 청소 판이 시작될 때의 광택 수치입니다.")]
        private float initialPolish;

        [SerializeField, Range(CoinSurfaceMaskData.MinimumResolution, CoinSurfaceMaskData.MaximumResolution)]
        [Tooltip("앞면과 뒷면 각각의 B 마스크 한 변 텍셀 수입니다. 기본 256입니다.")]
        private int surfaceMaskResolution = CoinSurfaceMaskData.DefaultResolution;

        [SerializeField]
        [Tooltip("위치별 B 마스크의 고정 초기 패턴입니다. 기존 스칼라 B는 S2-02c까지 유지합니다.")]
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
        private float roundInitialSurfaceDirt;
        private Color32[] maskUploadPixels;
        private long uploadedMaskRevision = -1;

        // S2-02a의 준비 상태입니다. 기존 화면/HUD 수치의 원본으로는 아직 사용하지 않습니다.
        public CoinSurfaceMaskData SurfaceMask { get; private set; }
        public Texture2D FrontDirtMask { get; private set; }
        public Texture2D BackDirtMask { get; private set; }

        public float SurfaceDirt { get; private set; }

        /// <summary>
        /// 현재 청소 판이 시작할 때의 B 오염량을 기준으로 계산한 제거율입니다.
        /// </summary>
        public float SurfaceDirtRemovalRatio => roundInitialSurfaceDirt <= Mathf.Epsilon
            ? 1f
            : Mathf.Clamp01(1f - SurfaceDirt / roundInitialSurfaceDirt);

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

        private void Update()
        {
            if (!surfacePointer.TryGetToolUseSurfaceHit(out _))
            {
                return;
            }

            ApplyCleaning(Time.deltaTime);
        }

        /// <summary>
        /// 현재 선택 도구의 B 오염 제거 및 광택 효과를 시간 기반으로 적용합니다.
        /// 광택천 효과는 남은 표면 오염이 적을수록 커집니다.
        /// </summary>
        public bool ApplyCleaning(float deltaTime)
        {
            if (deltaTime <= 0f || toolSelection.CurrentTool == null)
            {
                return false;
            }

            var tool = toolSelection.CurrentTool;
            var changed = false;
            if (SurfaceDirt > 0f && tool.SurfaceDirtRemovalPerSecond > 0f)
            {
                SurfaceDirt = Mathf.Max(0f, SurfaceDirt - tool.SurfaceDirtRemovalPerSecond * deltaTime);
                changed = true;
            }

            if (Polish < 1f && tool.PolishGainPerSecond > 0f)
            {
                var polishEfficiency = 1f - SurfaceDirt;
                if (polishEfficiency > 0f)
                {
                    Polish = Mathf.Min(1f, Polish + tool.PolishGainPerSecond * polishEfficiency * deltaTime);
                    changed = true;
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
            if (Application.isPlaying)
            {
                EnsureSurfaceMasks();
                SurfaceMask.Reset();
                UploadSurfaceMasks();
            }
            roundInitialSurfaceDirt = initialSurfaceDirt;
            SurfaceDirt = roundInitialSurfaceDirt;
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
