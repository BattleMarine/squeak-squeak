using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using SqueakSqueak.Cleaning;

namespace SqueakSqueak.EditorValidation
{
    /// <summary>실제 카메라/셰이더 GPU 매핑과 PBR 화면 검사. 일시 설정은 finally로 복원합니다.</summary>
    public static class CoinSurfaceRenderingValidation
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const int Width = 640, Height = 360;
        private const string Output = ".utmp/s203a-visual";

        [MenuItem("Squeak Squeak/Validation/S2-03a Shader Camera (Paused Play Mode)")]
        private static void Menu() => Debug.Log(Run());

        public static string Run()
        {
            if (!Application.isPlaying || !EditorApplication.isPaused ||
                SceneManager.GetActiveScene().path != "Assets/Scenes/CleaningRoom.unity")
                throw new InvalidOperationException("새 CleaningRoom Play를 Pause한 뒤 실행하세요.");
            var owner = GameObject.Find("Coin/CoinBody").GetComponent<CoinSurfaceContamination>();
            var renderer = owner.GetComponent<Renderer>(); var collider = owner.GetComponent<MeshCollider>();
            var coin = owner.transform.parent; var camera = Camera.main;
            var pointer = GameObject.Find("CleaningController").GetComponent<CoinSurfacePointer>();
            var selection = pointer.GetComponent<CleaningToolSelection>();
            var tracker = pointer.GetComponent<CleaningProgressTracker>();
            if (tracker.HasStarted) throw new InvalidOperationException("청소 전 새 판에서만 검사하세요.");
            var initialRotation = coin.rotation; var initialZoom = camera.orthographicSize;
            var originalTarget = camera.targetTexture; var originalAspect = camera.aspect;
            var cameraData = camera.GetUniversalAdditionalCameraData(); var originalPost = cameraData.renderPostProcessing;
            var originalTool = selection.CurrentTool;
            var originalBlock = new MaterialPropertyBlock(); renderer.GetPropertyBlock(originalBlock);
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var enabled = new bool[renderers.Length];
            for (var i = 0; i < renderers.Length; i++) enabled[i] = renderers[i].enabled;
            var originalCast = renderer.shadowCastingMode;
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            var directional = Array.Find(lights, light => light.type == LightType.Directional && light.shadows != LightShadows.None);
            var originalLightRotation = directional != null ? directional.transform.rotation : Quaternion.identity;
            var checks = 0; var sampled = 0; var fronts = 0; var backs = 0; var sides = 0; var maxError = 0d;
            var maximumContactPixelError = 0d; var maximumGameViewRayOffset = 0d; var maximumGameViewPixelOffset = 0d;
            Directory.CreateDirectory(Output);
            try
            {
                Check(renderer.sharedMaterial.shader.name == "SqueakSqueak/Coin Surface" &&
                    renderer.sharedMaterial.FindPass("CoinSurfaceShadow") >= 0 &&
                    renderer.sharedMaterial.FindPass("CoinSurfaceNormals") >= 0 &&
                    !ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader), "전용 셰이더/그림자/SSAO 패스", ref checks);
                Check(originalCast == ShadowCastingMode.Off && renderer.receiveShadows && directional != null,
                    "기존 동전 투영 Off·수신 On·Directional Soft 보존", ref checks);
                camera.aspect = Width / (float)Height;
                // 본래 PBR·조명·SSAO 상태로 실제 국소 브러시 전후를 기록합니다. A도 그대로 표시합니다.
                var before = Capture(camera, Output + "-before.png");
                selection.TrySelect(CleaningToolId.PolishingCloth);
                Apply(owner, pointer, CoinSurfaceFace.Front, new Vector2(.35f, .6f), 300f);
                var after = Capture(camera, Output + "-local-after.png");
                Check(PixelDifference(before, after) > .00005d && owner.Polish > 0f && owner.Polish < .1f &&
                    owner.SurfaceMask.GetCurrentPolishSum(CoinSurfaceFace.Back) == 0d,
                    "실제 셰이더 PBR 접촉 전후 화면 변화·반대면 보존", ref checks);
                coin.rotation = Quaternion.Euler(180f, 0f, 0f); Physics.SyncTransforms();
                Capture(camera, Output + "-back-untouched.png");
                Apply(owner, pointer, CoinSurfaceFace.Back, new Vector2(.7f, .3f), 300f);
                Capture(camera, Output + "-back-local-after.png");
                coin.rotation = initialRotation; Physics.SyncTransforms();

                // 그림자 투영은 본래 Off입니다. On을 일시 적용해 실제 ShadowCaster 출력 차이도 검사합니다.
                directional.transform.rotation = Quaternion.Euler(45f, 30f, 0f);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                var noShadow = Capture(camera, null);
                renderer.shadowCastingMode = ShadowCastingMode.On;
                var withShadow = Capture(camera, Output + "-shadow-probe.png");
                var shadowDifference = PixelDifference(noShadow, withShadow);
                Check(shadowDifference > .00001d, "일시 On일 때 실제 투영 그림자 차이", ref checks);
                renderer.shadowCastingMode = originalCast; directional.transform.rotation = originalLightRotation;

                // B=0으로 고정해 색/오염 제거가 아닌 지역 광택의 PBR 반사 변화만 분리합니다.
                foreach (var other in renderers) if (other != renderer) other.enabled = false;
                directional.transform.rotation = Quaternion.Euler(85f, 0f, 0f);
                foreach (var face in new[] { CoinSurfaceFace.Front, CoinSurfaceFace.Back })
                for (var index = 0; index < owner.SurfaceMask.TexelCount; index++)
                {
                    owner.SurfaceMask.TrySetDirt(face, index, 0f);
                    owner.SurfaceMask.TrySetPolish(face, index, 0f);
                }
                owner.UploadSurfaceMasks();
                var matte = Capture(camera, Output + "-polish-only-before.png");
                Apply(owner, pointer, CoinSurfaceFace.Front, new Vector2(.35f, .6f), 300f);
                var polished = Capture(camera, Output + "-polish-only-after.png");
                var polishDifference = PixelDifference(matte, polished);
                Check(polishDifference > .00005d && owner.SurfaceDirt == 0f && owner.Polish > 0f && owner.Polish < .1f,
                    "지역 광택만으로 PBR 반사·거칠기 화면 변화", ref checks);
                directional.transform.rotation = originalLightRotation;

                // 좌표 검사는 후처리를 끄고 양면별 비대칭 경사값을 설정합니다. 같은 실제 셰이더 샘플 경로입니다.
                cameraData.renderPostProcessing = false;
                foreach (var other in renderers) if (other != renderer) other.enabled = false;
                var data = owner.SurfaceMask; var n = data.Resolution;
                foreach (var face in new[] { CoinSurfaceFace.Front, CoinSurfaceFace.Back })
                for (var j = 0; j < n; j++)
                for (var i = 0; i < n; i++)
                {
                    var u = (i + .5f) / n; var v = (j + .5f) / n;
                    data.TrySetDirt(face, j * n + i, face == CoinSurfaceFace.Front ? .15f + .7f * u : .9f - .6f * v);
                    data.TrySetPolish(face, j * n + i, face == CoinSurfaceFace.Front ? .1f + .8f * v : .8f - .7f * u);
                }
                owner.UploadSurfaceMasks();
                var debugBlock = new MaterialPropertyBlock(); renderer.GetPropertyBlock(debugBlock);
                debugBlock.SetFloat("_SurfaceDebug", 1f); renderer.SetPropertyBlock(debugBlock);
                foreach (var zoom in new[] { 1.6f, 3.2f })
                foreach (var rotation in new[] { Vector3.zero, new Vector3(45f, 0f, 0f), new Vector3(75f, 0f, 0f),
                    new Vector3(180f, 0f, 0f), new Vector3(143f, 21f, 63f), new Vector3(90f, 0f, 0f) })
                {
                    coin.rotation = Quaternion.Euler(rotation); camera.orthographicSize = zoom; Physics.SyncTransforms();
                    var pixels = Capture(camera, null); var poseSamples = 0; var poseError = 0d; var worst = "";
                    for (var y = 7; y < Height; y += 9)
                    for (var x = 7; x < Width; x += 9)
                    {
                        // targetTexture 복원 뒤 ViewportPointToRay는 소수점 Game View pixelRect를 반올림합니다.
                        // 실제 640×360 픽셀 중심은 렌더에 사용한 투영행렬로 직접 역투영합니다.
                        var viewport = new Vector3((x + .5f) / Width, (y + .5f) / Height, 0f);
                        var cameraSpace = camera.projectionMatrix.inverse.MultiplyPoint(new Vector3(viewport.x * 2f - 1f,
                            viewport.y * 2f - 1f, -1f));
                        var ray = new Ray(camera.cameraToWorldMatrix.MultiplyPoint(cameraSpace), camera.transform.forward);
                        var viewRayOffset = Vector3.Distance(ray.origin, camera.ViewportPointToRay(viewport).origin);
                        maximumGameViewRayOffset = Math.Max(maximumGameViewRayOffset, viewRayOffset);
                        maximumGameViewPixelOffset = Math.Max(maximumGameViewPixelOffset, viewRayOffset * Height / (2d * zoom));
                        if (!collider.Raycast(ray, out var hit, 100f) ||
                            !CoinSurfaceMapper.TryMapHit(collider, collider.sharedMesh, pointer.SurfaceLocalBounds, hit, out var contact)) continue;
                        var world = contact.WorldPosition;
                        var projected = camera.projectionMatrix * camera.worldToCameraMatrix * new Vector4(world.x, world.y, world.z, 1f);
                        maximumContactPixelError = Math.Max(maximumContactPixelError,
                            Vector2.Distance(new Vector2((projected.x / projected.w * .5f + .5f) * Width,
                                (projected.y / projected.w * .5f + .5f) * Height), new Vector2(x + .5f, y + .5f)));
                        Color expected;
                        if (contact.Face == CoinSurfaceFace.Side)
                        {
                            expected = new Color(0f, 0f, .5f, 1f); sides++;
                        }
                        else if (contact.IsCleanable)
                        {
                            var tx = Mathf.Clamp(Mathf.FloorToInt(contact.Uv.x * n), 0, n - 1);
                            var ty = Mathf.Clamp(Mathf.FloorToInt(contact.Uv.y * n), 0, n - 1);
                            var index = ty * n + tx;
                            expected = new Color(Mathf.Round(data.GetDirt(contact.Face, index) * 255f) / 255f,
                                Mathf.Round(data.GetPolish(contact.Face, index) * 255f) / 255f,
                                contact.Face == CoinSurfaceFace.Front ? 1f : 0f, 1f);
                            if (contact.Face == CoinSurfaceFace.Front) fronts++; else backs++;
                        }
                        else continue;
                        var actual = pixels[y * Width + x];
                        var error = Math.Max(Math.Abs(actual.r - expected.r),
                            Math.Max(Math.Abs(actual.g - expected.g), Math.Abs(actual.b - expected.b)));
                        if (error > poseError)
                        {
                            poseError = error;
                            worst = $"pixel={x},{y} face={contact.Face} uv={contact.Uv:F6} actual={actual:F6} expected={expected:F6}";
                        }
                        maxError = Math.Max(maxError, error);
                        sampled++; poseSamples++;
                    }
                    Check(poseSamples >= 10 && maxError < .005d,
                        $"GPU 실제 면/UV/channel 회전·Zoom {rotation}/{zoom}, samples={poseSamples}, maxError={poseError:G7}, {worst}", ref checks);
                }
                Check(fronts > 0 && backs > 0 && sides > 0, "앞·뒤·옆면 GPU 표본 확보", ref checks);
                Check(maximumContactPixelError < .001d, "실제 Collider/매핑 접촉과 렌더 픽셀 재투영 일치", ref checks);
                return $"S2-03a SHADER PASS checks={checks}, samples={sampled}, front/back/side={fronts}/{backs}/{sides}, maxContactPixelError={maximumContactPixelError:G7}, maxGameViewRayOffset={maximumGameViewRayOffset:G7}, maxGameViewPixelOffset={maximumGameViewPixelOffset:G7}, maxChannelError={maxError:G7}, shadowDifference={shadowDifference:G7}, polishOnlyDifference={polishDifference:G7}, captures={Output}-*.png. Actual Collider/mapping vs camera/PBR/Deferred/SSAO, no hull-only exclusion; temporary API poses, not physical input/build.";
            }
            finally
            {
                for (var i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = enabled[i];
                renderer.shadowCastingMode = originalCast;
                if (directional != null) directional.transform.rotation = originalLightRotation;
                cameraData.renderPostProcessing = originalPost;
                camera.targetTexture = originalTarget; camera.aspect = originalAspect;
                coin.rotation = initialRotation; camera.orthographicSize = initialZoom; Physics.SyncTransforms();
                renderer.SetPropertyBlock(originalBlock); selection.TrySelect(originalTool);
                owner.ResetSurface(); tracker.ResetProgress(); pointer.ResetViewToInitialState();
            }
        }

        private static Color[] Capture(Camera camera, string path)
        {
            var previous = RenderTexture.active; var original = camera.targetTexture;
            var target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            Texture2D read = null; Texture2D png = null;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                read = new Texture2D(Width, Height, TextureFormat.RGBAFloat, false, true) { hideFlags = HideFlags.DontSave };
                read.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); read.Apply(false, false);
                var pixels = read.GetPixels();
                if (path != null)
                {
                    var display = new Color[pixels.Length];
                    for (var i = 0; i < pixels.Length; i++) display[i] = pixels[i].gamma;
                    png = new Texture2D(Width, Height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
                    png.SetPixels(display); png.Apply(false, false); File.WriteAllBytes(path, png.EncodeToPNG());
                }
                return pixels;
            }
            finally
            {
                camera.targetTexture = original; RenderTexture.active = previous;
                if (read != null) UnityEngine.Object.DestroyImmediate(read);
                if (png != null) UnityEngine.Object.DestroyImmediate(png);
                RenderTexture.ReleaseTemporary(target);
            }
        }
        private static double PixelDifference(Color[] a, Color[] b)
        {
            var sum = 0d;
            for (var i = 0; i < a.Length; i++) sum += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
            return sum / (3d * a.Length);
        }
        private static void Apply(CoinSurfaceContamination owner, CoinSurfacePointer pointer, CoinSurfaceFace face, Vector2 uv, float dt)
        {
            var bounds = pointer.SurfaceLocalBounds; var matrix = pointer.SurfaceLocalToWorldMatrix;
            var local = bounds.center + new Vector3((2f * uv.x - 1f) * bounds.extents.x,
                face == CoinSurfaceFace.Front ? bounds.extents.y : -bounds.extents.y, (2f * uv.y - 1f) * bounds.extents.z);
            if (!CoinSurfaceMapper.TryMapPoint(matrix, bounds, matrix.MultiplyPoint3x4(local),
                matrix.inverse.transpose.MultiplyVector(face == CoinSurfaceFace.Front ? Vector3.up : Vector3.down), out var contact))
                throw new InvalidOperationException("검사 접촉 실패");
            typeof(CoinSurfaceContamination).GetMethod("ApplySurfaceContact", PrivateInstance).Invoke(owner, new object[] { contact, dt });
        }
        private static void Check(bool condition, string name, ref int checks)
        {
            if (!condition) throw new InvalidOperationException("S2-03a SHADER FAIL: " + name);
            checks++;
        }
    }
}
