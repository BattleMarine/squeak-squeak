using UnityEngine;

namespace SqueakSqueak.Cleaning
{
    public enum CleaningToolId
    {
        DryCloth,
        SmallBrush,
        PolishingCloth
    }

    [CreateAssetMenu(fileName = "CleaningTool_", menuName = "Squeak Squeak/Cleaning Tool", order = 10)]
    public sealed class CleaningToolDefinition : ScriptableObject
    {
        [SerializeField]
        private CleaningToolId toolId;

        [SerializeField]
        private string displayName;

        [SerializeField, Min(0.01f)]
        [Tooltip("동전 표면에서 도구 효과가 닿는 반경입니다.")]
        private float brushRadius = 0.25f;

        [SerializeField, Min(0f)]
        [Tooltip("A 타입 개별 오염의 내구도를 초당 감소시키는 양입니다.")]
        private float objectDirtRemovalPerSecond;

        [SerializeField, Min(0f)]
        [Tooltip("B 타입 표면 오염 수치를 초당 감소시키는 양입니다.")]
        private float surfaceDirtRemovalPerSecond;

        [SerializeField, Min(0f)]
        [Tooltip("광택 수치를 초당 증가시키는 양입니다.")]
        private float polishGainPerSecond;

        public CleaningToolId ToolId => toolId;

        public string DisplayName => displayName;

        public float BrushRadius => brushRadius;

        public float ObjectDirtRemovalPerSecond => objectDirtRemovalPerSecond;

        public float SurfaceDirtRemovalPerSecond => surfaceDirtRemovalPerSecond;

        public float PolishGainPerSecond => polishGainPerSecond;

        private void OnValidate()
        {
            brushRadius = Mathf.Max(0.01f, brushRadius);
            objectDirtRemovalPerSecond = Mathf.Max(0f, objectDirtRemovalPerSecond);
            surfaceDirtRemovalPerSecond = Mathf.Max(0f, surfaceDirtRemovalPerSecond);
            polishGainPerSecond = Mathf.Max(0f, polishGainPerSecond);
        }
    }
}
