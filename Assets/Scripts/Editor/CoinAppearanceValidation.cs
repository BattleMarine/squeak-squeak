using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using SqueakSqueak.Cleaning;

namespace SqueakSqueak.EditorValidation
{
    /// <summary>기본 조명·카메라 외형 캡처와 동일 UV 반사 검사. 새 판에서만 실행하며 저장하지 않습니다.</summary>
    public static class CoinAppearanceValidation
    {
        private const int Width = 1280, Height = 720;
        private const string Output = ".utmp/s203b-visual";

        [MenuItem("Squeak Squeak/Validation/S2-03b Coin Appearance (Paused Play Mode)")]
        private static void Menu() => Debug.Log(Run());

        [MenuItem("Squeak Squeak/Validation/S2-03b Zoom Shadow Regression (Paused Play Mode)")]
        private static void ShadowMenu() => Debug.Log(RunZoomShadowRegression());

        /// <summary>확대/cascade 그림자 고리 회귀. 기본 SSAO는 그대로 두고 주광 shadows만 일시 비교합니다.</summary>
        public static string RunZoomShadowRegression()
        {
            if (!Application.isPlaying || !EditorApplication.isPaused ||
                SceneManager.GetActiveScene().path != "Assets/Scenes/CleaningRoom.unity")
                throw new InvalidOperationException("새 CleaningRoom Play를 Pause한 뒤 실행하세요.");
            var owner = GameObject.Find("Coin/CoinBody").GetComponent<CoinSurfaceContamination>();
            var cleaner = GameObject.Find("CleaningController").GetComponent<ObjectDirtCleaner>();
            var tracker = cleaner.GetComponent<CleaningProgressTracker>();
            if (tracker.HasStarted || cleaner.RemovedDirtCount != 0) throw new InvalidOperationException("청소 전 새 판에서만 검사하세요.");
            var camera = Camera.main; var zoom = camera.orthographicSize; var aspect = camera.aspect;
            var key = GameObject.Find("Directional Light").GetComponent<Light>(); var shadows = key.shadows;
            Directory.CreateDirectory(".utmp");
            try
            {
                foreach (var dirt in owner.transform.parent.GetComponentsInChildren<ObjectDirt>(true)) dirt.ApplyCleaning(dirt.CurrentDurability);
                foreach (var face in new[] { CoinSurfaceFace.Front, CoinSurfaceFace.Back })
                for (var i = 0; i < owner.SurfaceMask.TexelCount; i++)
                { owner.SurfaceMask.TrySetDirt(face, i, 0f); owner.SurfaceMask.TrySetPolish(face, i, 1f); }
                owner.UploadSurfaceMasks(); camera.aspect = Width / (float)Height; camera.orthographicSize = 1.6f;
                var before = CoinSurfaceRenderingValidation.Capture(camera, ".utmp/s203b-diagnostic-current-shadow-on.png", Width, Height);
                key.shadows = LightShadows.None;
                var noShadow = CoinSurfaceRenderingValidation.Capture(camera, ".utmp/s203b-diagnostic-current-shadow-off.png", Width, Height);
                var bounds = owner.GetComponent<MeshFilter>().sharedMesh.bounds; var maximumRingDifference = 0f;
                // A가 없고 투영 Off인 원판의 평평한 영역: 확대/cascade 경계에 가짜 그림자 고리가 없어야 합니다.
                for (var i = 0; i < 48; i++)
                {
                    var angle = i * Mathf.PI * 2f / 48;
                    var world = owner.transform.TransformPoint(bounds.center + new Vector3(Mathf.Cos(angle) * .6f * bounds.extents.x,
                        bounds.extents.y, Mathf.Sin(angle) * .6f * bounds.extents.z));
                    var a = SamplePixel(camera, world, before); var b = SamplePixel(camera, world, noShadow);
                    maximumRingDifference = Mathf.Max(maximumRingDifference,
                        Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b));
                }
                if (maximumRingDifference > .005f) throw new InvalidOperationException("확대/cascade 평면의 가짜 그림자 고리: " + maximumRingDifference);
                return $"S2-03b ZOOM SHADOW PASS ringSamples=48, maxRingDifference={maximumRingDifference:G7}, " +
                    $"shadow difference={Difference(before, noShadow):G7}; settings restored, no save.";
            }
            finally
            {
                owner.ResetSurface(); cleaner.ResetAllDirt(); tracker.ResetProgress();
                key.shadows = shadows;
                camera.orthographicSize = zoom; camera.aspect = aspect;
            }
        }

        public static string Run()
        {
            if (!Application.isPlaying || !EditorApplication.isPaused ||
                SceneManager.GetActiveScene().path != "Assets/Scenes/CleaningRoom.unity")
                throw new InvalidOperationException("새 CleaningRoom Play를 Pause한 뒤 실행하세요.");
            var owner = GameObject.Find("Coin/CoinBody").GetComponent<CoinSurfaceContamination>();
            var body = owner.transform; var coin = body.parent; var camera = Camera.main;
            var pointer = GameObject.Find("CleaningController").GetComponent<CoinSurfacePointer>();
            var selection = pointer.GetComponent<CleaningToolSelection>();
            var tracker = pointer.GetComponent<CleaningProgressTracker>();
            var cleaner = pointer.GetComponent<ObjectDirtCleaner>();
            var visibleTolerance = new SerializedObject(cleaner).FindProperty("visibleSurfaceTolerance").floatValue;
            var renderer = owner.GetComponent<Renderer>(); var collider = owner.GetComponent<MeshCollider>();
            var mesh = body.GetComponent<MeshFilter>().sharedMesh;
            var dirt = coin.GetComponentsInChildren<ObjectDirt>(true);
            if (tracker.HasStarted || dirt.Length != 5 || cleaner.RemovedDirtCount != 0)
                throw new InvalidOperationException("청소 전 새 판에서만 검사하세요.");
            var rotation = coin.rotation; var zoom = camera.orthographicSize; var aspect = camera.aspect;
            var originalTool = selection.CurrentTool;
            var dirtPosition = new Vector3[dirt.Length]; var dirtScale = new Vector3[dirt.Length];
            for (var i = 0; i < dirt.Length; i++)
            { dirtPosition[i] = dirt[i].transform.localPosition; dirtScale[i] = dirt[i].transform.localScale; }
            var checks = 0; var frontHits = 0; var backHits = 0;
            Directory.CreateDirectory(".utmp");
            Color[] Capture(string label) => CoinSurfaceRenderingValidation.Capture(camera, Output + "-" + label + ".png", Width, Height);
            void Pose(float angle) { coin.rotation = Quaternion.Euler(angle, 0f, 0f); Physics.SyncTransforms(); }
            void Channels(float polish)
            {
                foreach (var face in new[] { CoinSurfaceFace.Front, CoinSurfaceFace.Back })
                for (var i = 0; i < owner.SurfaceMask.TexelCount; i++)
                { owner.SurfaceMask.TrySetDirt(face, i, 0f); owner.SurfaceMask.TrySetPolish(face, i, polish); }
                owner.UploadSurfaceMasks();
            }
            try
            {
                Check(mesh == collider.sharedMesh && body.localScale == new Vector3(2f, .1f, 2f) &&
                    coin.GetComponentsInChildren<Collider>(true).Length == 1, "원판/Collider/배율·장식 Collider 없음", ref checks);
                Check(renderer.sharedMaterial.shader.name == "SqueakSqueak/Coin Surface" &&
                    renderer.sharedMaterial.GetFloat("_ReliefStrength") > 0f && !ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader),
                    "실제 전용 외형 셰이더 참조/컴파일", ref checks);
                Check(coin.rotation == Quaternion.identity && zoom == 2.4f, "기본 자세·Zoom", ref checks);
                camera.aspect = Width / (float)Height; Physics.SyncTransforms();
                foreach (var item in dirt) if (NearestIsCoin(camera, item, collider) &&
                    pointer.IsWorldPointVisibleOnCoinSurface(item.transform.position, visibleTolerance)) frontHits++;
                var dirtyFront = Capture("front-dirty");
                Pose(35f); Capture("tilt35-dirty");
                Pose(180f);
                foreach (var item in dirt) if (NearestIsCoin(camera, item, collider) &&
                    pointer.IsWorldPointVisibleOnCoinSurface(item.transform.position, visibleTolerance)) backHits++;
                Capture("back-dirty"); Pose(0f);
                Check(frontHits == 5 && backHits == 0, "A5 위치 최근접 Coin 적중·기존 가시성 앞5/뒤0", ref checks);
                selection.TrySelect(CleaningToolId.PolishingCloth);
                CoinSurfaceRenderingValidation.Apply(owner, pointer, CoinSurfaceFace.Front, new Vector2(.32f, .48f), 300f);
                var localFront = Capture("front-local");
                Pose(35f); Capture("tilt35-local"); Pose(0f);
                Check(Difference(dirtyFront, localFront) > .00005d && owner.Polish > 0f && owner.Polish < .1f &&
                    owner.SurfaceMask.GetCurrentPolishSum(CoinSurfaceFace.Back) == 0d, "실제 국소 브러시·반대면 광택 보존", ref checks);
                Pose(180f);
                CoinSurfaceRenderingValidation.Apply(owner, pointer, CoinSurfaceFace.Back, new Vector2(.63f, .63f), 300f);
                Capture("back-local");
                foreach (var item in dirt) item.ApplyCleaning(item.CurrentDurability);
                Channels(0f); Pose(0f); var matteFront = Capture("front-clean-matte");
                Channels(1f); var polishedFront = Capture("front-clean-polished");
                var reflectionFront = ResponseSamples(camera, body, pointer.SurfaceLocalBounds, matteFront, polishedFront);
                var clipped = 0; var maximum = 0f;
                foreach (var value in SurfaceSamples(camera, body, pointer.SurfaceLocalBounds, polishedFront))
                {
                    var peak = Mathf.Max(value.r, Mathf.Max(value.g, value.b));
                    maximum = Mathf.Max(maximum, peak); if (peak > 1f) clipped++;
                }
                Pose(180f); Capture("back-clean-polished");
                Pose(35f); Channels(0f); var matteTilt = Capture("tilt35-clean-matte");
                Channels(1f); var polishedTilt = Capture("tilt35-clean-polished");
                var reflectionTilt = ResponseSamples(camera, body, pointer.SurfaceLocalBounds, matteTilt, polishedTilt);
                var movingResponse = 0d; var polishResponse = 0d;
                for (var i = 0; i < reflectionFront.Length; i++)
                {
                    movingResponse += Math.Abs(reflectionFront[i] - reflectionTilt[i]);
                    polishResponse += Math.Abs(reflectionFront[i]);
                }
                movingResponse /= reflectionFront.Length; polishResponse /= reflectionFront.Length;
                Check(polishResponse > .001d && movingResponse > .001d,
                    "B0 고정·같은 UV의 거칠기/반사 반응이 회전에 따라 변화", ref checks);
                // 캡처의 비교 상태는 합성 CPU 채널/시간이며 실제 입력·체험 시간의 증거가 아닙니다.
                Pose(65f); Capture("tilt65-clean-polished");
                Pose(90f); Capture("rim90-clean-polished");
                Pose(0f); camera.orthographicSize = 1.6f; Capture("zoom16-clean-polished");
                camera.orthographicSize = 3.2f; Capture("zoom32-clean-polished");
                Check(collider.sharedMesh == mesh && body.GetComponent<MeshFilter>().sharedMesh == mesh &&
                    body.localScale == new Vector3(2f, .1f, 2f), "렌더/충돌 Mesh 보존", ref checks);
                return $"S2-03b APPEARANCE PASS checks={checks}, A front/back hits={frontHits}/{backHits}, " +
                    $"sameUV polishResponse={polishResponse:G7}, rotatingResponse={movingResponse:G7}, " +
                    $"frontPolishedHDR above1={clipped}/{reflectionFront.Length / 3}, max={maximum:G7}, " +
                    $"captures={Output}-*.png 1280x720. Default lights/camera; synthetic API state/time, not input/performance.";
            }
            finally
            {
                owner.ResetSurface(); cleaner.ResetAllDirt(); tracker.ResetProgress(); selection.TrySelect(originalTool);
                coin.rotation = rotation; camera.orthographicSize = zoom; camera.aspect = aspect; Physics.SyncTransforms();
                for (var i = 0; i < dirt.Length; i++)
                    if (dirt[i].transform.localPosition != dirtPosition[i] || dirt[i].transform.localScale != dirtScale[i])
                        Debug.LogError("S2-03b 검사 후 A 부착/배율이 원상복원되지 않았습니다.");
                pointer.ResetViewToInitialState();
            }
        }

        private static bool NearestIsCoin(Camera camera, ObjectDirt dirt, Collider coinCollider)
        {
            var point = camera.WorldToViewportPoint(dirt.transform.position);
            return Physics.Raycast(camera.ViewportPointToRay(point), out var hit, 100f) &&
                hit.collider == coinCollider;
        }

        // 서로 다른 자세에서도 같은 로컬 UV를 투영해 roughness 변경 전후의 RGB 반응을 비교합니다.
        private static Color[] SurfaceSamples(Camera camera, Transform body, Bounds bounds, Color[] image)
        {
            var samples = new Color[81]; var index = 0;
            for (var z = -4; z <= 4; z++)
            for (var x = -4; x <= 4; x++)
            {
                var local = bounds.center + new Vector3(x * .08f * bounds.extents.x, bounds.extents.y, z * .08f * bounds.extents.z);
                samples[index++] = SamplePixel(camera, body.TransformPoint(local), image);
            }
            return samples;
        }
        private static Color SamplePixel(Camera camera, Vector3 world, Color[] image)
        {
            var clip = camera.projectionMatrix * camera.worldToCameraMatrix * new Vector4(world.x, world.y, world.z, 1f);
            var px = Mathf.FloorToInt((clip.x / clip.w * .5f + .5f) * Width);
            var py = Mathf.FloorToInt((clip.y / clip.w * .5f + .5f) * Height);
            if (px < 0 || px >= Width || py < 0 || py >= Height) throw new InvalidOperationException("표본이 화면 밖입니다.");
            return image[py * Width + px];
        }
        private static double[] ResponseSamples(Camera camera, Transform body, Bounds bounds, Color[] matte, Color[] polished)
        {
            var a = SurfaceSamples(camera, body, bounds, matte); var b = SurfaceSamples(camera, body, bounds, polished);
            var response = new double[a.Length * 3];
            for (var i = 0; i < a.Length; i++)
            { response[i * 3] = b[i].r - a[i].r; response[i * 3 + 1] = b[i].g - a[i].g; response[i * 3 + 2] = b[i].b - a[i].b; }
            return response;
        }
        private static double Difference(Color[] a, Color[] b)
        {
            var sum = 0d;
            for (var i = 0; i < a.Length; i++) sum += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
            return sum / (3d * a.Length);
        }
        private static void Check(bool condition, string message, ref int checks)
        {
            if (!condition) throw new InvalidOperationException("S2-03b APPEARANCE FAIL: " + message);
            checks++;
        }
    }
}
