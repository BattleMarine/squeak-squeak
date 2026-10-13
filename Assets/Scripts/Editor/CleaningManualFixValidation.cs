using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using SqueakSqueak.Cleaning;

namespace SqueakSqueak.EditorValidation
{
    /// <summary>수동 보고의 방향·시간·줌 회귀. 합성 시각/접촉이며 물리 입력·사람의 완료 시간 검사가 아닙니다.</summary>
    public static class CleaningManualFixValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public static string Run()
        {
            if (!Application.isPlaying || !EditorApplication.isPaused ||
                SceneManager.GetActiveScene().path != "Assets/Scenes/CleaningRoom.unity")
                throw new InvalidOperationException("새 CleaningRoom Play를 Pause한 뒤 실행하세요.");
            var controller = GameObject.Find("CleaningController");
            var tracker = controller.GetComponent<CleaningProgressTracker>();
            var pointer = controller.GetComponent<CoinSurfacePointer>();
            var camera = Camera.main;
            var coin = GameObject.Find("Coin").transform;
            var body = coin.Find("CoinBody").GetComponent<MeshCollider>();
            var rotation = coin.rotation;
            var zoom = camera.orthographicSize;
            var checks = 0;
            void Check(bool ok, string name)
            {
                if (!ok) throw new InvalidOperationException("s2-manual-fix FAIL: " + name);
                checks++;
            }
            try
            {
                Check(!tracker.HasStarted && !tracker.IsTimingStopped, "새 판 전제");
                var mappingCases = 0;
                var maxAxisDifference = 0f;
                foreach (var angle in new[] { 0f, 45f, 75f, 180f })
                {
                    coin.rotation = Quaternion.Euler(angle, 0, 0);
                    Physics.SyncTransforms();
                    var matrix = body.transform.localToWorldMatrix;
                    var bounds = body.sharedMesh.bounds;
                    var ratios = new float[3];
                    for (var axis = 0; axis < 3; axis++)
                    {
                        var data = new CoinSurfaceMaskData(64, 1, CoinSurfaceDirtPattern.Uniform);
                        var stroke = new CoinSurfaceBrushStroke();
                        var tool = new object();
                        var face = angle > 90 ? CoinSurfaceFace.Back : CoinSurfaceFace.Front;
                        for (var i = 0; i <= 60; i++)
                        {
                            var q = (i / 60f - .5f) * .6f;
                            var local = bounds.center + new Vector3(axis == 1 ? 0 : q,
                                face == CoinSurfaceFace.Front ? bounds.extents.y : -bounds.extents.y, axis == 0 ? 0 : q);
                            var screen = camera.WorldToScreenPoint(matrix.MultiplyPoint3x4(local));
                            var ray = camera.ScreenPointToRay(screen);
                            Check(Physics.Raycast(ray, out var hit, 100, Physics.DefaultRaycastLayers,
                                QueryTriggerInteraction.Ignore) && hit.collider == body, "방향별 실제 씬 적중");
                            Check(CoinSurfaceMapper.TryMapHit(body, body.sharedMesh, bounds, hit, out var c) &&
                                c.IsCleanable && c.Face == face, "방향별 앞뒤 매핑");
                            Check(stroke.Apply(data, c, matrix, bounds, tool, .32f, .48f, 1f / 60), "방향별 지역 제거");
                            mappingCases++;
                        }
                        ratios[axis] = data.RemovalRatio;
                        Check(data.GetCurrentDirtSum(face == CoinSurfaceFace.Front ? CoinSurfaceFace.Back :
                            CoinSurfaceFace.Front) == data.GetInitialDirtSum(face == CoinSurfaceFace.Front ?
                            CoinSurfaceFace.Back : CoinSurfaceFace.Front), "반대면 쓰기 없음");
                    }
                    maxAxisDifference = Mathf.Max(maxAxisDifference, Mathf.Abs(ratios[0] - ratios[1]));
                    Check(Mathf.Abs(ratios[0] - ratios[1]) < 1e-6f, "동일 시간 수평/수직 대칭");
                }
                coin.rotation = rotation;
                Physics.SyncTransforms();

                tracker.ResetProgress();
                Set(tracker, "previousTimingTimestamp", 100d);
                Set(tracker, "wasTimingFocused", true);
                Call(tracker, "AdvanceTiming", 105d, true);
                Check(tracker.ElapsedSeconds == 0 && !tracker.HasStarted, "청소 전 대기 미합산");
                Call(tracker, "HandleCleaningApplied");
                Set(tracker, "previousTimingTimestamp", 105d);
                Set(tracker, "wasTimingFocused", true);
                Call(tracker, "AdvanceTiming", 110d, true);
                Check(tracker.ElapsedSeconds == 5, "첫 변화 이후 포커스5초");
                Call(tracker, "AdvanceTiming", 140d, false);
                Call(tracker, "AdvanceTiming", 170d, true);
                Check(tracker.ElapsedSeconds == 5, "Update 없음/복귀 큰 시간 간격 제외");
                Call(tracker, "AdvanceTiming", 171d, true);
                Check(tracker.ElapsedSeconds == 6, "복귀 이후1초만 합산");
                Set(tracker, "previousTimingTimestamp", Time.realtimeSinceStartupAsDouble - .01);
                Call(tracker, "OnApplicationFocus", false);
                var beforeReturn = tracker.ElapsedSeconds;
                Check(beforeReturn >= 6 && beforeReturn < 6.1f && !Get<bool>(tracker, "wasTimingFocused"),
                    "포커스 callback 이탈 경계까지 합산");
                Set(tracker, "previousTimingTimestamp", Time.realtimeSinceStartupAsDouble - 30);
                Call(tracker, "OnApplicationFocus", true);
                Check(tracker.ElapsedSeconds == beforeReturn && Get<bool>(tracker, "wasTimingFocused"),
                    "callback 복귀는30초 재합산 없음");
                tracker.StopTiming();
                var stopped = tracker.ElapsedSeconds;
                Call(tracker, "AdvanceTiming", Time.realtimeSinceStartupAsDouble + 10, true);
                Check(tracker.ElapsedSeconds == stopped, "완료 시간 고정");
                tracker.ResetProgress();
                Check(!tracker.HasStarted && tracker.ElapsedSeconds == 0 && !tracker.IsTimingStopped, "재시작 타이머 복원");

                Check(InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms,
                    "현재 정규화 휠 설정");
                pointer.ResetViewToInitialState();
                var initialZoom = camera.orthographicSize;
                Call(pointer, "ApplyZoom", 1f);
                Check(Mathf.Abs(camera.orthographicSize - (initialZoom - .12f)) < 1e-5f, "휠 정규화1단위 변화.12");
                Call(pointer, "ApplyZoom", -1f);
                Check(Mathf.Abs(camera.orthographicSize - initialZoom) < 1e-5f, "역방향 대칭");
                Call(pointer, "ApplyZoom", 100f);
                Check(camera.orthographicSize == 1.6f, "최소줌 제한");
                Call(pointer, "ApplyZoom", -100f);
                Check(camera.orthographicSize == 3.2f, "최대줌 제한");
                pointer.ResetViewToInitialState();
                Check(camera.orthographicSize == initialZoom && tracker.ElapsedSeconds == 0, "F/API 뷰만 복원");
                return $"s2-manual-fix PASS checks={checks}, rayCases={mappingCases}, maxAxisRatioDifference={maxAxisDifference:G7}. " +
                    "Explicit timestamp/focus and normalized wheel API; physical input/AltTab not tested.";
            }
            finally
            {
                tracker.ResetProgress();
                pointer.ResetViewToInitialState();
                coin.rotation = rotation;
                camera.orthographicSize = zoom;
                Physics.SyncTransforms();
            }
        }

        public static string RunCalibration()
        {
            var cloth = AssetDatabase.LoadAssetAtPath<CleaningToolDefinition>("Assets/Data/CleaningTools/DryCloth.asset");
            var polish = AssetDatabase.LoadAssetAtPath<CleaningToolDefinition>("Assets/Data/CleaningTools/PolishingCloth.asset");
            var path = new List<Vector2>();
            for (var y = 0; y < 9; y++) for (var k = 0; k < 9; k++)
            {
                var x = y % 2 == 0 ? k : 8 - k;
                var q = new Vector2((x - 4) * .2f, (y - 4) * .2f);
                if (q.sqrMagnitude <= .85f * .85f) path.Add((q + Vector2.one) * .5f);
            }
            var matrix = Matrix4x4.Scale(new Vector3(2, .1f, 2));
            var bounds = new Bounds(Vector3.zero, new Vector3(1, 2, 1));
            var data = new CoinSurfaceMaskData(256, 1);
            var stroke = new CoinSurfaceBrushStroke();
            var bFrame = -1;
            var frame = 0;
            for (; frame < 6000; frame++)
            {
                var polishing = data.RemovalRatio >= .8f;
                if (polishing && bFrame < 0) { bFrame = frame; stroke.BreakContact(); }
                var tool = polishing ? polish : cloth;
                var uv = path[frame % path.Count];
                var face = (frame / path.Count) % 2 == 0 ? CoinSurfaceFace.Front : CoinSurfaceFace.Back;
                var local = new Vector3(uv.x - .5f, face == CoinSurfaceFace.Front ? 1 : -1, uv.y - .5f);
                CoinSurfaceMapper.TryMapPoint(matrix, bounds, matrix.MultiplyPoint3x4(local),
                    face == CoinSurfaceFace.Front ? Vector3.up : Vector3.down, out var contact);
                stroke.Apply(data, contact, matrix, bounds, tool, tool.BrushRadius,
                    tool.SurfaceDirtRemovalPerSecond, .1f, tool.PolishGainPerSecond);
                if (data.RemovalRatio >= .8f && data.MeanPolish >= .7f) break;
            }
            if (frame == 6000) throw new InvalidOperationException("Calibration did not reach surface thresholds in600 model seconds.");
            return $"s2-manual-fix CALIBRATION resolution=256, dt=.1, grid=9x9/disk.85, " +
                $"B80={bFrame * .1f:F1}s, surfaceEnd={frame * .1f:F1}s, B={data.RemovalRatio:F6}, P={data.MeanPolish:F6}. " +
                "CPU synthetic path only; excludes A/rotation/UI/human thought and is NOT actual completion time.";
        }

        public static string RunTunedRates()
        {
            var bounds = new Bounds(Vector3.zero, new Vector3(1, 2, 1));
            var matrix = Matrix4x4.Scale(new Vector3(2, .1f, 2));
            var checks = 0;
            var maximumPixelError = 0f;
            foreach (var name in new[] { "DryCloth", "SmallBrush", "PolishingCloth" })
            foreach (var drag in new[] { false, true })
            {
                var tool = AssetDatabase.LoadAssetAtPath<CleaningToolDefinition>($"Assets/Data/CleaningTools/{name}.asset");
                CoinSurfaceMaskData Simulate(int hz)
                {
                    var data = new CoinSurfaceMaskData(64, .4f, CoinSurfaceDirtPattern.Uniform);
                    var stroke = new CoinSurfaceBrushStroke();
                    for (var i = 0; i <= 4 * hz; i++)
                    {
                        var local = new Vector3(drag ? -.3f + .6f * i / (4 * hz) : 0, 1, 0);
                        CoinSurfaceMapper.TryMapPoint(matrix, bounds, matrix.MultiplyPoint3x4(local), Vector3.up, out var c);
                        stroke.Apply(data, c, matrix, bounds, tool, tool.BrushRadius, tool.SurfaceDirtRemovalPerSecond,
                            i == 0 ? 1e-6f : 1f / hz, tool.PolishGainPerSecond);
                    }
                    return data;
                }
                var reference = Simulate(30);
                foreach (var hz in new[] { 60, 120 })
                {
                    var actual = Simulate(hz);
                    for (var i = 0; i < actual.TexelCount; i++)
                    {
                        maximumPixelError = Mathf.Max(maximumPixelError,
                            Mathf.Abs(actual.GetDirt(CoinSurfaceFace.Front, i) - reference.GetDirt(CoinSurfaceFace.Front, i)),
                            Mathf.Abs(actual.GetPolish(CoinSurfaceFace.Front, i) - reference.GetPolish(CoinSurfaceFace.Front, i)));
                    }
                    if (maximumPixelError > .01f || Mathf.Abs(actual.RemovalRatio - reference.RemovalRatio) > .01f ||
                        Mathf.Abs(actual.MeanPolish - reference.MeanPolish) > .01f)
                        throw new InvalidOperationException("Tuned rate Hz tolerance failed: " + name);
                    checks++;
                }
            }
            return $"s2-manual-fix TUNED Hz/Hold/Drag PASS checks={checks}, maxPixelDifference={maximumPixelError * 100:F6}%p. " +
                "Actual tool assets, 30/60/120Hz, 4 seconds CPU time per path; not physical input.";
        }

        private static void Call(object o, string method, params object[] args) =>
            o.GetType().GetMethod(method, Private).Invoke(o, args);
        private static void Set(object o, string field, object value) => o.GetType().GetField(field, Private).SetValue(o, value);
        private static T Get<T>(object o, string field) => (T)o.GetType().GetField(field, Private).GetValue(o);
    }
}
