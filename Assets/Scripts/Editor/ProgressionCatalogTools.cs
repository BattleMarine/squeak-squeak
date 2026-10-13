using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using SqueakSqueak.Cleaning;
using SqueakSqueak.Progression;

namespace SqueakSqueak.EditorValidation
{
    /// <summary>새 프로토타입 정적 정의만 생성합니다. 씬/공유 머티리얼/기존 에셋은 저장하지 않습니다.</summary>
    public static class ProgressionCatalogTools
    {
        public const string Folder = "Assets/Data/Progression";
        public const string CatalogPath = Folder + "/PrototypeCatalog.asset";

        [MenuItem("Squeak Squeak/Progression/Create Prototype Definitions")]
        private static void CreateFromMenu() => Debug.Log(CreatePrototype());
        [MenuItem("Squeak Squeak/Validation/S3-01 Catalog Definitions")]
        private static void ValidateFromMenu() => Debug.Log(ProgressionCatalogValidation.Run());

        public static string CreatePrototype()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().path != "Assets/Scenes/CleaningRoom.unity")
                throw new InvalidOperationException("CleaningRoom Edit 상태에서 실행하세요. 씬은 저장하지 않습니다.");
            foreach (var name in new[] { "CoinSun", "CoinSilver", "Stage01", "Stage02", "Stage03", "PrototypeCatalog" })
                if (AssetDatabase.LoadMainAssetAtPath(Folder + "/" + name + ".asset") != null)
                    throw new InvalidOperationException("기존 정의를 덮어쓰지 않습니다: " + name);
            var body = GameObject.Find("Coin/CoinBody");
            var mesh = body.GetComponent<MeshCollider>().sharedMesh;
            var material = body.GetComponent<Renderer>().sharedMaterial;
            var dirt = UnityEngine.Object.FindFirstObjectByType<ObjectDirt>();
            if (!CoinSurfaceMapper.TryGetSupportedBounds(mesh, out _) || !EditorUtility.IsPersistent(mesh) ||
                material == null || !EditorUtility.IsPersistent(material) || dirt == null)
                throw new InvalidOperationException("검증된 원판/고정 A의 영구 에셋 참조가 필요합니다.");
            var dirtMesh = dirt.GetComponent<MeshFilter>().sharedMesh;
            var dirtMaterial = dirt.GetComponent<Renderer>().sharedMaterial;
            var dry = AssetDatabase.LoadAssetAtPath<CleaningToolDefinition>("Assets/Data/CleaningTools/DryCloth.asset");
            var brush = AssetDatabase.LoadAssetAtPath<CleaningToolDefinition>("Assets/Data/CleaningTools/SmallBrush.asset");
            var polish = AssetDatabase.LoadAssetAtPath<CleaningToolDefinition>("Assets/Data/CleaningTools/PolishingCloth.asset");
            if (dry == null || brush == null || polish == null || dirtMesh == null || dirtMaterial == null ||
                !EditorUtility.IsPersistent(dirtMesh) || !EditorUtility.IsPersistent(dirtMaterial))
                throw new InvalidOperationException("기존 도구/A Mesh/Material 참조 누락");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Data", "Progression");

            CoinDefinition Coin(string file, string id, string label, Color color) =>
                Create<CoinDefinition>(file, so =>
                {
                    so.FindProperty("id").stringValue = id; so.FindProperty("displayName").stringValue = label;
                    so.FindProperty("surfaceMesh").objectReferenceValue = mesh;
                    so.FindProperty("surfaceMaterial").objectReferenceValue = material;
                    so.FindProperty("baseColor").colorValue = color;
                });
            var sun = Coin("CoinSun", "coin-sun", "태양 동전", new Color(.82f, .62f, .23f, 1));
            var silver = Coin("CoinSilver", "coin-silver", "은빛 동전", new Color(.72f, .78f, .84f, 1));
            StageDefinition Stage(string file, string id, CoinDefinition coin, string previous, string requiredTool,
                string[] allowed, float intensity, CoinSurfaceDirtPattern pattern, float targetPolish, Vector2[] positions, float durability) =>
                Create<StageDefinition>(file, so =>
                {
                    so.FindProperty("id").stringValue = id; so.FindProperty("displayName").stringValue = "청소 " + id;
                    so.FindProperty("coin").objectReferenceValue = coin;
                    Strings(so.FindProperty("prerequisiteStageIds"), string.IsNullOrEmpty(previous) ? Array.Empty<string>() : new[] { previous });
                    Strings(so.FindProperty("requiredOwnedToolIds"), string.IsNullOrEmpty(requiredTool) ? Array.Empty<string>() : new[] { requiredTool });
                    Strings(so.FindProperty("allowedToolIds"), allowed);
                    so.FindProperty("initialSurfaceDirt").floatValue = intensity;
                    so.FindProperty("initialPolish").floatValue = 0;
                    so.FindProperty("dirtPattern").intValue = (int)pattern;
                    so.FindProperty("requiredPolish").floatValue = targetPolish;
                    var placements = so.FindProperty("objectDirt"); placements.arraySize = positions.Length;
                    for (var i = 0; i < positions.Length; i++)
                    {
                        var p = placements.GetArrayElementAtIndex(i);
                        p.FindPropertyRelative("face").intValue = (int)CoinSurfaceFace.Front;
                        p.FindPropertyRelative("uv").vector2Value = positions[i];
                        p.FindPropertyRelative("durability").floatValue = durability;
                        p.FindPropertyRelative("worldRadius").floatValue = .12f;
                    }
                });
            var stage1 = Stage("Stage01", "stage-01", sun, "", "", new[] { "dry-cloth" }, .35f, CoinSurfaceDirtPattern.FixedPatches, 0,
                new[] { new Vector2(.35f, .5f), new Vector2(.65f, .5f) }, .3f);
            var stage2 = Stage("Stage02", "stage-02", silver, "stage-01", "small-brush", new[] { "dry-cloth", "small-brush" }, .7f, CoinSurfaceDirtPattern.Uniform, 0,
                new[] { new Vector2(.3f, .4f), new Vector2(.5f, .65f), new Vector2(.7f, .4f) }, 1);
            var stage3 = Stage("Stage03", "stage-03", sun, "stage-02", "polishing-cloth", new[] { "dry-cloth", "small-brush", "polishing-cloth" }, 1, CoinSurfaceDirtPattern.FixedPatches, .7f,
                new[] { new Vector2(.3f, .3f), new Vector2(.7f, .3f), new Vector2(.5f, .5f), new Vector2(.3f, .7f), new Vector2(.7f, .7f) }, 1);
            var catalog = Create<ProgressionCatalog>("PrototypeCatalog", so =>
            {
                so.FindProperty("id").stringValue = "prototype-catalog";
                so.FindProperty("objectDirtMesh").objectReferenceValue = dirtMesh;
                so.FindProperty("objectDirtMaterial").objectReferenceValue = dirtMaterial;
                var coins = so.FindProperty("coins"); coins.arraySize = 2;
                coins.GetArrayElementAtIndex(0).objectReferenceValue = sun; coins.GetArrayElementAtIndex(1).objectReferenceValue = silver;
                var stages = so.FindProperty("stages"); stages.arraySize = 3;
                stages.GetArrayElementAtIndex(0).objectReferenceValue = stage1; stages.GetArrayElementAtIndex(1).objectReferenceValue = stage2;
                stages.GetArrayElementAtIndex(2).objectReferenceValue = stage3;
                var entries = so.FindProperty("tools"); entries.arraySize = 3;
                var definitions = new[] { dry, brush, polish }; var ids = new[] { "dry-cloth", "small-brush", "polishing-cloth" };
                var prices = new[] { 0, 60, 100 }; var unlocks = new[] { "", "stage-01", "stage-02" };
                for (var i = 0; i < 3; i++)
                {
                    var e = entries.GetArrayElementAtIndex(i); e.FindPropertyRelative("id").stringValue = ids[i];
                    e.FindPropertyRelative("definition").objectReferenceValue = definitions[i];
                    e.FindPropertyRelative("price").intValue = prices[i]; e.FindPropertyRelative("unlockedByStageId").stringValue = unlocks[i];
                }
                Strings(so.FindProperty("initiallyOwnedToolIds"), new[] { "dry-cloth" });
            });
            var errors = ProgressionCatalogValidator.Validate(catalog);
            if (errors.Count != 0) throw new InvalidOperationException(string.Join("\n", errors));
            return "S3-01b created definitions: " + CatalogPath + ", coins2/stages3/tools3. No stage execution or scene save.";
        }

        private static T Create<T>(string file, Action<SerializedObject> configure) where T : ScriptableObject
        {
            var obj = ScriptableObject.CreateInstance<T>();
            var so = new SerializedObject(obj); configure(so); so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(obj, Folder + "/" + file + ".asset");
            Undo.RegisterCreatedObjectUndo(obj, "Create progression definition");
            AssetDatabase.SaveAssetIfDirty(obj);
            return obj;
        }
        public static void Strings(SerializedProperty array, string[] values)
        {
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).stringValue = values[i];
        }
    }
}
