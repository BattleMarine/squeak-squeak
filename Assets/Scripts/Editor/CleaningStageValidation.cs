using System;
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
    /// <summary>개발 검사만 접근 스냅샷/완료 수치를 합성합니다. 물리 입력·보상·저장·최종 UI 검사가 아닙니다.</summary>
    public static class CleaningStageValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static string resourceBaseline;
        private static string subscriptionBaseline;
        private static int verifiedRound;
        private static void Check(bool condition, string name, ref int checks)
        {
            if (!condition) throw new InvalidOperationException("s3-02 FAIL: " + name);
            checks++;
        }

        [MenuItem("Squeak Squeak/Validation/S3-02 Data And A Lifecycle")]
        private static void DataMenu() => Debug.Log(RunData());

        public static string RunData()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Edit Mode required.");
            var catalog = AssetDatabase.LoadAssetAtPath<ProgressionCatalog>(ProgressionCatalogTools.CatalogPath);
            var checks = 0;
            Check(ProgressionCatalogValidator.Validate(catalog).Count == 0, "실제 카탈로그", ref checks);
            var initial = new StageAccessContext(catalog.InitiallyOwnedToolIds, Array.Empty<string>());
            Check(initial.Validate(catalog, out _), "신규 접근", ref checks);
            Check(initial.CanEnter(catalog.Stages[0], out _), "초기 단계 진입", ref checks);
            Check(!initial.CanEnter(catalog.Stages[1], out _) && !initial.CanEnter(catalog.Stages[2], out _), "잠긴 단계 거부", ref checks);
            var tools = catalog.Tools.Select(t => t.Id).ToArray();
            var stages = catalog.Stages.Select(s => s.Id).ToArray();
            var developmentAccess = new StageAccessContext(tools, stages);
            tools[0] = "mutated"; stages[0] = "mutated";
            Check(developmentAccess.Validate(catalog, out _), "입력 배열 사본", ref checks);
            foreach (var stage in catalog.Stages) Check(developmentAccess.CanEnter(stage, out _), "개발 접근 " + stage.Id, ref checks);
            foreach (var context in new[] {
                new StageAccessContext(new[]{"unknown"},Array.Empty<string>()),
                new StageAccessContext(new[]{"dry-cloth","small-brush"},Array.Empty<string>()),
                new StageAccessContext(new[]{"dry-cloth"},new[]{"stage-02"}),
                new StageAccessContext(new[]{"dry-cloth"},new[]{"unknown"}) })
                Check(!context.Validate(catalog, out _), "잘못된 접근 거부", ref checks);
            var duplicateRejected = false;
            try { new StageAccessContext(new[] {"dry-cloth", "dry-cloth"}, Array.Empty<string>()); }
            catch (ArgumentException) { duplicateRejected = true; }
            Check(duplicateRejected, "중복 접근 ID 거부", ref checks);

            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var go = new GameObject("S3-02 A lifecycle probe");
                SceneManager.MoveGameObjectToScene(go, preview);
                var cleaner = go.AddComponent<ObjectDirtCleaner>();
                var batches = new ObjectDirt[3][];
                for (var b = 0; b < batches.Length; b++)
                {
                    batches[b] = new ObjectDirt[new[] { 2, 3, 5 }[b]];
                    for (var i = 0; i < batches[b].Length; i++)
                    {
                        var d = new GameObject("dirt"); d.transform.SetParent(go.transform, false);
                        batches[b][i] = d.AddComponent<ObjectDirt>();
                        batches[b][i].Configure(.3f, new Vector3(.24f,.084f,.24f));
                    }
                }
                var previous = Array.Empty<ObjectDirt>();
                for (var n = 0; n < 12; n++)
                {
                    var batch = batches[n % 3];
                    cleaner.ReplaceDirt(batch); cleaner.ResetAllDirt();
                    Check(cleaner.InitialDirtCount == batch.Length && cleaner.RemovedDirtCount == 0, "교체 분모/분자", ref checks);
                    foreach (var old in previous) old.ApplyCleaning(100);
                    Check(cleaner.RemovedDirtCount == 0, "옛 구독 해제", ref checks);
                    foreach (var d in batch)
                    {
                        var listeners = typeof(ObjectDirt).GetField("Removed", Private).GetValue(d) as Delegate;
                        Check(listeners?.GetInvocationList().Length == 1, "새 구독 정확히1회", ref checks);
                        d.ApplyCleaning(100); d.ApplyCleaning(100);
                    }
                    Check(cleaner.RemovedDirtCount == batch.Length && cleaner.RemovalRatio == 1, "실제 제거/중복 제거", ref checks);
                    cleaner.ResetAllDirt();
                    Check(batch.All(d => !d.IsRemoved && d.gameObject.activeSelf && Mathf.Approximately(d.CurrentDurability,.3f)), "내구도/활성 복원", ref checks);
                    previous = batch;
                }
                var duplicateDirtRejected = false;
                try { cleaner.ReplaceDirt(new[] { previous[0], previous[0] }); }
                catch (ArgumentException) { duplicateDirtRejected = true; }
                Check(duplicateDirtRejected && cleaner.InitialDirtCount == previous.Length, "잘못된 교체는 기존 집계 보존", ref checks);
                cleaner.ReplaceDirt(Array.Empty<ObjectDirt>());
                foreach (var d in previous)
                    Check(typeof(ObjectDirt).GetField("Removed", Private).GetValue(d) == null, "마지막 구독 해제", ref checks);
                var pointer = go.AddComponent<CoinSurfacePointer>();
                var progress = go.AddComponent<CleaningProgressTracker>();
                var completion = go.AddComponent<CleaningCompletionController>();
                var selection = go.AddComponent<CleaningToolSelection>();
                var panel = go.AddComponent<CleaningResultPanel>();
                var audio = go.AddComponent<CleaningAudioFeedback>();
                var visual = go.AddComponent<CleaningVisualFeedback>();
                var body = new GameObject("coin"); body.transform.SetParent(go.transform,false);
                var mesh = body.AddComponent<MeshFilter>(); mesh.sharedMesh=catalog.Coins[0].SurfaceMesh;
                var collider = body.AddComponent<MeshCollider>();collider.sharedMesh=mesh.sharedMesh;
                var renderer = body.AddComponent<MeshRenderer>();renderer.sharedMaterial=catalog.Coins[0].SurfaceMaterial;
                var surface = body.AddComponent<CoinSurfaceContamination>();
                var badOwner = go.AddComponent<CleaningStageController>();
                var ownerSo=new SerializedObject(badOwner);
                void Ref(string name,UnityEngine.Object value)=>ownerSo.FindProperty(name).objectReferenceValue=value;
                Ref("completion",completion);Ref("progress",progress);Ref("pointer",pointer);Ref("objectCleaner",cleaner);
                Ref("surface",surface);Ref("selection",selection);Ref("resultPanel",panel);Ref("coinMesh",mesh);
                Ref("coinCollider",collider);Ref("coinRenderer",renderer);Ref("coinRoot",go.transform);Ref("originalDirtRoot",body);
                Ref("audioFeedback",audio);Ref("visualFeedback",visual);ownerSo.ApplyModifiedPropertiesWithoutUndo();
                foreach(var ids in new[]{new[]{"dry-cloth","dry-cloth"},new[]{""},new[]{"unknown"},null})
                {
                    var badCatalog=UnityEngine.Object.Instantiate(catalog);
                    try
                    {
                        if(ids==null)typeof(ProgressionCatalog).GetField("initiallyOwnedToolIds",Private).SetValue(badCatalog,null);
                        else
                        {
                            var badSo=new SerializedObject(badCatalog);var owned=badSo.FindProperty("initiallyOwnedToolIds");owned.arraySize=ids.Length;
                            for(var i=0;i<ids.Length;i++)owned.GetArrayElementAtIndex(i).stringValue=ids[i];badSo.ApplyModifiedPropertiesWithoutUndo();
                        }
                        ownerSo.Update();ownerSo.FindProperty("catalog").objectReferenceValue=badCatalog;ownerSo.ApplyModifiedPropertiesWithoutUndo();
                        var ok=(bool)typeof(CleaningStageController).GetMethod("TryInitializeRuntime",Private).Invoke(badOwner,null);
                        Check(!ok && badOwner.State==CleaningStageState.Selecting && !pointer.enabled && !completion.EvaluationAllowed && !cleaner.ProcessingAllowed && !surface.ProcessingAllowed && progress.IsTimingPaused && !string.IsNullOrEmpty(badOwner.LastError),"부팅 불량 초기 ID는 예외 없이 입력 차단",ref checks);
                    }
                    finally{UnityEngine.Object.DestroyImmediate(badCatalog);}
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            return "s3-02 Edit/Preview API PASS checks=" + checks + "; 사용자 씬 Play/저장 없음";
        }

        public static string RunPlay()
        {
            if (!Application.isPlaying || !EditorApplication.isPaused || SceneManager.GetActiveScene().path != "Assets/Scenes/CleaningRoom.unity")
                throw new InvalidOperationException("초기화된 CleaningRoom Play를 Pause한 뒤 실행하세요.");
            var owner = UnityEngine.Object.FindFirstObjectByType<CleaningStageController>();
            if (owner == null || owner.State != CleaningStageState.Cleaning || owner.StageId != "stage-01")
                throw new InvalidOperationException("새 초기 stage-01 required.");
            var controller = owner.gameObject;
            var completion = controller.GetComponent<CleaningCompletionController>();
            var cleaner = controller.GetComponent<ObjectDirtCleaner>();
            var progress = controller.GetComponent<CleaningProgressTracker>();
            var pointer = controller.GetComponent<CoinSurfacePointer>();
            var selection = controller.GetComponent<CleaningToolSelection>();
            var presentation = controller.GetComponent<CleaningCompletionPresentation>();
            var surface = UnityEngine.Object.FindFirstObjectByType<CoinSurfaceContamination>();
            var catalog = AssetDatabase.LoadAssetAtPath<ProgressionCatalog>(ProgressionCatalogTools.CatalogPath);
            var checks = 0; var completedCount = 0; var restartedCount = 0;
            resourceBaseline = ResourcesSignature(); subscriptionBaseline = Subscriptions(controller, surface);
            void Completed(string _, long __) => completedCount++;
            void Restarted() => restartedCount++;
            owner.StageRoundCompleted += Completed; completion.RoundRestarted += Restarted;
            void Select()
            {
                if (owner.State == CleaningStageState.Cleaning)
                {
                    Check(owner.RequestAbandon(owner.RoundId), "포기 확인", ref checks);
                    Check(owner.ConfirmAbandon(owner.RoundId), "포기 확정", ref checks);
                }
            }
            void Complete()
            {
                foreach (var d in controller.scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<ObjectDirt>(true)).Where(d=>d.transform.parent.name=="StageDirt").ToArray())
                    d.ApplyCleaning(100);
                // 물리 입력을 흉내내지 않습니다. 완료 경계 수치만 실제 소유 데이터에 합성합니다.
                foreach (var face in new[] { CoinSurfaceFace.Front, CoinSurfaceFace.Back })
                for (var i = 0; i < surface.SurfaceMask.TexelCount; i++)
                { surface.SurfaceMask.TrySetDirt(face, i, 0); surface.SurfaceMask.TrySetPolish(face, i, 1); }
                surface.UploadSurfaceMasks();
                Check(completion.TryCompleteRound(owner.StageId, owner.RoundId), "실제 완료 소유자 전환", ref checks);
                Check(owner.State == CleaningStageState.Result && presentation.Phase == CompletionPresentationPhase.Showing && progress.IsTimingStopped, "결과/감상/시간 고정", ref checks);
                Check(!completion.TryCompleteRound(owner.StageId,owner.RoundId), "중복 완료 거부", ref checks);
            }
            try
            {
                Check(selection.AvailableToolCount == 1 && !selection.TrySelect(CleaningToolId.SmallBrush) && !selection.TrySelect(CleaningToolId.PolishingCloth), "초기 소유∩허용", ref checks);
                Check(!owner.RestartCurrentRound() && !completion.RestartRound(), "미완료 재도전 거부", ref checks);
                var initialRound = owner.RoundId;
                Check(owner.RequestAbandon(initialRound), "초기 포기 확인", ref checks);
                Check(!pointer.enabled && progress.IsTimingPaused && !cleaner.ProcessingAllowed && !surface.ProcessingAllowed && !completion.EvaluationAllowed, "모달 입력/시간/완료 차단", ref checks);
                var initialSum = surface.SurfaceMask.CurrentDirtSum;
                Check(owner.CancelAbandon(initialRound) && surface.SurfaceMask.CurrentDirtSum == initialSum && owner.RoundId == initialRound, "취소 판/오염 보존", ref checks);
                Select();
                Check(!owner.TryStartStage("stage-02",owner.RoundId) && owner.State == CleaningStageState.Selecting, "소유/전제 없는 다음 단계 거부", ref checks);
                Check(!owner.TryStartStage("unknown",owner.RoundId), "미정의 단계 거부", ref checks);
                // Editor-only 개발 검사의 명시적인 완성 진행 스냅샷. 출시 초기 접근은 위에서 이미 검사했습니다.
                Check(owner.TrySetAccessContext(new StageAccessContext(catalog.Tools.Select(t=>t.Id),catalog.Stages.Select(s=>s.Id))), "개발 접근 주입", ref checks);
                foreach (var index in new[] { 0, 1, 2, 1, 0, 2 })
                {
                    if (owner.State == CleaningStageState.Cleaning) Select();
                    var previousRound = owner.RoundId;
                    var stage = catalog.Stages[index];
                    Check(owner.TryStartStage(stage.Id, previousRound), "단계 적용 " + stage.Id, ref checks);
                    Check(owner.RoundId > previousRound && owner.StageId == stage.Id && owner.State == CleaningStageState.Cleaning, "단계/판 ID 증가", ref checks);
                    Check(cleaner.InitialDirtCount == stage.ObjectDirt.Count && cleaner.RemovedDirtCount == 0 && owner.ActiveDirtCount == stage.ObjectDirt.Count, "단계별 A2/3/5", ref checks);
                    Check(controller.scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<ObjectDirt>(true)).Where(d=>d.gameObject.activeInHierarchy&&d.transform.parent.name=="StageDirt").All(d=>d.GetComponent<Renderer>().shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off),"모든 단계 신규 먼지 그림자 Off",ref checks);
                    Check(surface.SurfaceMask.Pattern == stage.DirtPattern && Mathf.Approximately(surface.SurfaceMask.InitialIntensity, stage.InitialSurfaceDirt) && surface.SurfaceMask.CurrentDirtSum == surface.SurfaceMask.InitialDirtSum && surface.Polish == stage.InitialPolish, "양면 패턴/광택 초기화", ref checks);
                    Check(completion.RequiredObjectDirtRemovalRatio == stage.RequiredA && completion.RequiredSurfaceDirtRemovalRatio == stage.RequiredB && completion.RequiredPolish == stage.RequiredPolish, "실제 목표", ref checks);
                    Check(selection.AvailableToolCount == stage.AllowedToolIds.Count && selection.CurrentTool.ToolId == CleaningToolId.DryCloth, "허용 목록/기본 도구", ref checks);
                    Check(!progress.HasStarted && progress.ElapsedSeconds == 0 && !progress.IsTimingPaused && !progress.IsTimingStopped && !pointer.HasSurfaceHit, "시간/접촉 초기화", ref checks);
                    Check(presentation.Phase == CompletionPresentationPhase.Idle && !completion.IsCompleted && pointer.enabled, "연출/완료/입력 복원", ref checks);
                    var renderer = surface.GetComponent<Renderer>();
                    var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                    Check(renderer.sharedMaterial == stage.Coin.SurfaceMaterial && block.GetColor("_BaseColor") == stage.Coin.BaseColor && surface.GetComponent<MeshFilter>().sharedMesh == stage.Coin.SurfaceMesh && surface.GetComponent<MeshCollider>().sharedMesh == stage.Coin.SurfaceMesh, "외형 tint/mesh/material 계약", ref checks);
                    Check(!completion.TryCompleteRound(stage.Id,previousRound) && !owner.RequestAbandon(previousRound), "옛 판 명령 거부", ref checks);
                    typeof(CleaningStageController).GetMethod("OnRoundCompleted",Private).Invoke(owner,new object[]{stage.Id,previousRound});
                    Check(owner.State == CleaningStageState.Cleaning && !completion.IsCompleted, "늦은 완료 알림 무시", ref checks);
                    var revision = surface.SurfaceMask.Revision;
                    pointer.ResetViewToInitialState();
                    Check(surface.SurfaceMask.Revision == revision && owner.RoundId == completion.RoundId, "F 뷰 복원 진행 보존", ref checks);
                    if (index == 1)
                    {
                        var activeDirt = controller.scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<ObjectDirt>(true)).First(d=>d.transform.parent.name=="StageDirt" && d.gameObject.activeInHierarchy);
                        var collider = surface.GetComponent<MeshCollider>();
                        Physics.SyncTransforms();
                        Check(collider.Raycast(new Ray(activeDirt.transform.position+Vector3.up*5,Vector3.down),out var hit,10) && cleaner.ApplyCleaningAtSurfaceHit(hit,.01f)>0 && progress.HasStarted, "실제 A 적중/API 타이머 시작",ref checks);
                        var clock = typeof(CleaningProgressTracker).GetMethod("AdvanceTiming",Private);
                        var previous = typeof(CleaningProgressTracker).GetField("previousTimingTimestamp",Private);
                        previous.SetValue(progress,Time.realtimeSinceStartupAsDouble-2);
                        typeof(CleaningProgressTracker).GetField("wasTimingFocused",Private).SetValue(progress,true);
                        clock.Invoke(progress,new object[]{Time.realtimeSinceStartupAsDouble,true});
                        Check(progress.ElapsedSeconds>=2,"진행 중 타이머",ref checks);
                        Check(owner.RequestAbandon(owner.RoundId),"진행 중 확인",ref checks);
                        var elapsed = progress.ElapsedSeconds; var sum = surface.SurfaceMask.CurrentDirtSum;
                        var count = cleaner.RemovedDirtCount; var durability = activeDirt.CurrentDurability;
                        previous.SetValue(progress,Time.realtimeSinceStartupAsDouble-30);
                        clock.Invoke(progress,new object[]{Time.realtimeSinceStartupAsDouble,true});
                        Check(progress.ElapsedSeconds==elapsed && !completion.TryCompleteIfEligible() && cleaner.ApplyCleaningAtSurfaceHit(hit,10)==0,"모달 시간/완료/직접 A 차단",ref checks);
                        Check(owner.CancelAbandon(owner.RoundId) && progress.ElapsedSeconds==elapsed && surface.SurfaceMask.CurrentDirtSum==sum && cleaner.RemovedDirtCount==count && activeDirt.CurrentDurability==durability,"취소 후 시간/A/B 보존",ref checks);
                        owner.enabled=false;
                        Check(!pointer.enabled && !completion.EvaluationAllowed && progress.IsTimingPaused,"단계 소유자 비활성 차단",ref checks);
                        owner.enabled=true;
                        Check(pointer.enabled && !progress.IsTimingPaused,"재활성 기존 판 재개",ref checks);
                    }
                    Complete();
                    var resultRound = owner.RoundId;
                    Check(!owner.TryStartStage("unknown",resultRound) && completion.IsCompleted && owner.State == CleaningStageState.Result, "불량 전환은 기존 결과 보존", ref checks);
                    if (index == 0)
                    {
                        Check(completion.RestartRound() && owner.RoundId > resultRound && owner.State == CleaningStageState.Cleaning, "완료 버튼 경로 재도전", ref checks);
                        Check(presentation.Phase == CompletionPresentationPhase.Idle && !completion.IsCompleted && cleaner.RemovedDirtCount == 0, "감상 중 재도전 취소/복원", ref checks);
                        Select();
                    }
                }
                Check(completedCount == 6 && restartedCount == 2, "완료/재도전 알림 횟수", ref checks);
                var failedRound = owner.RoundId;
                void PreparationFault() => throw new InvalidOperationException("expected preparation fault");
                completion.RoundPreparing += PreparationFault;
                try { Check(!owner.TryStartStage("stage-01",failedRound),"적용 중 오류 반환",ref checks); }
                finally { completion.RoundPreparing -= PreparationFault; }
                Check(owner.State==CleaningStageState.Selecting && owner.CurrentStage==null && !pointer.enabled && !completion.EvaluationAllowed && !surface.ProcessingAllowed && !cleaner.ProcessingAllowed && cleaner.InitialDirtCount==0 && !surface.GetComponent<Renderer>().enabled && owner.RoundId>failedRound,"부분 적용 실패는 비활성 선택 상태/ID 소비",ref checks);
                // 원래 출시 초기 접근/단계로 검사 후 복원합니다. 보상·구매 상태를 저장하지 않습니다.
                Check(owner.TrySetAccessContext(new StageAccessContext(catalog.InitiallyOwnedToolIds,Array.Empty<string>())), "초기 접근 복원", ref checks);
                Check(owner.TryStartStage("stage-01",owner.RoundId), "초기 단계 복원", ref checks);
                verifiedRound = (int)owner.RoundId;
            }
            finally { owner.StageRoundCompleted -= Completed; completion.RoundRestarted -= Restarted; }
            Check(Subscriptions(controller,surface)==subscriptionBaseline, "기존 컴포넌트 구독 안정", ref checks);
            return "s3-02 paused Play API PASS checks="+checks+"; 단계6회 왕복/완료6/감상 중 재도전2. Destroy flush 후 VerifyResources 실행 필요";
        }

        public static string VerifyResources()
        {
            var owner = UnityEngine.Object.FindFirstObjectByType<CleaningStageController>();
            if (!Application.isPlaying || !EditorApplication.isPaused || owner == null || owner.RoundId != verifiedRound || resourceBaseline == null)
                throw new InvalidOperationException("RunPlay 후 실제 프레임/Destroy flush가 필요합니다.");
            if (ResourcesSignature() != resourceBaseline) throw new InvalidOperationException("Resource mismatch: "+resourceBaseline+" -> "+ResourcesSignature());
            return "s3-02 frame-flushed resources PASS: " + resourceBaseline;
        }

        private static string ResourcesSignature()
        {
            var scene = SceneManager.GetActiveScene();
            var objects = scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<Transform>(true)).ToArray();
            var textures = Resources.FindObjectsOfTypeAll<Texture2D>().Count(t=>t.name.StartsWith("SqueakSqueak.BMask."));
            return "objects="+objects.Length+";dirt="+objects.Sum(t=>t.GetComponents<ObjectDirt>().Length)+";roots="+objects.Count(t=>t.name=="StageDirt")+
                ";masks="+textures+";audio="+objects.Sum(t=>t.GetComponents<AudioSource>().Length)+";lines="+objects.Sum(t=>t.GetComponents<LineRenderer>().Length)+";particles="+objects.Sum(t=>t.GetComponents<ParticleSystem>().Length);
        }
        private static string Subscriptions(GameObject controller, CoinSurfaceContamination surface)
        {
            return string.Join(";",controller.GetComponents<MonoBehaviour>().Concat(new[]{surface}).SelectMany(c=>c.GetType().GetFields(Private).Where(f=>typeof(Delegate).IsAssignableFrom(f.FieldType)).Select(f=>c.GetType().Name+"."+f.Name+"="+((f.GetValue(c) as Delegate)?.GetInvocationList().Length??0))).OrderBy(s=>s));
        }
    }
}
