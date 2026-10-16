using System;
using System.Collections.Generic;
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
        public event Action AvailableToolsChanged;
        private readonly Dictionary<CleaningToolDefinition, string> unavailableReasons = new();

        public CleaningToolDefinition CurrentTool { get; private set; }

        public int AvailableToolCount => availableTools.Length;

        /// <summary>소유∩단계 허용 목록의 사본만 선택/실제 사용 경로에 노출합니다.</summary>
        public void ConfigureAvailableTools(CleaningToolDefinition[] tools,
            IReadOnlyDictionary<CleaningToolDefinition, string> reasons = null)
        {
            if (tools == null || tools.Length == 0) throw new ArgumentException("At least one allowed tool is required.", nameof(tools));
            var unique = new System.Collections.Generic.HashSet<CleaningToolDefinition>();
            foreach (var tool in tools)
                if (tool == null || !unique.Add(tool)) throw new ArgumentException("Missing or duplicate tool.", nameof(tools));
            availableTools = (CleaningToolDefinition[])tools.Clone();
            defaultTool = availableTools[0];
            unavailableReasons.Clear();
            if (reasons != null)
                foreach (var pair in reasons)
                    if (pair.Key != null && !unique.Contains(pair.Key)) unavailableReasons[pair.Key] = pair.Value;
            ResetToDefaultTool();
            // 기본 도구가 이전과 같아 ToolSelected가 발생하지 않아도 잠금 UI는 갱신합니다.
            AvailableToolsChanged?.Invoke();
        }

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

        public string GetUnavailableReason(CleaningToolDefinition tool)
        {
            if (tool == null) return "도구 정보 없음";
            if (IsAvailable(tool)) return null;
            return unavailableReasons.TryGetValue(tool, out var reason) && !string.IsNullOrEmpty(reason)
                ? reason : "이 단계 사용 불가";
        }

        public bool IsAvailable(CleaningToolDefinition tool)
        {
            if (tool == null) return false;
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
