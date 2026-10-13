using System;
using System.Collections.Generic;

namespace SqueakSqueak.Progression
{
    /// <summary>진행 소유자가 전달하는 읽기 전용 접근 스냅샷입니다. 보상/구매/저장하지 않습니다.</summary>
    public sealed class StageAccessContext
    {
        private readonly HashSet<string> owned;
        private readonly HashSet<string> completed;
        public StageAccessContext(IEnumerable<string> ownedToolIds, IEnumerable<string> completedStageIds)
        {
            owned = Copy(ownedToolIds);
            completed = Copy(completedStageIds);
        }

        private static HashSet<string> Copy(IEnumerable<string> ids)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids));
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ids)
                if (string.IsNullOrEmpty(id) || !result.Add(id)) throw new ArgumentException("Empty or duplicate access ID.");
            return result;
        }

        public bool Owns(string toolId) => owned.Contains(toolId);
        public bool Completed(string stageId) => completed.Contains(stageId);

        public bool Validate(ProgressionCatalog catalog, out string error)
        {
            var stages = new Dictionary<string, StageDefinition>(StringComparer.Ordinal);
            var tools = new Dictionary<string, CatalogTool>(StringComparer.Ordinal);
            foreach (var stage in catalog.Stages) stages.Add(stage.Id, stage);
            foreach (var tool in catalog.Tools) tools.Add(tool.Id, tool);
            foreach (var id in owned)
                if (!tools.TryGetValue(id, out var tool) || (!string.IsNullOrEmpty(tool.UnlockedByStageId) && !completed.Contains(tool.UnlockedByStageId)))
                { error = "소유 도구의 ID 또는 구매 자격이 유효하지 않습니다: " + id; return false; }
            foreach (var id in completed)
                if (!stages.TryGetValue(id, out var stage) || !CanEnter(stage, out error))
                { error = "완료 단계의 ID 또는 전제가 유효하지 않습니다: " + id; return false; }
            if (owned.Count == 0) { error = "사용할 소유 도구가 없습니다."; return false; }
            error = null;
            return true;
        }

        public bool CanEnter(StageDefinition stage, out string error)
        {
            foreach (var id in stage.PrerequisiteStageIds)
                if (!completed.Contains(id)) { error = "먼저 완료할 단계: " + id; return false; }
            foreach (var id in stage.RequiredOwnedToolIds)
                if (!owned.Contains(id)) { error = "필요한 소유 도구: " + id; return false; }
            foreach (var id in stage.AllowedToolIds)
                if (owned.Contains(id)) { error = null; return true; }
            error = "이 단계에 허용된 소유 도구가 없습니다.";
            return false;
        }
    }
}
