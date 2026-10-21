using UnityEngine;

namespace SqueakSqueak.Cleaning
{
    [DisallowMultipleComponent]
    public sealed class SurfaceToolCursor : MonoBehaviour
    {
        private const int RangeSegmentCount = 48;

        [SerializeField]
        [Min(0f)]
        private float surfaceOffset = 0.02f;

        [SerializeField]
        private Renderer cursorRenderer;

        [SerializeField]
        private LineRenderer rangeRenderer;

        [SerializeField]
        private CleaningToolSelection toolSelection;

        public bool IsVisible { get; private set; }

        private void Awake()
        {
            if (cursorRenderer == null)
            {
                cursorRenderer = GetComponent<MeshRenderer>();
            }

            if (rangeRenderer == null)
            {
                rangeRenderer = GetComponent<LineRenderer>();
            }

            if (cursorRenderer == null || rangeRenderer == null || toolSelection == null)
            {
                Debug.LogError("SurfaceToolCursor requires Cursor Renderer, Range Renderer, and Tool Selection references.", this);
                enabled = false;
                return;
            }

            rangeRenderer.positionCount = RangeSegmentCount;
            rangeRenderer.loop = true;
            Hide();
        }

        private void OnValidate()
        {
            if (cursorRenderer == null)
            {
                cursorRenderer = GetComponent<MeshRenderer>();
            }

            if (rangeRenderer == null)
            {
                rangeRenderer = GetComponent<LineRenderer>();
            }
        }

        public void ShowContact(CoinSurfaceContact contact,Matrix4x4? surfaceMatrix=null,Bounds? bounds=null)
        {
            if (!contact.IsCleanable)
            {
                Hide();
                return;
            }
            ShowAt(contact.WorldPosition, contact.WorldNormal);
            if(contact.Face==CoinSurfaceFace.Side&&surfaceMatrix.HasValue&&bounds.HasValue&&toolSelection.CurrentTool!=null)
            {
                var matrix=surfaceMatrix.Value;var geometry=bounds.Value;var radius=toolSelection.CurrentTool.BrushRadius;
                var angle=contact.Uv.x*2*Mathf.PI;
                var circumferenceRadius=geometry.extents.x*matrix.MultiplyVector(new Vector3(-Mathf.Sin(angle),0,Mathf.Cos(angle))).magnitude;
                var height=2*geometry.extents.y*matrix.MultiplyVector(Vector3.up).magnitude;
                for(var i=0;i<RangeSegmentCount;i++)
                {
                    var a=i*2*Mathf.PI/RangeSegmentCount;
                    var ring=CoinSideSurface.Contact(matrix,geometry,contact.Uv.x+radius*Mathf.Cos(a)/(2*Mathf.PI*circumferenceRadius),contact.Uv.y+radius*Mathf.Sin(a)/height);
                    rangeRenderer.SetPosition(i,ring.WorldPosition+ring.WorldNormal*surfaceOffset);
                }
            }
        }

        public void ShowAt(Vector3 surfacePosition, Vector3 surfaceNormal)
        {
            var tool = toolSelection != null ? toolSelection.CurrentTool : null;
            if (surfaceNormal.sqrMagnitude <= Mathf.Epsilon || tool == null)
            {
                Hide();
                return;
            }

            var normalizedNormal = surfaceNormal.normalized;
            transform.SetPositionAndRotation(
                surfacePosition + normalizedNormal * surfaceOffset,
                Quaternion.FromToRotation(Vector3.up, normalizedNormal));

            // LineRenderer는 월드 좌표를 사용해 커서 점의 고정 로컬 스케일과 무관하게
            // 실제 A 오염 접촉 반경을 표면의 접평면에 표시한다.
            for (var index = 0; index < RangeSegmentCount; index++)
            {
                var angle = index * Mathf.PI * 2f / RangeSegmentCount;
                var radialDirection = transform.right * Mathf.Cos(angle) + transform.forward * Mathf.Sin(angle);
                rangeRenderer.SetPosition(index, transform.position + radialDirection * tool.BrushRadius);
            }

            SetVisible(true);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        private void SetVisible(bool isVisible)
        {
            IsVisible = isVisible;

            if (cursorRenderer != null)
            {
                cursorRenderer.enabled = isVisible;
            }

            if (rangeRenderer != null)
            {
                rangeRenderer.enabled = isVisible;
            }
        }
    }
}
