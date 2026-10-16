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
        private static readonly Color LockedColor = new(.18f, .18f, .21f, .95f);
        private Text label;
        private string originalLabel;

        private void Awake()
        {
            if (toolSelection == null || tool == null || button == null || background == null)
            {
                Debug.LogError("CleaningToolButton requires selection, tool, button, and background references.", this);
                enabled = false;
                return;
            }

            button.onClick.AddListener(SelectTool);
            label = button.GetComponentInChildren<Text>(true);
            if (label != null) originalLabel = label.text;
        }

        private void OnEnable()
        {
            if (toolSelection == null)
            {
                return;
            }

            toolSelection.ToolSelected += HandleToolSelected;
            toolSelection.AvailableToolsChanged += RefreshVisual;
            RefreshVisual();
        }

        private void OnDisable()
        {
            if (toolSelection != null)
            {
                toolSelection.ToolSelected -= HandleToolSelected;
                toolSelection.AvailableToolsChanged -= RefreshVisual;
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
            var available = toolSelection.IsAvailable(tool);
            button.interactable = available;
            background.color = !available ? LockedColor : toolSelection.CurrentTool == tool ? selectedColor : normalColor;
            if (label != null)
                label.text = available ? originalLabel : tool.DisplayName + "\n<size=14>잠김 · " + toolSelection.GetUnavailableReason(tool) + "</size>";
        }
    }
}
