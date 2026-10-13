using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using SqueakSqueak.Cleaning;

namespace SqueakSqueak.Progression
{
    /// <summary>정적 정의/유한 구매 경로 검사. 플레이어 진행을 변경하거나 실제 청소 시간을 인증하지 않습니다.</summary>
    public static class ProgressionCatalogValidator
    {
        private static readonly Regex StableId = new("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$");
        private static readonly string[] SurfaceProperties = { "_BaseColor", "_Smoothness", "_Metallic",
            "_FrontSurfaceMask", "_BackSurfaceMask", "_SurfaceBounds", "_SurfaceHalfHeight", "_DirtColor",
            "_DirtySmoothness", "_MaximumSmoothness" };

        public static IReadOnlyList<string> Validate(ProgressionCatalog catalog)
        {
            var errors = new List<string>();
            void Error(string code, string detail) => errors.Add(code + ": " + detail);
            if (catalog == null) { Error("catalog.missing", "카탈로그 없음"); return errors; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            void Id(string id)
            {
                if (string.IsNullOrEmpty(id) || !StableId.IsMatch(id)) Error("id.invalid", id ?? "null");
                else if (!ids.Add(id)) Error("id.duplicate", id);
            }
            Id(catalog.Id); Id(catalog.CurrencyId); Id(catalog.SaveSlotId);
            if (catalog.Coins == null || catalog.Stages == null || catalog.Tools == null ||
                catalog.InitiallyOwnedToolIds == null) { Error("catalog.collection", "null 목록"); return errors; }
            // 유한 상태 탐색의 명시적 저작 한계입니다. 이 프로토타입은2/3/3입니다.
            if (catalog.Coins.Count == 0 || catalog.Stages.Count == 0 || catalog.Tools.Count == 0 ||
                catalog.Stages.Count > 16 || catalog.Tools.Count > 8)
            { Error("catalog.limit", "빈 콘텐츠 또는16단계/8도구 초과"); return errors; }
            if (catalog.StartingCurrency < 0) Error("economy.value", "음수 시작 재화");
            if (catalog.ObjectDirtMesh == null || catalog.ObjectDirtMaterial == null)
                Error("dirt.reference", "A 기본 Mesh/Material 없음");
            foreach (var coin in catalog.Coins)
            {
                if (coin == null) { Error("coin.reference", "null 동전"); continue; }
                Id(coin.Id);
                if (string.IsNullOrWhiteSpace(coin.DisplayName)) Error("coin.name", coin.Id);
                if (!CoinSurfaceMapper.TryGetSupportedBounds(coin.SurfaceMesh, out _)) Error("coin.geometry", coin.Id);
                if (coin.SurfaceMaterial == null || SurfaceProperties.Any(p => !coin.SurfaceMaterial.HasProperty(p)))
                    Error("coin.material", coin.Id);
                var c = coin.BaseColor;
                if (!Unit(c.r) || !Unit(c.g) || !Unit(c.b) || !Unit(c.a)) Error("coin.color", coin.Id);
            }
            var stageIds = new HashSet<string>(catalog.Stages.Where(s => s != null).Select(s => s.Id ?? ""), StringComparer.Ordinal);
            var toolIds = new HashSet<string>(catalog.Tools.Select(t => t.Id ?? ""), StringComparer.Ordinal);
            var toolKinds = new HashSet<CleaningToolId>();
            foreach (var tool in catalog.Tools)
            {
                Id(tool.Id);
                if (tool.Definition == null) { Error("tool.reference", tool.Id); continue; }
                if (!Enum.IsDefined(typeof(CleaningToolId), tool.Definition.ToolId) || !toolKinds.Add(tool.Definition.ToolId))
                    Error("tool.kind", tool.Id);
                if (!Positive(tool.Definition.BrushRadius) || !NonNegative(tool.Definition.ObjectDirtRemovalPerSecond) ||
                    !NonNegative(tool.Definition.SurfaceDirtRemovalPerSecond) || !NonNegative(tool.Definition.PolishGainPerSecond))
                    Error("tool.value", tool.Id);
                if (tool.Price < 0) Error("economy.value", tool.Id);
                if (!string.IsNullOrEmpty(tool.UnlockedByStageId) && !stageIds.Contains(tool.UnlockedByStageId))
                    Error("tool.unlock", tool.Id);
            }
            void References(IReadOnlyList<string> list, HashSet<string> known, string context)
            {
                if (list == null) { Error("reference.list", context); return; }
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var id in list)
                    if (string.IsNullOrEmpty(id) || !known.Contains(id) || !seen.Add(id)) Error("reference.id", context + "/" + id);
            }
            References(catalog.InitiallyOwnedToolIds, toolIds, "initial tools");
            if (catalog.InitiallyOwnedToolIds.Count == 0) Error("initial.tools", "소유 도구 없음");
            foreach (var tool in catalog.Tools.Where(t => catalog.InitiallyOwnedToolIds.Contains(t.Id)))
                if (!string.IsNullOrEmpty(tool.UnlockedByStageId)) Error("initial.unlock", tool.Id);
            long maximumCurrency = catalog.StartingCurrency;
            foreach (var stage in catalog.Stages)
            {
                if (stage == null) { Error("stage.reference", "null 단계"); continue; }
                Id(stage.Id);
                if (string.IsNullOrWhiteSpace(stage.DisplayName)) Error("stage.name", stage.Id);
                if (stage.Coin == null || !catalog.Coins.Contains(stage.Coin)) Error("stage.coin", stage.Id);
                References(stage.PrerequisiteStageIds, stageIds, stage.Id + " prerequisites");
                References(stage.RequiredOwnedToolIds, toolIds, stage.Id + " required tools");
                References(stage.AllowedToolIds, toolIds, stage.Id + " allowed tools");
                if (stage.AllowedToolIds == null || stage.AllowedToolIds.Count == 0) Error("stage.tools", stage.Id);
                if (stage.RequiredOwnedToolIds != null && stage.AllowedToolIds != null &&
                    stage.RequiredOwnedToolIds.Any(t => !stage.AllowedToolIds.Contains(t))) Error("stage.tools", stage.Id);
                if (!Unit(stage.InitialSurfaceDirt) || !Unit(stage.InitialPolish) || !Unit(stage.RequiredA) ||
                    !Unit(stage.RequiredB) || !Unit(stage.RequiredPolish) ||
                    !Enum.IsDefined(typeof(CoinSurfaceDirtPattern), stage.DirtPattern)) Error("stage.surface", stage.Id);
                if (!NonNegative(stage.GradeASeconds) || !Positive(stage.GradeBSeconds) ||
                    stage.GradeBSeconds <= stage.GradeASeconds) Error("stage.grades", stage.Id);
                if (stage.FirstCompletionReward < 0) Error("economy.value", stage.Id);
                maximumCurrency += stage.FirstCompletionReward;
                if (stage.ObjectDirt == null) Error("stage.dirt", stage.Id);
                else foreach (var dirt in stage.ObjectDirt)
                {
                    var q = (dirt.Uv - Vector2.one * .5f) * 2;
                    if ((dirt.Face != CoinSurfaceFace.Front && dirt.Face != CoinSurfaceFace.Back) ||
                        !Unit(dirt.Uv.x) || !Unit(dirt.Uv.y) || q.sqrMagnitude > 1 ||
                        !Positive(dirt.Durability) || !Positive(dirt.WorldRadius) || dirt.WorldRadius > 1)
                        Error("stage.dirt", stage.Id);
                }
            }
            if (maximumCurrency > int.MaxValue) Error("economy.overflow", "첫 보상 합이 int 범위 초과");
            if (errors.Count != 0) return errors;

            var stages = catalog.Stages.ToArray();
            var tools = catalog.Tools.ToArray();
            var stageIndex = stages.Select((s, i) => (s.Id, i)).ToDictionary(x => x.Id, x => x.i, StringComparer.Ordinal);
            var toolIndex = tools.Select((t, i) => (t.Id, i)).ToDictionary(x => x.Id, x => x.i, StringComparer.Ordinal);
            var marks = new int[stages.Length];
            bool Cycle(int index)
            {
                if (marks[index] == 1) return true;
                if (marks[index] == 2) return false;
                marks[index] = 1;
                foreach (var id in stages[index].PrerequisiteStageIds) if (Cycle(stageIndex[id])) return true;
                marks[index] = 2;
                return false;
            }
            for (var i = 0; i < stages.Length; i++) if (Cycle(i)) { Error("stage.cycle", stages[i].Id); break; }
            foreach (var stage in stages)
                if (!CanClean(stage, tools.Where(t => stage.AllowedToolIds.Contains(t.Id)).Select(t => t.Definition)))
                    Error("stage.capability", stage.Id);
            if (errors.Count != 0) return errors;

            int Mask(IEnumerable<string> names, Dictionary<string, int> index) => names.Aggregate(0, (m, id) => m | (1 << index[id]));
            var owned = Mask(catalog.InitiallyOwnedToolIds, toolIndex);
            var queue = new Queue<(int done, int owned, long balance)>();
            var best = new Dictionary<int, long>();
            int reachedStages = 0, reachedTools = owned;
            void Enqueue(int done, int inventory, long balance)
            {
                var key = (done << 8) | inventory;
                // 같은 완료/소유 집합의 낮은 잔액은 더 좋은 경로에 지배됩니다.
                if (best.TryGetValue(key, out var prior) && prior >= balance) return;
                best[key] = balance;
                queue.Enqueue((done, inventory, balance));
            }
            Enqueue(0, owned, catalog.StartingCurrency);
            while (queue.Count > 0)
            {
                if (best.Count > 8192) { Error("catalog.complexity", "도달성 탐색8192상태 초과"); return errors; }
                var state = queue.Dequeue();
                for (var i = 0; i < stages.Length; i++)
                {
                    var stage = stages[i]; var bit = 1 << i;
                    var prerequisites = Mask(stage.PrerequisiteStageIds, stageIndex);
                    var requiredTools = Mask(stage.RequiredOwnedToolIds, toolIndex);
                    if ((state.done & bit) != 0 || (state.done & prerequisites) != prerequisites ||
                        (state.owned & requiredTools) != requiredTools) continue;
                    var usable = tools.Where((t, j) => (state.owned & (1 << j)) != 0 && stage.AllowedToolIds.Contains(t.Id));
                    if (!CanClean(stage, usable.Select(t => t.Definition))) continue;
                    reachedStages |= bit;
                    Enqueue(state.done | bit, state.owned, state.balance + stage.FirstCompletionReward);
                }
                for (var i = 0; i < tools.Length; i++)
                {
                    var tool = tools[i]; var bit = 1 << i;
                    if ((state.owned & bit) != 0 || tool.Price > state.balance ||
                        (!string.IsNullOrEmpty(tool.UnlockedByStageId) && (state.done & (1 << stageIndex[tool.UnlockedByStageId])) == 0)) continue;
                    reachedTools |= bit;
                    Enqueue(state.done, state.owned | bit, state.balance - tool.Price);
                }
            }
            for (var i = 0; i < stages.Length; i++) if ((reachedStages & (1 << i)) == 0) Error("unreachable.stage", stages[i].Id);
            for (var i = 0; i < tools.Length; i++) if ((reachedTools & (1 << i)) == 0) Error("unreachable.tool", tools[i].Id);
            // 모두를 한 경로에서 완료/소유할 수 있어야 합니다. 개별 도달 가능만으로 경제를 인증하지 않습니다.
            var allDone = (1 << stages.Length) - 1; var allTools = (1 << tools.Length) - 1;
            if (!best.ContainsKey((allDone << 8) | allTools)) Error("economy.path", "전체 완료/도구 소유를 연결하는 경로 없음");
            return errors;
        }

        private static bool CanClean(StageDefinition stage, IEnumerable<CleaningToolDefinition> definitions)
        {
            var tools = definitions.ToArray();
            var removesB = tools.Any(t => t.SurfaceDirtRemovalPerSecond > 0);
            return tools.Length > 0 && (stage.ObjectDirt.Count == 0 || stage.RequiredA == 0 || tools.Any(t => t.ObjectDirtRemovalPerSecond > 0)) &&
                (stage.InitialSurfaceDirt == 0 || stage.RequiredB == 0 || removesB) &&
                (stage.RequiredPolish <= stage.InitialPolish || (tools.Any(t => t.PolishGainPerSecond > 0) &&
                    !(stage.InitialSurfaceDirt == 1 && stage.DirtPattern == CoinSurfaceDirtPattern.Uniform && !removesB)));
        }
        private static bool NonNegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
        private static bool Positive(float value) => NonNegative(value) && value > 0;
        private static bool Unit(float value) => NonNegative(value) && value <= 1;
    }
}
