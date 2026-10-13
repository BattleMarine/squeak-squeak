using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SqueakSqueak.Cleaning;
using SqueakSqueak.Progression;

namespace SqueakSqueak.EditorValidation
{
    /// <summary>실제 카탈로그와 비저장 복제의 결함 탐지 검사. 실제 단계/구매/저장 실행은 하지 않습니다.</summary>
    public static class ProgressionCatalogValidation
    {
        public static string Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Edit 상태에서 검사하세요.");
            var source = AssetDatabase.LoadAssetAtPath<ProgressionCatalog>(ProgressionCatalogTools.CatalogPath);
            if (source == null) throw new InvalidOperationException("PrototypeCatalog를 먼저 생성하세요.");
            var checks = 0;
            void Check(bool condition, string label)
            {
                if (!condition) throw new InvalidOperationException("S3-01b FAIL: " + label);
                checks++;
            }
            Check(ProgressionCatalogValidator.Validate(source).Count == 0, "실제 정의 유효");
            Check(source.Coins.Count == 2 && source.Stages.Count == 3 && source.Tools.Count == 3 &&
                source.CurrencyId == "cleaning-credit" && source.SaveSlotId == "slot-01", "2/3/3/1/1 기본안");
            foreach (var asset in source.Coins.Cast<ScriptableObject>().Concat(source.Stages).Append(source))
                Check(EditorUtility.IsPersistent(asset) && !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset))), "실제 에셋 GUID");
            foreach (var coin in source.Coins)
                Check(EditorUtility.IsPersistent(coin.SurfaceMesh) && EditorUtility.IsPersistent(coin.SurfaceMaterial), "동전 Mesh/Material 영구 참조");
            Check(EditorUtility.IsPersistent(source.ObjectDirtMesh) && EditorUtility.IsPersistent(source.ObjectDirtMaterial), "A 영구 참조");
            foreach (var tool in source.Tools) Check(EditorUtility.IsPersistent(tool.Definition), "기존 도구 영구 참조");
            Check(source.Coins[0].SurfaceMesh == source.Coins[1].SurfaceMesh && source.Coins[0].BaseColor != source.Coins[1].BaseColor,
                "같은 원판/색 구분2외형");
            Check(source.Stages[0].ObjectDirt.Count == 2 && source.Stages[1].ObjectDirt.Count == 3 && source.Stages[2].ObjectDirt.Count == 5,
                "고정 A 배치2/3/5");
            Check(source.Stages[0].DirtPattern == CoinSurfaceDirtPattern.FixedPatches &&
                source.Stages[1].DirtPattern == CoinSurfaceDirtPattern.Uniform && source.Stages[2].DirtPattern == CoinSurfaceDirtPattern.FixedPatches,
                "두 지원 B 패턴으로 단계 구분");
            Check(source.Stages[0].RequiredPolish == 0 && source.Stages[1].RequiredPolish == 0 && source.Stages[2].RequiredPolish == .7f,
                "잠긴 광택천 없이 초기 목표 가능");
            Check(source.Stages[0].FirstCompletionReward - source.Tools[1].Price + source.Stages[1].FirstCompletionReward -
                source.Tools[2].Price == 40, "광택천 구매까지100-60+100-100=40");
            void Reject(string code, Action<Fixture> change)
            {
                using var f = new Fixture(source); change(f);
                Check(ProgressionCatalogValidator.Validate(f.Catalog).Any(e => e.StartsWith(code + ":", StringComparison.Ordinal)), code);
            }
            Reject("id.invalid", f => Edit(f.Catalog, so => so.FindProperty("id").stringValue = "bad id"));
            Reject("id.duplicate", f => Edit(f.Coins[1], so => so.FindProperty("id").stringValue = f.Coins[0].Id));
            Reject("id.duplicate", f => Edit(f.Stages[1], so => so.FindProperty("id").stringValue = f.Stages[0].Id));
            Reject("id.duplicate", f => Tool(f, 1, "id", p => p.stringValue = "dry-cloth"));
            Reject("coin.reference", f => Edit(f.Catalog, so => so.FindProperty("coins").GetArrayElementAtIndex(0).objectReferenceValue = null));
            Reject("coin.geometry", f => Edit(f.Coins[0], so => so.FindProperty("surfaceMesh").objectReferenceValue = null));
            Reject("coin.geometry", f => Edit(f.Coins[0], so => so.FindProperty("surfaceMesh").objectReferenceValue = Resources.GetBuiltinResource<Mesh>("Cube.fbx")));
            Reject("coin.material", f => Edit(f.Coins[0], so => so.FindProperty("surfaceMaterial").objectReferenceValue = null));
            Reject("coin.color", f => Edit(f.Coins[0], so => so.FindProperty("baseColor").colorValue = new Color(float.NaN, 0, 0)));
            Reject("dirt.reference", f => Edit(f.Catalog, so => so.FindProperty("objectDirtMesh").objectReferenceValue = null));
            Reject("tool.reference", f => Tool(f, 1, "definition", p => p.objectReferenceValue = null));
            Reject("tool.kind", f => Edit(f.Tools[1], so => so.FindProperty("toolId").intValue = (int)CleaningToolId.DryCloth));
            Reject("tool.unlock", f => Tool(f, 1, "unlockedByStageId", p => p.stringValue = "stage-missing"));
            Reject("economy.value", f => Tool(f, 1, "price", p => p.intValue = -1));
            Reject("economy.value", f => Edit(f.Catalog, so => so.FindProperty("startingCurrency").intValue = -1));
            Reject("initial.tools", f => Edit(f.Catalog, so => so.FindProperty("initiallyOwnedToolIds").arraySize = 0));
            Reject("initial.unlock", f => Edit(f.Catalog, so => ProgressionCatalogTools.Strings(so.FindProperty("initiallyOwnedToolIds"), new[] { "small-brush" })));
            Reject("stage.coin", f => Edit(f.Stages[0], so => so.FindProperty("coin").objectReferenceValue = null));
            Reject("reference.id", f => Edit(f.Stages[0], so => ProgressionCatalogTools.Strings(so.FindProperty("allowedToolIds"), new[] { "unknown-tool" })));
            Reject("stage.cycle", f => Edit(f.Stages[0], so => ProgressionCatalogTools.Strings(so.FindProperty("prerequisiteStageIds"), new[] { "stage-03" })));
            Reject("stage.tools", f => Edit(f.Stages[1], so => ProgressionCatalogTools.Strings(so.FindProperty("allowedToolIds"), new[] { "dry-cloth" })));
            Reject("stage.surface", f => Edit(f.Stages[0], so => so.FindProperty("initialSurfaceDirt").floatValue = float.NaN));
            Reject("stage.surface", f => Edit(f.Stages[0], so => so.FindProperty("requiredB").floatValue = 1.1f));
            Reject("stage.grades", f => Edit(f.Stages[0], so => so.FindProperty("gradeBSeconds").floatValue = 100));
            Reject("stage.grades", f => Edit(f.Stages[0], so => so.FindProperty("gradeASeconds").floatValue = float.PositiveInfinity));
            Reject("economy.value", f => Edit(f.Stages[0], so => so.FindProperty("firstCompletionReward").intValue = -1));
            Reject("economy.overflow", f => Edit(f.Catalog, so => so.FindProperty("startingCurrency").intValue = int.MaxValue));
            Reject("stage.dirt", f => Placement(f, "uv", p => p.vector2Value = new Vector2(1, 1)));
            Reject("stage.dirt", f => Placement(f, "face", p => p.intValue = (int)CoinSurfaceFace.Side));
            Reject("stage.dirt", f => Placement(f, "durability", p => p.floatValue = 0));
            Reject("stage.dirt", f => Placement(f, "worldRadius", p => p.floatValue = float.NaN));
            Reject("stage.capability", f => { foreach (var t in f.Tools) Edit(t, so => so.FindProperty("surfaceDirtRemovalPerSecond").floatValue = 0); });
            Reject("stage.capability", f => { foreach (var t in f.Tools) Edit(t, so => so.FindProperty("objectDirtRemovalPerSecond").floatValue = 0); });
            Reject("stage.capability", f => { foreach (var t in f.Tools) Edit(t, so => so.FindProperty("polishGainPerSecond").floatValue = 0); });
            Reject("stage.capability", f =>
            {
                foreach (var t in f.Tools) Edit(t, so => so.FindProperty("surfaceDirtRemovalPerSecond").floatValue = 0);
                foreach (var s in f.Stages) Edit(s, so => so.FindProperty("requiredB").floatValue = 0);
                Edit(f.Stages[2], so => so.FindProperty("dirtPattern").intValue = (int)CoinSurfaceDirtPattern.Uniform);
            });
            Reject("unreachable.stage", f => Tool(f, 1, "price", p => p.intValue = 101));
            Reject("unreachable.stage", f => Tool(f, 2, "unlockedByStageId", p => p.stringValue = "stage-03"));
            Reject("economy.path", f =>
            {
                // 각 분기는 따로 열리지만 잔액100으로100짜리 두 도구를 모두 살 수 없는 사례입니다.
                for (var i = 1; i < 3; i++)
                {
                    Tool(f, i, "price", p => p.intValue = 100);
                    Tool(f, i, "unlockedByStageId", p => p.stringValue = "stage-01");
                    Edit(f.Stages[i], so =>
                    {
                        ProgressionCatalogTools.Strings(so.FindProperty("prerequisiteStageIds"), new[] { "stage-01" });
                        so.FindProperty("firstCompletionReward").intValue = 0;
                    });
                }
            });
            Reject("unreachable.stage", f => Edit(f.Stages[0], so =>
            {
                ProgressionCatalogTools.Strings(so.FindProperty("allowedToolIds"), new[] { "polishing-cloth" });
                so.FindProperty("requiredA").floatValue = 0; so.FindProperty("requiredB").floatValue = 0;
                so.FindProperty("requiredPolish").floatValue = 0;
            }));
            // 초기 B=0/광택 충족은 없는 역할을 요구하지 않습니다. 보상/소유 경로는 그대로 검사합니다.
            using (var f = new Fixture(source))
            {
                Edit(f.Stages[2], so => { so.FindProperty("initialSurfaceDirt").floatValue = 0; so.FindProperty("initialPolish").floatValue = .7f; });
                Check(ProgressionCatalogValidator.Validate(f.Catalog).Count == 0, "초기 충족 경계 유효");
            }
            Check(ProgressionCatalogValidator.Validate(source).Count == 0, "원본 정의 불변");
            return $"S3-01b CATALOG PASS checks={checks}; real asset refs + malformed clones; full completion/purchase reachability. " +
                "No gameplay, save transaction, or physical input executed.";
        }

        public static string RunPreferredToolComponents()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Edit 상태에서 검사하세요.");
            var preview = EditorSceneManager.NewPreviewScene();
            var checks = 0;
            try
            {
                var go = new GameObject("S3-01a isolated dirt"); SceneManager.MoveGameObjectToScene(go, preview);
                var dirt = go.AddComponent<ObjectDirt>();
                typeof(ObjectDirt).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(dirt, null);
                foreach (var name in new[] { "DryCloth", "SmallBrush", "PolishingCloth" })
                foreach (var hz in new[] { 30, 60, 120 })
                {
                    var tool = AssetDatabase.LoadAssetAtPath<CleaningToolDefinition>($"Assets/Data/CleaningTools/{name}.asset");
                    dirt.ResetDirt(); var removals = 0;
                    void Removed(ObjectDirt _) => removals++;
                    dirt.Removed += Removed;
                    var frame = 0; var maximum = Mathf.CeilToInt(hz / tool.ObjectDirtRemovalPerSecond) + 2;
                    while (!dirt.IsRemoved && frame < maximum) { dirt.ApplyCleaning(tool.ObjectDirtRemovalPerSecond / hz); frame++; }
                    dirt.Removed -= Removed;
                    if (!dirt.IsRemoved || removals != 1 || Mathf.Abs(frame / (float)hz - 1 / tool.ObjectDirtRemovalPerSecond) > 1.01f / hz)
                        throw new InvalidOperationException("A rate/frequency failure " + name + "/" + hz);
                    checks++;
                    dirt.ApplyCleaning(100); dirt.ResetDirt();
                    if (dirt.IsRemoved || dirt.CurrentDurability != 1 || !go.activeSelf || go.transform.localScale != Vector3.one)
                        throw new InvalidOperationException("A reset failure");
                    checks++;
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            return $"S3-01a A COMPONENT PASS checks={checks}; preview scene/explicit Awake, 3 tools x30/60/120Hz + reset. Not live Play Mode.";
        }

        private static void Edit(UnityEngine.Object obj, Action<SerializedObject> change)
        { var so = new SerializedObject(obj); change(so); so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Tool(Fixture f, int index, string field, Action<SerializedProperty> change) =>
            Edit(f.Catalog, so => change(so.FindProperty("tools").GetArrayElementAtIndex(index).FindPropertyRelative(field)));
        private static void Placement(Fixture f, string field, Action<SerializedProperty> change) =>
            Edit(f.Stages[0], so => change(so.FindProperty("objectDirt").GetArrayElementAtIndex(0).FindPropertyRelative(field)));

        private sealed class Fixture : IDisposable
        {
            public readonly ProgressionCatalog Catalog;
            public readonly CoinDefinition[] Coins;
            public readonly StageDefinition[] Stages;
            public readonly CleaningToolDefinition[] Tools;
            public Fixture(ProgressionCatalog source)
            {
                Catalog = Clone(source); Coins = source.Coins.Select(Clone).ToArray();
                Stages = source.Stages.Select(Clone).ToArray(); Tools = source.Tools.Select(t => Clone(t.Definition)).ToArray();
                for (var i = 0; i < Stages.Length; i++)
                {
                    var index = source.Coins.ToList().IndexOf(source.Stages[i].Coin);
                    Edit(Stages[i], so => so.FindProperty("coin").objectReferenceValue = Coins[index]);
                }
                Edit(Catalog, so =>
                {
                    for (var i = 0; i < Coins.Length; i++) so.FindProperty("coins").GetArrayElementAtIndex(i).objectReferenceValue = Coins[i];
                    for (var i = 0; i < Stages.Length; i++) so.FindProperty("stages").GetArrayElementAtIndex(i).objectReferenceValue = Stages[i];
                    for (var i = 0; i < Tools.Length; i++) so.FindProperty("tools").GetArrayElementAtIndex(i).FindPropertyRelative("definition").objectReferenceValue = Tools[i];
                });
            }
            private static T Clone<T>(T source) where T : ScriptableObject
            { var copy = UnityEngine.Object.Instantiate(source); copy.hideFlags = HideFlags.HideAndDontSave; return copy; }
            public void Dispose()
            {
                foreach (var obj in Coins.Cast<ScriptableObject>().Concat(Stages).Concat(Tools).Append(Catalog))
                    UnityEngine.Object.DestroyImmediate(obj);
            }
        }
    }
}
