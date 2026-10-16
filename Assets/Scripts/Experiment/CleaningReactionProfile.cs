using System;
using UnityEngine;

namespace SqueakSqueak.Cleaning
{
    /// <summary>실험용 가상 재질 반응. 기존 도구 속도/ID를 덮어쓰지 않습니다.</summary>
    [CreateAssetMenu(menuName="Squeak Squeak/Experiment/Reaction")]
    public sealed class CleaningReactionProfile : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField, Range(0,1)] private float toothbrushA = 1;
        [SerializeField, Range(0,1)] private float clothA;
        [SerializeField, Range(0,1)] private float toothbrushUntreatedB;
        [SerializeField, Range(0,1)] private float clothUntreatedB = .5f;
        [SerializeField, Range(0,1)] private float toothbrushSoftenedB = .25f;
        [SerializeField, Range(0,1)] private float clothSoftenedB = 1;
        public string Id => id;
        public float ObjectCoefficient(CleaningToolId tool) => tool == CleaningToolId.ExperimentToothbrush ? toothbrushA :
            tool == CleaningToolId.ExperimentCloth ? clothA : 0;
        public float SurfaceCoefficient(CleaningToolId tool, bool softened) => tool switch
        {
            CleaningToolId.ExperimentToothbrush => softened ? toothbrushSoftenedB : toothbrushUntreatedB,
            CleaningToolId.ExperimentCloth => softened ? clothSoftenedB : clothUntreatedB,
            _ => 0
        };
        public bool IsValid => !string.IsNullOrEmpty(id) && Array.TrueForAll(new[]{toothbrushA,clothA,toothbrushUntreatedB,
            clothUntreatedB,toothbrushSoftenedB,clothSoftenedB},v=>float.IsFinite(v)&&v>=0&&v<=1);
    }
}
