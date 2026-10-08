using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;
using UnityEngine.SceneManagement;
using SqueakSqueak.Cleaning;

namespace SqueakSqueak.EditorValidation
{
    /// <summary>Editor 전용 S2-06a 검사. 합성 접촉/gate이며 실제 입력·Player 성능 판정이 아닙니다.</summary>
    public static class CleaningIntegratedValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string Output = ".utmp/s206a-performance.txt";
        private const int Samples = 300, Warmup = 60, Capacity = 4096;
        private static CoinSurfaceContamination owner;
        private static CleaningToolSelection selection;
        private static CoinSurfacePointer pointer;
        private static CleaningProgressTracker tracker;
        private static CleaningAudioFeedback audio;
        private static CleaningVisualFeedback visual;
        private static Func<CoinSurfaceContact, float, bool> apply;
        private static Action<float, bool, CoinSurfaceContact> advanceAudio, advanceVisual;
        private static CoinSurfaceContact[] contacts;
        private static bool originalBackground, originalFocus, live;
        private static CleaningToolDefinition originalTool;
        private static int lastFrame, warmFrames, sampleCount, changed, skippedFrames;
        private static double captureStart, deadline;
        private static double[] frameMs, methodMs, methodBytes, frameGc;
        private static ProfilerRecorder gcRecorder;
        private static string resourceBaseline;
        public static string LiveResult { get; private set; } = "Not run";

        private static void Setup()
        {
            if (!Application.isPlaying || !EditorApplication.isPaused || live ||
                SceneManager.GetActiveScene().path != "Assets/Scenes/CleaningRoom.unity")
                throw new InvalidOperationException("Initialized, paused CleaningRoom required; no concurrent probe.");
            var c = GameObject.Find("CleaningController");
            owner = GameObject.Find("Coin/CoinBody").GetComponent<CoinSurfaceContamination>();
            selection = c.GetComponent<CleaningToolSelection>(); pointer = c.GetComponent<CoinSurfacePointer>();
            tracker = c.GetComponent<CleaningProgressTracker>(); audio = c.GetComponent<CleaningAudioFeedback>();
            visual = c.GetComponent<CleaningVisualFeedback>();
            if (owner.SurfaceMask == null || c.GetComponent<CleaningCompletionController>().IsCompleted || tracker.HasStarted)
                throw new InvalidOperationException("Fresh initialized round required.");
            originalTool = selection.CurrentTool;
            originalFocus = (bool)typeof(CleaningAudioFeedback).GetField("hasFocus", Private).GetValue(audio);
            apply = (Func<CoinSurfaceContact, float, bool>)typeof(CoinSurfaceContamination)
                .GetMethod("ApplySurfaceContact", Private).CreateDelegate(typeof(Func<CoinSurfaceContact, float, bool>), owner);
            advanceAudio = (Action<float, bool, CoinSurfaceContact>)typeof(CleaningAudioFeedback)
                .GetMethod("AdvanceAudio", Private).CreateDelegate(typeof(Action<float, bool, CoinSurfaceContact>), audio);
            advanceVisual = (Action<float, bool, CoinSurfaceContact>)typeof(CleaningVisualFeedback)
                .GetMethod("AdvanceFeedback", Private).CreateDelegate(typeof(Action<float, bool, CoinSurfaceContact>), visual);
        }

        // 접촉 생성·리플렉션·자원 조회·배열 할당은 측정 구간 밖에서만 수행합니다.
        private static CoinSurfaceContact[] Path(int mode)
        {
            var result = new CoinSurfaceContact[512]; var bounds = pointer.SurfaceLocalBounds;
            var matrix = pointer.SurfaceLocalToWorldMatrix;
            for (var i = 0; i < result.Length; i++)
            {
                var sign = i < 256 ? 1f : -1f; var j = i % 256;
                var uv = mode == 0 ? new Vector2(.5f, .5f) : new Vector2(
                    .2f + .6f * (mode == 2 ? (j % 2) : ((j / 16 % 2 == 0 ? j % 16 : 15 - j % 16) / 15f)),
                    .2f + .6f * (j / 16) / 15f);
                var local = bounds.center + new Vector3((uv.x * 2 - 1) * bounds.extents.x,
                    sign * bounds.extents.y, (uv.y * 2 - 1) * bounds.extents.z);
                if (!CoinSurfaceMapper.TryMapPoint(matrix, bounds, matrix.MultiplyPoint3x4(local),
                    matrix.inverse.transpose.MultiplyVector(Vector3.up * sign), out result[i]))
                    throw new InvalidOperationException("Synthetic contact mapping failed");
            }
            return result;
        }

        private static bool Work(CoinSurfaceContact contact)
        {
            var didChange = apply(contact, 1f / 60f);
            advanceVisual(1f / 60f, true, contact);
            advanceAudio(1f / 60f, true, contact);
            return didChange;
        }

        public static string RunMethods()
        {
            Setup(); var report = new StringBuilder(EnvironmentReport());
            var baseline = ResourcesAndSubscriptions();
            try
            {
                typeof(CleaningAudioFeedback).GetField("hasFocus", Private).SetValue(audio, true);
                foreach (var tool in new[] { CleaningToolId.DryCloth, CleaningToolId.SmallBrush, CleaningToolId.PolishingCloth })
                for (var mode = 0; mode < 3; mode++)
                {
                    selection.TrySelect(tool); owner.ResetSurface(); tracker.ResetProgress(); contacts = Path(mode);
                    for (var i = 0; i < Warmup; i++) Work(contacts[i]);
                    // Reset은 측정 밖에서 수행해 각 도구/경로 측정이 새 표면에서 시작하도록 맞춥니다.
                    owner.ResetSurface(); tracker.ResetProgress();
                    var times = new double[Samples]; var bytes = new double[Samples]; var mutations = 0;
                    var gen0 = GC.CollectionCount(0); var gen1 = GC.CollectionCount(1); var gen2 = GC.CollectionCount(2);
                    for (var i = 0; i < Samples; i++)
                    {
                        var contact = contacts[i % contacts.Length];
                        var allocated = GC.GetAllocatedBytesForCurrentThread(); var tick = Stopwatch.GetTimestamp();
                        var modified = Work(contact);
                        var elapsed = Stopwatch.GetTimestamp() - tick;
                        bytes[i] = GC.GetAllocatedBytesForCurrentThread() - allocated;
                        times[i] = elapsed * 1000d / Stopwatch.Frequency;
                        if (modified) mutations++;
                    }
                    report.AppendLine($"method {tool}/{(mode == 0 ? "Hold" : mode == 1 ? "Drag" : "FastDrag")}: warmup={Warmup}, n={Samples}, changed={mutations}, ms {Stats(times, Samples)}, threadBytes {Stats(bytes, Samples)}, collections={GC.CollectionCount(0)-gen0}/{GC.CollectionCount(1)-gen1}/{GC.CollectionCount(2)-gen2}");
                }
                RestoreRound();
                var after = ResourcesAndSubscriptions();
                if (after != baseline) throw new InvalidOperationException("Method probe resource/subscriber mismatch: " + baseline + " => " + after);
                report.AppendLine("method resources/subscribers stable: " + after);
                Directory.CreateDirectory(".utmp"); File.WriteAllText(Output, report.ToString());
                return report.ToString();
            }
            finally { RestoreRound(); }
        }

        public static string RunRepeat()
        {
            Setup(); var baseline = ResourcesAndSubscriptions(); var completed = 0; var restarted = 0;
            var c = selection.GetComponent<CleaningCompletionController>();
            var presentation = c.GetComponent<CleaningCompletionPresentation>();
            void Completed() => completed++; void Restarted() => restarted++;
            c.RoundCompleted += Completed; c.RoundRestarted += Restarted;
            try
            {
                for (var round = 0; round < 2; round++)
                {
                    selection.TrySelect(CleaningToolId.PolishingCloth); contacts = Path(1);
                    if (!Work(contacts[0]) || !tracker.HasStarted || owner.Polish <= 0)
                        throw new InvalidOperationException("Actual contact owner/change/timer path failed");
                    // 완료 진행만 합성. 실제 controller 이벤트·연출·재시작 경로를 사용합니다.
                    CleaningCompletionPresentationValidation.Complete();
                    if (presentation.Phase != CompletionPresentationPhase.Showing || !c.IsCompleted || !tracker.IsTimingStopped)
                        throw new InvalidOperationException("Completion integration failed");
                    if (round == 1) typeof(CleaningCompletionPresentation).GetMethod("AdvancePresentation", Private)
                        .Invoke(presentation, new object[] { 1.8f });
                    if (!c.RestartRound() || c.RestartRound() || presentation.Phase != CompletionPresentationPhase.Idle ||
                        tracker.HasStarted || owner.Polish != 0 || owner.SurfaceMask.CurrentDirtSum != owner.SurfaceMask.InitialDirtSum ||
                        visual.IsContactVisible || visual.ActiveRemovalParticleCount != 0)
                        throw new InvalidOperationException("Restart/reset/residual integration failed");
                    foreach (var source in c.GetComponentsInChildren<AudioSource>(true))
                        if (source.isPlaying || source.volume != 0) throw new InvalidOperationException("Restart audio residual");
                }
            }
            finally { c.RoundCompleted -= Completed; c.RoundRestarted -= Restarted; RestoreRound(); }
            var after = ResourcesAndSubscriptions();
            if (completed != 2 || restarted != 2 || after != baseline)
                throw new InvalidOperationException("Repeat event/resource/subscriber mismatch");
            return "S2-06a REPEAT PASS rounds=2, completed/restarted=2/2; real owner/events, synthetic gate/completion/time; resources/subscribers stable: " + after;
        }

        public static string RunCostBreakdown()
        {
            Setup(); var report = new StringBuilder();
            try
            {
                selection.TrySelect(CleaningToolId.DryCloth); contacts = Path(2);
                var tool = selection.CurrentTool; var matrix = pointer.SurfaceLocalToWorldMatrix;
                var bounds = pointer.SurfaceLocalBounds;
                var data = new CoinSurfaceMaskData(owner.SurfaceMask.Resolution, owner.SurfaceMask.InitialIntensity,
                    owner.SurfaceMask.Pattern, owner.SurfaceMask.InitialPolish);
                var stroke = new CoinSurfaceBrushStroke();
                for (var i = 0; i < Warmup; i++) stroke.Apply(data, contacts[i], matrix, bounds, tool,
                    tool.BrushRadius, tool.SurfaceDirtRemovalPerSecond, 1f/60f, tool.PolishGainPerSecond);
                data.Reset(); stroke.BreakContact(); var times = new double[Samples];
                var bytes = new double[Samples]; var sumSamples = 0; var maxSamples = 0;
                for (var i = 0; i < Samples; i++)
                {
                    var allocated = GC.GetAllocatedBytesForCurrentThread(); var tick = Stopwatch.GetTimestamp();
                    stroke.Apply(data, contacts[i], matrix, bounds, tool, tool.BrushRadius,
                        tool.SurfaceDirtRemovalPerSecond, 1f/60f, tool.PolishGainPerSecond);
                    times[i] = (Stopwatch.GetTimestamp()-tick)*1000d/Stopwatch.Frequency;
                    bytes[i] = GC.GetAllocatedBytesForCurrentThread()-allocated;
                    sumSamples += stroke.LastSampleCount; maxSamples = Math.Max(maxSamples, stroke.LastSampleCount);
                }
                report.AppendLine($"breakdown DryCloth/FastDrag CPU stroke only: warmup={Warmup}, n={Samples}, radius={tool.BrushRadius}, spacing=.25 radii, interpolatedSamples mean={sumSamples/(double)Samples:F3}, max={maxSamples}; ms {Stats(times,Samples)}, threadBytes {Stats(bytes,Samples)}");
                // 한 유효 텍셀을 측정 밖에서 교대로 변경해 Revision/업로드가 실제 진행되게 합니다.
                var index = owner.SurfaceMask.Resolution/2*owner.SurfaceMask.Resolution+owner.SurfaceMask.Resolution/2;
                for (var i = 0; i < Warmup; i++)
                { owner.SurfaceMask.TrySetDirt(CoinSurfaceFace.Front,index,i%2==0?0f:1f); owner.UploadSurfaceMasks(); }
                for (var i = 0; i < Samples; i++)
                {
                    owner.SurfaceMask.TrySetDirt(CoinSurfaceFace.Front,index,i%2==0?0f:1f);
                    var allocated = GC.GetAllocatedBytesForCurrentThread(); var tick = Stopwatch.GetTimestamp();
                    owner.UploadSurfaceMasks();
                    times[i] = (Stopwatch.GetTimestamp()-tick)*1000d/Stopwatch.Frequency;
                    bytes[i] = GC.GetAllocatedBytesForCurrentThread()-allocated;
                }
                report.AppendLine($"breakdown dirty upload only (both RGBA32 256x256 masks): warmup={Warmup}, n={Samples}; ms {Stats(times,Samples)}, threadBytes {Stats(bytes,Samples)}; separate runs, not subtraction from combined timing.");
                File.AppendAllText(Output, report.ToString()); return report.ToString();
            }
            finally { RestoreRound(); }
        }

        public static string BeginFrames()
        {
            Setup(); resourceBaseline = ResourcesAndSubscriptions(); originalBackground = Application.runInBackground;
            frameMs = new double[Capacity]; methodMs = new double[Capacity]; methodBytes = new double[Capacity]; frameGc = new double[Capacity];
            selection.TrySelect(CleaningToolId.PolishingCloth); contacts = Path(1);
            gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            warmFrames = sampleCount = changed = skippedFrames = 0; lastFrame = Time.frameCount;
            deadline = EditorApplication.timeSinceStartup + 60; LiveResult = "Warming up 60 observed frames";
            Application.runInBackground = true; live = true;
            AssemblyReloadEvents.beforeAssemblyReload += Cancel;
            EditorApplication.playModeStateChanged += OnPlayState;
            EditorApplication.update += ObserveFrame; EditorApplication.isPaused = false;
            return "Editor observed-frame probe started: warmup60, capture>=10s and >=120 samples, capacity4096, timeout60s. Synthetic contact once per observed frame, skipped frames counted.";
        }

        private static void ObserveFrame()
        {
            if (!live) return;
            try
            {
                if (!Application.isPlaying || owner == null) { Finish("CANCELLED: Play stopped"); return; }
                if (EditorApplication.timeSinceStartup > deadline) { Finish("INCOMPLETE: timeout"); return; }
                if (lastFrame == Time.frameCount) return;
                if (warmFrames == Warmup) skippedFrames += Math.Max(0, Time.frameCount - lastFrame - 1);
                lastFrame = Time.frameCount;
                if (warmFrames < Warmup)
                {
                    Work(contacts[warmFrames++ % contacts.Length]);
                    if (warmFrames == Warmup) { owner.ResetSurface(); tracker.ResetProgress(); captureStart = EditorApplication.timeSinceStartup; }
                    return;
                }
                if (sampleCount >= Capacity) { Finish("INCOMPLETE: capacity"); return; }
                frameMs[sampleCount] = Time.unscaledDeltaTime * 1000d;
                frameGc[sampleCount] = gcRecorder.Valid ? gcRecorder.LastValue : -1;
                var contact = contacts[sampleCount % contacts.Length];
                var allocated = GC.GetAllocatedBytesForCurrentThread(); var tick = Stopwatch.GetTimestamp();
                var modified = Work(contact);
                var elapsed = Stopwatch.GetTimestamp() - tick;
                methodBytes[sampleCount] = GC.GetAllocatedBytesForCurrentThread() - allocated;
                methodMs[sampleCount] = elapsed * 1000d / Stopwatch.Frequency;
                if (modified) changed++;
                sampleCount++;
                if (sampleCount >= 120 && EditorApplication.timeSinceStartup - captureStart >= 10) Finish("MEASURED");
            }
            catch (Exception ex) { Finish("FAILED: " + ex); }
        }

        private static void OnPlayState(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode) Cancel(); }
        public static void Cancel() { if (live) Finish("CANCELLED"); }
        private static void Finish(string status)
        {
            var duration = warmFrames == Warmup ? EditorApplication.timeSinceStartup - captureStart : 0;
            var gcValid = gcRecorder.Valid;
            EditorApplication.update -= ObserveFrame; EditorApplication.playModeStateChanged -= OnPlayState;
            AssemblyReloadEvents.beforeAssemblyReload -= Cancel; live = false; gcRecorder.Dispose();
            Application.runInBackground = originalBackground;
            if (Application.isPlaying) EditorApplication.isPaused = true;
            try
            {
                RestoreRound(); var after = ResourcesAndSubscriptions();
                if (resourceBaseline != after) status = "FAILED: resource/subscriber mismatch";
                LiveResult = $"S2-06a EDITOR {status}; seconds={duration:F3}, warmup={warmFrames}, observedSamples={sampleCount}, skippedFrames={skippedFrames}, changed={changed}, observedFrameMs {Stats(frameMs, sampleCount)}, injectedMethodMs {Stats(methodMs, sampleCount)}, injectedThreadBytes {Stats(methodBytes, sampleCount)}, frameGcValid={gcValid}, frameGcBytes {Stats(frameGc, sampleCount)}; resourcesStable={resourceBaseline == after}: {after}. Editor/MCP/probe allocations and stalls included in observed full-frame counters; recorder value is last available frame, not paired method allocation; not Player FPS or actual input.\n" + EnvironmentReport();
                Directory.CreateDirectory(".utmp"); File.AppendAllText(Output, LiveResult + "\n");
            }
            catch (Exception ex) { LiveResult = "FAILED cleanup/report: " + ex; }
        }

        private static void RestoreRound()
        {
            if (owner == null) return;
            advanceVisual?.Invoke(0, false, default); advanceAudio?.Invoke(0, false, default);
            typeof(CleaningAudioFeedback).GetField("hasFocus", Private).SetValue(audio, originalFocus);
            selection.TrySelect(originalTool); owner.ResetSurface(); tracker.ResetProgress(); pointer.ResetViewToInitialState();
        }

        // 측정 밖에서 소유 자원 ID·이벤트 대상/메서드를 비교합니다. 무관한 Editor 캐시는 제외합니다.
        public static string ResourcesAndSubscriptions()
        {
            var ids = new List<int>(); var c = GameObject.Find("CleaningController");
            var textures = Resources.FindObjectsOfTypeAll<Texture2D>(); var maskCount = 0;
            foreach (var t in textures) if (t.name.StartsWith("SqueakSqueak.BMask.")) { maskCount++; ids.Add(t.GetInstanceID()); }
            var sources = c.GetComponentsInChildren<AudioSource>(true); foreach (var x in sources) ids.Add(x.GetInstanceID());
            var lines = c.GetComponentsInChildren<LineRenderer>(true); foreach (var x in lines) { ids.Add(x.GetInstanceID()); if(x.sharedMaterial != null) ids.Add(x.sharedMaterial.GetInstanceID()); }
            var particles = c.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var x in particles)
            {
                ids.Add(x.GetInstanceID());
                var renderer = x.GetComponent<ParticleSystemRenderer>();
                if (renderer != null && renderer.sharedMaterial != null) ids.Add(renderer.sharedMaterial.GetInstanceID());
            }
            var body = GameObject.Find("Coin/CoinBody"); ids.Add(body.GetInstanceID()); ids.Add(body.GetComponent<Renderer>().sharedMaterial.GetInstanceID());
            ids.Sort(); var events = new StringBuilder(); var eventCount = 0;
            foreach (var component in c.GetComponents<MonoBehaviour>()) AddEvents(component, events, ref eventCount);
            AddEvents(body.GetComponent<CoinSurfaceContamination>(), events, ref eventCount);
            return $"masks={maskCount}, sources={sources.Length}, lines={lines.Length}, particles={particles.Length}, ids={string.Join(",", ids)}, subscribers={eventCount}; {events}";
        }

        private static void AddEvents(MonoBehaviour component, StringBuilder events, ref int count)
        {
            foreach (var field in component.GetType().GetFields(Private))
            {
                if (!typeof(Delegate).IsAssignableFrom(field.FieldType)) continue;
                var d = field.GetValue(component) as Delegate; events.Append(component.GetType().Name).Append('.').Append(field.Name).Append('=');
                var members = new List<string>();
                if (d != null) foreach (var subscriber in d.GetInvocationList())
                {
                    count++; var target = subscriber.Target as UnityEngine.Object;
                    members.Add((target != null ? target.GetInstanceID() : 0) + ":" + subscriber.Method.Name);
                }
                // 재활성화는 순서를 바꿀 수 있습니다. 대상/메서드의 다중집합을 비교해 중복은 보존합니다.
                members.Sort(StringComparer.Ordinal);
                foreach (var member in members) events.Append(member).Append(',');
                events.Append(';');
            }
        }

        private static string Stats(double[] values, int count)
        {
            if (count == 0) return "unavailable(n=0)";
            var sorted = new double[count]; Array.Copy(values, sorted, count); Array.Sort(sorted);
            var sum = 0d; foreach (var v in sorted) sum += v;
            return $"mean={sum/count:F4}, p95={sorted[Math.Max(0, (int)Math.Ceiling(count*.95)-1)]:F4}, max={sorted[count-1]:F4}";
        }

        private static string EnvironmentReport() => $"Unity={Application.unityVersion}; Editor Play; scene=CleaningRoom; screen={Screen.width}x{Screen.height}; OS={SystemInfo.operatingSystem}; CPU={SystemInfo.processorType} ({SystemInfo.processorCount} logical); RAM={SystemInfo.systemMemorySize}MiB; GPU={SystemInfo.graphicsDeviceName}, VRAM={SystemInfo.graphicsMemorySize}MiB; API={SystemInfo.graphicsDeviceType}; quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}; vSync={QualitySettings.vSyncCount}; targetFPS={Application.targetFrameRate}; background={Application.runInBackground}; managedUsed={Profiler.GetMonoUsedSizeLong()}B. Stopwatch region=owner brush+both mask upload+PropertyBlock+subscribers+synthetic visual/audio advance; no per-sample reflection/LINQ/forcedGC.\n";
    }
}
