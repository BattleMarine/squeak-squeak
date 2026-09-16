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
        private InputAction rotateCoinAction;
        private InputAction zoomAction;
        private InputAction resetViewAction;
        private Vector3 initialCoinPosition;
        private Quaternion initialCoinRotation;
        private float initialOrthographicSize;

        public bool HasSurfaceHit { get; private set; }

        public RaycastHit CurrentSurfaceHit { get; private set; }

        public bool IsRotating { get; private set; }

        /// <summary>
        /// 도구 처리에서 사용할 수 있는 현재 포인터 상태입니다.
        /// UI 위 포인터는 동전 표면에 적중하더라도 게임플레이 입력으로 취급하지 않습니다.
        /// </summary>
        public bool CanUseTool => HasSurfaceHit && !IsPointerOverUi;

        public bool IsPointerOverUi => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

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

        private void Update()
        {
            if (pointerPositionAction == null)
            {
                return;
            }

            if (resetViewAction.WasPressedThisFrame())
            {
                ResetView();
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

        private void ResetView()
        {
            coinTransform.SetPositionAndRotation(initialCoinPosition, initialCoinRotation);
            gameplayCamera.orthographicSize = initialOrthographicSize;
            IsRotating = false;
            ClearSurfaceHit();
        }

        private void UpdateSurfaceFromScreenPosition(Vector2 screenPosition)
        {
            if (TryFindCoinSurfaceHit(screenPosition, out var hit))
            {
                CurrentSurfaceHit = hit;
                HasSurfaceHit = true;
                toolCursor.ShowAt(hit.point, hit.normal);
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

            cleaningMap = inputActions.FindActionMap(CleaningMapName, throwIfNotFound: false);
            pointerPositionAction = cleaningMap?.FindAction(PointerPositionActionName, throwIfNotFound: false);
            pointerDeltaAction = cleaningMap?.FindAction(PointerDeltaActionName, throwIfNotFound: false);
            rotateCoinAction = cleaningMap?.FindAction(RotateCoinActionName, throwIfNotFound: false);
            zoomAction = cleaningMap?.FindAction(ZoomActionName, throwIfNotFound: false);
            resetViewAction = cleaningMap?.FindAction(ResetViewActionName, throwIfNotFound: false);
            if (pointerPositionAction == null || pointerDeltaAction == null || rotateCoinAction == null || zoomAction == null || resetViewAction == null)
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

        private bool TryFindCoinSurfaceHit(Vector2 screenPosition, out RaycastHit closestCoinHit)
        {
            var ray = gameplayCamera.ScreenPointToRay(screenPosition);
            var hitCount = Physics.RaycastNonAlloc(
                ray,
                raycastHits,
                gameplayCamera.farClipPlane,
                raycastMask,
                QueryTriggerInteraction.Ignore);

            var closestDistance = float.PositiveInfinity;
            closestCoinHit = default;

            for (var index = 0; index < hitCount; index++)
            {
                var hit = raycastHits[index];
                if (hit.collider != coinCollider || hit.distance >= closestDistance)
                {
                    continue;
                }

                closestCoinHit = hit;
                closestDistance = hit.distance;
            }

            return closestDistance < float.PositiveInfinity;
        }

        private void ClearSurfaceHit()
        {
            HasSurfaceHit = false;
            CurrentSurfaceHit = default;
            toolCursor?.Hide();
        }
    }
}
