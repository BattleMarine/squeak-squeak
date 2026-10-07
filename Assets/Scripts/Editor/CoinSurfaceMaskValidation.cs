using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using SqueakSqueak.Cleaning;

namespace SqueakSqueak.EditorValidation
{
    /// <summary>CPU 회귀 검사와 실제 Play Mode 소유 자원 해제 검사를 분리합니다.</summary>
    public static class CoinSurfaceMaskValidation
    {
        private static readonly List<Texture2D> releasedTextures = new();
        private static readonly List<GameObject> destroyedOwners = new();
        private static int expectedTextureCount;

        [MenuItem("Squeak Squeak/Validation/S2-02a Mask Data (Edit Mode)")]
        private static void DataFromMenu() => Debug.Log(RunData());

        [MenuItem("Squeak Squeak/Validation/S2-02a Mask Lifecycle Begin (Paused Play Mode)")]
        private static void LifecycleFromMenu() => Debug.Log(BeginLifecycle());

        [MenuItem("Squeak Squeak/Validation/S2-02a Mask Lifecycle Verify (After 2 Steps)")]
        private static void CleanupFromMenu() => Debug.Log(VerifyLifecycleCleanup());

        public static string RunData()
        {
            var checks = 0;
            var texels = 0;
            foreach (var resolution in new[] { 4, 17, 256 })
            foreach (var pattern in new[] { CoinSurfaceDirtPattern.Uniform, CoinSurfaceDirtPattern.FixedPatches })
            foreach (var intensity in new[] { 0f, .4f, 1f })
            {
                var data = new CoinSurfaceMaskData(resolution, intensity, pattern);
                var initial = new[] { new float[data.TexelCount], new float[data.TexelCount] };
                var validCount = 0;
                var allValid = true;
                var valuesCorrect = true;
                var sums = new double[2];
                var differ = false;
                var hasZero = false;
                var hasDirt = false;
                for (var index = 0; index < data.TexelCount; index++)
                {
                    // 정수 격자식은 구현의 정규화 double 판정과 독립적인 기대값입니다.
                    var x = 2L * (index % resolution) + 1L - resolution;
                    var z = 2L * (index / resolution) + 1L - resolution;
                    var valid = x * x + z * z <= (long)resolution * resolution;
                    allValid &= data.IsValidTexel(index) == valid;
                    if (valid) validCount++;
                    for (var face = 0; face < 2; face++)
                    {
                        var id = face == 0 ? CoinSurfaceFace.Front : CoinSurfaceFace.Back;
                        var value = data.GetDirt(id, index);
                        initial[face][index] = value;
                        sums[face] += value;
                        valuesCorrect &= !float.IsNaN(value) && value >= 0f && value <= intensity && (valid || value == 0f);
                        if (pattern == CoinSurfaceDirtPattern.Uniform) valuesCorrect &= value == (valid ? intensity : 0f);
                        if (valid) { hasZero |= value == 0f; hasDirt |= value > 0f; }
                    }
                    differ |= initial[0][index] != initial[1][index];
                    texels++;
                }
                Check(allValid && validCount == data.ValidTexelCount, "독립 원판 유효영역", ref checks);
                Check(valuesCorrect, "0/부분/전체 초기 범위와 원판 밖 0", ref checks);
                Check(sums[0] == data.GetInitialDirtSum(CoinSurfaceFace.Front) &&
                    sums[1] == data.GetInitialDirtSum(CoinSurfaceFace.Back) &&
                    sums[0] + sums[1] == data.CurrentDirtSum, "유효영역 초기/현재 합", ref checks);
                Check(data.RemovalRatio == (intensity == 0f ? 1f : 0f), "0 분모/판 시작 제거율", ref checks);
                if (pattern == CoinSurfaceDirtPattern.FixedPatches && intensity > 0f)
                    Check(differ && hasZero && hasDirt, "서로 다른 재현 가능한 패치", ref checks);
                var indexToChange = (resolution / 2) * resolution + resolution / 2;
                var initialSum = data.InitialDirtSum;
                var backSum = data.GetCurrentDirtSum(CoinSurfaceFace.Back);
                var frontBefore = data.GetCurrentDirtSum(CoinSurfaceFace.Front);
                var oldValue = data.GetDirt(CoinSurfaceFace.Front, indexToChange);
                data.TrySetDirt(CoinSurfaceFace.Front, indexToChange, -2f);
                Check(data.GetDirt(CoinSurfaceFace.Front, indexToChange) == 0f &&
                    Math.Abs(data.GetCurrentDirtSum(CoinSurfaceFace.Front) - (frontBefore - oldValue)) < 1e-10,
                    "0 clamp와 증분 합", ref checks);
                data.TrySetDirt(CoinSurfaceFace.Front, indexToChange, 2f);
                Check(data.GetDirt(CoinSurfaceFace.Front, indexToChange) == 1f &&
                    data.GetCurrentDirtSum(CoinSurfaceFace.Back) == backSum && data.InitialDirtSum == initialSum,
                    "1 clamp/반대면 독립/분모 불변", ref checks);
                var revision = data.Revision;
                Check(!data.TrySetDirt(CoinSurfaceFace.Front, indexToChange, 1f) &&
                    !data.TrySetDirt(CoinSurfaceFace.Side, indexToChange, 0f) &&
                    !data.TrySetDirt(CoinSurfaceFace.Invalid, indexToChange, 0f) &&
                    !data.TrySetDirt(CoinSurfaceFace.Front, -1, 0f) &&
                    !data.TrySetDirt(CoinSurfaceFace.Back, data.TexelCount, 1f) &&
                    !data.TrySetDirt(CoinSurfaceFace.Front, 0, 1f) &&
                    !data.TrySetDirt(CoinSurfaceFace.Front, indexToChange, float.NaN) &&
                    !data.TrySetDirt(CoinSurfaceFace.Front, indexToChange, float.PositiveInfinity) && data.Revision == revision,
                    "잘못된 면/인덱스/원판 밖/비유한 값/중복 쓰기 거부", ref checks);
                data.Reset();
                var same = true;
                foreach (var face in new[] { CoinSurfaceFace.Front, CoinSurfaceFace.Back })
                    for (var index = 0; index < data.TexelCount; index++)
                        same &= data.GetDirt(face, index) == initial[face == CoinSurfaceFace.Front ? 0 : 1][index];
                Check(same && data.CurrentDirtSum == initialSum && data.InitialDirtSum == initialSum, "Reset 비트 동일/분모 복원", ref checks);
                var recreated = new CoinSurfaceMaskData(resolution, intensity, pattern);
                same = true;
                foreach (var face in new[] { CoinSurfaceFace.Front, CoinSurfaceFace.Back })
                    for (var index = 0; index < data.TexelCount; index++)
                        same &= data.GetDirt(face, index) == recreated.GetDirt(face, index);
                Check(same && recreated.InitialDirtSum == initialSum, "새 판 재생성 동일성", ref checks);
                for (var index = 0; index < data.TexelCount; index++)
                    if (data.IsValidTexel(index)) data.TrySetDirt(CoinSurfaceFace.Front, index, 0f);
                Check(data.GetCurrentDirtSum(CoinSurfaceFace.Front) == 0d &&
                    Math.Abs(data.RemovalRatio - (initialSum == 0d ? 1d : sums[0] / initialSum)) < 1e-6,
                    "한 면 제거의 동일 면적 가중치", ref checks);
                for (var index = 0; index < data.TexelCount; index++)
                    if (data.IsValidTexel(index)) data.TrySetDirt(CoinSurfaceFace.Back, index, 0f);
                Check(data.CurrentDirtSum == 0d && data.RemovalRatio == 1f && data.InitialDirtSum == initialSum,
                    "양면 전체 제거/고정 분모", ref checks);
            }
            foreach (var resolution in new[] { 3, 1025 })
                ExpectArgument(() => new CoinSurfaceMaskData(resolution), ref checks);
            foreach (var intensity in new[] { -1f, 2f, float.NaN, float.PositiveInfinity })
                ExpectArgument(() => new CoinSurfaceMaskData(16, intensity), ref checks);
            ExpectArgument(() => new CoinSurfaceMaskData(16, 1f, (CoinSurfaceDirtPattern)99), ref checks);
            var sample = new CoinSurfaceMaskData();
            Check(sample.Resolution == 256 && sample.ValidTexelCount == 51468, "기본256² 원판 texel 수", ref checks);
            return $"S2-02a DATA PASS checks={checks}, initialTexels={texels}, defaultValidPerFace={sample.ValidTexelCount}. CPU API only.";
        }

        public static string BeginLifecycle()
        {
            RequirePausedPlay();
            if (releasedTextures.Count != 0) throw new InvalidOperationException("이전 Begin의 Step/Verify를 먼저 완료하세요.");
            var owner = GameObject.Find("Coin/CoinBody").GetComponent<CoinSurfaceContamination>();
            var checks = 0;
            expectedTextureCount = OwnedTextureCount();
            Check(expectedTextureCount == 2 && owner.SurfaceMask != null, "Awake 양면 생성", ref checks);
            Check(owner.SurfaceDirtRemovalRatio == 0f && owner.SurfaceDirt > 0f && owner.Polish == 0f &&
                !GameObject.Find("CleaningController").GetComponent<CleaningProgressTracker>().HasStarted,
                "청소 전 새 Play에서 실행", ref checks);
            var scalar = owner.SurfaceDirt;
            var initialSum = owner.SurfaceMask.InitialDirtSum;
            var frontId = owner.FrontDirtMask.GetInstanceID();
            var backId = owner.BackDirtMask.GetInstanceID();
            Check(frontId != backId && owner.SurfaceMask.Resolution == 256, "독립 기본 텍스처", ref checks);
            ValidateTextures(owner, ref checks);
            for (var i = 0; i < 3; i++) owner.ResetSurface();
            Check(frontId == owner.FrontDirtMask.GetInstanceID() && backId == owner.BackDirtMask.GetInstanceID() &&
                owner.SurfaceMask.InitialDirtSum == initialSum, "반복 Reset 자원 재사용/초기 합", ref checks);
            var changedIndex = 128 * 256 + 128;
            owner.SurfaceMask.TrySetDirt(CoinSurfaceFace.Front, changedIndex, 0f);
            owner.UploadSurfaceMasks();
            ValidateTextures(owner, ref checks);
            Check(owner.SurfaceDirt < scalar && owner.SurfaceDirtRemovalRatio > 0f && owner.Polish == 0f,
                "마스크 변경에서 파생된 잔여 평균/제거율과 광택 분리", ref checks);
            owner.enabled = false;
            owner.enabled = true;
            Check(frontId == owner.FrontDirtMask.GetInstanceID(), "Disable/Enable 자원 유지", ref checks);
            for (var i = 0; i < 3; i++)
            {
                releasedTextures.Add(owner.FrontDirtMask);
                releasedTextures.Add(owner.BackDirtMask);
                owner.ReleaseSurfaceMasks();
                owner.ReleaseSurfaceMasks();
                Check(owner.SurfaceMask == null && owner.FrontDirtMask == null && owner.BackDirtMask == null,
                    "Release 멱등/참조 해제", ref checks);
                owner.ResetSurface();
                Check(owner.SurfaceMask.InitialDirtSum == initialSum, "Release 후 새 판 재생성", ref checks);
                ValidateTextures(owner, ref checks);
            }
            // 실제 OnDestroy를 검사할 소유자만 생성합니다. Collider·씬 에셋은 복제하지 않습니다.
            foreach (var pattern in new[] { CoinSurfaceDirtPattern.Uniform, CoinSurfaceDirtPattern.FixedPatches })
            foreach (var intensity in new[] { 0f, .4f, 1f })
            {
                var temporary = new GameObject("S2-02a Validation Owner");
                temporary.SetActive(false);
                try
                {
                    var renderer = temporary.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = owner.GetComponent<Renderer>().sharedMaterial;
                    var clone = temporary.AddComponent<CoinSurfaceContamination>();
                    var original = new SerializedObject(owner);
                    var serialized = new SerializedObject(clone);
                    foreach (var field in new[] { "surfacePointer", "toolSelection" })
                        serialized.FindProperty(field).objectReferenceValue = original.FindProperty(field).objectReferenceValue;
                    serialized.FindProperty("coinRenderer").objectReferenceValue = renderer;
                    serialized.FindProperty("initialSurfaceDirt").floatValue = intensity;
                    serialized.FindProperty("surfaceDirtPattern").enumValueIndex = (int)pattern;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    temporary.SetActive(true);
                    Check(clone.SurfaceMask != null && clone.SurfaceMask.InitialIntensity == intensity &&
                        clone.SurfaceMask.Pattern == pattern && Math.Abs(clone.SurfaceDirt -
                            clone.SurfaceMask.CurrentDirtSum / (2d * clone.SurfaceMask.ValidTexelCount)) < 1e-7,
                        "동적 소유자 Awake 0/부분/전체 설정", ref checks);
                    Check(clone.SurfaceDirtRemovalRatio == (intensity == 0f ? 1f : 0f), "소유자 마스크 초기 분모", ref checks);
                    ValidateTextures(clone, ref checks);
                    var cloneFront = clone.FrontDirtMask;
                    var cloneSum = clone.SurfaceMask.InitialDirtSum;
                    clone.ResetSurface();
                    Check(clone.FrontDirtMask == cloneFront && clone.SurfaceMask.CurrentDirtSum == cloneSum,
                        "0/부분/전체 소유자 Reset", ref checks);
                    releasedTextures.Add(clone.FrontDirtMask);
                    releasedTextures.Add(clone.BackDirtMask);
                    destroyedOwners.Add(temporary);
                }
                finally { UnityEngine.Object.Destroy(temporary); }
            }
            return $"S2-02a LIFECYCLE BEGIN PASS checks={checks}, pendingTextures={releasedTextures.Count}. Step twice, then VerifyLifecycleCleanup().";
        }

        public static string VerifyLifecycleCleanup()
        {
            RequirePausedPlay();
            if (releasedTextures.Count == 0) throw new InvalidOperationException("BeginLifecycle()을 먼저 실행하세요.");
            var checks = 0;
            foreach (var texture in releasedTextures)
                Check(texture == null, "Destroy 큐의 실제 텍스처 해제", ref checks);
            foreach (var owner in destroyedOwners)
                Check(owner == null, "실제 OnDestroy 소유자 해제", ref checks);
            Check(OwnedTextureCount() == expectedTextureCount, "재생성/파괴 후 자원 누적 없음", ref checks);
            var count = releasedTextures.Count;
            releasedTextures.Clear();
            destroyedOwners.Clear();
            return $"S2-02a LIFECYCLE CLEANUP PASS checks={checks}, releasedTextures={count}, remainingOwnedTextures={expectedTextureCount}.";
        }

        private static void ValidateTextures(CoinSurfaceContamination owner, ref int checks)
        {
            foreach (var face in new[] { CoinSurfaceFace.Front, CoinSurfaceFace.Back })
            {
                var texture = face == CoinSurfaceFace.Front ? owner.FrontDirtMask : owner.BackDirtMask;
                Check(texture.width == 256 && texture.height == 256 && texture.mipmapCount == 1 &&
                    texture.format == TextureFormat.RGBA32 && texture.wrapMode == TextureWrapMode.Clamp &&
                    texture.filterMode == FilterMode.Point && texture.isReadable, "Texture 계약", ref checks);
                var pixels = texture.GetPixels32();
                var same = true;
                for (var index = 0; index < pixels.Length; index++)
                    same &= Math.Abs(pixels[index].r / 255d - owner.SurfaceMask.GetDirt(face, index)) <= 1d / 510d + 1e-7 &&
                        pixels[index].g == 0 && pixels[index].b == 0 &&
                        pixels[index].a == (owner.SurfaceMask.IsValidTexel(index) ? 255 : 0);
                Check(same, "CPU 파생 R8 양자화/원판 alpha", ref checks);
            }
        }

        private static int OwnedTextureCount()
        {
            var count = 0;
            foreach (var texture in Resources.FindObjectsOfTypeAll<Texture2D>())
                if (texture.name.StartsWith("SqueakSqueak.BMask.", StringComparison.Ordinal)) count++;
            return count;
        }

        private static void RequirePausedPlay()
        {
            if (!Application.isPlaying || !EditorApplication.isPaused ||
                SceneManager.GetActiveScene().path != "Assets/Scenes/CleaningRoom.unity")
                throw new InvalidOperationException("새 CleaningRoom Play를 Pause한 뒤 실행하세요.");
        }

        private static void ExpectArgument(Action action, ref int checks)
        {
            try { action(); }
            catch (ArgumentOutOfRangeException) { checks++; return; }
            throw new InvalidOperationException("잘못된 설정 거부 실패");
        }

        private static void Check(bool condition, string name, ref int checks)
        {
            if (!condition) throw new InvalidOperationException("S2-02a FAIL: " + name);
            checks++;
        }
    }
}
