using UnityEngine;

namespace SqueakSqueak.Cleaning
{
    [DisallowMultipleComponent]
    public sealed class SurfaceToolCursor : MonoBehaviour
    {
        [SerializeField]
        [Min(0f)]
        private float surfaceOffset = 0.02f;

        [SerializeField]
        private Renderer cursorRenderer;

        public bool IsVisible { get; private set; }

        private void Awake()
        {
            if (cursorRenderer == null)
            {
                cursorRenderer = GetComponent<Renderer>();
            }

            Hide();
        }

        private void OnValidate()
        {
            if (cursorRenderer == null)
            {
                cursorRenderer = GetComponent<Renderer>();
            }
        }

        public void ShowAt(Vector3 surfacePosition, Vector3 surfaceNormal)
        {
            if (surfaceNormal.sqrMagnitude <= Mathf.Epsilon)
            {
                Hide();
                return;
            }

            var normalizedNormal = surfaceNormal.normalized;
            transform.SetPositionAndRotation(
                surfacePosition + normalizedNormal * surfaceOffset,
                Quaternion.FromToRotation(Vector3.up, normalizedNormal));
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
        }
    }
}
