using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SqueakSqueak.Cleaning;

namespace SqueakSqueak.EditorValidation
{
    /// <summary>실제 소유자/Tracker/HUD/완료 경로를 검사합니다. 물리 입력이나 체험 시간을 대체하지 않습니다.</summary>
    public static class CoinSurfaceAggregateValidation
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("Squeak Squeak/Validation/S2-02c Aggregate Integration (Paused Play Mode)")]
        private static void FromMenu() => Debug.Log(Run());

        [MenuItem("Squeak Squeak/Validation/S2-02c View Reset (Paused Play Mode)")]
        private static void ViewFromMenu() => Debug.Log(RunViewReset());

        public static string RunViewReset()
        {
            if (!Application.isPlaying || !EditorApplication.isPaused ||
                SceneManager.GetActiveScene().path != "Assets/Scenes/CleaningRoom.unity")
                throw new InvalidOperationException("새 CleaningRoom Play를 Pause한 뒤 실행하세요.");
            var owner = GameObject.Find("Coin/CoinBody").GetComponent<CoinSurfaceContamination>();
            var controller = GameObject.Find("CleaningController");
            var pointer = controller.GetComponent<CoinSurfacePointer>();
            var selection = controller.GetComponent<CleaningToolSelection>();
            var tracker = controller.GetComponent<CleaningProgressTracker>();
            var completion = controller.GetComponent<CleaningCompletionController>();
            var cleaner = controller.GetComponent<ObjectDirtCleaner>();
            var coin = Get<Transform>(pointer, "coinTransform");
            var camera = Get<Camera>(pointer, "gameplayCamera");
            var tool = selection.CurrentTool;
            var initialPosition = coin.position; var initialRotation = coin.rotation; var initialZoom = camera.orthographicSize;
            var checks = 0;
            try
            {
                Check(!completion.IsCompleted && !tracker.HasStarted, "새 판에서 뷰 복원 검사", ref checks);
                owner.SurfaceMask.TrySetDirt(CoinSurfaceFace.Front, 128 * 256 + 128, 0f);
                owner.UploadSurfaceMasks(); SetPolish(owner, .4f);
                var sum = owner.SurfaceMask.CurrentDirtSum; var ratio = owner.SurfaceDirtRemovalRatio;
                coin.SetPositionAndRotation(initialPosition + Vector3.up, Quaternion.Euler(37f, 21f, 63f));
                camera.orthographicSize = 3.2f;
                pointer.ResetViewToInitialState();
                Check(coin.position == initialPosition && coin.rotation == initialRotation && camera.orthographicSize == initialZoom,
                    "F/API 자세·줌만 기본값 복원", ref checks);
                Check(owner.SurfaceMask.CurrentDirtSum == sum && owner.SurfaceDirtRemovalRatio == ratio && owner.Polish == .4f &&
                    !tracker.HasStarted && tracker.ElapsedSeconds == 0f, "F는 B/광택/타이머를 초기화하지 않음", ref checks);
                SetMask(owner, 0f); SetPolish(owner, 1f); RemoveA(cleaner, 5);
                selection.TrySelect(CleaningToolId.PolishingCloth);
                coin.SetPositionAndRotation(initialPosition + Vector3.right, Quaternion.Euler(75f, 45f, 0f));
                camera.orthographicSize = 1.6f;
                Check(completion.TryCompleteIfEligible() && completion.RestartRound(), "변경된 자세/줌/도구에서 완료→재시작", ref checks);
                Check(coin.position == initialPosition && coin.rotation == initialRotation && camera.orthographicSize == initialZoom &&
                    selection.CurrentTool == Get<CleaningToolDefinition>(selection, "defaultTool") && owner.Polish == 0f &&
                    owner.SurfaceDirtRemovalRatio == 0f && cleaner.RemovedDirtCount == 0 && !owner.HasSurfaceContactHistory,
                    "재시작은 자세/줌/도구와 청소 상태를 함께 복원", ref checks);
                return $"S2-02c VIEW PASS checks={checks}. API state reset only; F/Wheel physical input not tested.";
            }
            finally
            {
                if (completion.IsCompleted) completion.RestartRound();
                owner.ResetSurface(); cleaner.ResetAllDirt(); tracker.ResetProgress();
                selection.TrySelect(tool); pointer.enabled = true; pointer.ResetViewToInitialState();
            }
        }

        public static string Run()
        {
            if (!Application.isPlaying || !EditorApplication.isPaused ||
                SceneManager.GetActiveScene().path != "Assets/Scenes/CleaningRoom.unity")
                throw new InvalidOperationException("새 CleaningRoom Play를 Pause한 뒤 실행하세요.");
            var owner = GameObject.Find("Coin/CoinBody").GetComponent<CoinSurfaceContamination>();
            var controller = GameObject.Find("CleaningController");
            var pointer = controller.GetComponent<CoinSurfacePointer>();
            var selection = controller.GetComponent<CleaningToolSelection>();
            var tracker = controller.GetComponent<CleaningProgressTracker>();
            var completion = controller.GetComponent<CleaningCompletionController>();
            var cleaner = controller.GetComponent<ObjectDirtCleaner>();
            var hud = UnityEngine.Object.FindFirstObjectByType<CleaningProgressHud>();
            var resultPanel = UnityEngine.Object.FindFirstObjectByType<CleaningResultPanel>();
            var originalIntensity = Get<float>(owner, "initialSurfaceDirt");
            var originalPattern = Get<CoinSurfaceDirtPattern>(owner, "surfaceDirtPattern");
            var originalPolish = Get<float>(owner, "initialPolish");
            var originalResolution = Get<int>(owner, "surfaceMaskResolution");
            var originalRequiredA = Get<float>(completion, "requiredObjectDirtRemovalRatio");
            var originalRequiredB = Get<float>(completion, "requiredSurfaceDirtRemovalRatio");
            var originalRequiredPolish = Get<float>(completion, "requiredPolish");
            var originalTool = selection.CurrentTool;
            var checks = 0;
            var appliedEvents = 0;
            var progressEvents = 0;
            var maximumGpuError = 0d;
            void OnApplied() => appliedEvents++;
            void OnProgress() => progressEvents++;
            void Configure(float intensity, CoinSurfaceDirtPattern pattern, float polish = 0f)
            {
                if (completion.IsCompleted) completion.RestartRound();
                Set(owner, "initialSurfaceDirt", intensity);
                Set(owner, "surfaceDirtPattern", pattern);
                Set(owner, "initialPolish", polish);
                owner.ResetSurface();
                cleaner.ResetAllDirt();
                tracker.ResetProgress();
                pointer.ResetViewToInitialState();
            }
            bool Apply(CoinSurfaceFace face, Vector2 uv, float dt)
            {
                var bounds = pointer.SurfaceLocalBounds;
                var matrix = pointer.SurfaceLocalToWorldMatrix;
                var local = bounds.center + new Vector3((2f * uv.x - 1f) * bounds.extents.x,
                    face == CoinSurfaceFace.Front ? bounds.extents.y : -bounds.extents.y,
                    (2f * uv.y - 1f) * bounds.extents.z);
                if (!CoinSurfaceMapper.TryMapPoint(matrix, bounds, matrix.MultiplyPoint3x4(local),
                    matrix.inverse.transpose.MultiplyVector(face == CoinSurfaceFace.Front ? Vector3.up : Vector3.down), out var contact))
                    throw new InvalidOperationException("검사 접촉 생성 실패");
                // 입력 승인 뒤 호출되는 실제 private 경로입니다. 공개 입력 gate를 우회하는 기능을 추가하지 않습니다.
                return (bool)Call(owner, "ApplySurfaceContact", contact, dt);
            }
            void CheckHud()
            {
                Call(hud, "Refresh");
                Check(Get<Text>(hud, "surfaceDirtText").text == $"B 표면 제거: {Mathf.RoundToInt(tracker.SurfaceDirtRemovalRatio * 100f)}%" &&
                    Get<Text>(hud, "polishText").text == $"광택: {Mathf.RoundToInt(tracker.Polish * 100f)}%",
                    "HUD 현재 마스크/광택 표시", ref checks);
            }
            owner.CleaningApplied += OnApplied;
            tracker.ProgressChanged += OnProgress;
            try
            {
                Check(owner.SurfaceMask != null && !completion.IsCompleted && !tracker.HasStarted &&
                    pointer.enabled && originalResolution == 256, "청소 전 새 판/참조", ref checks);
                selection.TrySelect(CleaningToolId.SmallBrush); // 광택 없음: B 변화만 분리
                foreach (var pattern in new[] { CoinSurfaceDirtPattern.Uniform, CoinSurfaceDirtPattern.FixedPatches })
                foreach (var intensity in new[] { 0f, .4f, 1f })
                {
                    Configure(intensity, pattern);
                    var sum = Sum(owner.SurfaceMask);
                    var denominator = owner.SurfaceMask.InitialDirtSum;
                    Check(sum == denominator && Math.Abs(owner.SurfaceDirt - sum / (2d * owner.SurfaceMask.ValidTexelCount)) < 1e-7 &&
                        owner.SurfaceDirtRemovalRatio == (intensity == 0f ? 1f : 0f) &&
                        tracker.SurfaceDirtRemovalRatio == owner.SurfaceDirtRemovalRatio,
                        "초기0/부분/전체의 집계·면적 평균·고정 초기 합", ref checks);
                    CheckHud();
                    var beforeEvents = appliedEvents; var beforeProgress = progressEvents;
                    var changed = Apply(CoinSurfaceFace.Front, new Vector2(.5f, .5f), .25f);
                    Check(changed == (intensity > 0f) && appliedEvents - beforeEvents == (changed ? 1 : 0) &&
                        progressEvents - beforeProgress == (changed ? 1 : 0) && tracker.HasStarted == changed,
                        "실제 B 변경시에만 이벤트1회/타이머 시작", ref checks);
                    sum = Sum(owner.SurfaceMask);
                    Check(Math.Abs(owner.SurfaceDirtRemovalRatio - (denominator == 0d ? 1d : 1d - sum / denominator)) < 1e-7 &&
                        Math.Abs(owner.SurfaceDirt - sum / (2d * owner.SurfaceMask.ValidTexelCount)) < 1e-7 &&
                        tracker.Cleanliness == (tracker.ObjectDirtRemovalRatio + tracker.SurfaceDirtRemovalRatio) * .5f,
                        "현재 배열 독립 합/Tracker 청결도", ref checks);
                    CheckHud();
                    maximumGpuError = Math.Max(maximumGpuError, ValidateGpu(owner, ref checks));
                    var sameMask = owner.SurfaceMask; var sameTexture = owner.FrontDirtMask;
                    var sameRatio = owner.SurfaceDirtRemovalRatio; var sameMean = owner.SurfaceDirt;
                    Set(owner, "initialSurfaceDirt", .73f);
                    Set(owner, "surfaceDirtPattern", pattern == CoinSurfaceDirtPattern.Uniform ? CoinSurfaceDirtPattern.FixedPatches : CoinSurfaceDirtPattern.Uniform);
                    Set(owner, "surfaceMaskResolution", 17);
                    Check(owner.SurfaceMask == sameMask && owner.SurfaceMask.InitialDirtSum == denominator &&
                        owner.SurfaceDirtRemovalRatio == sameRatio && owner.SurfaceDirt == sameMean,
                        "판 중 초기 강도/패턴/해상도 변경이 분모·현재 상태에 영향 없음", ref checks);
                    Apply(CoinSurfaceFace.Front, new Vector2(.5f, .5f), .25f);
                    Check(owner.SurfaceMask == sameMask && owner.FrontDirtMask == sameTexture &&
                        owner.SurfaceMask.InitialDirtSum == denominator, "청소 중 설정 재생성 없음", ref checks);
                    owner.ResetSurface();
                    Check(owner.SurfaceMask.Resolution == 17 && owner.SurfaceMask.InitialIntensity == .73f &&
                        owner.SurfaceDirtRemovalRatio == 0f, "다음 판 Reset에서만 설정 반영", ref checks);
                    Set(owner, "surfaceMaskResolution", originalResolution);
                }
                // B가 전혀 없어도 광택이 독립적으로 진행합니다. 지역 광택은 아직 아닙니다.
                selection.TrySelect(CleaningToolId.PolishingCloth);
                Configure(0f, CoinSurfaceDirtPattern.Uniform);
                var beforeApplied = appliedEvents; var beforeChanged = progressEvents;
                Check(Apply(CoinSurfaceFace.Front, new Vector2(.5f, .5f), 1f) &&
                    Math.Abs(owner.Polish - selection.CurrentTool.PolishGainPerSecond) < 1e-7 &&
                    owner.SurfaceDirt == 0f && owner.SurfaceDirtRemovalRatio == 1f &&
                    appliedEvents == beforeApplied + 1 && progressEvents == beforeChanged + 1 && tracker.HasStarted,
                    "초기B0 광택/이벤트/타이머", ref checks);
                Configure(.4f, CoinSurfaceDirtPattern.FixedPatches);
                beforeApplied = appliedEvents; beforeChanged = progressEvents;
                Apply(CoinSurfaceFace.Front, new Vector2(.5f, .5f), 1f);
                Check(Math.Abs(owner.Polish - selection.CurrentTool.PolishGainPerSecond * (1f - owner.SurfaceDirt)) < 1e-7 &&
                    appliedEvents == beforeApplied + 1 && progressEvents == beforeChanged + 1,
                    "B/광택 동시 변화도 이벤트1회, 잔여 평균 기반 전역 효율", ref checks);
                CheckVisual(owner, ref checks);
                Configure(0f, CoinSurfaceDirtPattern.Uniform, .5f);
                beforeApplied = appliedEvents; beforeChanged = progressEvents;
                Check(!Apply(CoinSurfaceFace.Front, new Vector2(.5f, .5f), float.Epsilon) &&
                    owner.Polish == .5f && appliedEvents == beforeApplied && progressEvents == beforeChanged && !tracker.HasStarted,
                    "float 미변경 광택은 이벤트/타이머 없음", ref checks);
                Configure(0f, CoinSurfaceDirtPattern.Uniform, 1f);
                beforeApplied = appliedEvents;
                Check(!Apply(CoinSurfaceFace.Front, new Vector2(.5f, .5f), 1f) &&
                    appliedEvents == beforeApplied && !tracker.HasStarted, "포화 상태는 실제 변화 없음", ref checks);

                // 현재 A는 오브젝트5개라 제거율이20% 단위입니다. 4/5의 정확한 .8 경계를 기준값 전후로 검사합니다.
                foreach (var condition in new[] { "A", "B", "Polish" })
                foreach (var offset in new[] { -1, 0, 1 })
                {
                    Configure(1f, CoinSurfaceDirtPattern.Uniform);
                    Set(completion, "requiredObjectDirtRemovalRatio", .9f);
                    Set(completion, "requiredSurfaceDirtRemovalRatio", .8f);
                    Set(completion, "requiredPolish", .7f);
                    RemoveA(cleaner, condition == "A" ? 4 : 5);
                    if (condition == "A") Set(completion, "requiredObjectDirtRemovalRatio", .8f - offset * .0001f);
                    SetMask(owner, condition == "B" ? .2f - offset * .0001f : 0f);
                    SetPolish(owner, condition == "Polish" ? .7f + offset * .0001f : 1f);
                    if (condition == "B" && offset == 0) Check(tracker.SurfaceDirtRemovalRatio == .8f, "B 정확한 경계 .8f", ref checks);
                    Check(completion.TryCompleteIfEligible() == (offset >= 0) && completion.IsCompleted == (offset >= 0),
                        condition + " 직전/동일/직후의 동시 판정", ref checks);
                    if (completion.IsCompleted)
                        Check(!completion.TryCompleteIfEligible() && tracker.IsTimingStopped && !pointer.enabled,
                            "중복 완료 없음/타이머·입력 정지", ref checks);
                }
                // 2판은 합성 임계값 설정 대신 실제 브러시 경로로 B를 끝까지 제거합니다.
                Set(completion, "requiredObjectDirtRemovalRatio", originalRequiredA);
                Set(completion, "requiredSurfaceDirtRemovalRatio", originalRequiredB);
                Set(completion, "requiredPolish", originalRequiredPolish);
                Configure(1f, CoinSurfaceDirtPattern.FixedPatches);
                var frontTexture = owner.FrontDirtMask; var backTexture = owner.BackDirtMask;
                var initialSum = owner.SurfaceMask.InitialDirtSum;
                for (var round = 0; round < 2; round++)
                {
                    selection.TrySelect(CleaningToolId.SmallBrush);
                    var textureCount = OwnedTextures();
                    beforeApplied = appliedEvents; beforeChanged = progressEvents;
                    var sweepChanges = 0;
                    foreach (var face in new[] { CoinSurfaceFace.Front, CoinSurfaceFace.Back })
                    for (var j = 0; j <= 16; j++)
                    for (var i = 0; i <= 16; i++)
                    {
                        var q = new Vector2((i - 8) * .12f, (j - 8) * .12f);
                        if (q.sqrMagnitude > 1f) continue;
                        if (Apply(face, (q + Vector2.one) * .5f, 10000f)) sweepChanges++;
                    }
                    Check(owner.SurfaceDirtRemovalRatio == 1f && owner.SurfaceDirt == 0f &&
                        appliedEvents - beforeApplied == sweepChanges && progressEvents - beforeChanged == sweepChanges && tracker.HasStarted,
                        "양면 실제 브러시 API 전체 제거·호출별 이벤트1회", ref checks);
                    RemoveA(cleaner, 4);
                    selection.TrySelect(CleaningToolId.PolishingCloth);
                    Apply(CoinSurfaceFace.Front, new Vector2(.5f, .5f), 100f);
                    Check(!completion.TryCompleteIfEligible(), "기본A90% 미충족(4/5)은 완료하지 않음", ref checks);
                    RemoveA(cleaner, 5);
                    CheckHud();
                    maximumGpuError = Math.Max(maximumGpuError, ValidateGpu(owner, ref checks));
                    Set(tracker, "<ElapsedSeconds>k__BackingField", 12.75f); // 시간 누적 시험이 아닌 결과 스냅샷 시험
                    Check(completion.TryCompleteIfEligible() && !completion.TryCompleteIfEligible(), "2판 각각 완료1회", ref checks);
                    var revision = owner.SurfaceMask.Revision;
                    beforeApplied = appliedEvents;
                    var resultRoot = Get<GameObject>(resultPanel, "resultRoot");
                    var resultTime = Get<Text>(resultPanel, "completionTimeText").text;
                    Check(resultRoot.activeSelf && Get<Text>(resultPanel, "cleanlinessText").text == "청결도: 100%" &&
                        Get<Text>(resultPanel, "polishText").text == "광택도: 100%" && resultTime == "완료 시간: 00:12",
                        "결과 수치/시간 스냅샷", ref checks);
                    Call(tracker, "Update");
                    Check(!owner.ApplyCleaning(10f) && owner.SurfaceMask.Revision == revision &&
                        appliedEvents == beforeApplied && tracker.ElapsedSeconds == 12.75f && tracker.IsTimingStopped &&
                        !pointer.enabled && !owner.HasSurfaceContactHistory &&
                        Get<Text>(resultPanel, "completionTimeText").text == resultTime,
                        "완료 후 공개 API gate/이력/타이머/결과 고정", ref checks);
                    Check(completion.RestartRound() && !completion.RestartRound() && !completion.IsCompleted &&
                        !resultRoot.activeSelf && pointer.enabled && !tracker.HasStarted && !tracker.IsTimingStopped &&
                        tracker.ElapsedSeconds == 0f && cleaner.RemovedDirtCount == 0 && owner.Polish == 0f &&
                        owner.SurfaceMask.CurrentDirtSum == initialSum && owner.SurfaceDirtRemovalRatio == 0f &&
                        owner.FrontDirtMask == frontTexture && owner.BackDirtMask == backTexture &&
                        selection.CurrentTool == originalTool && OwnedTextures() == textureCount && !owner.HasSurfaceContactHistory,
                        "재시작 전 경로 복원/중복 방지/자원 재사용", ref checks);
                    CheckHud();
                    maximumGpuError = Math.Max(maximumGpuError, ValidateGpu(owner, ref checks));
                }
                return $"S2-02c PASS checks={checks}, maxGpuError={maximumGpuError:G7}, completedRounds=2. " +
                    "Actual runtime APIs/GPU readback; synthetic completion boundaries/time, not physical input.";
            }
            finally
            {
                if (completion.IsCompleted) completion.RestartRound();
                owner.CleaningApplied -= OnApplied;
                tracker.ProgressChanged -= OnProgress;
                Set(owner, "initialSurfaceDirt", originalIntensity);
                Set(owner, "surfaceDirtPattern", originalPattern);
                Set(owner, "initialPolish", originalPolish);
                Set(owner, "surfaceMaskResolution", originalResolution);
                Set(completion, "requiredObjectDirtRemovalRatio", originalRequiredA);
                Set(completion, "requiredSurfaceDirtRemovalRatio", originalRequiredB);
                Set(completion, "requiredPolish", originalRequiredPolish);
                owner.ResetSurface(); cleaner.ResetAllDirt(); tracker.ResetProgress();
                selection.TrySelect(originalTool); pointer.enabled = true; pointer.ResetViewToInitialState();
                resultPanel.Hide(); Call(hud, "Refresh");
            }
        }

        private static void CheckVisual(CoinSurfaceContamination owner, ref int checks)
        {
            var renderer = owner.GetComponent<Renderer>(); var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            var clean = 1f - owner.SurfaceDirt;
            var expectedColor = Color.Lerp(Get<Color>(owner, "dirtColor"), renderer.sharedMaterial.GetColor("_BaseColor"), clean);
            var expectedSmoothness = Mathf.Lerp(Mathf.Lerp(Get<float>(owner, "dirtySmoothness"),
                renderer.sharedMaterial.GetFloat("_Smoothness"), clean), Get<float>(owner, "maximumSmoothness"), owner.Polish);
            Check(block.GetColor("_BaseColor") == expectedColor && Math.Abs(block.GetFloat("_Smoothness") - expectedSmoothness) < 1e-7,
                "전체 색상/광택은 마스크 잔여 평균의 임시 표현", ref checks);
        }

        private static double ValidateGpu(CoinSurfaceContamination owner, ref int checks)
        {
            var previous = RenderTexture.active; var maximumError = 0d;
            foreach (var face in new[] { CoinSurfaceFace.Front, CoinSurfaceFace.Back })
            {
                var n = owner.SurfaceMask.Resolution;
                var target = RenderTexture.GetTemporary(n, n, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Texture2D readback = null;
                try
                {
                    target.filterMode = FilterMode.Point;
                    Graphics.Blit(face == CoinSurfaceFace.Front ? owner.FrontDirtMask : owner.BackDirtMask, target);
                    RenderTexture.active = target;
                    readback = new Texture2D(n, n, TextureFormat.RGBA32, false, true) { hideFlags = HideFlags.DontSave };
                    readback.ReadPixels(new Rect(0, 0, n, n), 0, 0); readback.Apply(false, false);
                    var pixels = readback.GetPixels32(); var valid = true;
                    for (var index = 0; index < pixels.Length; index++)
                    {
                        maximumError = Math.Max(maximumError, Math.Abs(pixels[index].r / 255d - owner.SurfaceMask.GetDirt(face, index)));
                        valid &= pixels[index].g == 0 && pixels[index].b == 0 &&
                            pixels[index].a == (owner.SurfaceMask.IsValidTexel(index) ? 255 : 0);
                    }
                    Check(valid && maximumError <= 1d / 510d + 1e-7, "GPU 양면 R8 양자화/유효 alpha 대조", ref checks);
                }
                finally
                {
                    RenderTexture.active = previous;
                    if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
                    RenderTexture.ReleaseTemporary(target);
                }
            }
            return maximumError;
        }

        private static double Sum(CoinSurfaceMaskData data)
        {
            var sum = 0d;
            foreach (var face in new[] { CoinSurfaceFace.Front, CoinSurfaceFace.Back })
                for (var index = 0; index < data.TexelCount; index++) if (data.IsValidTexel(index)) sum += data.GetDirt(face, index);
            return sum;
        }

        private static void SetMask(CoinSurfaceContamination owner, float value)
        {
            foreach (var face in new[] { CoinSurfaceFace.Front, CoinSurfaceFace.Back })
                for (var index = 0; index < owner.SurfaceMask.TexelCount; index++) owner.SurfaceMask.TrySetDirt(face, index, value);
            owner.UploadSurfaceMasks(); Call(owner, "ApplyVisuals");
        }

        private static void SetPolish(CoinSurfaceContamination owner, float value)
        { Set(owner, "<Polish>k__BackingField", value); Call(owner, "ApplyVisuals"); }

        private static void RemoveA(ObjectDirtCleaner cleaner, int count)
        {
            var dirt = Get<ObjectDirt[]>(cleaner, "dirtObjects");
            for (var index = 0; index < count; index++) dirt[index].ApplyCleaning(dirt[index].CurrentDurability);
        }

        private static int OwnedTextures()
        {
            var count = 0;
            foreach (var texture in Resources.FindObjectsOfTypeAll<Texture2D>())
                if (texture.name.StartsWith("SqueakSqueak.BMask.", StringComparison.Ordinal)) count++;
            return count;
        }

        private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, PrivateInstance).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, PrivateInstance).Invoke(target, args);
        private static void Check(bool condition, string name, ref int checks)
        {
            if (!condition) throw new InvalidOperationException("S2-02c FAIL: " + name);
            checks++;
        }
    }
}
