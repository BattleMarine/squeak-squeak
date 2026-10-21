using System;
using UnityEngine;

namespace SqueakSqueak.Cleaning
{
    public readonly struct CleaningRubSample
    {
        public CoinSurfaceContact Start { get; }
        public CoinSurfaceContact End { get; }
        public float EffectiveSeconds { get; }
        public long FrameId { get; }
        public bool IsRubbing => EffectiveSeconds > 0f;
        public CleaningRubSample(CoinSurfaceContact start, CoinSurfaceContact end, float seconds, long frameId)
        { Start = start; End = end; EffectiveSeconds = seconds; FrameId = frameId; }
    }

    /// <summary>한 프레임의 이동 예산을 비소모성으로 공유합니다. 정지 시간은 누적하지 않습니다.</summary>
    public sealed class CleaningRubMotion
    {
        public const float DefaultMinimumDistance = .001f;
        public const float DefaultReferenceSpeed = .6f;
        private bool hasPrevious;
        private long previousFrame = -1;
        private object previousTool, previousSurface;
        private CoinSurfaceContact previousContact, pendingStart;
        private Matrix4x4 previousMatrix;
        private float previousMinimum, previousSpeed;
        private double pendingDistance, pendingSeconds;
        private CleaningRubSample cached;

        public void BreakContact()
        {
            hasPrevious = false; previousFrame = -1;
            pendingDistance = pendingSeconds = 0;
            previousTool = previousSurface = null;
            cached = default;
        }

        public CleaningRubSample Sample(long frame, bool eligible, CoinSurfaceContact contact, Matrix4x4 matrix,
            object tool, object surface, float deltaTime, float minimumDistance = DefaultMinimumDistance,
            float referenceSpeed = DefaultReferenceSpeed)
        {
            if (frame == previousFrame) return cached;
            if (!eligible || !contact.IsCleanable || tool == null || surface == null ||
                !float.IsFinite(deltaTime) || deltaTime <= 0 || !float.IsFinite(minimumDistance) || minimumDistance <= 0 ||
                !float.IsFinite(referenceSpeed) || referenceSpeed <= 0)
            { BreakContact(); previousFrame = frame; return default; }
            var continuous = hasPrevious && frame == previousFrame + 1 && ReferenceEquals(tool, previousTool) &&
                ReferenceEquals(surface, previousSurface) && contact.Face == previousContact.Face && matrix.Equals(previousMatrix) &&
                previousMinimum == minimumDistance && previousSpeed == referenceSpeed;
            cached = default;
            if (!continuous)
            {
                pendingDistance = pendingSeconds = 0;
                pendingStart = contact;
            }
            else
            {
                var distance = contact.Face==CoinSurfaceFace.Side?CoinSideSurface.Distance(matrix,previousContact,contact):matrix.MultiplyVector(contact.LocalPosition - previousContact.LocalPosition).magnitude;
                if (!float.IsFinite(distance)) { BreakContact(); previousFrame = frame; return default; }
                if (distance > 0)
                {
                    pendingDistance += distance;
                    // 기준 속도 이하: 거리 기반, 그 이상: 해당 이동 프레임 dt까지만 허용합니다.
                    pendingSeconds += Math.Min(distance / (double)referenceSpeed, deltaTime);
                    if (pendingDistance >= minimumDistance &&
                        matrix.MultiplyVector(contact.LocalPosition - pendingStart.LocalPosition).sqrMagnitude > 0)
                    {
                        cached = new CleaningRubSample(pendingStart, contact, (float)pendingSeconds, frame);
                        pendingDistance = pendingSeconds = 0;
                        pendingStart = contact;
                    }
                }
            }
            hasPrevious = true; previousFrame = frame; previousContact = contact; previousMatrix = matrix;
            previousTool = tool; previousSurface = surface; previousMinimum = minimumDistance; previousSpeed = referenceSpeed;
            return cached;
        }
    }
}
