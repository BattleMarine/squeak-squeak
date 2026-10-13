using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using SqueakSqueak.Cleaning;

namespace SqueakSqueak.EditorValidation
{
    public static class CoinSurfaceBrushValidation
    {
        private static readonly Bounds Bounds = new(Vector3.zero, new Vector3(1f, 2f, 1f));
        private static readonly Matrix4x4 Matrix = Matrix4x4.Scale(new Vector3(2f, .1f, 2f));
        private static readonly object Tool = new();

        [MenuItem("Squeak Squeak/Validation/S2-02b Brush Data")]
        private static void DataFromMenu() => Debug.Log(RunData());

        [MenuItem("Squeak Squeak/Validation/S2-02b Brush Integration (Paused Play Mode)")]
        private static void IntegrationFromMenu() => Debug.Log(RunIntegration());

        public static string RunData()
        {
            var checks = 0;
            var maximumHoldError = 0d;
            var maximumFrequencyPixelError = 0d;
            var maximumFrequencyRatioError = 0d;
            foreach (var matrix in new[] { Matrix, Matrix4x4.Scale(new Vector3(3f, .1f, 1f)),
                Matrix4x4.TRS(new Vector3(4f, 2f, -3f), Quaternion.Euler(37f, 21f, 63f), new Vector3(3f, .1f, 1f)) })
            foreach (var face in new[] { CoinSurfaceFace.Front, CoinSurfaceFace.Back })
            foreach (var radius in new[] { .32f, .18f, .28f })
            {
                var data = Uniform();
                var stroke = new CoinSurfaceBrushStroke();
                var point = Contact(matrix, face, new Vector2(64.5f / 128f, 64.5f / 128f));
                var revision = data.Revision;
                Check(!stroke.Apply(data, point, matrix, Bounds, Tool, radius, .4f, .5f) &&
                    !stroke.Apply(data, point, matrix, Bounds, Tool, radius, .4f, 10f) && data.Revision == revision,
                    "최초 클릭/정지 Hold 변화0", ref checks);
                var from = Contact(matrix, face, point.Uv - new Vector2(.000001f,0));
                var to = Contact(matrix, face, point.Uv + new Vector2(.000001f,0));
                Check(stroke.ApplyApprovedSweep(data, from, to, matrix, Bounds, Tool, radius, .4f, .5f),
                    "승인된 미소 구간의 커널 적분(입력 예산 검사는 별도)", ref checks);
                var valid = true;
                var opposite = face == CoinSurfaceFace.Front ? CoinSurfaceFace.Back : CoinSurfaceFace.Front;
                var sx = matrix.MultiplyVector(Vector3.right).magnitude;
                var sz = matrix.MultiplyVector(Vector3.forward).magnitude;
                for (var j = 0; j < 128; j++)
                for (var i = 0; i < 128; i++)
                {
                    var index = j * 128 + i;
                    // 지원 행렬에서 X/Z 축 거리의 독립 기준입니다.
                    var dx = ((i + .5d) / 128d - point.Uv.x) * sx;
                    var dz = ((j + .5d) / 128d - point.Uv.y) * sz;
                    var weight = Math.Max(0d, 1d - (dx * dx + dz * dz) / ((double)radius * radius));
                    var expected = data.IsValidTexel(index) ? 1d - .2d * weight * weight : 0d;
                    maximumHoldError = Math.Max(maximumHoldError, Math.Abs(expected - data.GetDirt(face, index)));
                    valid &= data.GetDirt(opposite, index) == (data.IsValidTexel(index) ? 1f : 0f);
                }
                Check(maximumHoldError < 2e-6 && valid && stroke.LastSampleCount == 1,
                    "세 도구/앞뒤/배율 월드 반경·가중치·반대면/원판 밖 보존", ref checks);
            }
            foreach (var mode in new[] { 0, 1, 2 }) // Hold / 일반 Drag / 빠른 Drag
            {
                var reference = Simulate(120, mode);
                foreach (var hz in new[] { 30, 60 })
                {
                    var actual = Simulate(hz, mode);
                    var pixelError = 0d;
                    for (var index = 0; index < actual.TexelCount; index++)
                        pixelError = Math.Max(pixelError, Math.Abs(actual.GetDirt(CoinSurfaceFace.Front, index) -
                            reference.GetDirt(CoinSurfaceFace.Front, index)));
                    var ratioError = Math.Abs(actual.RemovalRatio - reference.RemovalRatio);
                    maximumFrequencyPixelError = Math.Max(maximumFrequencyPixelError, pixelError);
                    maximumFrequencyRatioError = Math.Max(maximumFrequencyRatioError, ratioError);
                    Check(pixelError <= .01d && ratioError <= .01d, "30/60/120Hz 같은 시간/경로 1%p", ref checks);
                }
            }
            var coarse = Uniform(); var fine = Uniform();
            var a = new CoinSurfaceBrushStroke(.25f); var b = new CoinSurfaceBrushStroke(.125f);
            var left = Contact(Matrix, CoinSurfaceFace.Front, new Vector2(.3f, .5f));
            var right = Contact(Matrix, CoinSurfaceFace.Front, new Vector2(.7f, .5f));
            a.Apply(coarse, left, Matrix, Bounds, Tool, .18f, .5f, 1e-6f);
            b.Apply(fine, left, Matrix, Bounds, Tool, .18f, .5f, 1e-6f);
            a.Apply(coarse, right, Matrix, Bounds, Tool, .18f, .5f, .2f);
            b.Apply(fine, right, Matrix, Bounds, Tool, .18f, .5f, .2f);
            var sampleRatioError = Math.Abs(coarse.RemovalRatio - fine.RemovalRatio);
            Check(b.LastSampleCount > a.LastSampleCount && sampleRatioError < 1e-5d, "표본 증가로 제거량 증가 없음", ref checks);
            var budgetValid = true;
            for (var index = 0; index < fine.TexelCount; index++)
                if (fine.IsValidTexel(index)) budgetValid &= fine.GetDirt(CoinSurfaceFace.Front, index) >= .899999f;
            Check(budgetValid, "텍셀별 프레임 시간 예산 상한", ref checks);
            foreach (var interruption in new[] { "명시 단절", "도구", "면", "Reset", "변환", "반경", "속도", "잘못된 dt" })
            {
                var data = Uniform(); var stroke = new CoinSurfaceBrushStroke();
                stroke.Apply(data, left, Matrix, Bounds, Tool, .18f, .5f, .1f);
                var matrix = Matrix;
                var face = CoinSurfaceFace.Front;
                var key = Tool;
                var radius = .18f; var rate = .5f;
                switch (interruption)
                {
                    case "명시 단절": stroke.BreakContact(); break;
                    case "도구": key = new object(); break;
                    case "면": face = CoinSurfaceFace.Back; break;
                    case "Reset": data.Reset(); break;
                    case "변환": matrix = Matrix4x4.TRS(Vector3.up, Quaternion.Euler(45f, 0f, 0f), new Vector3(2f, .1f, 2f)); break;
                    case "반경": radius = .2f; break;
                    case "속도": rate = .4f; break;
                    case "잘못된 dt": stroke.Apply(data, right, Matrix, Bounds, Tool, .18f, .5f, float.NaN); break;
                }
                var point = Contact(matrix, face, right.Uv);
                stroke.Apply(data, point, matrix, Bounds, key, radius, rate, .1f);
                Check(stroke.LastSampleCount == 0 && data.GetDirt(face, 64 * 128 + 64) == 1f,
                    interruption + " 후 재진입 연결선 없음", ref checks);
            }
            foreach (var invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                var data = Uniform(); var stroke = new CoinSurfaceBrushStroke(); var revision = data.Revision;
                Check(!stroke.Apply(data, left, Matrix, Bounds, Tool, .18f, .5f, invalid) &&
                    !stroke.HasPreviousContact && data.Revision == revision, "잘못된 dt 거부", ref checks);
                Check(!stroke.Apply(data, left, Matrix, Bounds, Tool, invalid, .5f, .1f) && data.Revision == revision,
                    "잘못된 반경 거부", ref checks);
            }
            foreach (var invalidRate in new[] { -1f, float.NaN, float.PositiveInfinity })
                Check(!new CoinSurfaceBrushStroke().Apply(Uniform(), left, Matrix, Bounds, Tool, .18f, invalidRate, .1f),
                    "잘못된 제거 속도 거부", ref checks);
            var rejected = Uniform(); var rejectStroke = new CoinSurfaceBrushStroke(); var initialRevision = rejected.Revision;
            CoinSurfaceMapper.TryMapPoint(Matrix, Bounds, Matrix.MultiplyPoint3x4(Vector3.zero), Vector3.right, out var side);
            var shear = Matrix; shear.m01 = .2f;
            foreach (var invalidMatrix in new[] { shear, Matrix4x4.Scale(new Vector3(-2f, .1f, 2f)), default(Matrix4x4) })
                Check(!rejectStroke.Apply(rejected, left, invalidMatrix, Bounds, Tool, .18f, .5f, .1f), "지원 밖 변환 거부", ref checks);
            Check(!rejectStroke.Apply(rejected, default, Matrix, Bounds, Tool, .18f, .5f, .1f) &&
                !rejectStroke.Apply(rejected, side, Matrix, Bounds, Tool, .18f, .5f, .1f) &&
                !rejectStroke.Apply(rejected, left, Matrix, Bounds, null, .18f, .5f, .1f) && rejected.Revision == initialRevision,
                "Invalid/Side/null 도구 쓰기 없음", ref checks);
            rejectStroke.Apply(rejected, left, Matrix, Bounds, Tool, 1e-6f, .5f, .1f);
            var revisionBeforeJump = rejected.Revision;
            Check(!rejectStroke.Apply(rejected, right, Matrix, Bounds, Tool, 1e-6f, .5f, .1f) &&
                !rejectStroke.LastFrameAccepted && !rejectStroke.HasPreviousContact && rejected.Revision == revisionBeforeJump,
                "과도한 표본 프레임 거부/단절", ref checks);
            return $"S2-02b DATA PASS checks={checks}, maxHoldError={maximumHoldError:G7}, " +
                $"maxHzPixelError={maximumFrequencyPixelError:G7}, maxHzRatioError={maximumFrequencyRatioError:G7}, " +
                $"sampleCounts={a.LastSampleCount}/{b.LastSampleCount}, sampleRatioError={sampleRatioError:G7}. CPU API only.";
        }

        public static string RunIntegration()
        {
            if (!Application.isPlaying || !EditorApplication.isPaused ||
                SceneManager.GetActiveScene().path != "Assets/Scenes/CleaningRoom.unity")
                throw new InvalidOperationException("새 CleaningRoom Play를 Pause한 뒤 실행하세요.");
            var owner = GameObject.Find("Coin/CoinBody").GetComponent<CoinSurfaceContamination>();
            var pointer = GameObject.Find("CleaningController").GetComponent<CoinSurfacePointer>();
            var selection = pointer.GetComponent<CleaningToolSelection>();
            var tracker = pointer.GetComponent<CleaningProgressTracker>();
            var apply = typeof(CoinSurfaceContamination).GetMethod("ApplySurfaceContact", BindingFlags.Instance | BindingFlags.NonPublic);
            var clear = typeof(CoinSurfacePointer).GetMethod("ClearSurfaceHit", BindingFlags.Instance | BindingFlags.NonPublic);
            var originalTool = selection.CurrentTool;
            var checks = 0; var events = 0;
            void OnApplied() => events++;
            CoinSurfaceContact Point() => Contact(pointer.SurfaceLocalToWorldMatrix, CoinSurfaceFace.Front, new Vector2(.5f, .5f));
            void Seed() => apply.Invoke(owner, new object[] { Point(), .1f });
            owner.CleaningApplied += OnApplied;
            try
            {
                Check(owner.SurfaceMask != null && !tracker.HasStarted, "참조/Awake·새 판", ref checks);
                var scalar = owner.SurfaceDirt;
                var back = owner.SurfaceMask.GetCurrentDirtSum(CoinSurfaceFace.Back);
                Seed();
                Check(owner.HasSurfaceContactHistory && events == 1 && tracker.HasStarted &&
                    owner.SurfaceMask.CurrentDirtSum < owner.SurfaceMask.InitialDirtSum &&
                    owner.SurfaceMask.GetCurrentDirtSum(CoinSurfaceFace.Back) == back && owner.SurfaceDirt < scalar &&
                    owner.SurfaceDirtRemovalRatio == owner.SurfaceMask.RemovalRatio,
                    "실제 소유자 해당 면만 감소·이벤트1회·마스크 기반 집계", ref checks);
                var pixels = owner.FrontDirtMask.GetPixels32();
                Check(Math.Abs(pixels[128 * 256 + 128].r / 255d -
                    owner.SurfaceMask.GetDirt(CoinSurfaceFace.Front, 128 * 256 + 128)) <= 1d / 510d + 1e-7,
                    "실제 마스크 텍스처 갱신", ref checks);
                clear.Invoke(pointer, null);
                Check(!owner.HasSurfaceContactHistory, "동전 밖/UI/MMB/Pointer Reset 공통 무효화 연결", ref checks);
                Seed(); pointer.SendMessage("OnApplicationFocus", false, SendMessageOptions.RequireReceiver);
                Check(!owner.HasSurfaceContactHistory, "포커스 이탈 즉시 단절", ref checks);
                Seed(); pointer.enabled = false;
                Check(!owner.HasSurfaceContactHistory, "Pointer 비활성 단절", ref checks);
                pointer.enabled = true;
                Seed(); selection.TrySelect(selection.GetToolAt(1));
                Check(!owner.HasSurfaceContactHistory, "도구 선택 즉시 단절", ref checks);
                Seed(); owner.enabled = false;
                Check(!owner.HasSurfaceContactHistory, "소유자 비활성 단절", ref checks);
                owner.enabled = true;
                Seed(); pointer.ResetViewToInitialState();
                Check(!owner.HasSurfaceContactHistory, "F/API 자세 초기화 단절", ref checks);
                Seed(); owner.ResetSurface();
                Check(!owner.HasSurfaceContactHistory && owner.SurfaceMask.CurrentDirtSum == owner.SurfaceMask.InitialDirtSum,
                    "판 Reset 복원/단절", ref checks);
                var revision = owner.SurfaceMask.Revision;
                Check(!owner.ApplyCleaning(.1f) && owner.SurfaceMask.Revision == revision && !owner.HasSurfaceContactHistory,
                    "위치 없는 공개 API도 LMB 미사용 또는 포커스 gate를 거침", ref checks);
                return $"S2-02b INTEGRATION PASS checks={checks}. Actual owner/event/texture/lifecycle APIs; physical input not tested.";
            }
            finally
            {
                owner.CleaningApplied -= OnApplied;
                pointer.enabled = true;
                owner.enabled = true;
                selection.TrySelect(originalTool);
                owner.ResetSurface();
                tracker.ResetProgress();
                pointer.ResetViewToInitialState();
            }
        }

        private static CoinSurfaceMaskData Simulate(int hz, int mode)
        {
            var data = Uniform(); var stroke = new CoinSurfaceBrushStroke();
            var duration = mode == 2 ? .5f : 2f;
            var radius = mode == 2 ? .18f : .28f;
            var start = mode == 0 ? .5f : .2f;
            stroke.Apply(data, Contact(Matrix, CoinSurfaceFace.Front, new Vector2(start, .5f)), Matrix, Bounds,
                Tool, radius, .35f, 1e-6f);
            for (var frame = 1; frame <= (int)(duration * hz); frame++)
            {
                var t = frame / (duration * hz);
                var uv = new Vector2(mode == 0 ? .5f : Mathf.Lerp(.2f, .8f, t), .5f);
                stroke.Apply(data, Contact(Matrix, CoinSurfaceFace.Front, uv), Matrix, Bounds, Tool, radius, .35f, 1f / hz);
            }
            return data;
        }

        private static CoinSurfaceMaskData Uniform() => new(128, 1f, CoinSurfaceDirtPattern.Uniform);

        private static CoinSurfaceContact Contact(Matrix4x4 matrix, CoinSurfaceFace face, Vector2 uv)
        {
            var sign = face == CoinSurfaceFace.Front ? 1f : -1f;
            var point = new Vector3(uv.x - .5f, sign, uv.y - .5f);
            if (!CoinSurfaceMapper.TryMapPoint(matrix, Bounds, matrix.MultiplyPoint3x4(point),
                matrix.inverse.transpose.MultiplyVector(Vector3.up * sign), out var contact))
                throw new InvalidOperationException("검증 기준 접촉 생성 실패");
            return contact;
        }

        private static void Check(bool condition, string name, ref int checks)
        {
            if (!condition) throw new InvalidOperationException("S2-02b FAIL: " + name);
            checks++;
        }
    }
}
