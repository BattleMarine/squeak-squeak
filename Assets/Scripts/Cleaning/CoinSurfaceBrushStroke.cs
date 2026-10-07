using System;
using UnityEngine;

namespace SqueakSqueak.Cleaning
{
    /// <summary>입력 승인된 연속 접촉의 B 제거/지역 광택을 계산합니다. 원본·텍스처·입력은 소유하지 않습니다.</summary>
    public sealed class CoinSurfaceBrushStroke
    {
        public const int MaximumSamples = 256;
        private readonly float sampleSpacingInRadii;
        private CoinSurfaceMaskData previousData;
        private object previousTool;
        private CoinSurfaceContact previousContact;
        private Matrix4x4 previousMatrix;
        private Bounds previousBounds;
        private float previousRadius;
        private float previousRate;
        private float previousPolishRate;
        private long previousRevision;

        public bool HasPreviousContact { get; private set; }
        public bool LastFrameAccepted { get; private set; }
        public int LastSampleCount { get; private set; }

        public CoinSurfaceBrushStroke(float sampleSpacingInRadii = .25f)
        {
            if (!Finite(sampleSpacingInRadii) || sampleSpacingInRadii <= 0f || sampleSpacingInRadii > 1f)
                throw new ArgumentOutOfRangeException(nameof(sampleSpacingInRadii));
            this.sampleSpacingInRadii = sampleSpacingInRadii;
        }

        public void BreakContact()
        {
            HasPreviousContact = false;
            LastFrameAccepted = false;
            LastSampleCount = 0;
            previousData = null;
            previousTool = null;
        }

        public bool Apply(CoinSurfaceMaskData data, CoinSurfaceContact contact, Matrix4x4 matrix,
            Bounds bounds, object toolKey, float worldRadius, float removalPerSecond, float deltaTime, float polishPerSecond = 0f)
        {
            LastFrameAccepted = false;
            LastSampleCount = 0;
            if (data == null || toolKey == null || !contact.IsCleanable || !Finite(deltaTime) || deltaTime <= 0f ||
                !Finite(worldRadius) || worldRadius <= 0f || !Finite(removalPerSecond) || removalPerSecond < 0f ||
                !Finite(polishPerSecond) || polishPerSecond < 0f ||
                !CoinSurfaceMapper.TryMapPoint(matrix, bounds, contact.WorldPosition, contact.WorldNormal, out var current) ||
                current.Face != contact.Face)
            {
                BreakContact();
                return false;
            }
            var continuous = HasPreviousContact && ReferenceEquals(data, previousData) &&
                ReferenceEquals(toolKey, previousTool) && previousContact.Face == current.Face &&
                previousMatrix.Equals(matrix) && previousBounds.Equals(bounds) && previousRadius == worldRadius &&
                previousRate == removalPerSecond && previousPolishRate == polishPerSecond && previousRevision == data.Revision;
            var start = continuous ? previousContact.LocalPosition : current.LocalPosition;
            var distance = matrix.MultiplyVector(current.LocalPosition - start).magnitude;
            var requested = Math.Max(1d, Math.Ceiling(distance / ((double)worldRadius * sampleSpacingInRadii)));
            if (double.IsNaN(requested) || double.IsInfinity(requested) || requested > MaximumSamples)
            {
                BreakContact();
                return false;
            }
            var samples = (int)requested;
            // 전체 프레임 예산을 표본마다 나눕니다. 같은 texel을 여러 번 지나도 예산이 늘지 않습니다.
            var amountPerSample = (double)removalPerSecond * deltaTime / samples;
            var polishPerSample = (double)polishPerSecond * deltaTime / samples;
            var changed = false;
            if (amountPerSample > 0d || polishPerSample > 0d)
                for (var sample = 0; sample < samples; sample++)
                {
                    var center = Vector3.Lerp(start, current.LocalPosition, (sample + .5f) / samples);
                    changed |= ApplyDisk(data, current.Face, matrix, bounds, center, worldRadius, amountPerSample, polishPerSample);
                }
            previousData = data;
            previousTool = toolKey;
            previousContact = current;
            previousMatrix = matrix;
            previousBounds = bounds;
            previousRadius = worldRadius;
            previousRate = removalPerSecond;
            previousPolishRate = polishPerSecond;
            previousRevision = data.Revision;
            HasPreviousContact = true;
            LastFrameAccepted = true;
            LastSampleCount = samples;
            return changed;
        }

        private static bool ApplyDisk(CoinSurfaceMaskData data, CoinSurfaceFace face, Matrix4x4 matrix,
            Bounds bounds, Vector3 center, float radius, double amount, double polishAmount)
        {
            var sx = matrix.MultiplyVector(Vector3.right).magnitude;
            var sz = matrix.MultiplyVector(Vector3.forward).magnitude;
            var localRadius = bounds.extents.x;
            var u = .5d + (center.x - bounds.center.x) / (2d * localRadius);
            var v = .5d + (center.z - bounds.center.z) / (2d * localRadius);
            var halfU = radius / (2d * localRadius * sx);
            var halfV = radius / (2d * localRadius * sz);
            var n = data.Resolution;
            var minimumX = PixelBound((u - halfU) * n - .5d, n, false);
            var maximumX = PixelBound((u + halfU) * n - .5d, n, true);
            var minimumZ = PixelBound((v - halfV) * n - .5d, n, false);
            var maximumZ = PixelBound((v + halfV) * n - .5d, n, true);
            var radiusSquared = (double)radius * radius;
            var changed = false;
            for (var j = minimumZ; j <= maximumZ; j++)
            for (var i = minimumX; i <= maximumX; i++)
            {
                var index = j * n + i;
                if (!data.IsValidTexel(index)) continue;
                var local = bounds.center + new Vector3((2f * (i + .5f) / n - 1f) * localRadius,
                    center.y - bounds.center.y, (2f * (j + .5f) / n - 1f) * localRadius);
                var delta = local - center;
                delta.y = 0f;
                var squared = matrix.MultiplyVector(delta).sqrMagnitude;
                if (!Finite(squared) || squared > radiusSquared * (1d + 1e-5d)) continue;
                // 캡 접평면의 월드 원형 가중치. 경계는0, 중심은1입니다.
                var weight = Math.Max(0d, 1d - squared / radiusSquared);
                weight *= weight;
                var dirtBefore = data.GetDirt(face, index);
                var decrease = amount * weight;
                var remaining = Math.Max(0d, dirtBefore - decrease);
                changed |= data.TrySetDirt(face, index, (float)remaining);
                // 이 표본 시간 동안 선형으로 줄어드는 해당 텍셀 B의 평균입니다.
                // 중간에 B가0이 되면 남은 시간은 효율1로 처리해 주파수별 Euler 편향을 피합니다.
                var meanDirt = decrease > dirtBefore
                    ? dirtBefore * dirtBefore / (2d * decrease) : (dirtBefore + remaining) * .5d;
                var polish = Math.Min(1d, data.GetPolish(face, index) + polishAmount * weight * (1d - meanDirt));
                changed |= data.TrySetPolish(face, index, (float)polish);
            }
            return changed;
        }

        private static int PixelBound(double position, int resolution, bool maximum) =>
            (int)Math.Max(0d, Math.Min(resolution - 1d, maximum ? Math.Ceiling(position) : Math.Floor(position)));

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
