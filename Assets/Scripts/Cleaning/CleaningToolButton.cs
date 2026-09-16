using UnityEngine;
using UnityEngine.UI;

namespace SqueakSqueak.Cleaning
{
    [DisallowMultipleComponent]
    public sealed class CleaningToolButton : MonoBehaviour
    {
        [SerializeField]
        private CleaningToolSelection toolSelection;

        [SerializeField]
        private CleaningToolDefinition tool;

        [SerializeField]
        private Button button;

        [SerializeField]
        private Image background;

        [SerializeField]
        private Color normalColor = new Color(0.13f, 0.2f, 0.28f, 0.95f);

        [SerializeField]
        private Color selectedColor = new Color(0.12f, 0.54f, 0.62f, 0.98f);

        private void Awake()
        {
            if (toolSelection == null || tool == null || button == null || background == null)
            {
                Debug.LogError("CleaningToolButton requires selection, tool, button, and background references.", this);
                enabled = false;
                return;
            }

            button.onClick.AddListener(SelectTool);
        }

        private void OnEnable()
        {
            if (toolSelection == null)
            {
                return;
            }

            toolSelection.ToolSelected += HandleToolSelected;
            RefreshVisual();
        }

        private void OnDisable()
        {
            if (toolSelection != null)
            {
                toolSelection.ToolSelected -= HandleToolSelected;
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(SelectTool);
            }
        }

        private void SelectTool()
        {
            toolSelection.TrySelect(tool);
        }

        private void HandleToolSelected(CleaningToolDefinition selectedTool)
        {
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            background.color = toolSelection.CurrentTool == tool ? selectedColor : normalColor;
        }
    }
}
