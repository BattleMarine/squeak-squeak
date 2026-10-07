using System;
using UnityEditor;
using UnityEngine;
using SqueakSqueak.Cleaning;

namespace SqueakSqueak.EditorValidation
{
    /// <summary>지역 광택의 독립 수치 검사입니다. 입력 승인과 플레이 시간은 증명하지 않습니다.</summary>
    public static class CoinSurfacePolishValidation
    {
        private static readonly Bounds Bounds = new(Vector3.zero, new Vector3(1f, 2f, 1f));
        private static readonly Matrix4x4 Matrix = Matrix4x4.Scale(new Vector3(2f, .1f, 2f));
        private static readonly object Tool = new();

        [MenuItem("Squeak Squeak/Validation/S2-03a Regional Polish Data")]
        private static void Menu() => Debug.Log(Run());

        public static string Run()
        {
            var checks = 0; var maxHz = 0d; var maxAnalytic = 0d;
            foreach (var initial in new[] { 0f, .4f, 1f })
            {
                var data = new CoinSurfaceMaskData(65, .4f, CoinSurfaceDirtPattern.Uniform, initial);
                var initialSum = data.InitialDirtSum; var index = 32 * 65 + 32;
                Check(data.MeanPolish == initial && data.GetPolish(CoinSurfaceFace.Front, 0) == 0f,
                    "초기 광택/원판 밖 제외", ref checks);
                data.TrySetPolish(CoinSurfaceFace.Front, index, 1f - initial);
                Check(Math.Abs(data.CurrentPolishSum - IndependentSum(data)) < 1e-9 && data.InitialDirtSum == initialSum,
                    "양면 합 원본/고정 B 분모", ref checks);
                var revision = data.Revision;
                Check(!data.TrySetPolish(CoinSurfaceFace.Side, index, .2f) && !data.TrySetPolish(CoinSurfaceFace.Front, 0, .2f) &&
                    !data.TrySetPolish(CoinSurfaceFace.Back, index, float.NaN) && data.Revision == revision,
                    "Side/원판 밖/비유한 값 쓰기 거부", ref checks);
                data.TrySetPolish(CoinSurfaceFace.Front, index, 2f);
                Check(data.GetPolish(CoinSurfaceFace.Front, index) == 1f, "상한 clamp", ref checks);
                data.TrySetPolish(CoinSurfaceFace.Front, index, -1f);
                Check(data.GetPolish(CoinSurfaceFace.Front, index) == 0f, "하한 clamp", ref checks);
                data.Reset();
                Check(data.MeanPolish == initial && data.CurrentDirtSum == initialSum,
                    "B/광택 함께 Reset", ref checks);
            }
            foreach (var dirt in new[] { 0f, .1f, .4f, 1f })
            foreach (var face in new[] { CoinSurfaceFace.Front, CoinSurfaceFace.Back })
            {
                var data = new CoinSurfaceMaskData(65, dirt, CoinSurfaceDirtPattern.Uniform);
                var stroke = new CoinSurfaceBrushStroke();
                var point = Contact(Matrix, face, new Vector2(.5f, .5f));
                Check(stroke.Apply(data, point, Matrix, Bounds, Tool, .2f, .2f, 1f, .3f), "지역 Hold 변화", ref checks);
                var valid = true; var sum = 0d;
                for (var j = 0; j < 65; j++)
                for (var i = 0; i < 65; i++)
                {
                    var index = j * 65 + i;
                    var x = 2d * (i + .5d) / 65d - 1d; var z = 2d * (j + .5d) / 65d - 1d;
                    var weight = Math.Pow(Math.Max(0d, 1d - (x * x + z * z) / (.2d * .2d)), 2);
                    var decrease = .2d * weight;
                    var mean = decrease > dirt ? dirt * dirt / (2d * decrease) : (dirt + Math.Max(0d, dirt - decrease)) * .5d;
                    var expected = data.IsValidTexel(index) ? .3d * weight * (1d - mean) : 0d;
                    maxAnalytic = Math.Max(maxAnalytic, Math.Abs(expected - data.GetPolish(face, index)));
                    sum += expected;
                    valid &= data.GetPolish(face == CoinSurfaceFace.Front ? CoinSurfaceFace.Back : CoinSurfaceFace.Front, index) == 0f;
                }
                Check(valid && maxAnalytic < 2e-6 && Math.Abs(data.MeanPolish - sum / (2d * data.ValidTexelCount)) < 2e-7,
                    "해당 텍셀 B 시간적분 효율·양면 면적 평균·반대면 보존", ref checks);
                stroke.Apply(data, point, Matrix, Bounds, Tool, .2f, .2f, 10000f, .3f);
                Check(data.MeanPolish < .03f && data.GetPolish(face, 32 * 65 + 32) == 1f,
                    "한 점 포화는 전체 광택 완료 아님", ref checks);
            }
            foreach (var mode in new[] { 0, 1, 2 })
            {
                var reference = Simulate(120, mode);
                foreach (var hz in new[] { 30, 60 })
                {
                    var actual = Simulate(hz, mode); var error = 0d;
                    for (var i = 0; i < actual.TexelCount; i++)
                    {
                        error = Math.Max(error, Math.Abs(actual.GetPolish(CoinSurfaceFace.Front, i) - reference.GetPolish(CoinSurfaceFace.Front, i)));
                        error = Math.Max(error, Math.Abs(actual.GetDirt(CoinSurfaceFace.Front, i) - reference.GetDirt(CoinSurfaceFace.Front, i)));
                    }
                    maxHz = Math.Max(maxHz, error);
                    Check(error < .001 && Math.Abs(actual.MeanPolish - reference.MeanPolish) < .0001,
                        "30/60/120Hz Hold/Drag/빠른 Drag 동일 경로·시간", ref checks);
                }
            }
            var left = Contact(Matrix, CoinSurfaceFace.Front, new Vector2(.3f, .5f));
            var right = Contact(Matrix, CoinSurfaceFace.Front, new Vector2(.7f, .5f));
            var coarse = new CoinSurfaceMaskData(128, 0f); var fine = new CoinSurfaceMaskData(128, 0f);
            var a = new CoinSurfaceBrushStroke(.25f); var b = new CoinSurfaceBrushStroke(.125f);
            a.Apply(coarse, left, Matrix, Bounds, Tool, .18f, 0f, 1e-6f, .3f);
            b.Apply(fine, left, Matrix, Bounds, Tool, .18f, 0f, 1e-6f, .3f);
            a.Apply(coarse, right, Matrix, Bounds, Tool, .18f, 0f, .2f, .3f);
            b.Apply(fine, right, Matrix, Bounds, Tool, .18f, 0f, .2f, .3f);
            Check(b.LastSampleCount > a.LastSampleCount && Math.Abs(coarse.MeanPolish - fine.MeanPolish) < 1e-5,
                "표본 수 증가도 전체 광택 시간 예산 유지", ref checks);
            var budget = true;
            for (var i = 0; i < fine.TexelCount; i++) budget &= fine.GetPolish(CoinSurfaceFace.Front, i) <= .060001f;
            Check(budget, "텍셀별 광택 시간 예산 상한", ref checks);
            foreach (var reason in new[] { "Break", "Tool", "Face", "Reset", "Transform", "Rate", "Invalid" })
            {
                var data = new CoinSurfaceMaskData(128, 0f); var stroke = new CoinSurfaceBrushStroke();
                stroke.Apply(data, left, Matrix, Bounds, Tool, .18f, 0f, .1f, .3f);
                var matrix = Matrix; var key = Tool; var face = CoinSurfaceFace.Front; var rate = .3f;
                switch (reason)
                {
                    case "Break": stroke.BreakContact(); break;
                    case "Tool": key = new object(); break;
                    case "Face": face = CoinSurfaceFace.Back; break;
                    case "Reset": data.Reset(); break;
                    case "Transform": matrix = Matrix4x4.TRS(Vector3.up, Quaternion.Euler(45f, 0f, 0f), new Vector3(2f, .1f, 2f)); break;
                    case "Rate": rate = .4f; break;
                    case "Invalid": stroke.Apply(data, right, Matrix, Bounds, Tool, .18f, 0f, float.NaN, .3f); break;
                }
                stroke.Apply(data, Contact(matrix, face, right.Uv), matrix, Bounds, key, .18f, 0f, .1f, rate);
                Check(stroke.LastSampleCount == 1 && data.GetPolish(face, 64 * 128 + 64) == 0f,
                    reason + " 후 광택 연결선 없음", ref checks);
            }
            foreach (var invalid in new[] { -1f, float.NaN, float.PositiveInfinity })
            {
                var data = new CoinSurfaceMaskData(); var revision = data.Revision;
                Check(!new CoinSurfaceBrushStroke().Apply(data, left, Matrix, Bounds, Tool, .2f, .2f, .1f, invalid) &&
                    data.Revision == revision, "잘못된 광택 속도 거부", ref checks);
            }
            var rejected = new CoinSurfaceMaskData(); var before = rejected.Revision;
            CoinSurfaceMapper.TryMapPoint(Matrix, Bounds, Matrix.MultiplyPoint3x4(Vector3.zero), Vector3.right, out var side);
            Check(!new CoinSurfaceBrushStroke().Apply(rejected, side, Matrix, Bounds, Tool, .2f, .2f, .1f, .3f) &&
                !new CoinSurfaceBrushStroke().Apply(rejected, default, Matrix, Bounds, Tool, .2f, .2f, .1f, .3f) && rejected.Revision == before,
                "Side/Invalid은 B/광택 모두 거부", ref checks);
            return $"S2-03a POLISH DATA PASS checks={checks}, maxAnalyticError={maxAnalytic:G7}, maxHzChannelError={maxHz:G7}, sampleCounts={a.LastSampleCount}/{b.LastSampleCount}. CPU APIs, not input/time tuning.";
        }

        private static CoinSurfaceMaskData Simulate(int hz, int mode)
        {
            var data = new CoinSurfaceMaskData(128, .4f, CoinSurfaceDirtPattern.Uniform);
            var stroke = new CoinSurfaceBrushStroke();
            Vector2 Position(float t) => mode == 0 ? new Vector2(.5f, .5f) : new Vector2(.2f + .6f * t, .5f);
            stroke.Apply(data, Contact(Matrix, CoinSurfaceFace.Front, Position(0f)), Matrix, Bounds, Tool, .18f, .2f, 1e-6f, .3f);
            var seconds = mode == 2 ? .5f : 2f; var frames = (int)(hz * seconds);
            for (var frame = 1; frame <= frames; frame++)
                stroke.Apply(data, Contact(Matrix, CoinSurfaceFace.Front, Position(frame / (float)frames)), Matrix, Bounds, Tool, .18f, .2f, 1f / hz, .3f);
            return data;
        }
        private static CoinSurfaceContact Contact(Matrix4x4 matrix, CoinSurfaceFace face, Vector2 uv)
        {
            var local = new Vector3(uv.x - .5f, face == CoinSurfaceFace.Front ? 1f : -1f, uv.y - .5f);
            if (!CoinSurfaceMapper.TryMapPoint(matrix, Bounds, matrix.MultiplyPoint3x4(local),
                matrix.inverse.transpose.MultiplyVector(face == CoinSurfaceFace.Front ? Vector3.up : Vector3.down), out var contact))
                throw new InvalidOperationException("접촉 생성 실패");
            return contact;
        }
        private static double IndependentSum(CoinSurfaceMaskData data)
        {
            var sum = 0d;
            foreach (var face in new[] { CoinSurfaceFace.Front, CoinSurfaceFace.Back })
                for (var i = 0; i < data.TexelCount; i++) if (data.IsValidTexel(i)) sum += data.GetPolish(face, i);
            return sum;
        }
        private static void Check(bool condition, string name, ref int checks)
        {
            if (!condition) throw new InvalidOperationException("S2-03a POLISH FAIL: " + name);
            checks++;
        }
    }
}
