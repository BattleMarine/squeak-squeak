using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using SqueakSqueak.Cleaning;
using SqueakSqueak.Progression;
using SqueakSqueak.Experiment;

namespace SqueakSqueak.EditorValidation
{
    public static class CleaningExperimentTools
    {
        public const string Folder="Assets/Data/CleaningExperiment";
        public const string DefinitionPath=Folder+"/RbExperiment.asset";
        [MenuItem("Squeak Squeak/Experiment/Create Definitions And Connect")]
        private static void CreateMenu()=>Debug.Log(CreateAndConnect());
        [MenuItem("Squeak Squeak/Experiment/Start M1 (Play Only)")]
        private static void StartM1()=>Start(0);
        [MenuItem("Squeak Squeak/Experiment/Start M2 (Play Only)")]
        private static void StartM2()=>Start(1);
        private static void Start(int index)
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Play에서만 명시적으로 시작하세요.");
            var controller=UnityEngine.Object.FindFirstObjectByType<CleaningExperimentController>();
            if(controller==null||!controller.RequestCoin(index))throw new InvalidOperationException("실험 시작 불가. 참조/현재 상태를 확인하세요.");
        }

        public static string CreateAndConnect()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||EditorApplication.isCompiling||scene.path!="Assets/Scenes/CleaningRoom.unity"||scene.isDirty)
                throw new InvalidOperationException("저장된 CleaningRoom Edit/Idle 필요. 사용자 변경을 묻어 저장하지 않습니다.");
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            if(all.OfType<CleaningExperimentController>().Any())throw new InvalidOperationException("기존 실험 연결을 덮어쓰지 않습니다.");
            T One<T>()where T:MonoBehaviour=>all.OfType<T>().Single();
            var legacy=One<CleaningStageController>();var legacySo=new SerializedObject(legacy);
            var source=AssetDatabase.LoadAssetAtPath<ProgressionCatalog>(ProgressionCatalogTools.CatalogPath);
            if(source==null||ProgressionCatalogValidator.Validate(source).Count>0)throw new InvalidOperationException("기존 카탈로그 기준선 오류.");
            foreach(var file in new[]{"ReactionM1","ReactionM2","CoinM1","CoinM2","Toothbrush","Cloth","Dropper","RbExperiment"})
                if(AssetDatabase.LoadMainAssetAtPath(Folder+"/"+file+".asset")!=null)throw new InvalidOperationException("기존 실험 자산 보존: "+file);
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Data","CleaningExperiment");
            var m1=Create<CleaningReactionProfile>("ReactionM1",so=>so.FindProperty("id").stringValue="rb-material-m1");
            var m2=Create<CleaningReactionProfile>("ReactionM2",so=>
            {so.FindProperty("id").stringValue="rb-material-m2";so.FindProperty("clothUntreatedB").floatValue=0;so.FindProperty("toothbrushSoftenedB").floatValue=1;so.FindProperty("clothSoftenedB").floatValue=.25f;});
            CoinDefinition Coin(string file,string id,string label,CoinDefinition original,CleaningReactionProfile reaction)=>Create<CoinDefinition>(file,so=>
            {so.FindProperty("id").stringValue=id;so.FindProperty("displayName").stringValue=label;so.FindProperty("surfaceMesh").objectReferenceValue=original.SurfaceMesh;
             so.FindProperty("surfaceMaterial").objectReferenceValue=original.SurfaceMaterial;so.FindProperty("baseColor").colorValue=original.BaseColor;so.FindProperty("reactionProfile").objectReferenceValue=reaction;});
            var c1=Coin("CoinM1","rb-coin-m1","M1",source.Coins[0],m1);var c2=Coin("CoinM2","rb-coin-m2","M2",source.Coins[1],m2);
            CleaningToolDefinition Tool(string file,string label,CleaningToolId id,float radius,float a,float b)=>Create<CleaningToolDefinition>(file,so=>
            {so.FindProperty("toolId").intValue=(int)id;so.FindProperty("displayName").stringValue=label;so.FindProperty("brushRadius").floatValue=radius;
             so.FindProperty("objectDirtRemovalPerSecond").floatValue=a;so.FindProperty("surfaceDirtRemovalPerSecond").floatValue=b;so.FindProperty("polishGainPerSecond").floatValue=0;});
            var brush=Tool("Toothbrush","칫솔",CleaningToolId.ExperimentToothbrush,.18f,1.25f,4.8f);
            var cloth=Tool("Cloth","천",CleaningToolId.ExperimentCloth,.32f,0,4.8f);
            var drop=Tool("Dropper","스포이드",CleaningToolId.Dropper,.18f,0,0);
            var definition=Create<CleaningExperimentDefinition>("RbExperiment",so=>
            {
                var coins=so.FindProperty("coins");coins.arraySize=2;coins.GetArrayElementAtIndex(0).objectReferenceValue=c1;coins.GetArrayElementAtIndex(1).objectReferenceValue=c2;
                var tools=so.FindProperty("tools");tools.arraySize=3;tools.GetArrayElementAtIndex(0).objectReferenceValue=brush;tools.GetArrayElementAtIndex(1).objectReferenceValue=cloth;tools.GetArrayElementAtIndex(2).objectReferenceValue=drop;
                so.FindProperty("dirtMesh").objectReferenceValue=source.ObjectDirtMesh;so.FindProperty("dirtMaterial").objectReferenceValue=source.ObjectDirtMaterial;
                var positions=new[]{new Vector2(.8f,.75f),new Vector2(.5f,.12f),new Vector2(.85f,.7f),new Vector2(.3f,.12f)};
                var placements=so.FindProperty("placements");placements.arraySize=4;
                for(var i=0;i<4;i++){var p=placements.GetArrayElementAtIndex(i);p.FindPropertyRelative("face").intValue=(int)(i<2?CoinSurfaceFace.Front:CoinSurfaceFace.Back);
                    p.FindPropertyRelative("uv").vector2Value=positions[i];p.FindPropertyRelative("durability").floatValue=1;p.FindPropertyRelative("worldRadius").floatValue=.12f;}
            });
            if(definition.Validate()!=null)throw new InvalidOperationException(definition.Validate());
            var owner=Undo.AddComponent<CleaningExperimentController>(legacy.gameObject);var serialized=new SerializedObject(owner);
            void Ref(string name,UnityEngine.Object value)=>serialized.FindProperty(name).objectReferenceValue=value;
            Ref("definition",definition);Ref("legacyStage",legacy);Ref("completion",One<CleaningCompletionController>());Ref("tracker",One<CleaningProgressTracker>());
            Ref("pointer",One<CoinSurfacePointer>());Ref("cleaner",One<ObjectDirtCleaner>());Ref("surface",One<CoinSurfaceContamination>());Ref("selection",One<CleaningToolSelection>());
            Ref("audioFeedback",One<CleaningAudioFeedback>());Ref("visualFeedback",One<CleaningVisualFeedback>());
            foreach(var field in new[]{"coinRoot","coinMesh","coinCollider","coinRenderer"})Ref(field,legacySo.FindProperty(field).objectReferenceValue);
            var canvas=One<CleaningResultPanel>().GetComponent<Canvas>();Ref("canvas",canvas);Ref("uiFont",canvas.GetComponentInChildren<Text>(true).font);
            var roots=serialized.FindProperty("normalUi");var names=new[]{"ToolPanel","ProgressPanel","CleaningGuide","ControlsGuide","ResultPanel"};roots.arraySize=names.Length;
            for(var i=0;i<names.Length;i++){var child=canvas.transform.Find(names[i]);if(child==null)throw new InvalidOperationException("기존 UI 누락: "+names[i]);roots.GetArrayElementAtIndex(i).objectReferenceValue=child.gameObject;}
            serialized.ApplyModifiedProperties();EditorSceneManager.MarkSceneDirty(scene);
            return "Rb: 새 정의8개와 실험 컴포넌트 연결 완료. 참조 재조회 후 해당 씬만 저장하세요.";
        }
        private static T Create<T>(string name,Action<SerializedObject> configure)where T:ScriptableObject
        {
            var asset=ScriptableObject.CreateInstance<T>();asset.name=name;var so=new SerializedObject(asset);configure(so);so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(asset,Folder+"/"+name+".asset");Undo.RegisterCreatedObjectUndo(asset,"Create Rb Definition");AssetDatabase.SaveAssetIfDirty(asset);return asset;
        }
    }
}
