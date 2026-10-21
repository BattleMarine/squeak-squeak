using System;
using UnityEngine;

namespace SqueakSqueak.Cleaning
{
    [DisallowMultipleComponent]
    public sealed class ObjectDirtCleaner : MonoBehaviour
    {
        [SerializeField]
        private CoinSurfacePointer surfacePointer;

        [SerializeField]
        private CleaningToolSelection toolSelection;

        [SerializeField]
        private ObjectDirt[] dirtObjects = System.Array.Empty<ObjectDirt>();

        [SerializeField, Min(0f)]
        [Tooltip("카메라에서 본 Coin 표면과 오염 위치 사이에 허용할 거리입니다.")]
        private float visibleSurfaceTolerance = 0.08f;
        private long appliedRubFrame = -1;

        public int InitialDirtCount { get; private set; }

        public int RemovedDirtCount { get; private set; }
        public bool ProcessingAllowed { get; private set; } = true;
        public CleaningReactionProfile ReactionProfile { get; private set; }
        public void SetReactionProfile(CleaningReactionProfile profile)
        {
            if (ProcessingAllowed) throw new InvalidOperationException("Block processing before changing reactions.");
            if (profile != null && !profile.IsValid) throw new ArgumentException("Invalid reaction profile.");
            ReactionProfile = profile;
        }

        public float RemovalRatio => InitialDirtCount == 0 ? 1f : (float)RemovedDirtCount / InitialDirtCount;

        /// <summary>
        /// A 타입 오염이 실제로 감소했을 때 발생합니다.
        /// </summary>
        public event Action CleaningApplied;
        /// <summary>A가 실제로 감소한 호출1회의 표면 적중입니다. CurrentTool은 콜백 안에서 읽을 수 있습니다.</summary>
        public event Action<RaycastHit> ContactCleaningApplied;
        /// <summary>A의 Removed 알림1회를 중계합니다. 추가 제거/진행 상태를 만들지 않습니다.</summary>
        public event Action<ObjectDirt> DirtRemoved;

        private void Awake()
        {
            if (surfacePointer == null || toolSelection == null)
            {
                Debug.LogError("ObjectDirtCleaner requires Coin Surface Pointer and Tool Selection references.", this);
                enabled = false;
                return;
            }

            ReplaceDirt(dirtObjects);
        }

        private void OnDestroy()
        {
            for (var index = 0; index < dirtObjects.Length; index++)
            {
                if (dirtObjects[index] != null)
                {
                    dirtObjects[index].Removed -= HandleDirtRemoved;
                }
            }
        }

        private void Update()
        {
            if (!surfacePointer.TryGetRubSample(out var sample, out var surfaceHit))
            {
                return;
            }

            ApplyRubSample(sample, surfaceHit);
        }

        /// <summary>
        /// 현재 선택 도구로 지정 표면 적중 주변의 보이는 A 오염에 시간 기반 제거량을 적용합니다.
        /// 입력 처리와 분리해 테스트 및 이후 청소 상태 제어에서 같은 규칙을 사용합니다.
        /// </summary>
        public int ApplyCleaningAtSurfaceHit(RaycastHit surfaceHit, float deltaTime)
        {
            if (!ProcessingAllowed || !isActiveAndEnabled || !float.IsFinite(deltaTime) || deltaTime <= 0f)
            {
                return 0;
            }

            if(CoinSurfaceMapper.TryMapPoint(surfacePointer.SurfaceLocalToWorldMatrix,surfacePointer.SurfaceLocalBounds,surfaceHit.point,surfaceHit.normal,out var mapped)&&mapped.Face==CoinSurfaceFace.Side)return 0;
            var affectedCount = ApplyAtPoint(surfaceHit.point, deltaTime);
            NotifyCleaning(surfaceHit, affectedCount);
            return affectedCount;
        }

        private void ApplyRubSample(CleaningRubSample sample, RaycastHit surfaceHit)
        {
            // 현 A 배치는 캡 전용입니다. 옆면 stroke가 캡 모서리의 A를 함께 지우지 않습니다.
            if(sample.End.Face==CoinSurfaceFace.Side)return;
            if (!ProcessingAllowed || !sample.IsRubbing || sample.FrameId == appliedRubFrame || toolSelection.CurrentTool == null) return;
            appliedRubFrame = sample.FrameId;
            var radius = toolSelection.CurrentTool.BrushRadius;
            var distance = Vector3.Distance(sample.Start.WorldPosition,sample.End.WorldPosition);
            var count = Mathf.Max(1, Mathf.CeilToInt(distance / (radius * .25f)));
            if (count > CoinSurfaceBrushStroke.MaximumSamples) return;
            var affected = 0;
            for (var i = 0; i < count; i++)
                affected += ApplyAtPoint(Vector3.Lerp(sample.Start.WorldPosition, sample.End.WorldPosition, (i+.5f)/count),sample.EffectiveSeconds/count);
            NotifyCleaning(surfaceHit,affected);
        }

        private int ApplyAtPoint(Vector3 point, float deltaTime)
        {
            var tool = toolSelection.CurrentTool;
            if (tool == null || tool.ObjectDirtRemovalPerSecond <= 0f)
            {
                return 0;
            }

            var brushRadiusSquared = tool.BrushRadius * tool.BrushRadius;
            var removalAmount = tool.ObjectDirtRemovalPerSecond * deltaTime *
                (ReactionProfile != null ? ReactionProfile.ObjectCoefficient(tool.ToolId) : 1);
            if (removalAmount <= 0) return 0;
            var affectedCount = 0;
            for (var index = 0; index < dirtObjects.Length; index++)
            {
                var dirt = dirtObjects[index];
                if (dirt == null || dirt.IsRemoved || !dirt.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if ((dirt.transform.position - point).sqrMagnitude > brushRadiusSquared ||
                    !surfacePointer.IsWorldPointVisibleOnCoinSurface(dirt.transform.position, visibleSurfaceTolerance))
                {
                    continue;
                }

                if (dirt.ApplyCleaning(removalAmount))
                {
                    affectedCount++;
                }
            }

            return affectedCount;
        }

        private void NotifyCleaning(RaycastHit surfaceHit, int affectedCount)
        {
            if (affectedCount > 0)
            {
                ContactCleaningApplied?.Invoke(surfaceHit);
                CleaningApplied?.Invoke();
            }

        }

        public void ResetAllDirt()
        {
            RemovedDirtCount = 0;
            for (var index = 0; index < dirtObjects.Length; index++)
            {
                if (dirtObjects[index] != null)
                {
                    dirtObjects[index].ResetDirt();
                }
            }
        }

        public void SetProcessingAllowed(bool allowed) => ProcessingAllowed = allowed;

        /// <summary>배열·옛 Removed 구독·분모를 함께 교체해 이전 판의 알림을 받지 않습니다.</summary>
        public void ReplaceDirt(ObjectDirt[] objects)
        {
            var copy = objects == null ? Array.Empty<ObjectDirt>() : (ObjectDirt[])objects.Clone();
            var unique = new System.Collections.Generic.HashSet<ObjectDirt>();
            foreach (var dirt in copy)
                if (dirt != null && !unique.Add(dirt)) throw new ArgumentException("Duplicate dirt reference.", nameof(objects));
            foreach (var dirt in dirtObjects)
                if (dirt != null) dirt.Removed -= HandleDirtRemoved;
            dirtObjects = copy;
            InitialDirtCount = unique.Count;
            RemovedDirtCount = 0;
            foreach (var dirt in dirtObjects)
                if (dirt != null) dirt.Removed += HandleDirtRemoved;
        }

        private void HandleDirtRemoved(ObjectDirt dirt)
        {
            RemovedDirtCount = Mathf.Min(InitialDirtCount, RemovedDirtCount + 1);
            DirtRemoved?.Invoke(dirt);
        }

        private void OnValidate()
        {
            visibleSurfaceTolerance = Mathf.Max(0f, visibleSurfaceTolerance);
        }
    }
}
