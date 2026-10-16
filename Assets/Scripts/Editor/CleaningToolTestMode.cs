using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using SqueakSqueak.Cleaning;
using SqueakSqueak.Progression;

namespace SqueakSqueak.EditorValidation
{
    /// <summary>이번 Play/판의 도구 선택 목록만 일시적으로 바꿉니다. 데이터/소유권/진행/Player에는 적용하지 않습니다.</summary>
    [InitializeOnLoad]
    public static class CleaningToolTestMode
    {
        private static CleaningStageController owner;
        private static CleaningToolSelection selection;
        private static CleaningToolDefinition[] originalTools;
        private static CleaningToolDefinition originalSelection;
        private static Dictionary<CleaningToolDefinition, string> originalReasons;
        private static long roundId;
        public static bool IsActive => Application.isPlaying && owner != null && selection != null && owner.RoundId == roundId && originalTools != null;

        static CleaningToolTestMode() => EditorApplication.playModeStateChanged += OnPlayState;

        [MenuItem("Squeak Squeak/Testing/Tools/Enable All Tools (This Play Only)")]
        private static void EnableMenu() => Debug.Log(EnableAllTools());
        [MenuItem("Squeak Squeak/Testing/Tools/Enable All Tools (This Play Only)", true)]
        private static bool CanEnable() => Application.isPlaying;
        [MenuItem("Squeak Squeak/Testing/Tools/Restore Normal Tool Limits")]
        private static void RestoreMenu() => Debug.Log(RestoreNormalTools());
        [MenuItem("Squeak Squeak/Testing/Tools/Restore Normal Tool Limits", true)]
        private static bool CanRestore() => IsActive;

        public static string EnableAllTools()
        {
            if (!Application.isPlaying || SceneManager.GetActiveScene().path != "Assets/Scenes/CleaningRoom.unity")
                throw new InvalidOperationException("CleaningRoom Play에서만 사용하는 Editor 개발 검사 메뉴입니다.");
            if (IsActive) return "이미 이번 판의 Editor 전체 도구 검사 모드입니다.";
            var current = UnityEngine.Object.FindFirstObjectByType<CleaningStageController>();
            if (current == null || current.State != CleaningStageState.Cleaning)
                throw new InvalidOperationException("청소 상태에서만 전체 도구를 검사할 수 있습니다.");
            var catalog = AssetDatabase.LoadAssetAtPath<ProgressionCatalog>(ProgressionCatalogTools.CatalogPath);
            var issues = ProgressionCatalogValidator.Validate(catalog);
            if (issues.Count != 0) throw new InvalidOperationException(issues[0]);
            owner = current; selection = current.GetComponent<CleaningToolSelection>(); roundId = current.RoundId;
            originalTools = Enumerable.Range(0, selection.AvailableToolCount).Select(selection.GetToolAt).ToArray();
            originalSelection = selection.CurrentTool;
            originalReasons = new Dictionary<CleaningToolDefinition, string>();
            foreach (var tool in catalog.Tools)
                if (!selection.IsAvailable(tool.Definition)) originalReasons[tool.Definition] = selection.GetUnavailableReason(tool.Definition);
            selection.ConfigureAvailableTools(catalog.Tools.Select(t => t.Definition).ToArray());
            return "개발 검사 전용: 이번 Play/판에서 도구3종 허용. 단계/오염/목표/소유권·정식 데이터는 그대로입니다. 새 판/새 Play는 정상 제한으로 돌아갑니다.";
        }

        public static string RestoreNormalTools()
        {
            if (!IsActive) { Clear(); return "검사 판이 끝났습니다. 이전 판의 도구 목록은 복원하지 않습니다."; }
            selection.ConfigureAvailableTools(originalTools, originalReasons);
            selection.TrySelect(originalSelection);
            Clear();
            return "이번 판의 정상 소유∩단계 허용 목록과 잠금 사유를 복원했습니다.";
        }

        private static void OnPlayState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode) Clear();
        }
        private static void Clear() { owner = null; selection = null; originalTools = null; originalSelection = null; originalReasons = null; roundId = 0; }
    }
}
