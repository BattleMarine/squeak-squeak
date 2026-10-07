using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using SqueakSqueak.Cleaning;

namespace SqueakSqueak.EditorValidation
{
    /// <summary>실제 소유자 이벤트→표현 검사. 합성 승인 접촉은 private 표현 단계에서만 주입합니다.</summary>
    public static class CleaningVisualFeedbackValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static GameObject destructionProbe;
        private static UnityEngine.Object[] destructionObjects;
        private static CoinSurfaceContamination destructionSource;
        private static int expectedSubscribers;

        [MenuItem("Squeak Squeak/Validation/S2-04a Visual Feedback (Paused Play Mode)")]
        private static void Menu() => Debug.Log(Run());

        public static string Run()
        {
            RequirePaused();
            var controller = GameObject.Find("CleaningController"); var owner = GameObject.Find("Coin/CoinBody").GetComponent<CoinSurfaceContamination>();
            var pointer = controller.GetComponent<CoinSurfacePointer>(); var selection = controller.GetComponent<CleaningToolSelection>();
            var cleaner = controller.GetComponent<ObjectDirtCleaner>(); var completion = controller.GetComponent<CleaningCompletionController>();
            var tracker = controller.GetComponent<CleaningProgressTracker>(); var feedback = controller.GetComponent<CleaningVisualFeedback>();
            if (tracker.HasStarted || completion.IsCompleted) throw new InvalidOperationException("청소 전 새 Play에서 검사하세요.");
            var lines = Get<LineRenderer[]>(feedback, "contactLines"); var particles = Get<ParticleSystem>(feedback, "removalParticles");
            var coin = owner.transform.parent; var camera = Camera.main; var originalTool = selection.CurrentTool;
            var originalRotation = coin.rotation; var zoom = camera.orthographicSize; var aspect = camera.aspect;
            var dirt = coin.GetComponentsInChildren<ObjectDirt>(true);
            var ids = lines.Select(x => x.GetInstanceID()).Append(particles.GetInstanceID()).ToArray();
            var materials = lines.Select(x => x.sharedMaterial).Append(particles.GetComponent<Renderer>().sharedMaterial).ToArray();
            var sourceEvents = 0; var detailEvents = 0; var removedEvents = 0; var objectEvents = 0; var resetEvents = 0;
            var lastDetail = default(SurfaceCleaningChange); var checks = 0; var capturedDifference = 0d; var particleDifference = 0d;
            void Applied() => sourceEvents++;
            void Detail(SurfaceCleaningChange change) { detailEvents++; lastDetail = change; }
            void Removed(ObjectDirt _) => removedEvents++;
            void ObjectApplied(RaycastHit _) => objectEvents++;
            void ResetEvent() => resetEvents++;
            owner.CleaningApplied += Applied; owner.SurfaceContactChanged += Detail; owner.SurfaceReset += ResetEvent;
            cleaner.DirtRemoved += Removed; cleaner.ContactCleaningApplied += ObjectApplied;
            var advance = (Action<float, bool, CoinSurfaceContact>)Delegate.CreateDelegate(typeof(Action<float, bool, CoinSurfaceContact>),
                feedback, typeof(CleaningVisualFeedback).GetMethod("AdvanceFeedback", Private));
            CoinSurfaceContact Point(CoinSurfaceFace face = CoinSurfaceFace.Front, Vector2? uv = null)
            {
                var bounds = pointer.SurfaceLocalBounds; var matrix = pointer.SurfaceLocalToWorldMatrix; var v = uv ?? new Vector2(.35f, .6f);
                var local = bounds.center + new Vector3((v.x * 2f - 1f) * bounds.extents.x,
                    face == CoinSurfaceFace.Front ? bounds.extents.y : -bounds.extents.y, (v.y * 2f - 1f) * bounds.extents.z);
                if (!CoinSurfaceMapper.TryMapPoint(matrix, bounds, matrix.MultiplyPoint3x4(local),
                    matrix.inverse.transpose.MultiplyVector(face == CoinSurfaceFace.Front ? Vector3.up : Vector3.down), out var point))
                    throw new InvalidOperationException("검사 접촉 생성 실패");
                return point;
            }
            void Reset()
            {
                owner.ResetSurface(); cleaner.ResetAllDirt(); tracker.ResetProgress(); feedback.StopFeedback();
                pointer.ResetViewToInitialState(); Physics.SyncTransforms();
            }
            void Seed(CoinSurfaceContact point, float dt = .15f)
            {
                CoinSurfaceRenderingValidation.Apply(owner, pointer, point.Face, point.Uv, dt);
                advance(.016f, true, point);
            }
            void Off(string label)
            { Check(!feedback.IsContactVisible && feedback.ActiveRemovalParticleCount == 0 && lines.All(x => !x.enabled), label, ref checks); }
            void Geometry(CoinSurfaceContact point)
            {
                foreach (var line in lines)
                for (var i = 0; i < line.positionCount; i++)
                {
                    var offset = line.GetPosition(i) - point.WorldPosition;
                    Check(Math.Abs(Vector3.Dot(offset, point.WorldNormal) - Get<float>(feedback, "surfaceOffset")) < 1e-5f &&
                        offset.magnitude < selection.CurrentTool.BrushRadius, "접촉 법선·도구 반경 안의 선", ref checks);
                }
            }
            Color[] Capture(string label) => CoinSurfaceRenderingValidation.Capture(camera, ".utmp/s204a-visual-" + label + ".png", 1280, 720);
            Directory.CreateDirectory(".utmp");
            try
            {
                camera.aspect = 1280f / 720f;
                Check(lines.Length == 3 && particles.main.maxParticles == 48 && !particles.main.loop && !particles.main.playOnAwake &&
                    !particles.emission.enabled && !particles.collision.enabled && controller.transform.Find("CleaningFeedback").GetComponentsInChildren<Collider>(true).Length == 0,
                    "저장된 재사용 표현·Collider/자동 방출 없음", ref checks);
                Check(materials.All(x => x != null && x.shader.name == "SqueakSqueak/Cleaning Feedback" && !ShaderUtil.ShaderHasError(x.shader)),
                    "전용 저장 shader/material 계약", ref checks);
                Off("초기 표현 없음"); advance(.016f, true, Point()); Off("실제 변화 없는 합성 사용은 효과 없음");
                foreach (var tool in new[] { CleaningToolId.DryCloth, CleaningToolId.SmallBrush, CleaningToolId.PolishingCloth })
                {
                    Reset(); selection.TrySelect(tool); var point = Point(); var a = sourceEvents; var b = detailEvents;
                    CoinSurfaceRenderingValidation.Apply(owner, pointer, point.Face, point.Uv, 0f);
                    Check(sourceEvents == a && detailEvents == b, "dt0 무변화는 양 이벤트 없음", ref checks);
                    Seed(point);
                    Check(sourceEvents == a + 1 && detailEvents == b + 1 && lastDetail.Tool == tool && lastDetail.Contact.Face == point.Face &&
                        lastDetail.DirtChanged && (tool != CleaningToolId.SmallBrush || !lastDetail.PolishChanged), "실제 변경1회당 타입 이벤트1회", ref checks);
                    Check(feedback.IsContactVisible && lines.All(x => x.enabled), "도구별 선 표시", ref checks); Geometry(point);
                    var withEffect = Capture(tool.ToString().ToLowerInvariant()); feedback.StopFeedback();
                    var without = Capture(tool.ToString().ToLowerInvariant() + "-off");
                    var difference = Difference(withEffect, without); capturedDifference += difference;
                    Check(difference > .000001d, "실제 카메라의 표현 on/off 픽셀 차이", ref checks);
                }
                // 실제 소유자의 무변화/지역 광택 플래그를 별도 확인합니다.
                Reset(); selection.TrySelect(CleaningToolId.PolishingCloth); Channels(owner, 0f, 1f);
                var unchanged = owner.SurfaceMask.Revision; var eventsBefore = detailEvents;
                Seed(Point()); Check(owner.SurfaceMask.Revision == unchanged && detailEvents == eventsBefore, "B0/P1 실제 무변화", ref checks);
                Off("B0/P1에는 광택 피드백 없음"); Channels(owner, 0f, 0f); Seed(Point());
                Check(lastDetail.PolishChanged && !lastDetail.DirtChanged && feedback.IsContactVisible, "광택만의 마무리 반응", ref checks);
                Capture("polish-only"); advance(.2f, true, Point()); Off("추가 변화 없으면 접촉 선 수명 종료");

                Reset(); selection.TrySelect(CleaningToolId.SmallBrush); var target = dirt[0];
                var view = camera.WorldToScreenPoint(target.transform.position);
                Check(Physics.Raycast(camera.ScreenPointToRay(view), out var hit, 100f) && hit.collider == owner.GetComponent<Collider>(), "효과가 최근접 Coin Raycast 차단하지 않음", ref checks);
                var pointAtA = Point(CoinSurfaceFace.Front, new Vector2(.5f + pointer.SurfaceLocalToWorldMatrix.inverse.MultiplyPoint3x4(hit.point).x,
                    .5f + pointer.SurfaceLocalToWorldMatrix.inverse.MultiplyPoint3x4(hit.point).z));
                var removedBefore = removedEvents; var contactBefore = objectEvents;
                cleaner.ApplyCleaningAtSurfaceHit(hit, .01f); advance(.016f, true, pointAtA);
                Check(objectEvents == contactBefore + 1 && removedEvents == removedBefore && feedback.IsContactVisible && feedback.ActiveRemovalParticleCount == 0,
                    "A 부분 감소는 솔질만·파편 없음", ref checks);
                cleaner.ApplyCleaningAtSurfaceHit(hit, 10f); advance(.016f, true, pointAtA);
                Check(removedEvents == removedBefore + 1 && feedback.ActiveRemovalParticleCount == 8, "A 실제 제거1회·파편8개", ref checks);
                particles.Simulate(.08f, true, false, false); Capture("brush-removal");
                foreach (var line in lines) line.enabled = false;
                var particleOn = Capture("brush-removal-particles-only"); var particleRenderer = particles.GetComponent<Renderer>();
                try { particleRenderer.enabled = false; particleDifference = Difference(particleOn, Capture("brush-removal-particles-off")); }
                finally { particleRenderer.enabled = true; }
                Check(particleDifference > .0000001d, "제거 파편만의 실제 화면 차이", ref checks);
                cleaner.ApplyCleaningAtSurfaceHit(hit, 10f); Check(removedEvents == removedBefore + 1, "이미 제거된 A 중복 이벤트 없음", ref checks);
                particles.Simulate(.4f, true, false, false); Check(feedback.ActiveRemovalParticleCount == 0, "파편 제한 수명 종료", ref checks);
                dirt[1].ApplyCleaning(dirt[1].CurrentDurability); advance(.016f, true, pointAtA);
                Check(feedback.ActiveRemovalParticleCount == 8, "면 전환 전 실제 제거 파편", ref checks);
                coin.rotation = Quaternion.Euler(180f, 0, 0); Physics.SyncTransforms(); var backContact = Point(CoinSurfaceFace.Back);
                Seed(backContact); Check(feedback.ActiveRemovalParticleCount == 0, "반대면 새 접촉은 이전 제거 파편을 운반하지 않음", ref checks);
                Reset(); selection.TrySelect(CleaningToolId.SmallBrush);

                foreach (var surfaceFirst in new[] { true, false })
                {
                    Reset(); selection.TrySelect(CleaningToolId.PolishingCloth); Channels(owner, 0f, 0f);
                    if (surfaceFirst) CoinSurfaceRenderingValidation.Apply(owner, pointer, pointAtA.Face, pointAtA.Uv, .1f);
                    cleaner.ApplyCleaningAtSurfaceHit(hit, .01f);
                    if (!surfaceFirst) CoinSurfaceRenderingValidation.Apply(owner, pointer, pointAtA.Face, pointAtA.Uv, .1f);
                    advance(.016f, true, pointAtA); var combined = Get<SurfaceCleaningChange>(feedback, "lastChange");
                    Check(combined.DirtChanged && combined.PolishChanged && feedback.IsContactVisible && lines[0].startColor.r > .99f,
                        "같은 프레임 A/표면 양 순서에서 광택 플래그 보존", ref checks);
                }

                foreach (var pose in new[] { new Vector3(35f, 0, 0), new Vector3(180f, 0, 0) })
                foreach (var currentZoom in new[] { 1.6f, 3.2f })
                {
                    Reset(); selection.TrySelect(CleaningToolId.PolishingCloth); coin.rotation = Quaternion.Euler(pose); camera.orthographicSize = currentZoom;
                    Physics.SyncTransforms(); var face = pose.x == 180f ? CoinSurfaceFace.Back : CoinSurfaceFace.Front;
                    var point = Point(face); Seed(point); Geometry(point);
                    Capture((face == CoinSurfaceFace.Back ? "back" : "tilt35") + "-zoom" + currentZoom.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
                    advance(.016f, true, Point(face == CoinSurfaceFace.Front ? CoinSurfaceFace.Back : CoinSurfaceFace.Front)); Off("반대면으로 옛 효과 누출 없음");
                }
                Reset(); selection.TrySelect(CleaningToolId.DryCloth);
                Seed(Point()); advance(.016f, false, Point()); Off("UI/MMB/상실 공통 유효 사용=false에서 즉시 정지");
                advance(.016f, true, Point()); Off("재진입 때 오래된 변화 재생 없음");
                Seed(Point()); advance(.016f, true, default); Off("Side/Invalid 접촉 정지");
                Seed(Point()); pointer.SendMessage("OnApplicationFocus", false, SendMessageOptions.RequireReceiver); Off("Pointer 포커스 이탈 즉시 정지");
                Seed(Point()); feedback.SendMessage("OnApplicationFocus", false, SendMessageOptions.RequireReceiver); Off("표현 포커스 이탈 즉시 정지");
                Seed(Point()); pointer.ResetViewToInitialState(); Off("F/API 위치 이력 제거");
                Seed(Point()); selection.TrySelect(CleaningToolId.SmallBrush); Off("도구 선택 이력 제거");
                Seed(Point()); owner.ResetSurface(); Off("표면 Reset 이벤트 정지");
                Seed(Point()); pointer.enabled = false; Off("Pointer 비활성 즉시 정지"); pointer.enabled = true;
                Seed(Point()); owner.enabled = false; advance(.016f, true, Point()); Off("표면 소유자 비활성 정지"); owner.enabled = true;
                Seed(Point()); cleaner.enabled = false; advance(.016f, true, Point()); Off("A 소유자 비활성 정지"); cleaner.enabled = true;
                var subscriptions = SubscriberCount(owner, "SurfaceContactChanged");
                for (var i = 0; i < 8; i++) { feedback.enabled = false; Off("표현 비활성 정지"); feedback.enabled = true; }
                Check(SubscriberCount(owner, "SurfaceContactChanged") == subscriptions, "8회 재활성 구독 누적 없음", ref checks);
                Reset(); Seed(Point()); Call(feedback, "LateUpdate"); Off("실제 LMB 미사용/포커스 gate 소비"); Capture("blocked");

                var enabledData = Simulate(true); var disabledData = Simulate(false);
                Check(enabledData.SequenceEqual(disabledData), "표현 on/off 전체 B/P/A 상태·이벤트 수 동일", ref checks);
                feedback.enabled = true;
                for (var round = 0; round < 2; round++)
                {
                    Reset(); selection.TrySelect(CleaningToolId.PolishingCloth); Seed(Point());
                    foreach (var item in dirt) item.ApplyCleaning(item.CurrentDurability);
                    advance(.016f, true, Point()); Check(feedback.ActiveRemovalParticleCount == 40, "동시 A5 제거도 파편 상한 안", ref checks);
                    Channels(owner, 0f, 1f); Check(completion.TryCompleteIfEligible(), "합성 완료 경계", ref checks);
                    Off("완료 시 표현/대기 파편 즉시 정지"); var resets = resetEvents;
                    Check(completion.RestartRound() && resetEvents == resets + 1, "재시작1회당 Reset 이벤트1회", ref checks); Off("재시작 초기 표시");
                    Check(owner.Polish == 0f && cleaner.RemovedDirtCount == 0 && !tracker.HasStarted, "2판 후 상태 초기화", ref checks);
                }
                Check(ids.SequenceEqual(lines.Select(x => x.GetInstanceID()).Append(particles.GetInstanceID())) &&
                    materials.SequenceEqual(lines.Select(x => x.sharedMaterial).Append(particles.GetComponent<Renderer>().sharedMaterial)),
                    "동일 선/PS/공유 재질 재사용·런타임 재질 생성 없음", ref checks);
                return $"S2-04a VISUAL PASS checks={checks}, source/detail={sourceEvents}/{detailEvents}, removals={removedEvents}, " +
                    $"rounds=2, effectPixelDifferenceSum={capturedDifference:G7}, particleOnlyDifference={particleDifference:G7}, fixedLines=3 particles<=48, captures=.utmp/s204a-visual-*.png. " +
                    "Actual source events/rendering; synthetic gate/time/poses, not physical UI/MMB/input/performance/build.";
            }
            finally
            {
                owner.CleaningApplied -= Applied; owner.SurfaceContactChanged -= Detail; owner.SurfaceReset -= ResetEvent;
                cleaner.DirtRemoved -= Removed; cleaner.ContactCleaningApplied -= ObjectApplied;
                feedback.enabled = true; owner.enabled = true; cleaner.enabled = true;
                if (completion.IsCompleted) completion.RestartRound(); pointer.enabled = true;
                Reset(); selection.TrySelect(originalTool); coin.rotation = originalRotation; camera.orthographicSize = zoom; camera.aspect = aspect; Physics.SyncTransforms();
            }

            double[] Simulate(bool enabled)
            {
                feedback.enabled = enabled; Reset(); selection.TrySelect(CleaningToolId.SmallBrush);
                var a = sourceEvents; var b = detailEvents; var c = removedEvents; var d = objectEvents;
                for (var i = 0; i < 5; i++) CoinSurfaceRenderingValidation.Apply(owner, pointer, CoinSurfaceFace.Front, new Vector2(.3f + .02f * i, .6f), .1f);
                var target = dirt[0]; var screen = camera.WorldToScreenPoint(target.transform.position);
                Physics.Raycast(camera.ScreenPointToRay(screen), out var hit, 100f); cleaner.ApplyCleaningAtSurfaceHit(hit, 10f);
                var snapshot = new double[owner.SurfaceMask.TexelCount * 4 + dirt.Length + 4]; var cursor = 0;
                foreach (var face in new[] { CoinSurfaceFace.Front, CoinSurfaceFace.Back })
                for (var i = 0; i < owner.SurfaceMask.TexelCount; i++)
                { snapshot[cursor++] = owner.SurfaceMask.GetDirt(face, i); snapshot[cursor++] = owner.SurfaceMask.GetPolish(face, i); }
                foreach (var item in dirt) snapshot[cursor++] = item.CurrentDurability;
                snapshot[cursor++] = sourceEvents - a; snapshot[cursor++] = detailEvents - b; snapshot[cursor++] = removedEvents - c; snapshot[cursor] = objectEvents - d;
                return snapshot;
            }
        }

        public static string BeginDestruction()
        {
            RequirePaused(); if (destructionProbe != null) throw new InvalidOperationException("Probe already pending.");
            var controller = GameObject.Find("CleaningController"); var source = controller.GetComponent<CleaningVisualFeedback>();
            destructionSource = GameObject.Find("Coin/CoinBody").GetComponent<CoinSurfaceContamination>();
            expectedSubscribers = SubscriberCount(destructionSource, "SurfaceContactChanged");
            destructionProbe = UnityEngine.Object.Instantiate(controller.transform.Find("CleaningFeedback").gameObject);
            destructionProbe.name = "FeedbackLifecycleProbe"; destructionProbe.SetActive(false);
            var clone = destructionProbe.AddComponent<CleaningVisualFeedback>();
            var so = new SerializedObject(clone); var original = new SerializedObject(source);
            foreach (var name in new[] { "surfacePointer", "toolSelection", "surfaceContamination", "objectDirtCleaner", "completion" })
                so.FindProperty(name).objectReferenceValue = original.FindProperty(name).objectReferenceValue;
            so.FindProperty("removalParticles").objectReferenceValue = destructionProbe.GetComponentInChildren<ParticleSystem>(true);
            var lines = destructionProbe.GetComponentsInChildren<LineRenderer>(true); var array = so.FindProperty("contactLines"); array.arraySize = 3;
            for (var i = 0; i < lines.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = lines[i];
            so.ApplyModifiedPropertiesWithoutUndo(); destructionObjects = destructionProbe.GetComponentsInChildren<Transform>(true).Select(x => (UnityEngine.Object)x.gameObject).ToArray();
            destructionProbe.SetActive(true);
            if (SubscriberCount(destructionSource, "SurfaceContactChanged") != expectedSubscribers + 1) throw new InvalidOperationException("Probe did not subscribe once.");
            UnityEngine.Object.Destroy(destructionProbe);
            return "S2-04a destruction begun; process two separate Editor Steps, then VerifyDestruction().";
        }

        public static string VerifyDestruction()
        {
            RequirePaused(); if (destructionObjects == null) throw new InvalidOperationException("Begin first.");
            if (destructionObjects.Any(x => x != null) || SubscriberCount(destructionSource, "SurfaceContactChanged") != expectedSubscribers)
                throw new InvalidOperationException("Probe objects or subscriptions remain.");
            destructionObjects = null; destructionSource = null; destructionProbe = null;
            return "S2-04a DESTRUCTION PASS objects=5; actual deferred destruction/subscription cleanup, original graphics preserved.";
        }

        private static void Channels(CoinSurfaceContamination owner, float dirt, float polish)
        {
            foreach (var face in new[] { CoinSurfaceFace.Front, CoinSurfaceFace.Back })
            for (var i = 0; i < owner.SurfaceMask.TexelCount; i++)
            { owner.SurfaceMask.TrySetDirt(face, i, dirt); owner.SurfaceMask.TrySetPolish(face, i, polish); }
            owner.UploadSurfaceMasks();
        }
        private static int SubscriberCount(object owner, string field) =>
            ((Delegate)owner.GetType().GetField(field, Private).GetValue(owner))?.GetInvocationList().Length ?? 0;
        private static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, Private).GetValue(owner);
        private static void Call(object owner, string method) => owner.GetType().GetMethod(method, Private).Invoke(owner, null);
        private static double Difference(Color[] a, Color[] b)
        { var sum = 0d; for (var i = 0; i < a.Length; i++) sum += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b); return sum / (a.Length * 3d); }
        private static void Check(bool condition, string label, ref int checks)
        { if (!condition) throw new InvalidOperationException("S2-04a VISUAL FAIL: " + label); checks++; }
        private static void RequirePaused()
        { if (!Application.isPlaying || !EditorApplication.isPaused || SceneManager.GetActiveScene().path != "Assets/Scenes/CleaningRoom.unity") throw new InvalidOperationException("새 CleaningRoom Play를 Pause하세요."); }
    }
}
