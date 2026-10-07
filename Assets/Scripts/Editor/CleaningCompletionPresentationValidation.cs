using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEditor;
using SqueakSqueak.Cleaning;

namespace SqueakSqueak.EditorValidation
{
    /// <summary>실제 완료/재시작 이벤트와 표현·결과 불변·복원을 검사합니다. 진행/time/dt는 합성입니다.</summary>
    public static class CleaningCompletionPresentationValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static GameObject destructionProbe;
        private static CleaningCompletionPresentation originalPresentation, live;
        private static CleaningCompletionController probeCompletion;
        private static int subscribers, liveFrame;
        private static double liveStart;
        private static bool liveMovingCaptured, liveWarmingUp;
        private static double liveSum, livePolish;
        public static string LiveResult { get; private set; } = "Not run";

        [MenuItem("Squeak Squeak/Validation/S2-05b Completion Presentation (Paused Play Mode)")]
        private static void Menu() => Debug.Log(Run());

        public static string Run()
        {
            RequirePaused();
            var c = GameObject.Find("CleaningController"); var p = c.GetComponent<CleaningCompletionPresentation>();
            var completion = c.GetComponent<CleaningCompletionController>(); var tracker = c.GetComponent<CleaningProgressTracker>();
            var cleaner = c.GetComponent<ObjectDirtCleaner>(); var selection = c.GetComponent<CleaningToolSelection>();
            var pointer = c.GetComponent<CoinSurfacePointer>(); var owner = GameObject.Find("Coin/CoinBody").GetComponent<CoinSurfaceContamination>();
            var coin = Get<Transform>(p, "coin"); var camera = Get<Camera>(p, "gameplayCamera");
            var tray = Get<GameObject>(p, "storageTray"); var panel = UnityEngine.Object.FindFirstObjectByType<CleaningResultPanel>();
            if (completion.IsCompleted || tracker.HasStarted || p.Phase != CompletionPresentationPhase.Idle || owner.SurfaceMask == null)
                throw new InvalidOperationException("청소 전 초기화가 끝난 새 Play에서 검사하세요.");
            var parent = coin.parent; var pos = coin.localPosition; var rot = coin.localRotation; var scale = coin.localScale; var zoom = camera.orthographicSize;
            var initialSum = owner.SurfaceMask.InitialDirtSum; var textures = new[] { owner.FrontDirtMask, owner.BackDirtMask }; var tool = selection.CurrentTool;
            var sourceIds = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Select(x => x.GetInstanceID()).OrderBy(x => x).ToArray();
            var baseline = SubscriberCount(completion); var checks = 0; var completed = 0; var restarting = 0;
            void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException("S2-05b FAIL: " + name); checks++; }
            void Completed() => completed++; void Restarting() => restarting++;
            completion.RoundCompleted += Completed; completion.RoundRestarting += Restarting;
            void Restored()
            {
                Check(p.Phase == CompletionPresentationPhase.Idle && !tray.activeSelf && coin.parent == parent && coin.localPosition == pos &&
                    coin.localRotation == rot && coin.localScale == scale && camera.orthographicSize == zoom, "parent/local pose/scale/zoom 복원");
                Check(!completion.IsCompleted && pointer.enabled && !tracker.HasStarted && !tracker.IsTimingStopped && tracker.ElapsedSeconds == 0 &&
                    cleaner.RemovedDirtCount == 0 && selection.CurrentTool == tool && owner.SurfaceMask.CurrentDirtSum == initialSum && owner.Polish == 0 &&
                    owner.FrontDirtMask == textures[0] && owner.BackDirtMask == textures[1] && Get<GameObject>(p, "toolPanel").activeSelf && Get<GameObject>(p, "controlsGuide").activeSelf &&
                    !Get<GameObject>(panel, "resultRoot").activeSelf, "청소/UI/입력/타이머/자원 복원");
                Check(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).All(x => !x.isPlaying && x.volume == 0) &&
                    !c.GetComponent<CleaningVisualFeedback>().IsContactVisible && c.GetComponent<CleaningVisualFeedback>().ActiveRemovalParticleCount == 0, "소리/효과 즉시 취소");
                Advance(p, 10); Check(p.Phase == CompletionPresentationPhase.Idle && coin.localPosition == pos, "취소 뒤 지연 동작 없음");
            }
            void Restart() { var button = Get<GameObject>(panel, "resultRoot").GetComponentsInChildren<Button>().Single(); Click(button); Click(button); Restored(); }
            try
            {
                Check(tray.GetComponentsInChildren<Collider>(true).Length == 0 && !tray.activeSelf, "트레이 Collider/초기 표시 없음");
                // 비활성화가 미완료 플레이의 사용자 자세를 F처럼 덮지 않아야 합니다.
                coin.rotation = Quaternion.Euler(25, 30, 10); var userRot = coin.rotation; p.enabled = false;
                Check(coin.rotation == userRot, "Idle disable은 플레이 자세 보존"); p.enabled = true; pointer.ResetViewToInitialState();
                foreach (var phase in new[] { CompletionPresentationPhase.Showing, CompletionPresentationPhase.Moving, CompletionPresentationPhase.Stored })
                for (var repeat = 0; repeat < 2; repeat++)
                {
                    Complete();
                    Check(p.Phase == CompletionPresentationPhase.Showing && tray.activeSelf && !pointer.enabled && tracker.IsTimingStopped &&
                        !Get<GameObject>(p, "toolPanel").activeSelf && !Get<GameObject>(p, "controlsGuide").activeSelf && coin.parent == parent, "실제 완료 알림/감상/입력 차단");
                    var sum = owner.SurfaceMask.CurrentDirtSum; var polish = owner.SurfaceMask.CurrentPolishSum; var revision = owner.SurfaceMask.Revision;
                    var resultTexts = Get<GameObject>(panel, "resultRoot").GetComponentsInChildren<Text>().Where(x => x.name != "StorageStatusText").Select(x => x.text).ToArray();
                    var show = Get<float>(p, "showSeconds"); var move = Get<float>(p, "moveSeconds");
                    if (phase == CompletionPresentationPhase.Moving) Advance(p, show + move * .65f);
                    if (phase == CompletionPresentationPhase.Stored) Advance(p, show + move + .1f);
                    Check(p.Phase == phase, "단계 " + phase);
                    var pose = coin.position; Call(p, "OnCompleted");
                    Check(!completion.TryCompleteIfEligible() && coin.position == pose && p.Phase == phase, "중복 완료/표현 호출 멱등");
                    Check(owner.SurfaceMask.CurrentDirtSum == sum && owner.SurfaceMask.CurrentPolishSum == polish && owner.SurfaceMask.Revision == revision &&
                        tracker.ElapsedSeconds == 12.75f && Get<GameObject>(panel, "resultRoot").GetComponentsInChildren<Text>().Where(x => x.name != "StorageStatusText").Select(x => x.text).SequenceEqual(resultTexts) &&
                        owner.Polish < 1f && owner.SurfaceDirtRemovalRatio < 1f, "완료 마스크/실제 결과값 고정,100% 덮기 없음");
                    var button = Get<GameObject>(panel, "resultRoot").GetComponentsInChildren<Button>().Single();
                    Canvas.ForceUpdateCanvases();
                    Check(button.IsInteractable() && button.gameObject.activeInHierarchy, "각 단계 재시작 활성");
                    var card = button.transform.parent as RectTransform;
                    var coinScreen = camera.WorldToScreenPoint(coin.position);
                    var cardCorners = new Vector3[4]; card.GetWorldCorners(cardCorners);
                    var radius = 360f / camera.orthographicSize * coin.localScale.x;
                    Check(coinScreen.x + radius < cardCorners[0].x || coinScreen.y + radius < cardCorners[0].y, "동전/결과 카드 비겹침");
                    Restart();
                }
                foreach (var phase in new[] { CompletionPresentationPhase.Showing, CompletionPresentationPhase.Moving, CompletionPresentationPhase.Stored })
                {
                    Complete(); if (phase != CompletionPresentationPhase.Showing) Advance(p, Get<float>(p, "showSeconds") + Get<float>(p, "moveSeconds") * (phase == CompletionPresentationPhase.Moving ? .6f : 1.1f));
                    p.enabled = false;
                    Check(!tray.activeSelf && coin.localScale == scale && coin.localPosition == pos && camera.orthographicSize == zoom &&
                        SubscriberCount(completion) == baseline, "Disable 복원/구독 해제");
                    p.enabled = true;
                    Check(p.Phase == CompletionPresentationPhase.Stored && SubscriberCount(completion) == baseline + 1 &&
                        owner.Polish < 1f && tracker.ElapsedSeconds == 12.75f, "완료 재활성: 보관만/수치·시간 불변");
                    Restart();
                }
                Check(completed == 9 && restarting == 9 && SubscriberCount(completion) == baseline + 1, "판당 이벤트1회/반복 구독 없음");
                Check(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Select(x => x.GetInstanceID()).OrderBy(x => x).SequenceEqual(sourceIds) &&
                    UnityEngine.Object.FindObjectsByType<CoinSurfaceContamination>(FindObjectsSortMode.None).Length == 1, "Source3/실제 동전1개 재사용");
                return "S2-05b PRESENTATION PASS checks=" + checks + ", completed=" + completed + ", restarted=" + restarting + "; actual callbacks/scene/restore, synthetic progress/time/dt.";
            }
            finally
            {
                p.enabled = true; if (completion.IsCompleted) completion.RestartRound();
                completion.RoundCompleted -= Completed; completion.RoundRestarting -= Restarting;
                owner.ResetSurface(); cleaner.ResetAllDirt(); tracker.ResetProgress(); selection.TrySelect(tool); pointer.enabled = true; pointer.ResetViewToInitialState();
            }
        }

        public static void Complete()
        {
            var c = GameObject.Find("CleaningController"); var completion = c.GetComponent<CleaningCompletionController>();
            var owner = GameObject.Find("Coin/CoinBody").GetComponent<CoinSurfaceContamination>();
            owner.ResetSurface();
            foreach (var face in new[] { CoinSurfaceFace.Front, CoinSurfaceFace.Back })
                for (var i = 0; i < owner.SurfaceMask.TexelCount; i++)
                { owner.SurfaceMask.TrySetDirt(face, i, owner.SurfaceMask.GetDirt(face, i) * .19f); owner.SurfaceMask.TrySetPolish(face, i, .72f); }
            owner.UploadSurfaceMasks(); Call(owner, "ApplyVisuals");
            foreach (var dirt in Get<ObjectDirt[]>(c.GetComponent<ObjectDirtCleaner>(), "dirtObjects")) dirt.ApplyCleaning(dirt.CurrentDurability);
            Set(c.GetComponent<CleaningProgressTracker>(), "<ElapsedSeconds>k__BackingField", 12.75f);
            if (!completion.TryCompleteIfEligible()) throw new InvalidOperationException("Synthetic completion not eligible");
        }

        // 활성화 직후 Graphic.depth=-1이므로 최소 한 번 실제 렌더된 뒤 별도 호출합니다.
        public static string VerifyRenderedUi()
        {
            RequirePaused();
            var button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(x => x.name == "RestartButton");
            var hits = Raycast(Center(button.transform as RectTransform));
            var modalHits = Raycast(new Vector2(640, 360));
            var panel = UnityEngine.Object.FindFirstObjectByType<CleaningResultPanel>();
            if (hits.Count == 0 || hits[0].gameObject != button.gameObject || modalHits.Count == 0 || modalHits[0].gameObject != Get<GameObject>(panel, "resultRoot"))
                throw new InvalidOperationException("Rendered restart/modal Raycast failed");
            return "S2-05b RENDERED UI PASS restart first hit, transparent modal blocks coin, phase=" +
                GameObject.Find("CleaningController").GetComponent<CleaningCompletionPresentation>().Phase;
        }

        public static string BeginRealTime()
        {
            RequirePaused(); if (live != null) throw new InvalidOperationException("Live probe already running");
            live = GameObject.Find("CleaningController").GetComponent<CleaningCompletionPresentation>();
            if (live.Phase != CompletionPresentationPhase.Idle || live.GetComponent<CleaningCompletionController>().IsCompleted)
            { live = null; throw new InvalidOperationException("Restart before live probe"); }
            liveFrame = Time.frameCount; liveStart = EditorApplication.timeSinceStartup;
            liveMovingCaptured = false; liveWarmingUp = true; LiveResult = "Warming up";
            EditorApplication.update += WatchRealTime; EditorApplication.isPaused = false;
            return "Actual frame warm-up started; completion input/progress is synthetic.";
        }
        private static void WatchRealTime()
        {
            if (!Application.isPlaying || live == null) { FinishLive("Interrupted"); return; }
            // Editor Pause 뒤 첫 unscaled dt에 중단 시간이 포함될 수 있어 정상 프레임2개 뒤 시작합니다.
            if (liveWarmingUp)
            {
                if (Time.frameCount - liveFrame < 2)
                { if (EditorApplication.timeSinceStartup - liveStart >= 10) FinishLive("LIVE FAIL: no warm-up frames"); return; }
                Complete(); var surface = GameObject.Find("Coin/CoinBody").GetComponent<CoinSurfaceContamination>();
                liveSum = surface.SurfaceMask.CurrentDirtSum; livePolish = surface.SurfaceMask.CurrentPolishSum;
                liveFrame = Time.frameCount; liveStart = EditorApplication.timeSinceStartup; liveWarmingUp = false; LiveResult = "Running";
                ScreenCapture.CaptureScreenshot(".utmp/s205b-live-show.png"); return;
            }
            if (live.Phase == CompletionPresentationPhase.Moving && Get<float>(live, "elapsed") / Get<float>(live, "moveSeconds") > .65f && !liveMovingCaptured)
            { ScreenCapture.CaptureScreenshot(".utmp/s205b-live-moving.png"); liveMovingCaptured = true; }
            if (live.Phase != CompletionPresentationPhase.Stored && EditorApplication.timeSinceStartup - liveStart < 10) return;
            var owner = GameObject.Find("Coin/CoinBody").GetComponent<CoinSurfaceContamination>();
            ScreenCapture.CaptureScreenshot(".utmp/s205b-live-stored.png");
            var unchanged = owner.SurfaceMask.CurrentDirtSum == liveSum && owner.SurfaceMask.CurrentPolishSum == livePolish;
            var passed = live.Phase == CompletionPresentationPhase.Stored && Time.frameCount - liveFrame > 2 && liveMovingCaptured && unchanged;
            FinishLive("S2-05b LIVE " + (passed ? "PASS" : "FAIL") + " phase=" + live.Phase + ", frames=" + (Time.frameCount - liveFrame) + ", wallSeconds=" +
                (EditorApplication.timeSinceStartup - liveStart).ToString("F3") + ", movingCapture=" + liveMovingCaptured + ", dataUnchanged=" +
                unchanged + "; actual frames, synthetic completion.");
        }
        private static void FinishLive(string result)
        {
            EditorApplication.update -= WatchRealTime; LiveResult = result;
            // 저장 화면을 한 프레임 렌더한 뒤 별도 조회에서 Pause합니다.
            live = null; liveWarmingUp = false; Debug.Log(result);
        }
        public static string BeginDestruction()
        {
            RequirePaused(); if (destructionProbe != null) throw new InvalidOperationException("Probe already running");
            originalPresentation = GameObject.Find("CleaningController").GetComponent<CleaningCompletionPresentation>();
            originalPresentation.enabled = false; probeCompletion = originalPresentation.GetComponent<CleaningCompletionController>();
            subscribers = SubscriberCount(probeCompletion);
            destructionProbe = new GameObject("S205bLifecycleProbe"); destructionProbe.SetActive(false);
            var clone = destructionProbe.AddComponent<CleaningCompletionPresentation>();
            var source = new SerializedObject(originalPresentation); var target = new SerializedObject(clone);
            foreach (var name in new[] { "completion", "coin", "gameplayCamera", "storageTray", "storageStatusText", "toolPanel", "controlsGuide", "audioFeedback", "visualFeedback" })
                target.FindProperty(name).objectReferenceValue = source.FindProperty(name).objectReferenceValue;
            target.ApplyModifiedPropertiesWithoutUndo(); destructionProbe.SetActive(true); Complete();
            if (SubscriberCount(probeCompletion) != subscribers + 1) throw new InvalidOperationException("Clone subscription missing");
            UnityEngine.Object.Destroy(destructionProbe); return "Actual destroy pending; separate Step2 then VerifyDestruction.";
        }
        public static string VerifyDestruction()
        {
            RequirePaused();
            if (destructionProbe != null || SubscriberCount(probeCompletion) != subscribers) throw new InvalidOperationException("Destroy/subscription not cleared");
            originalPresentation.enabled = true; probeCompletion.RestartRound(); originalPresentation = null; probeCompletion = null;
            return "S2-05b DESTRUCTION PASS original coin restored, one clone destroyed, callbacks removed.";
        }
        private static void RequirePaused() { if (!Application.isPlaying || !EditorApplication.isPaused || SceneManager.GetActiveScene().path != "Assets/Scenes/CleaningRoom.unity") throw new InvalidOperationException("새 CleaningRoom Play 초기화 완료 후 Pause하세요."); }
        private static void Advance(CleaningCompletionPresentation p, float dt) => typeof(CleaningCompletionPresentation).GetMethod("AdvancePresentation", Private).Invoke(p, new object[] { dt });
        private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        private static void Call(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
        private static int SubscriberCount(CleaningCompletionController c) => ((Delegate)typeof(CleaningCompletionController).GetField("RoundCompleted", Private).GetValue(c))?.GetInvocationList().Length ?? 0;
        private static void Click(Button b) => ExecuteEvents.Execute(b.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        private static Vector2 Center(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        private static List<RaycastResult> Raycast(Vector2 point) { var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits); return hits; }
    }
}
