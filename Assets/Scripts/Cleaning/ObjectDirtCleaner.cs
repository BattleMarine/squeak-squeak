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

        public int InitialDirtCount { get; private set; }

        public int RemovedDirtCount { get; private set; }

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

            for (var index = 0; index < dirtObjects.Length; index++)
            {
                var dirt = dirtObjects[index];
                if (dirt == null)
                {
                    continue;
                }

                InitialDirtCount++;
                dirt.Removed += HandleDirtRemoved;
            }
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
            if (!surfacePointer.TryGetToolUseSurfaceHit(out var surfaceHit))
            {
                return;
            }

            ApplyCleaningAtSurfaceHit(surfaceHit, Time.deltaTime);
        }

        /// <summary>
        /// 현재 선택 도구로 지정 표면 적중 주변의 보이는 A 오염에 시간 기반 제거량을 적용합니다.
        /// 입력 처리와 분리해 테스트 및 이후 청소 상태 제어에서 같은 규칙을 사용합니다.
        /// </summary>
        public int ApplyCleaningAtSurfaceHit(RaycastHit surfaceHit, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return 0;
            }

            var tool = toolSelection.CurrentTool;
            if (tool == null || tool.ObjectDirtRemovalPerSecond <= 0f)
            {
                return 0;
            }

            var brushRadiusSquared = tool.BrushRadius * tool.BrushRadius;
            var removalAmount = tool.ObjectDirtRemovalPerSecond * deltaTime;
            var affectedCount = 0;
            for (var index = 0; index < dirtObjects.Length; index++)
            {
                var dirt = dirtObjects[index];
                if (dirt == null || dirt.IsRemoved || !dirt.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if ((dirt.transform.position - surfaceHit.point).sqrMagnitude > brushRadiusSquared ||
                    !surfacePointer.IsWorldPointVisibleOnCoinSurface(dirt.transform.position, visibleSurfaceTolerance))
                {
                    continue;
                }

                if (dirt.ApplyCleaning(removalAmount))
                {
                    affectedCount++;
                }
            }

            if (affectedCount > 0)
            {
                ContactCleaningApplied?.Invoke(surfaceHit);
                CleaningApplied?.Invoke();
            }

            return affectedCount;
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
