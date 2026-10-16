using System;
using System.Collections.Generic;
using UnityEngine;
using SqueakSqueak.Cleaning;
using SqueakSqueak.Progression;

namespace SqueakSqueak.Experiment
{
    [CreateAssetMenu(menuName="Squeak Squeak/Experiment/Definition")]
    public sealed class CleaningExperimentDefinition : ScriptableObject
    {
        [SerializeField] private string layoutId = "rb-fixed-v1";
        [SerializeField] private CoinDefinition[] coins = Array.Empty<CoinDefinition>();
        [SerializeField] private CleaningToolDefinition[] tools = Array.Empty<CleaningToolDefinition>();
        [SerializeField] private StageDirtPlacement[] placements = Array.Empty<StageDirtPlacement>();
        [SerializeField] private Mesh dirtMesh;
        [SerializeField] private Material dirtMaterial;
        [SerializeField, Range(0,1)] private float initialB = 1;
        public string LayoutId => layoutId;
        public IReadOnlyList<CoinDefinition> Coins => coins;
        public IReadOnlyList<CleaningToolDefinition> Tools => tools;
        public IReadOnlyList<StageDirtPlacement> Placements => placements;
        public Mesh DirtMesh => dirtMesh;
        public Material DirtMaterial => dirtMaterial;
        public float InitialB => initialB;

        public string Validate()
        {
            if (string.IsNullOrEmpty(layoutId) || coins == null || coins.Length != 2 || tools == null || tools.Length != 3 || placements == null || dirtMesh == null || dirtMaterial == null || !float.IsFinite(initialB) || initialB<0 || initialB>1) return "실험 정의/참조/초기 오염 오류";
            var ids = new HashSet<string>();
            foreach (var c in coins)
                if (c == null || string.IsNullOrEmpty(c.Id) || !ids.Add(c.Id) || c.ReactionProfile == null || !c.ReactionProfile.IsValid || c.SurfaceMaterial == null ||
                    !CoinSurfaceMapper.TryGetSupportedBounds(c.SurfaceMesh,out _)) return "실험 동전/반응/원판 오류";
            foreach(var c in coins)
            {
                var color=c.BaseColor;
                if(!float.IsFinite(color.r)||!float.IsFinite(color.g)||!float.IsFinite(color.b)||!float.IsFinite(color.a))return "실험 색상 오류";
                foreach(var name in new[]{"_BaseColor","_Smoothness","_Metallic","_FrontSurfaceMask","_BackSurfaceMask","_SurfaceBounds","_SurfaceHalfHeight","_DirtColor","_DirtySmoothness","_MaximumSmoothness","_ExperimentScanner"})
                    if(!c.SurfaceMaterial.HasProperty(name))return "실험 셰이더 계약 오류";
            }
            var kinds = new HashSet<CleaningToolId>();
            foreach (var t in tools)
                if (t == null || !kinds.Add(t.ToolId) || !float.IsFinite(t.BrushRadius) || t.BrushRadius <= 0 ||
                    !float.IsFinite(t.ObjectDirtRemovalPerSecond) || t.ObjectDirtRemovalPerSecond < 0 ||
                    !float.IsFinite(t.SurfaceDirtRemovalPerSecond) || t.SurfaceDirtRemovalPerSecond < 0 || t.PolishGainPerSecond != 0) return "실험 도구 오류";
            if (!kinds.SetEquals(new[]{CleaningToolId.ExperimentToothbrush,CleaningToolId.ExperimentCloth,CleaningToolId.Dropper})) return "실험 도구 종류 오류";
            foreach (var p in placements)
                if ((p.Face != CoinSurfaceFace.Front && p.Face != CoinSurfaceFace.Back) || !float.IsFinite(p.Durability) || p.Durability<.01f ||
                    !float.IsFinite(p.WorldRadius) || p.WorldRadius<=0 || !float.IsFinite(p.Uv.x) || !float.IsFinite(p.Uv.y) ||
                    (p.Uv-new Vector2(.5f,.5f)).sqrMagnitude>.25f) return "실험 A 배치 오류";
            return null;
        }
    }
}
