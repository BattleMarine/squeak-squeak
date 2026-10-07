using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using SqueakSqueak.Cleaning;

namespace SqueakSqueak.EditorValidation
{
    /// <summary>uGUI/API 검사와 Overlay 캡처 준비. 물리 입력/초보 체험은 별도로 검증합니다.</summary>
    public static class CleaningHudValidation
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string Key = "SqueakSqueak.S205a.Capture";
        private const string Title = "S2-05a temporary capture";

        // Play/Stop의 domain reload 뒤에도 원래 선택과 프리셋 소유권을 보존합니다.
        public static string ConfigureCapture1280()
        {
            var view = GameView(); var group = SizeGroup(view); var total = SizeCount(group);
            if (SessionState.GetInt(Key + "OriginalSize", -1) < 0)
                SessionState.SetInt(Key + "OriginalSize", (int)Property(view, "selectedSizeIndex"));
            var chosen = -1;
            for (var i = 0; i < total; i++) if (IsFixed1280(SizeAt(group, i))) { chosen = i; break; }
            if (chosen < 0)
            {
                var type = view.GetType().Assembly.GetType("UnityEditor.GameViewSize");
                var enumType = type.GetProperty("sizeType", Instance).PropertyType;
                var size = Activator.CreateInstance(type, Instance, null,
                    new[] { Enum.Parse(enumType, "FixedResolution"), (object)1280, 720, Title }, null);
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                chosen = total; SessionState.SetBool(Key + "OwnPreset", true);
            }
            view.GetType().GetProperty("selectedSizeIndex", Instance).SetValue(view, chosen);
            view.Focus(); view.Repaint();
            return "Native1280x720 selected; original=" + SessionState.GetInt(Key + "OriginalSize", -1);
        }

        public static string RestoreCaptureSize()
        {
            var original = SessionState.GetInt(Key + "OriginalSize", -1);
            if (original < 0) return "Capture size not configured; " + CaptureSizeStatus();
            var view = GameView(); var group = SizeGroup(view); var owned = -1;
            if (SessionState.GetBool(Key + "OwnPreset", false))
            {
                for (var i = 0; i < SizeCount(group); i++)
                    if (IsFixed1280(SizeAt(group, i)) && (string)Property(SizeAt(group, i), "baseText") == Title)
                    {
                        if (owned >= 0) throw new InvalidOperationException("Ambiguous presets; preserve them");
                        owned = i;
                    }
                if (owned < 0 || !(bool)group.GetType().GetMethod("IsCustomSize").Invoke(group, new object[] { owned }))
                    throw new InvalidOperationException("Own preset changed; preserve user settings");
            }
            if (original >= SizeCount(group) || original == owned) throw new InvalidOperationException("Original preset changed");
            view.GetType().GetProperty("selectedSizeIndex", Instance).SetValue(view, original);
            var count = SizeCount(group);
            if (owned >= 0)
            {
                group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { owned });
                if (SizeCount(group) != count - 1) throw new InvalidOperationException("Temporary preset removal not confirmed");
            }
            view.Repaint(); SessionState.EraseInt(Key + "OriginalSize"); SessionState.EraseBool(Key + "OwnPreset");
            return "Original selection restored; only own temporary preset removed; window rect unchanged. " + CaptureSizeStatus();
        }

        [MenuItem("Squeak Squeak/Validation/S2-05a HUD (Paused Play Mode)")]
        private static void FromMenu() => Debug.Log(Run());

        public static string Run()
        {
            RequirePaused();
            var c = GameObject.Find("CleaningController");
            var completion = c.GetComponent<CleaningCompletionController>(); var tracker = c.GetComponent<CleaningProgressTracker>();
            var selection = c.GetComponent<CleaningToolSelection>(); var pointer = c.GetComponent<CoinSurfacePointer>();
            var cleaner = c.GetComponent<ObjectDirtCleaner>(); var owner = GameObject.Find("Coin/CoinBody").GetComponent<CoinSurfaceContamination>();
            var hud = UnityEngine.Object.FindFirstObjectByType<CleaningProgressHud>();
            var canvas = hud.GetComponentInParent<Canvas>();
            if (completion.IsCompleted || tracker.HasStarted) throw new InvalidOperationException("새 Play에서 검사하세요");
            var tool = selection.CurrentTool; var targetB = completion.RequiredSurfaceDirtRemovalRatio; var checks = 0;
            void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException("S2-05a FAIL: " + name); checks++; }
            void Refresh() => Call(hud, "Refresh");
            Text Label(string name) => Get<Text>(hud, name);
            try
            {
                Refresh(); Canvas.ForceUpdateCanvases();
                Check(Screen.width == 1280 && Screen.height == 720, "native1280x720");
                Check(Label("objectDirtText").text == "큰 먼지 제거: 0% / 목표 90%" && Label("surfaceDirtText").text == "얼룩 제거: 0% / 목표 80%" &&
                    Label("polishText").text == "광택: 0% / 목표 70%" && Label("timerText").text == "청소 시간: 00:00", "초기/목표/단위");
                var sum = owner.SurfaceMask.CurrentDirtSum; var revision = owner.SurfaceMask.Revision;
                foreach (var button in UnityEngine.Object.FindObjectsByType<CleaningToolButton>(FindObjectsSortMode.None))
                {
                    var ui = button.GetComponent<Button>(); var hit = Raycast(Center(ui.transform as RectTransform));
                    Check(hit.Count > 0 && hit[0].gameObject == ui.gameObject, "실제 버튼 Raycast");
                    Click(ui); Refresh();
                    Check(selection.CurrentTool == Get<CleaningToolDefinition>(button, "tool") && Label("selectedToolText").text.Contains(selection.CurrentTool.DisplayName), "onClick/선택/HUD");
                    Check(Get<Image>(button, "background").color == Get<Color>(button, "selectedColor"), "선택 강조");
                    Check(owner.SurfaceMask.CurrentDirtSum == sum && owner.SurfaceMask.Revision == revision && cleaner.RemovedDirtCount == 0 &&
                        !tracker.HasStarted && tracker.ElapsedSeconds == 0 && !completion.IsCompleted, "UI가 청소/시간/완료를 쓰지 않음");
                }
                foreach (var name in new[] { "CleaningGuide", "ControlsGuide", "ProgressPanel" })
                {
                    var go = canvas.transform.Find(name).gameObject;
                    Check(go.GetComponentsInChildren<Graphic>().All(x => !x.raycastTarget), "안내/장식 raycast=false");
                    Check(Raycast(Center(go.transform as RectTransform)).Count == 0, "안내 영역 Raycast 통과");
                }
                Check(!canvas.transform.Find("ToolPanel").GetComponent<Image>().raycastTarget && Raycast(new Vector2(640, 26)).Count == 0, "패널 장식 통과");
                Check(Raycast(new Vector2(640, 360)).Count == 0, "동전 위 UI 없음");
                foreach (var face in new[] { CoinSurfaceFace.Front, CoinSurfaceFace.Back, CoinSurfaceFace.Side, CoinSurfaceFace.Invalid })
                {
                    SetFace(pointer, face); Refresh();
                    Check(Label("faceHintText").text.Contains(face == CoinSurfaceFace.Front ? "앞면(태양)" : face == CoinSurfaceFace.Back ? "뒷면(물결)" : face == CoinSurfaceFace.Side ? "옆면" : "두 면"), "면 안내 " + face);
                }
                Check(Label("nextActionText").text.Contains("작은 솔"), "A 안내");
                RemoveA(cleaner); Refresh(); Check(Label("nextActionText").text.Contains("마른 천"), "B 안내");
                SetSurface(owner, 0, 0); Refresh(); Check(Label("nextActionText").text.Contains("광택천"), "광택 안내");
                Set(completion, "requiredSurfaceDirtRemovalRatio", .85f); Refresh();
                Check(Label("surfaceDirtText").text.Contains("목표 85%") && !completion.IsCompleted, "진행률 불변/목표 캐시 갱신");
                Set(completion, "requiredSurfaceDirtRemovalRatio", targetB);
                Set(tracker, "<ElapsedSeconds>k__BackingField", 6000f); Refresh();
                Check(Label("timerText").text == "청소 시간: 100:00", "긴 시간"); ValidateLayout(Check);
                for (var round = 0; round < 2; round++)
                {
                    RemoveA(cleaner); SetSurface(owner, 0, 1);
                    Check(completion.TryCompleteIfEligible() && !completion.TryCompleteIfEligible(), "완료1회");
                    Refresh(); Canvas.ForceUpdateCanvases();
                    Check(Label("nextActionText").text.StartsWith("완료!") && tracker.IsTimingStopped && !pointer.enabled, "결과/입력/타이머 고정");
                    ValidateLayout(Check);
                    Click(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(x => x.name == "RestartButton")); Refresh();
                    Check(!completion.IsCompleted && tracker.ElapsedSeconds == 0 && !tracker.HasStarted && pointer.enabled &&
                        owner.SurfaceDirtRemovalRatio == 0 && owner.Polish == 0 && cleaner.RemovedDirtCount == 0 && selection.CurrentTool == tool &&
                        Label("nextActionText").text.Contains("작은 솔"), "실제 재시작 onClick/2판 복원");
                }
                return "S2-05a HUD PASS checks=" + checks + "; uGUI raycasts/callbacks, synthetic face/progress/time, 2 rounds. Physical input not tested.";
            }
            finally
            {
                if (completion.IsCompleted) completion.RestartRound();
                Set(completion, "requiredSurfaceDirtRemovalRatio", targetB); owner.ResetSurface(); cleaner.ResetAllDirt(); tracker.ResetProgress();
                selection.TrySelect(tool); pointer.enabled = true; pointer.ResetViewToInitialState(); SetFace(pointer, CoinSurfaceFace.Invalid); Refresh();
            }
        }

        // 합성 상태 캡처만 준비합니다. 호출자는 별도 프레임의 ScreenCapture를 사용하고 마지막에 Stop합니다.
        public static string PrepareCapture(string state)
        {
            RequirePaused();
            var c = GameObject.Find("CleaningController"); var completion = c.GetComponent<CleaningCompletionController>();
            var tracker = c.GetComponent<CleaningProgressTracker>(); var pointer = c.GetComponent<CoinSurfacePointer>();
            var cleaner = c.GetComponent<ObjectDirtCleaner>(); var selection = c.GetComponent<CleaningToolSelection>();
            var owner = GameObject.Find("Coin/CoinBody").GetComponent<CoinSurfaceContamination>();
            if (completion.IsCompleted) completion.RestartRound();
            owner.ResetSurface(); cleaner.ResetAllDirt(); tracker.ResetProgress(); pointer.enabled = true; pointer.ResetViewToInitialState(); selection.ResetToDefaultTool();
            if (state == "brush") selection.TrySelect(CleaningToolId.SmallBrush);
            if (state == "back-zoom" || state == "max" || state == "result")
            {
                RemoveA(cleaner); SetSurface(owner, 0, state == "back-zoom" ? .69f : 1);
                selection.TrySelect(CleaningToolId.PolishingCloth); Set(tracker, "<ElapsedSeconds>k__BackingField", 6000f); tracker.StopTiming();
            }
            if (state == "back-zoom")
            {
                Get<Transform>(pointer, "coinTransform").rotation = Quaternion.Euler(180, 0, 0);
                Get<Camera>(pointer, "gameplayCamera").orthographicSize = 1.6f;
            }
            if (state == "result") completion.TryCompleteIfEligible();
            pointer.enabled = false; SetFace(pointer, state == "back-zoom" ? CoinSurfaceFace.Back : CoinSurfaceFace.Front);
            Call(UnityEngine.Object.FindFirstObjectByType<CleaningProgressHud>(), "Refresh"); Canvas.ForceUpdateCanvases();
            return "Synthetic native Overlay state: " + state;
        }

        private static void ValidateLayout(Action<bool, string> check)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var text in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
            {
                text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
                check(text.text.Where(x => x >= '\uac00' && x <= '\ud7a3').All(text.font.HasCharacter), "한글 glyph " + text.name);
                check(text.preferredHeight <= text.rectTransform.rect.height + 1 && text.preferredWidth <= text.rectTransform.rect.width + 1, "overflow " + text.name);
                var corners = new Vector3[4]; text.rectTransform.GetWorldCorners(corners);
                check(corners.All(x => x.x >= -1 && x.x <= Screen.width + 1 && x.y >= -1 && x.y <= Screen.height + 1), "screen bounds " + text.name);
            }
        }
        private static void RequirePaused()
        {
            if (!Application.isPlaying || !EditorApplication.isPaused || SceneManager.GetActiveScene().path != "Assets/Scenes/CleaningRoom.unity")
                throw new InvalidOperationException("새 CleaningRoom Play를 Pause한 뒤 실행하세요.");
        }
        private static void RemoveA(ObjectDirtCleaner cleaner) { foreach (var dirt in Get<ObjectDirt[]>(cleaner, "dirtObjects")) dirt.ApplyCleaning(dirt.CurrentDurability); }
        private static void SetSurface(CoinSurfaceContamination owner, float dirt, float polish)
        {
            foreach (var face in new[] { CoinSurfaceFace.Front, CoinSurfaceFace.Back })
                for (var i = 0; i < owner.SurfaceMask.TexelCount; i++) { owner.SurfaceMask.TrySetDirt(face, i, dirt); owner.SurfaceMask.TrySetPolish(face, i, polish); }
            owner.UploadSurfaceMasks(); Call(owner, "ApplyVisuals");
        }
        private static void SetFace(CoinSurfacePointer pointer, CoinSurfaceFace face)
        {
            var bounds = pointer.SurfaceLocalBounds; var matrix = pointer.SurfaceLocalToWorldMatrix;
            var normal = face == CoinSurfaceFace.Side ? Vector3.right : face == CoinSurfaceFace.Back ? Vector3.down : Vector3.up;
            var local = bounds.center + (face == CoinSurfaceFace.Side ? Vector3.right * bounds.extents.x : normal * bounds.extents.y);
            CoinSurfaceMapper.TryMapPoint(matrix, bounds, matrix.MultiplyPoint3x4(local), matrix.inverse.transpose.MultiplyVector(normal), out var contact);
            Set(pointer, "<HasSurfaceHit>k__BackingField", face != CoinSurfaceFace.Invalid); Set(pointer, "<CurrentSurfaceContact>k__BackingField", contact);
        }
        private static void Click(Button button) => ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        private static List<RaycastResult> Raycast(Vector2 point)
        {
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits); return hits;
        }
        private static Vector2 Center(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Instance).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Instance).SetValue(target, value);
        private static object Call(object target, string name) => target.GetType().GetMethod(name, Instance).Invoke(target, null);
        private static EditorWindow GameView() => Resources.FindObjectsOfTypeAll<EditorWindow>().First(x => x.GetType().Name == "GameView");
        private static string CaptureSizeStatus()
        {
            var view = GameView(); var group = SizeGroup(view); var ownTitleCount = 0;
            for (var i = 0; i < SizeCount(group); i++) if ((string)Property(SizeAt(group, i), "baseText") == Title) ownTitleCount++;
            return "selected=" + Property(view, "selectedSizeIndex") + ", presets=" + SizeCount(group) + ", temporaryTitleCount=" + ownTitleCount;
        }
        private static object Property(object target, string name) => target.GetType().GetProperty(name, Instance).GetValue(target);
        private static object SizeGroup(EditorWindow view)
        {
            var type = view.GetType().Assembly.GetType("UnityEditor.GameViewSizes");
            return Property(type.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null), "currentGroup");
        }
        private static int SizeCount(object group) => (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
        private static object SizeAt(object group, int index) => group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { index });
        private static bool IsFixed1280(object size) => (int)Property(size, "width") == 1280 && (int)Property(size, "height") == 720 && Property(size, "sizeType").ToString() == "FixedResolution";
    }
}
