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

        public float SurfaceDirt { get; private set; }

        public float SurfaceDirtRemovalRatio => 1f - SurfaceDirt;

        public float Polish { get; private set; }

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
            }

            return changed;
        }

        public void ResetSurface()
        {
            SurfaceDirt = initialSurfaceDirt;
            Polish = initialPolish;
            ApplyVisuals();
        }

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
            initialSurfaceDirt = Mathf.Clamp01(initialSurfaceDirt);
            initialPolish = Mathf.Clamp01(initialPolish);
            dirtySmoothness = Mathf.Clamp01(dirtySmoothness);
            maximumSmoothness = Mathf.Clamp(maximumSmoothness, dirtySmoothness, 1f);
        }
    }
}
