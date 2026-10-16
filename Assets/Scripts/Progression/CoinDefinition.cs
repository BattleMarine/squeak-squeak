using UnityEngine;

namespace SqueakSqueak.Progression
{
    /// <summary>공유 원판 계약과 외형 설정. 런타임 청소/진행 상태를 저장하지 않습니다.</summary>
    [CreateAssetMenu(menuName = "Squeak Squeak/Progression/Coin")]
    public sealed class CoinDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private Mesh surfaceMesh;
        [SerializeField] private Material surfaceMaterial;
        [SerializeField] private Color baseColor = Color.white;
        [SerializeField] private SqueakSqueak.Cleaning.CleaningReactionProfile reactionProfile;

        public string Id => id;
        public string DisplayName => displayName;
        public Mesh SurfaceMesh => surfaceMesh;
        public Material SurfaceMaterial => surfaceMaterial;
        // 적용 담당은 인스턴스 PropertyBlock을 사용해야 합니다. 공유 Material을 수정하지 않습니다.
        public Color BaseColor => baseColor;
        public SqueakSqueak.Cleaning.CleaningReactionProfile ReactionProfile => reactionProfile;
    }
}
