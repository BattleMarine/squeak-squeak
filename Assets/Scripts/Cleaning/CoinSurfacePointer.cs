using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SqueakSqueak.Cleaning
{
    [DisallowMultipleComponent]
    public sealed class CoinSurfacePointer : MonoBehaviour
    {
        private const string CleaningMapName = "Cleaning";
        private const string PointerPositionActionName = "PointerPosition";
        private const string PointerDeltaActionName = "PointerDelta";
        private const string UseToolActionName = "UseTool";
        private const string RotateCoinActionName = "RotateCoin";
        private const string ZoomActionName = "Zoom";
        private const string ResetViewActionName = "ResetView";

        [SerializeField]
        private InputActionAsset inputActions;

        [SerializeField]
        private Camera gameplayCamera;

        [SerializeField]
        private Collider coinCollider;

        [SerializeField]
        private Transform coinTransform;

        [SerializeField]
        private SurfaceToolCursor toolCursor;

        [SerializeField]
        private LayerMask raycastMask = Physics.DefaultRaycastLayers;

        [SerializeField, Min(0f)]
        private float rotationSensitivity = 0.2f;

        [SerializeField, Min(0f)]
        private float zoomSensitivity = 0.0025f;

        [SerializeField, Min(0.01f)]
        private float minimumZoom = 1.6f;

        [SerializeField, Min(0.01f)]
        private float maximumZoom = 3.2f;

        private readonly RaycastHit[] raycastHits = new RaycastHit[8];

        private InputActionMap cleaningMap;
        private InputAction pointerPositionAction;
        private InputAction pointerDeltaAction;
        private InputAction useToolAction;
        private InputAction rotateCoinAction;
        private InputAction zoomAction;
        private InputAction resetViewAction;
        private Vector3 initialCoinPosition;
        private Quaternion initialCoinRotation;
        private float initialOrthographicSize;
        private MeshCollider surfaceMeshCollider;
        private Mesh surfaceMesh;
        private Bounds surfaceLocalBounds;

        public CoinSurfaceContact CurrentSurfaceContact { get; private set; }

        public Bounds SurfaceLocalBounds => surfaceLocalBounds;
        public Matrix4x4 SurfaceLocalToWorldMatrix => surfaceMeshCollider != null
            ? surfaceMeshCollider.transform.localToWorldMatrix : default;
        public event Action ContactInvalidated;

        public bool HasCleanableContact => HasSurfaceHit && CurrentSurfaceContact.IsCleanable;

        public bool HasSurfaceHit { get; private set; }

        public RaycastHit CurrentSurfaceHit { get; private set; }

        public bool IsRotating { get; private set; }

        /// <summary>
        /// 도구 처리에서 사용할 수 있는 현재 포인터 상태입니다.
        /// UI 위 포인터는 동전 표면에 적중하더라도 게임플레이 입력으로 취급하지 않습니다.
        /// </summary>
        public bool CanUseTool => HasSurfaceHit && !IsPointerOverUi;

        public bool IsPointerOverUi => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        /// <summary>
        /// 현재 포인터 위치에서 도구 사용에 유효한 동전 표면 적중을 가져옵니다.
        /// 회전 시작 중이거나 UI 위, 또는 MMB가 눌린 프레임에는 청소를 차단합니다.
        /// </summary>
        public bool TryGetToolUseSurfaceHit(out RaycastHit surfaceHit)
        {
            surfaceHit = default;
            if (useToolAction == null || !useToolAction.IsPressed() || IsRotating || rotateCoinAction.IsPressed() || IsPointerOverUi)
            {
                return false;
            }

            UpdateSurfaceFromScreenPosition(pointerPositionAction.ReadValue<Vector2>());
            if (!CanUseTool)
            {
                return false;
            }

            surfaceHit = CurrentSurfaceHit;
            return true;
        }

        /// <summary>기존 LMB·UI·MMB 입력 판정을 통과한 앞/뒷면 접촉만 제공합니다.</summary>
        public bool TryGetToolUseContact(out CoinSurfaceContact contact)
        {
            contact = default;
            if (!isActiveAndEnabled || !Application.isFocused || !TryGetToolUseSurfaceHit(out _) || !HasCleanableContact)
                return false;
            contact = CurrentSurfaceContact;
            return true;
        }

        /// <summary>
        /// 카메라에서 봤을 때 지정 위치가 Coin 표면 바로 앞에 있는지 검사합니다.
        /// 반대편의 A 오염을 거리만으로 제거하지 않기 위한 가시성 판정입니다.
        /// </summary>
        public bool IsWorldPointVisibleOnCoinSurface(Vector3 worldPoint, float surfaceTolerance)
        {
            if (surfaceTolerance < 0f)
            {
                return false;
            }

            // 포인터 표면 판정과 동일한 화면 Ray를 사용해야 Orthographic 카메라의
            // 화면 가장자리에서도 같은 가림물과 동전 표면을 검사할 수 있다.
            var screenPosition = gameplayCamera.WorldToScreenPoint(worldPoint);
            if (screenPosition.z <= 0f)
            {
                return false;
            }

            var ray = gameplayCamera.ScreenPointToRay(screenPosition);
            var targetDistance = Vector3.Dot(worldPoint - ray.origin, ray.direction);
            if (targetDistance <= Mathf.Epsilon)
            {
                return false;
            }

            if (!TryFindClosestPhysicsHit(
                    ray,
                    targetDistance + surfaceTolerance,
                    out var closestHit))
            {
                return false;
            }

            return closestHit.collider == coinCollider &&
                Mathf.Abs(closestHit.distance - targetDistance) <= surfaceTolerance;
        }

        private void Awake()
        {
            if (!TryResolveDependencies())
            {
                enabled = false;
                return;
            }

            CaptureInitialViewState();
            ClearSurfaceHit();
        }

        private void OnEnable()
        {
            cleaningMap?.Enable();
        }

        private void OnDisable()
        {
            cleaningMap?.Disable();
            IsRotating = false;
            ClearSurfaceHit();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            IsRotating = false;
            ClearSurfaceHit();
        }

        private void Update()
        {
            if (pointerPositionAction == null)
            {
                return;
            }

            if (resetViewAction.WasPressedThisFrame())
            {
                ResetViewToInitialState();
            }

            if (UpdateRotation())
            {
                return;
            }

            if (!IsPointerOverUi)
            {
                ApplyZoom(zoomAction.ReadValue<Vector2>().y);
            }

            var screenPosition = pointerPositionAction.ReadValue<Vector2>();
            UpdateSurfaceFromScreenPosition(screenPosition);
        }

        private bool UpdateRotation()
        {
            if (IsRotating)
            {
                if (!rotateCoinAction.IsPressed())
                {
                    IsRotating = false;
                    return false;
                }

                ApplyRotation(pointerDeltaAction.ReadValue<Vector2>());
                ClearSurfaceHit();
                return true;
            }

            if (!rotateCoinAction.WasPressedThisFrame() || IsPointerOverUi)
            {
                return false;
            }

            return TryBeginRotation(pointerPositionAction.ReadValue<Vector2>());
        }

        private bool TryBeginRotation(Vector2 screenPosition)
        {
            if (IsPointerOverUi)
            {
                return false;
            }

            // 중클릭이 들어온 현재 프레임의 포인터 위치를 기준으로 회전 시작을 판정한다.
            UpdateSurfaceFromScreenPosition(screenPosition);
            if (!HasSurfaceHit)
            {
                return false;
            }

            IsRotating = true;
            ApplyRotation(pointerDeltaAction.ReadValue<Vector2>());
            ClearSurfaceHit();
            return true;
        }

        private void ApplyRotation(Vector2 pointerDelta)
        {
            if (pointerDelta.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            coinTransform.Rotate(gameplayCamera.transform.up, -pointerDelta.x * rotationSensitivity, Space.World);
            coinTransform.Rotate(gameplayCamera.transform.right, pointerDelta.y * rotationSensitivity, Space.World);
        }

        private void ApplyZoom(float scrollDelta)
        {
            if (Mathf.Abs(scrollDelta) <= Mathf.Epsilon)
            {
                return;
            }

            gameplayCamera.orthographicSize = Mathf.Clamp(
                gameplayCamera.orthographicSize - scrollDelta * zoomSensitivity,
                minimumZoom,
                maximumZoom);
        }

        /// <summary>
        /// 동전 자세와 카메라 Zoom만 처음 상태로 되돌립니다.
        /// 청소 진행·도구 선택·완료 상태는 변경하지 않습니다.
        /// </summary>
        public void ResetViewToInitialState()
        {
            coinTransform.SetPositionAndRotation(initialCoinPosition, initialCoinRotation);
            gameplayCamera.orthographicSize = initialOrthographicSize;
            IsRotating = false;
            ClearSurfaceHit();
        }

        private void UpdateSurfaceFromScreenPosition(Vector2 screenPosition)
        {
            if (IsPointerOverUi)
            {
                ClearSurfaceHit();
                return;
            }

            if (TryFindCoinSurfaceHit(screenPosition, out var hit))
            {
                CurrentSurfaceHit = hit;
                HasSurfaceHit = true;
                CurrentSurfaceContact = default;
                CoinSurfaceMapper.TryMapHit(surfaceMeshCollider, surfaceMesh, surfaceLocalBounds,
                    hit, out var contact);
                CurrentSurfaceContact = contact;
                toolCursor.ShowContact(contact);
                return;
            }

            ClearSurfaceHit();
        }

        private bool TryResolveDependencies()
        {
            if (inputActions == null || gameplayCamera == null || coinCollider == null || coinTransform == null || toolCursor == null)
            {
                Debug.LogError("CoinSurfacePointer requires Input Actions, Camera, Coin Collider, Coin Transform, and Tool Cursor references.", this);
                return false;
            }

            surfaceMeshCollider = coinCollider as MeshCollider;
            var meshFilter = coinCollider.GetComponent<MeshFilter>();
            surfaceMesh = surfaceMeshCollider != null ? surfaceMeshCollider.sharedMesh : null;
            if (meshFilter == null || meshFilter.sharedMesh != surfaceMesh ||
                !CoinSurfaceMapper.TryGetSupportedBounds(surfaceMesh, out surfaceLocalBounds))
            {
                Debug.LogError("CoinSurfacePointer requires matching flat-cap circular Mesh/Collider geometry.", this);
                return false;
            }

            cleaningMap = inputActions.FindActionMap(CleaningMapName, throwIfNotFound: false);
            pointerPositionAction = cleaningMap?.FindAction(PointerPositionActionName, throwIfNotFound: false);
            pointerDeltaAction = cleaningMap?.FindAction(PointerDeltaActionName, throwIfNotFound: false);
            useToolAction = cleaningMap?.FindAction(UseToolActionName, throwIfNotFound: false);
            rotateCoinAction = cleaningMap?.FindAction(RotateCoinActionName, throwIfNotFound: false);
            zoomAction = cleaningMap?.FindAction(ZoomActionName, throwIfNotFound: false);
            resetViewAction = cleaningMap?.FindAction(ResetViewActionName, throwIfNotFound: false);
            if (pointerPositionAction == null || pointerDeltaAction == null || useToolAction == null || rotateCoinAction == null || zoomAction == null || resetViewAction == null)
            {
                Debug.LogError("CoinSurfacePointer could not find every required action in the assigned Cleaning map.", this);
                return false;
            }

            return true;
        }

        private void CaptureInitialViewState()
        {
            initialCoinPosition = coinTransform.position;
            initialCoinRotation = coinTransform.rotation;
            initialOrthographicSize = gameplayCamera.orthographicSize;
        }

        private void OnValidate()
        {
            maximumZoom = Mathf.Max(minimumZoom, maximumZoom);
        }

        private bool TryFindCoinSurfaceHit(Vector2 screenPosition, out RaycastHit surfaceHit)
        {
            var ray = gameplayCamera.ScreenPointToRay(screenPosition);
            return TryFindClosestPhysicsHit(ray, gameplayCamera.farClipPlane, out surfaceHit) &&
                surfaceHit.collider == coinCollider;
        }

        /// <summary>
        /// Coin 전용 필터보다 먼저 가장 가까운 물리 적중을 구해 가림물을 우선 처리합니다.
        /// RaycastNonAlloc의 반환 순서는 보장되지 않으므로 거리를 직접 비교합니다.
        /// </summary>
        private bool TryFindClosestPhysicsHit(Ray ray, float maxDistance, out RaycastHit closestHit)
        {
            var hitCount = Physics.RaycastNonAlloc(
                ray,
                raycastHits,
                maxDistance,
                raycastMask,
                QueryTriggerInteraction.Ignore);

            var closestDistance = float.PositiveInfinity;
            closestHit = default;

            for (var index = 0; index < hitCount; index++)
            {
                var hit = raycastHits[index];
                if (hit.collider == null || hit.distance >= closestDistance)
                {
                    continue;
                }

                closestHit = hit;
                closestDistance = hit.distance;
            }

            return closestDistance < float.PositiveInfinity;
        }

        private void ClearSurfaceHit()
        {
            HasSurfaceHit = false;
            CurrentSurfaceHit = default;
            CurrentSurfaceContact = default;
            toolCursor?.Hide();
            ContactInvalidated?.Invoke();
        }
    }
}
