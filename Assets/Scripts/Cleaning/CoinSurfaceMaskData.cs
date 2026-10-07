using System;

namespace SqueakSqueak.Cleaning
{
    public enum CoinSurfaceDirtPattern { FixedPatches = 0, Uniform = 1 }

    /// <summary>양면 B 오염의 CPU 원본입니다. 텍스처·입력·씬 수명은 소유하지 않습니다.</summary>
    public sealed class CoinSurfaceMaskData
    {
        public const int DefaultResolution = 256;
        public const int MinimumResolution = 4;
        public const int MaximumResolution = 1024;
        private readonly bool[] validTexels;
        private readonly float[][] initialDirt;
        private readonly float[][] currentDirt;
        private readonly double[] initialSums = new double[2];
        private readonly double[] currentSums = new double[2];

        public int Resolution { get; }
        public int TexelCount => validTexels.Length;
        public int ValidTexelCount { get; private set; }
        public float InitialIntensity { get; }
        public CoinSurfaceDirtPattern Pattern { get; }
        public long Revision { get; private set; }
        public double InitialDirtSum => initialSums[0] + initialSums[1];
        public double CurrentDirtSum => currentSums[0] + currentSums[1];
        public float RemovalRatio => InitialDirtSum == 0d ? 1f :
            (float)Math.Max(0d, Math.Min(1d, 1d - CurrentDirtSum / InitialDirtSum));

        public CoinSurfaceMaskData(int resolution = DefaultResolution, float initialIntensity = 1f,
            CoinSurfaceDirtPattern pattern = CoinSurfaceDirtPattern.FixedPatches)
        {
            if (resolution < MinimumResolution || resolution > MaximumResolution)
                throw new ArgumentOutOfRangeException(nameof(resolution));
            if (!IsFinite(initialIntensity) || initialIntensity < 0f || initialIntensity > 1f)
                throw new ArgumentOutOfRangeException(nameof(initialIntensity));
            if (pattern != CoinSurfaceDirtPattern.FixedPatches && pattern != CoinSurfaceDirtPattern.Uniform)
                throw new ArgumentOutOfRangeException(nameof(pattern));
            Resolution = resolution;
            InitialIntensity = initialIntensity;
            Pattern = pattern;
            validTexels = new bool[resolution * resolution];
            initialDirt = new[] { new float[TexelCount], new float[TexelCount] };
            currentDirt = new[] { new float[TexelCount], new float[TexelCount] };
            for (var j = 0; j < resolution; j++)
            for (var i = 0; i < resolution; i++)
            {
                var index = j * resolution + i;
                var x = 2d * (i + .5d) / resolution - 1d;
                var z = 2d * (j + .5d) / resolution - 1d;
                if (x * x + z * z > 1d) continue;
                validTexels[index] = true;
                ValidTexelCount++;
                for (var face = 0; face < 2; face++)
                {
                    var coverage = pattern == CoinSurfaceDirtPattern.Uniform ? 1d : FixedCoverage(face, x, z);
                    var value = (float)(coverage * initialIntensity);
                    initialDirt[face][index] = value;
                    initialSums[face] += value;
                }
            }
            Reset();
        }

        public bool IsValidTexel(int index) => index >= 0 && index < TexelCount && validTexels[index];

        public float GetDirt(CoinSurfaceFace face, int index)
        {
            if (index < 0 || index >= TexelCount) throw new ArgumentOutOfRangeException(nameof(index));
            return currentDirt[FaceIndex(face)][index];
        }

        public double GetInitialDirtSum(CoinSurfaceFace face) => initialSums[FaceIndex(face)];
        public double GetCurrentDirtSum(CoinSurfaceFace face) => currentSums[FaceIndex(face)];

        /// <summary>텍셀 단위 데이터 변경만 수행합니다. 접촉·시간·브러시 규칙은 후속 청소 경로의 책임입니다.</summary>
        public bool TrySetDirt(CoinSurfaceFace face, int index, float value)
        {
            if ((face != CoinSurfaceFace.Front && face != CoinSurfaceFace.Back) ||
                !IsValidTexel(index) || !IsFinite(value)) return false;
            value = Math.Max(0f, Math.Min(1f, value));
            var faceIndex = FaceIndex(face);
            var previous = currentDirt[faceIndex][index];
            if (previous == value) return false;
            currentDirt[faceIndex][index] = value;
            currentSums[faceIndex] += (double)value - previous;
            Revision++;
            return true;
        }

        public void Reset()
        {
            for (var face = 0; face < 2; face++)
            {
                Array.Copy(initialDirt[face], currentDirt[face], TexelCount);
                currentSums[face] = initialSums[face];
            }
            Revision++;
        }

        private static double FixedCoverage(int face, double x, double z) => face == 0
            ? Math.Max(Patch(x, z, -.32d, .22d, .72d, .25d), Patch(x, z, .42d, -.38d, .28d, .45d))
            : Math.Max(Patch(x, z, .28d, -.2d, .64d, .25d), Patch(x, z, -.4d, .32d, .38d, .45d));

        private static double Patch(double x, double z, double cx, double cz, double radius, double edge)
        {
            var distance = ((x - cx) * (x - cx) + (z - cz) * (z - cz)) / (radius * radius);
            return distance >= 1d ? 0d : edge + (1d - edge) * (1d - distance);
        }

        private static int FaceIndex(CoinSurfaceFace face) => face switch
        {
            CoinSurfaceFace.Front => 0,
            CoinSurfaceFace.Back => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(face))
        };

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
