using UnityEngine;

namespace SqueakSqueak.Cleaning
{
    public enum CoinSurfaceFace { Invalid = 0, Front = 1, Back = 2, Side = 3 }

    public readonly struct CoinSurfaceContact
    {
        public CoinSurfaceFace Face { get; }
        public bool IsCleanable => Face == CoinSurfaceFace.Front || Face == CoinSurfaceFace.Back;
        public Vector2 Uv { get; }
        public Vector3 LocalPosition { get; }
        public Vector3 WorldPosition { get; }
        public Vector3 WorldNormal { get; }

        internal CoinSurfaceContact(CoinSurfaceFace face, Vector2 uv, Vector3 localPosition,
            Vector3 worldPosition, Vector3 worldNormal)
        {
            Face = face;
            Uv = uv;
            LocalPosition = localPosition;
            WorldPosition = worldPosition;
            WorldNormal = worldNormal;
        }
    }

    /// <summary>입력/상태 변경 없이 검증된 원통의 접촉을 로컬 평면에 매핑합니다.</summary>
    public static class CoinSurfaceMapper
    {
        private const float DiskTolerance = 1e-5f;

        // 초기화 때만 Mesh를 읽으며 bounds 일치만으로 임의 형상을 허용하지 않습니다.
        public static bool TryGetSupportedBounds(Mesh mesh, out Bounds bounds)
        {
            bounds = default;
            if (mesh == null || !mesh.isReadable || !IsValidBounds(mesh.bounds)) return false;
            var candidate = mesh.bounds;
            var frontVertices = 0;
            var backVertices = 0;
            var sideVertices = 0;
            foreach (var vertex in mesh.vertices)
            {
                if (!IsFinite(vertex)) return false;
                var p = vertex - candidate.center;
                var radius = candidate.extents.x;
                var height = candidate.extents.y;
                var q2 = (p.x * p.x + p.z * p.z) / (radius * radius);
                if (!IsFinite(q2) || q2 > 1f + DiskTolerance || Mathf.Abs(p.y) > height * 1.001f)
                    return false;
                if (Mathf.Abs(p.y - height) <= height * .001f) frontVertices++;
                else if (Mathf.Abs(p.y + height) <= height * .001f) backVertices++;
                else if (Mathf.Abs(q2 - 1f) <= DiskTolerance) sideVertices++;
                else return false;
            }
            // Cylinder 측면 정점도 캡 경계에 있으므로 중간 높이의 측면 정점은 필수가 아닙니다.
            if (frontVertices < 3 || backVertices < 3 || frontVertices + backVertices + sideVertices < 6)
                return false;
            bounds = candidate;
            return true;
        }

        /// <summary>검증/캐시한 Mesh를 유지하는 Collider의 실제 적중만 받습니다.</summary>
        public static bool TryMapHit(MeshCollider target, Mesh expectedMesh, Bounds localBounds,
            RaycastHit hit, out CoinSurfaceContact contact)
        {
            contact = default;
            return target != null && target.enabled && target.gameObject.activeInHierarchy &&
                expectedMesh != null && target.sharedMesh == expectedMesh && hit.collider == target &&
                TryMapPoint(target.transform.localToWorldMatrix, localBounds, hit.point, hit.normal, out contact);
        }

        /// <summary>검증된 원판 Geometry의 순수 변환입니다. 가시성/입력 허가는 호출자가 관리합니다.</summary>
        public static bool TryMapPoint(Matrix4x4 matrix, Bounds bounds, Vector3 worldPoint,
            Vector3 worldNormal, out CoinSurfaceContact contact)
        {
            contact = default;
            if (!IsValidBounds(bounds) || !IsSupportedTransform(matrix) || !IsFinite(worldPoint) ||
                !IsFinite(worldNormal) || !IsFinite(worldNormal.sqrMagnitude) || worldNormal.sqrMagnitude <= 1e-12f)
                return false;
            var inverse = matrix.inverse;
            var localPoint = inverse.MultiplyPoint3x4(worldPoint);
            var localNormal = matrix.transpose.MultiplyVector(worldNormal).normalized;
            if (!IsFinite(localPoint) || !IsFinite(localNormal) || localNormal.sqrMagnitude < .5f) return false;
            var relative = localPoint - bounds.center;
            var radius = bounds.extents.x;
            var height = bounds.extents.y;
            var q = new Vector2(relative.x / radius, relative.z / radius);
            if (!IsFinite(q.sqrMagnitude) || q.sqrMagnitude > 1f + DiskTolerance ||
                Mathf.Abs(relative.y) > height * 1.001f) return false;
            if (Mathf.Abs(localNormal.y) < .999f)
            {
                contact = new CoinSurfaceContact(CoinSurfaceFace.Side, default, default, worldPoint, worldNormal.normalized);
                return true;
            }
            var sign = localNormal.y > 0f ? 1f : -1f;
            if (Mathf.Abs(relative.y - sign * height) > height * .001f) return false;
            if (q.sqrMagnitude > 1f) q.Normalize();
            var projected = bounds.center + new Vector3(q.x * radius, sign * height, q.y * radius);
            var projectedWorld = matrix.MultiplyPoint3x4(projected);
            var capNormal = inverse.transpose.MultiplyVector(Vector3.up * sign).normalized;
            if (!IsFinite(projectedWorld) || !IsFinite(capNormal) || capNormal.sqrMagnitude < .5f) return false;
            contact = new CoinSurfaceContact(sign > 0f ? CoinSurfaceFace.Front : CoinSurfaceFace.Back,
                (q + Vector2.one) * .5f, projected, projectedWorld, capNormal);
            return true;
        }

        public static bool IsWithinBrush(Matrix4x4 matrix, CoinSurfaceContact center,
            CoinSurfaceContact candidate, float worldRadius)
        {
            if (!center.IsCleanable || !candidate.IsCleanable || center.Face != candidate.Face ||
                !IsFinite(worldRadius) || worldRadius <= 0f || !IsSupportedTransform(matrix)) return false;
            var delta = candidate.LocalPosition - center.LocalPosition;
            delta.y = 0f;
            var distanceSquared = matrix.MultiplyVector(delta).sqrMagnitude;
            // float 변환/회전에서 정확한 경계가 바깥으로 밀리는 수치 오차만 허용합니다.
            return IsFinite(distanceSquared) && (double)distanceSquared <=
                (double)worldRadius * worldRadius * (1d + DiskTolerance);
        }

        public static bool IsSupportedTransform(Matrix4x4 matrix)
        {
            for (var i = 0; i < 16; i++) if (!IsFinite(matrix[i])) return false;
            if (matrix.m30 != 0f || matrix.m31 != 0f || matrix.m32 != 0f || matrix.m33 != 1f) return false;
            var x = matrix.MultiplyVector(Vector3.right);
            var y = matrix.MultiplyVector(Vector3.up);
            var z = matrix.MultiplyVector(Vector3.forward);
            var sx = x.magnitude; var sy = y.magnitude; var sz = z.magnitude;
            var minimum = Mathf.Min(sx, Mathf.Min(sy, sz));
            var maximum = Mathf.Max(sx, Mathf.Max(sy, sz));
            if (minimum < 1e-4f || maximum > 1e4f || maximum / minimum > 1e4f) return false;
            x /= sx; y /= sy; z /= sz;
            return Mathf.Abs(Vector3.Dot(x, y)) <= 1e-4f && Mathf.Abs(Vector3.Dot(x, z)) <= 1e-4f &&
                Mathf.Abs(Vector3.Dot(y, z)) <= 1e-4f && Vector3.Dot(Vector3.Cross(x, y), z) > .9999f;
        }

        private static bool IsValidBounds(Bounds bounds) => IsFinite(bounds.center) && IsFinite(bounds.extents) &&
            bounds.extents.x > 0f && bounds.extents.y > 0f &&
            Mathf.Abs(bounds.extents.x - bounds.extents.z) <= 1e-5f * bounds.extents.x;

        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
