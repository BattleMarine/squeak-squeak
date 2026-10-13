using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SqueakSqueak.Cleaning;
using SqueakSqueak.Progression;

namespace SqueakSqueak.EditorValidation
{
    public static class CleaningStageTools
    {
        [MenuItem("Squeak Squeak/Progression/Connect Stage Runtime")]
        private static void ConnectMenu() => Debug.Log(Connect());

        /// <summary>저장된 CleaningRoom에 최소 참조만 Undo로 연결합니다. 씬 저장/재로드는 하지 않습니다.</summary>
        public static string Connect()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || EditorApplication.isCompiling || scene.path != "Assets/Scenes/CleaningRoom.unity" || scene.isDirty)
                throw new InvalidOperationException("먼저 사용자 변경 보존/저장 방침을 확인하고 저장된 CleaningRoom Edit에서 실행하세요.");
            var components = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            T One<T>() where T : MonoBehaviour
            {
                var found = components.OfType<T>().ToArray();
                if (found.Length != 1) throw new InvalidOperationException("Exactly one required: " + typeof(T).Name);
                return found[0];
            }
            if (components.OfType<CleaningStageController>().Any()) throw new InvalidOperationException("기존 단계 연결을 덮어쓰지 않습니다.");
            var completion = One<CleaningCompletionController>();
            var pointer = One<CoinSurfacePointer>();
            var cleaner = One<ObjectDirtCleaner>();
            var surface = One<CoinSurfaceContamination>();
            var progress = One<CleaningProgressTracker>();
            var selection = One<CleaningToolSelection>();
            var panel = One<CleaningResultPanel>();
            var audio = One<CleaningAudioFeedback>();
            var visual = One<CleaningVisualFeedback>();
            var pointerSo = new SerializedObject(pointer);
            var coinRoot = pointerSo.FindProperty("coinTransform").objectReferenceValue as Transform;
            var collider = pointerSo.FindProperty("coinCollider").objectReferenceValue as MeshCollider;
            var renderer = new SerializedObject(surface).FindProperty("coinRenderer").objectReferenceValue as Renderer;
            var mesh = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
            var original = new SerializedObject(cleaner).FindProperty("dirtObjects");
            var originals = Enumerable.Range(0, original.arraySize).Select(i => original.GetArrayElementAtIndex(i).objectReferenceValue as ObjectDirt).ToArray();
            var dirtRoot = originals.Length > 0 && originals[0] != null ? originals[0].transform.parent : null;
            var catalog = AssetDatabase.LoadAssetAtPath<ProgressionCatalog>(ProgressionCatalogTools.CatalogPath);
            var issues = ProgressionCatalogValidator.Validate(catalog);
            if (issues.Count != 0 || coinRoot == null || collider == null || mesh == null || renderer == null || dirtRoot == null ||
                mesh.transform != collider.transform || mesh.transform != renderer.transform ||
                !mesh.transform.IsChildOf(coinRoot) || !dirtRoot.IsChildOf(coinRoot) ||
                originals.Any(d => d == null || d.transform.parent != dirtRoot))
                throw new InvalidOperationException("원본 동전/A/카탈로그 참조 계약을 확인하세요.");
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Connect Stage Runtime");
            var owner = Undo.AddComponent<CleaningStageController>(completion.gameObject);
            var so = new SerializedObject(owner);
            void Ref(string name, UnityEngine.Object value) => so.FindProperty(name).objectReferenceValue = value;
            Ref("catalog", catalog); Ref("completion", completion); Ref("progress", progress); Ref("pointer", pointer);
            Ref("objectCleaner", cleaner); Ref("surface", surface); Ref("selection", selection); Ref("resultPanel", panel);
            Ref("coinMesh", mesh); Ref("coinCollider", collider); Ref("coinRenderer", renderer); Ref("coinRoot", coinRoot);
            Ref("originalDirtRoot", dirtRoot.gameObject); Ref("audioFeedback", audio); Ref("visualFeedback", visual);
            so.FindProperty("initialStageId").stringValue = "stage-01";
            so.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            return "S3-02 runtime connected: " + completion.gameObject.name + ", initial=stage-01. 씬 저장 전 참조를 재조회하세요.";
        }
    }
}
