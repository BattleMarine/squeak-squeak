using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using SqueakSqueak.Cleaning;

namespace SqueakSqueak.EditorValidation
{
    /// <summary>새 CleaningRoom Play Mode를 Pause한 뒤 실행하는 지역 좌표 회귀 검사입니다.</summary>
    public static class CoinSurfaceMappingValidation
    {
        [MenuItem("Squeak Squeak/Validation/S2-01 Surface Mapping (Paused Play Mode)")]
        private static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            if (!Application.isPlaying || !EditorApplication.isPaused ||
                SceneManager.GetActiveScene().path != "Assets/Scenes/CleaningRoom.unity")
                throw new InvalidOperationException("새 CleaningRoom Play Mode를 Pause한 뒤 실행하세요.");
            var coin = GameObject.Find("Coin").transform;
            var body = coin.Find("CoinBody");
            var collider = body.GetComponent<MeshCollider>();
            var camera = Camera.main;
            var pointer = GameObject.Find("CleaningController").GetComponent<CoinSurfacePointer>();
            var selection = pointer.GetComponent<CleaningToolSelection>();
            var cursor = GameObject.Find("ToolCursor").GetComponent<SurfaceToolCursor>();
            var range = cursor.GetComponent<LineRenderer>();
            var hover = typeof(CoinSurfacePointer).GetMethod("UpdateSurfaceFromScreenPosition", BindingFlags.Instance | BindingFlags.NonPublic);
            var clear = typeof(CoinSurfacePointer).GetMethod("ClearSurfaceHit", BindingFlags.Instance | BindingFlags.NonPublic);
            var originalRotation = coin.rotation;
            var originalScale = body.localScale;
            var originalZoom = camera.orthographicSize;
            var originalTool = selection.CurrentTool;
            var originalEnabled = pointer.enabled;
            var count = 0;
            var rayCases = 0;
            var maximumUvError = 0f;
            var maximumWorldError = 0f;
            var maximumRingError = 0f;
            void Check(bool condition, string name)
            {
                if (!condition) throw new InvalidOperationException("S2-01 FAIL: " + name);
                count++;
            }
            try
            {
                Check(!pointer.IsPointerOverUi, "현재 실제 포인터가 UI 밖이어야 hover 경로 검사 가능");
                Check(pointer.enabled && collider.sharedMesh == body.GetComponent<MeshFilter>().sharedMesh, "핵심 참조");
                Check(CoinSurfaceMapper.TryGetSupportedBounds(collider.sharedMesh, out var bounds), "실제 Mesh 캡/측면 Geometry");
                var baseMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(2f, .1f, 2f));
                var rotations = new[] { Quaternion.identity, Quaternion.Euler(45f, 0f, 0f),
                    Quaternion.Euler(75f, 0f, 0f), Quaternion.Euler(180f, 0f, 0f), Quaternion.Euler(37f, 21f, 63f) };
                foreach (var rotation in rotations)
                {
                    foreach (var scale in new[] { new Vector3(2f, .1f, 2f), new Vector3(3f, .1f, 1f) })
                    {
                        var matrix = Matrix4x4.TRS(new Vector3(4f, 2f, -3f), rotation, scale);
                        Check(CoinSurfaceMapper.IsSupportedTransform(matrix), "회전/비균일 배율 지원");
                        foreach (var sign in new[] { 1f, -1f })
                        {
                            var local = new Vector3(.2f, sign, -.15f);
                            Check(CoinSurfaceMapper.TryMapPoint(matrix, bounds, matrix.MultiplyPoint3x4(local),
                                rotation * Vector3.up * sign, out var contact), "앞뒤 순수 매핑");
                            Check(contact.Face == (sign > 0 ? CoinSurfaceFace.Front : CoinSurfaceFace.Back), "로컬 면 ID");
                            Check(Vector2.Distance(contact.Uv, new Vector2(.7f, .35f)) < 1e-5f, "앞뒤 동일 축 UV");
                            Check(Vector3.Distance(contact.WorldPosition, matrix.MultiplyPoint3x4(local)) < 1e-5f, "투영 위치");
                            Check(Vector3.Distance(contact.WorldNormal, rotation * Vector3.up * sign) < 1e-5f, "월드 캡 법선");
                        }
                    }
                }
                Check(!CoinSurfaceMapper.TryMapPoint(baseMatrix, bounds, baseMatrix.MultiplyPoint3x4(Vector3.up),
                    baseMatrix.inverse.transpose.MultiplyVector(new Vector3(.3f, .954f, 0f)), out var oblique) &&
                    !oblique.IsCleanable, "캡 내부 혼합 법선은 옆면 청소 좌표로 허용하지 않음");
                foreach (var invalidNormal in new[] { Vector3.zero, new Vector3(float.NaN, 1f, 0f),
                    new Vector3(0f, float.PositiveInfinity, 0f), Vector3.down })
                    Check(!CoinSurfaceMapper.TryMapPoint(baseMatrix, bounds, baseMatrix.MultiplyPoint3x4(Vector3.up),
                        invalidNormal, out _), "영/비유한/캡 불일치 법선");
                foreach (var invalidPoint in new[] { Vector3.zero, new Vector3(.51f, 1f, 0f),
                    new Vector3(float.NaN, 1f, 0f), new Vector3(0f, float.PositiveInfinity, 0f) })
                    Check(!CoinSurfaceMapper.TryMapPoint(baseMatrix, bounds, baseMatrix.MultiplyPoint3x4(invalidPoint),
                        Vector3.up, out _), "내부 cap/원판 밖/비유한 점");
                var shear = baseMatrix; shear.m01 = .2f;
                foreach (var invalidMatrix in new[] { shear, Matrix4x4.Scale(new Vector3(-2f, .1f, 2f)),
                    Matrix4x4.Scale(new Vector3(0f, .1f, 2f)), Matrix4x4.Scale(new Vector3(2f, 1e-5f, 2f)),
                    Matrix4x4.Scale(new Vector3(1e4f, 1e-4f, 2f)) })
                {
                    Check(!CoinSurfaceMapper.IsSupportedTransform(invalidMatrix), "잘못된 배율 거부");
                    Check(!CoinSurfaceMapper.TryMapPoint(invalidMatrix, bounds, Vector3.up, Vector3.up, out _), "잘못된 배율 매핑 거부");
                }
                Check(CoinSurfaceMapper.IsSupportedTransform(Matrix4x4.Scale(new Vector3(-2f, -.1f, 2f))), "두 축 부호의 회전 동등");
                Check(!CoinSurfaceMapper.TryMapPoint(baseMatrix, new Bounds(Vector3.zero, new Vector3(1f, 2f, 2f)),
                    Vector3.up, Vector3.up, out _), "타원 local bounds 거부");
                foreach (var rotation in rotations)
                foreach (var scale in new[] { new Vector3(2f, .1f, 2f), new Vector3(3f, .1f, 1f) })
                {
                    var matrix = Matrix4x4.TRS(Vector3.zero, rotation, scale);
                    CoinSurfaceMapper.TryMapPoint(matrix, bounds, matrix.MultiplyPoint3x4(Vector3.up), rotation * Vector3.up, out var center);
                    CoinSurfaceMapper.TryMapPoint(matrix, bounds, matrix.MultiplyPoint3x4(Vector3.down), rotation * Vector3.down, out var back);
                    foreach (var radius in new[] { .32f, .18f, .28f })
                    {
                        Check(!CoinSurfaceMapper.IsWithinBrush(matrix, center, back, radius), "반대면 거부");
                        foreach (var axis in new[] { Vector3.right, Vector3.forward })
                        {
                            var worldScale = axis.x != 0 ? scale.x : scale.z;
                            foreach (var factor in new[] { .999f, 1f, 1.001f })
                            {
                                var local = Vector3.up + axis * (radius * factor / worldScale);
                                Check(CoinSurfaceMapper.TryMapPoint(matrix, bounds, matrix.MultiplyPoint3x4(local), rotation * Vector3.up,
                                    out var candidate), "브러시 경계 후보");
                                Check(CoinSurfaceMapper.IsWithinBrush(matrix, center, candidate, radius) == (factor <= 1f), "반경 안/정확/밖");
                            }
                        }
                    }
                    foreach (var radius in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                        Check(!CoinSurfaceMapper.IsWithinBrush(matrix, center, center, radius), "잘못된 반경 거부");
                    Check(!CoinSurfaceMapper.IsWithinBrush(matrix, center, default, .32f), "Invalid 후보 거부");
                }
                foreach (var rotation in rotations)
                {
                    coin.rotation = rotation;
                    Physics.SyncTransforms();
                    var sign = Vector3.Dot(rotation * Vector3.up, camera.transform.position - coin.position) >= 0f ? 1f : -1f;
                    var expectedFace = sign > 0f ? CoinSurfaceFace.Front : CoinSurfaceFace.Back;
                    foreach (var zoom in new[] { 1.6f, 3.2f })
                    {
                        camera.orthographicSize = zoom;
                        foreach (var local in new[] { new Vector3(0f, sign, 0f), new Vector3(.2f, sign, -.15f), new Vector3(.49f, sign, 0f) })
                        {
                            var world = body.TransformPoint(local);
                            var screen = (Vector2)camera.WorldToScreenPoint(world);
                            Check(Physics.Raycast(camera.ScreenPointToRay(screen), out var hit, camera.farClipPlane,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.collider == collider, "카메라 가장 가까운 실제 적중");
                            Check(CoinSurfaceMapper.TryMapHit(collider, collider.sharedMesh, bounds, hit, out var mapped), "실제 Raycast 매핑");
                            Check(mapped.Face == expectedFace, "카메라에 보이는 면만 선택");
                            var uvError = Vector2.Distance(mapped.Uv, new Vector2(local.x + .5f, local.z + .5f));
                            var worldError = Vector3.Distance(mapped.WorldPosition, world);
                            maximumUvError = Mathf.Max(maximumUvError, uvError);
                            maximumWorldError = Mathf.Max(maximumWorldError, worldError);
                            Check(uvError < 2e-4f && worldError < 4e-4f, "독립 local 기준과 Physics 적중 오차");
                            hover.Invoke(pointer, new object[] { screen });
                            Check(pointer.HasSurfaceHit && pointer.HasCleanableContact &&
                                pointer.CurrentSurfaceContact.Face == expectedFace, "Pointer hover 실제 경로");
                            foreach (var toolIndex in new[] { 0, 1, 2 })
                            {
                                selection.TrySelect(selection.GetToolAt(toolIndex));
                                hover.Invoke(pointer, new object[] { screen });
                                Check(cursor.IsVisible && range.enabled && range.positionCount == 48, "투영 좌표 커서 표시");
                                for (var i = 0; i < range.positionCount; i++)
                                    maximumRingError = Mathf.Max(maximumRingError,
                                        Mathf.Abs(Vector3.Distance(range.GetPosition(i), cursor.transform.position) - selection.CurrentTool.BrushRadius));
                                Check(maximumRingError < 1e-5f, "모든 도구/회전/줌의 월드 링 반경");
                            }
                            rayCases++;
                        }
                        if (rotation == rotations[0] || rotation == rotations[1] || rotation == rotations[2] || rotation == rotations[3])
                        {
                            var visible = 0;
                            foreach (var dirt in UnityEngine.Object.FindObjectsByType<ObjectDirt>(FindObjectsSortMode.None))
                                if (pointer.IsWorldPointVisibleOnCoinSurface(dirt.transform.position, .12f)) visible++;
                            Check(visible == (sign > 0f ? 5 : 0), "기존 A 가시성 유지");
                        }
                    }
                }
                coin.rotation = Quaternion.Euler(90f, 0f, 0f);
                Physics.SyncTransforms();
                hover.Invoke(pointer, new object[] { (Vector2)camera.WorldToScreenPoint(coin.position) });
                Check(pointer.HasSurfaceHit && pointer.CurrentSurfaceContact.Face == CoinSurfaceFace.Side &&
                    pointer.HasCleanableContact && cursor.IsVisible && range.enabled, "실제 옆면 hover/커서 표시");
                Check(!pointer.TryGetToolUseContact(out _), "옆면도 LMB/문지르기 승인 없는 접촉은 거부");
                Check(CoinSurfaceMapper.IsWithinBrush(baseMatrix, pointer.CurrentSurfaceContact, pointer.CurrentSurfaceContact, .32f), "Side 유효 브러시 중심 허용");
                Check(!CoinSurfaceMapper.TryMapHit(collider, collider.sharedMesh, bounds, default, out _), "미적중 거부");
                Check(Physics.Raycast(new Ray(new Vector3(3f, 6f, 0f), Vector3.down), out var otherHit, 20f) &&
                    otherHit.collider != collider && !CoinSurfaceMapper.TryMapHit(collider, collider.sharedMesh, bounds, otherHit, out _), "다른 Collider 거부");
                pointer.enabled = false;
                Check(!pointer.TryGetToolUseContact(out _) && !pointer.HasCleanableContact && !cursor.IsVisible, "비활성 API와 접촉 초기화");
                pointer.enabled = originalEnabled;
                coin.rotation = originalRotation;
                body.localScale = originalScale;
                Physics.SyncTransforms();
                hover.Invoke(pointer, new object[] { (Vector2)camera.WorldToScreenPoint(body.TransformPoint(Vector3.up)) });
                Check(!pointer.TryGetToolUseContact(out _), "LMB 미사용 또는 포커스 차단; 물리 입력 검증 아님");
                clear.Invoke(pointer, null);
                Check(!pointer.HasSurfaceHit && pointer.CurrentSurfaceContact.Face == CoinSurfaceFace.Invalid && !cursor.IsVisible,
                    "동전 밖/Reset 공통 접촉 초기화");
                return $"S2-01 PASS checks={count}, cameraRayCases={rayCases}, maxUvError={maximumUvError:G7}, " +
                    $"maxWorldError={maximumWorldError:G7}, maxRingError={maximumRingError:G7}. API/Physics only; s1-06-f2 deferred.";
            }
            finally
            {
                coin.rotation = originalRotation;
                body.localScale = originalScale;
                camera.orthographicSize = originalZoom;
                selection.TrySelect(originalTool);
                pointer.enabled = originalEnabled;
                Physics.SyncTransforms();
                clear.Invoke(pointer, null);
                cursor.Hide();
            }
        }
    }
}
