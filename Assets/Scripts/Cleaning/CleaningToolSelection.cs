using System;
using UnityEngine;

namespace SqueakSqueak.Cleaning
{
    [DisallowMultipleComponent]
    public sealed class CleaningToolSelection : MonoBehaviour
    {
        [SerializeField]
        private CleaningToolDefinition[] availableTools = Array.Empty<CleaningToolDefinition>();

        [SerializeField]
        private CleaningToolDefinition defaultTool;

        public event Action<CleaningToolDefinition> ToolSelected;

        public CleaningToolDefinition CurrentTool { get; private set; }

        public int AvailableToolCount => availableTools.Length;

        private void Awake()
        {
            if (ResetToDefaultTool())
            {
                return;
            }

            Debug.LogError("CleaningToolSelection requires an available default tool.", this);
            enabled = false;
        }

        private void OnEnable()
        {
            if (CurrentTool != null || ResetToDefaultTool())
            {
                return;
            }

            Debug.LogError("CleaningToolSelection could not restore its default tool.", this);
            enabled = false;
        }

        public CleaningToolDefinition GetToolAt(int index)
        {
            return availableTools[index];
        }

        public bool TrySelect(CleaningToolId toolId)
        {
            for (var index = 0; index < availableTools.Length; index++)
            {
                var tool = availableTools[index];
                if (tool != null && tool.ToolId == toolId)
                {
                    return TrySelect(tool);
                }
            }

            return false;
        }

        public bool TrySelect(CleaningToolDefinition tool)
        {
            if (tool == null || !IsAvailable(tool))
            {
                return false;
            }

            if (CurrentTool == tool)
            {
                return true;
            }

            CurrentTool = tool;
            ToolSelected?.Invoke(tool);
            return true;
        }

        public bool ResetToDefaultTool()
        {
            return TrySelect(defaultTool);
        }

        private bool IsAvailable(CleaningToolDefinition tool)
        {
            for (var index = 0; index < availableTools.Length; index++)
            {
                if (availableTools[index] == tool)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
