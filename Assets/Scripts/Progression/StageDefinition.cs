using System;
using System.Collections.Generic;
using UnityEngine;
using SqueakSqueak.Cleaning;

namespace SqueakSqueak.Progression
{
    [Serializable]
    public struct StageDirtPlacement
    {
        [SerializeField] private CoinSurfaceFace face;
        [SerializeField, Tooltip("앞/뒷면 공통 원판 UV(0~1). 유효 원판 안에 배치합니다.")]
        private Vector2 uv;
        [SerializeField, Min(.01f)] private float durability;
        [SerializeField, Min(.01f), Tooltip("기본 Sphere Mesh 기준의 월드 반경입니다.")]
        private float worldRadius;
        public CoinSurfaceFace Face => face;
        public Vector2 Uv => uv;
        public float Durability => durability;
        public float WorldRadius => worldRadius;
    }

    [CreateAssetMenu(menuName = "Squeak Squeak/Progression/Stage")]
    public sealed class StageDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private CoinDefinition coin;
        [SerializeField] private string[] prerequisiteStageIds = Array.Empty<string>();
        [SerializeField] private string[] requiredOwnedToolIds = Array.Empty<string>();
        [SerializeField] private string[] allowedToolIds = Array.Empty<string>();
        [SerializeField] private StageDirtPlacement[] objectDirt = Array.Empty<StageDirtPlacement>();
        [SerializeField] private CoinSurfaceDirtPattern dirtPattern;
        [SerializeField, Range(0, 1)] private float initialSurfaceDirt = 1;
        [SerializeField, Range(0, 1)] private float initialPolish;
        [SerializeField, Range(0, 1)] private float requiredA = .9f;
        [SerializeField, Range(0, 1)] private float requiredB = .8f;
        [SerializeField, Range(0, 1)] private float requiredPolish;
        [SerializeField, Min(0)] private int firstCompletionReward = 100;
        [SerializeField, Min(0)] private float gradeASeconds = 180;
        [SerializeField, Min(0)] private float gradeBSeconds = 300;

        public string Id => id;
        public string DisplayName => displayName;
        public CoinDefinition Coin => coin;
        public IReadOnlyList<string> PrerequisiteStageIds => prerequisiteStageIds;
        public IReadOnlyList<string> RequiredOwnedToolIds => requiredOwnedToolIds;
        public IReadOnlyList<string> AllowedToolIds => allowedToolIds;
        public IReadOnlyList<StageDirtPlacement> ObjectDirt => objectDirt;
        public CoinSurfaceDirtPattern DirtPattern => dirtPattern;
        public float InitialSurfaceDirt => initialSurfaceDirt;
        public float InitialPolish => initialPolish;
        public float RequiredA => requiredA;
        public float RequiredB => requiredB;
        public float RequiredPolish => requiredPolish;
        public int FirstCompletionReward => firstCompletionReward;
        public float GradeASeconds => gradeASeconds;
        public float GradeBSeconds => gradeBSeconds;
    }
}
