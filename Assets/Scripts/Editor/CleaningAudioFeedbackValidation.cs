using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using SqueakSqueak.Cleaning;

namespace SqueakSqueak.EditorValidation
{
    /// <summary>합성 사용 gate/포커스 콜백과 실제 소유자·AudioSource를 대조하는 Editor 전용 검사입니다.</summary>
    public static class CleaningAudioFeedbackValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static GameObject probe;
        private static UnityEngine.Object[] probeObjects;
        private static CleaningCompletionController probeOwner;
        private static int expectedSubscribers;
        private static CleaningAudioFeedback outputFeedback;
        private static AudioSource outputSource;
        private static readonly float[] outputSamples = new float[1024];
        private static double outputStart, dspStart, sourceSquare, listenerSquare;
        private static float sourcePeak, listenerPeak;
        private static int outputReads;
        public static string OutputResult { get; private set; } = "Not run";

        public static string DescribeAudioEnvironment()
        {
            var property = typeof(EditorUtility).GetProperty("audioMasterMute", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            return "Editor audioMasterMute=" + (property == null ? "unavailable" : property.GetValue(null)) +
                "; listenerVolume=" + AudioListener.volume + "; listenerPause=" + AudioListener.pause + "; focused=" + Application.isFocused;
        }

        [MenuItem("Squeak Squeak/Validation/S2-04b Audio Feedback (Paused Play Mode)")]
        private static void Menu() => Debug.Log(Run());

        public static string Run()
        {
            RequirePaused();
            var controller = GameObject.Find("CleaningController");
            var feedback = controller.GetComponent<CleaningAudioFeedback>(); var pointer = controller.GetComponent<CoinSurfacePointer>();
            var selection = controller.GetComponent<CleaningToolSelection>(); var cleaner = controller.GetComponent<ObjectDirtCleaner>();
            var completion = controller.GetComponent<CleaningCompletionController>(); var tracker = controller.GetComponent<CleaningProgressTracker>();
            var owner = GameObject.Find("Coin/CoinBody").GetComponent<CoinSurfaceContamination>();
            if (tracker.HasStarted || completion.IsCompleted) throw new InvalidOperationException("청소 전 새 Play에서 검사하세요.");
            var sources = new[] { Get<AudioSource>(feedback, "frictionSource"), Get<AudioSource>(feedback, "removalSource"), Get<AudioSource>(feedback, "completionSource") };
            var ids = sources.Select(x => x.GetInstanceID()).ToArray();
            var advance = (Action<float, bool, CoinSurfaceContact>)Delegate.CreateDelegate(typeof(Action<float, bool, CoinSurfaceContact>), feedback,
                typeof(CleaningAudioFeedback).GetMethod("AdvanceAudio", Private));
            var bounds = pointer.SurfaceLocalBounds; var matrix = pointer.SurfaceLocalToWorldMatrix;
            CoinSurfaceMapper.TryMapPoint(matrix, bounds, matrix.MultiplyPoint3x4(bounds.center + new Vector3(0, bounds.extents.y, 0)),
                matrix.inverse.transpose.MultiplyVector(Vector3.up), out var front);
            CoinSurfaceMapper.TryMapPoint(matrix, bounds, matrix.MultiplyPoint3x4(bounds.center - new Vector3(0, bounds.extents.y, 0)),
                matrix.inverse.transpose.MultiplyVector(Vector3.down), out var back);
            var dirt = owner.transform.parent.GetComponentsInChildren<ObjectDirt>(true);
            var originalTool = selection.CurrentTool; var checks = 0; var completed = 0; var restarted = 0;
            void Completed() => completed++; void Restarted() => restarted++;
            completion.RoundCompleted += Completed; completion.RoundRestarted += Restarted;
            void Off(string name) => Check(sources.All(x => !x.isPlaying && x.volume == 0f) && !Get<bool>(feedback, "pendingRemoval"), name, ref checks);
            void Reset() { feedback.StopAudio(); if (completion.IsCompleted) completion.RestartRound(); owner.ResetSurface(); cleaner.ResetAllDirt(); tracker.ResetProgress(); pointer.enabled = true; pointer.ResetViewToInitialState(); feedback.SendMessage("OnApplicationFocus", true); }
            void Use(CoinSurfaceContact? contact = null, float dt = .03f) => advance(dt, true, contact ?? front);
            void Complete() { foreach (var item in dirt) item.ApplyCleaning(item.CurrentDurability); Channels(owner, 0f, 1f); Check(completion.TryCompleteIfEligible(), "완료 소유자 전환", ref checks); }
            try
            {
                Check(sources.Distinct().Count() == 3 && sources.All(x => !x.playOnAwake && x.spatialBlend == 0f && x.pitch == 1f) &&
                    sources[0].loop && !sources[1].loop && !sources[2].loop, "고정2D Source3/자동 재생 없음", ref checks);
                Check(controller.transform.Find("CleaningAudio").GetComponentsInChildren<Collider>(true).Length == 0, "오디오 Raycast 간섭 없음", ref checks);
                Off("초기 무음"); feedback.SendMessage("OnApplicationFocus", true);
                foreach (var field in new[] { "dryClothClip", "smallBrushClip", "polishingClothClip", "removalClip", "completionClip" })
                {
                    var clip = Get<AudioClip>(feedback, field); var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
                    Check(clip.channels == 1 && clip.frequency == 44100 && clip.loadState == AudioDataLoadState.Loaded &&
                        importer.defaultSampleSettings.compressionFormat == AudioCompressionFormat.PCM && importer.defaultSampleSettings.loadType == AudioClipLoadType.DecompressOnLoad &&
                        importer.defaultSampleSettings.preloadAudioData, "PCM/mono/import 준비", ref checks);
                    var data = new float[clip.samples]; Check(clip.GetData(data, 0), "실제 clip 샘플 읽기", ref checks);
                    var peak = data.Max(x => Mathf.Abs(x)); var rms = Math.Sqrt(data.Sum(x => (double)x * x) / data.Length);
                    Check(peak > .1f && peak <= .281f && rms > .01d && Math.Abs(data.Average()) < .002f, "무음/클리핑/DC 검사", ref checks);
                    if (field.EndsWith("ClothClip") || field == "smallBrushClip")
                    { Check(Math.Abs(data[0] - data[data.Length - 1]) < .025f, "주기 루프 샘플 경계", ref checks); }
                    else Check(data[0] == 0f && data[data.Length - 1] == 0f, "원샷 시작/끝0", ref checks);
                    Debug.Log($"S2-04b CLIP {clip.name}: samples={clip.samples}, duration={clip.length}, peak={peak:G7}, rms={rms:G7}, boundary={Math.Abs(data[0] - data[data.Length - 1]):G7}");
                }
                var clips = new[] { "dryClothClip", "smallBrushClip", "polishingClothClip" };
                for (var i = 0; i < 3; i++)
                {
                    selection.TrySelect((CleaningToolId)i); Use();
                    Check(sources[0].isPlaying && sources[0].clip == Get<AudioClip>(feedback, clips[i]), "도구별 실제 loop Play", ref checks);
                    sources[0].timeSamples = 300; Use(); Check(sources[0].timeSamples >= 300, "같은 도구 매 프레임 Play 재시작 없음", ref checks);
                    feedback.StopAudio(); advance(.005f, true, front);
                    var volume = Get<float>(feedback, "frictionVolume");
                    Check(sources[0].volume > 0f && sources[0].volume < volume * .3f, "5ms 실제 음량 ramp", ref checks);
                    advance(.02f, true, front); Check(Math.Abs(sources[0].volume - volume) < 1e-6f, "25ms 목표 음량 도달", ref checks);
                    advance(.01f, false, front); Off("유효 사용=false 중단"); Use(back); Check(sources[0].isPlaying, "뒷면 유효 사용", ref checks);
                    Use(default(CoinSurfaceContact)); Off("Side/Invalid 중단");
                }
                Reset(); selection.TrySelect(CleaningToolId.PolishingCloth); Channels(owner, 0f, 1f);
                var revision = owner.SurfaceMask.Revision; Use(); Check(sources[0].isPlaying && owner.SurfaceMask.Revision == revision, "깨끗한 무변화 Hold 마찰 정책", ref checks);
                Channels(owner, 1f, 0f); CoinSurfaceRenderingValidation.Apply(owner, pointer, front.Face, front.Uv, .1f); Use();
                Check(sources[0].volume > Get<float>(feedback, "frictionVolume"), "실제 변화 작은 음량 강조", ref checks);
                Reset(); selection.TrySelect(CleaningToolId.SmallBrush);
                foreach (var item in dirt) item.ApplyCleaning(item.CurrentDurability); Use();
                Check(sources[1].isPlaying && sources[1].clip == Get<AudioClip>(feedback, "removalClip") && !Get<bool>(feedback, "pendingRemoval"), "A5 프레임 묶음 제거음1회", ref checks);
                sources[1].timeSamples = 400; cleaner.ResetAllDirt(); dirt[0].ApplyCleaning(dirt[0].CurrentDurability); Use();
                Check(sources[1].timeSamples >= 400, "재생 중 추가 제거음 중첩/예약 없음", ref checks);
                feedback.StopAudio(); dirt[1].ApplyCleaning(dirt[1].CurrentDurability); advance(.01f, false, front); Use(); Check(!sources[1].isPlaying, "차단 전 제거 대기 재진입에서 재생 안 함", ref checks);
                dirt[2].ApplyCleaning(dirt[2].CurrentDurability); Use(back); Check(!sources[1].isPlaying, "뒷면 A 제거음 누출 없음", ref checks);
                foreach (var action in new Action[] { () => pointer.ResetViewToInitialState(), () => owner.ResetSurface(),
                    () => selection.TrySelect(CleaningToolId.DryCloth), () => pointer.SendMessage("OnApplicationFocus", false), () => feedback.SendMessage("OnApplicationFocus", false) })
                { Reset(); selection.TrySelect(CleaningToolId.SmallBrush); Use(); action(); Off("F/Reset/선택/포커스 callback 즉시 정지"); }
                Reset(); Use(); pointer.enabled = false; Off("Pointer 비활성 정지"); pointer.enabled = true;
                Use(); owner.enabled = false; Use(); Off("표면 소유자 비활성 정지"); owner.enabled = true;
                Use(); cleaner.enabled = false; Use(); Off("A 소유자 비활성 정지"); cleaner.enabled = true;
                Use(); completion.enabled = false; Use(); Off("완료 소유자 비활성 정지"); completion.enabled = true;
                var count = SubscriberCount(completion, "RoundCompleted");
                for (var i = 0; i < 8; i++) { feedback.enabled = false; Off("관찰자 비활성 정지"); feedback.enabled = true; }
                Check(SubscriberCount(completion, "RoundCompleted") == count, "8회 재활성 구독 원복", ref checks);
                Reset(); Use(); Call(feedback, "LateUpdate"); Off("실제 private LateUpdate 기존 LMB/포커스 gate 정지");
                for (var round = 0; round < 2; round++)
                {
                    Reset(); Use(); var n = completed; Complete();
                    Check(completed == n + 1 && !sources[0].isPlaying && !sources[1].isPlaying && sources[2].isPlaying, "판당 완료1회·사용음 취소", ref checks);
                    sources[2].timeSamples = 500; Check(!completion.TryCompleteIfEligible() && completed == n + 1 && sources[2].timeSamples >= 500, "중복 완료 Play 재시작 없음", ref checks);
                    advance(.016f, false, default); Check(sources[2].isPlaying, "닫힌 gate도 완료 원샷 보존", ref checks);
                    var r = restarted; Check(completion.RestartRound() && restarted == r + 1, "재시작 알림1회", ref checks); Off("재시작 완료음 즉시 취소");
                }
                Reset(); feedback.SendMessage("OnApplicationFocus", false); Complete(); Off("포커스 이탈 직후 완료 알림도 무음");
                feedback.SendMessage("OnApplicationFocus", true); Use(); Off("복귀 후 누락 완료음/루프 재생 없음");
                feedback.enabled = false; feedback.enabled = true; Off("완료 중 재활성도 재생 없음"); Reset();
                Check(Snapshot(true).SequenceEqual(Snapshot(false)), "오디오 on/off 청소/이벤트 동일", ref checks);
                feedback.enabled = true;
                var visual = controller.GetComponent<CleaningVisualFeedback>(); visual.enabled = false;
                Reset(); feedback.SendMessage("OnApplicationFocus", true); Use(); Check(sources[0].isPlaying, "시각 효과 off도 오디오 독립", ref checks); visual.enabled = true;
                Check(ids.SequenceEqual(sources.Select(x => x.GetInstanceID())) && UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 3,
                    "고정 Source3/참조 재사용", ref checks);
                return $"S2-04b AUDIO PASS checks={checks}, completed={completed}, restarted={restarted}, fixedSources=3; actual imported clips/source state, synthetic gate/time/focus callbacks; no physical input/hearing/performance proof.";
            }
            finally
            {
                completion.RoundCompleted -= Completed; completion.RoundRestarted -= Restarted;
                feedback.enabled = owner.enabled = cleaner.enabled = completion.enabled = true;
                controller.GetComponent<CleaningVisualFeedback>().enabled = true; Reset(); selection.TrySelect(originalTool); feedback.StopAudio();
            }

            double[] Snapshot(bool audioEnabled)
            {
                feedback.enabled = audioEnabled; Reset(); selection.TrySelect(CleaningToolId.SmallBrush);
                var surfaceEvents = 0; var removalEvents = 0;
                void Changed() => surfaceEvents++; void Removed(ObjectDirt _) => removalEvents++;
                owner.CleaningApplied += Changed; cleaner.DirtRemoved += Removed;
                try
                {
                    for (var i = 0; i < 5; i++) CoinSurfaceRenderingValidation.Apply(owner, pointer, front.Face, new Vector2(.4f + i * .02f, .6f), .1f);
                    dirt[0].ApplyCleaning(dirt[0].CurrentDurability);
                    var data = new double[owner.SurfaceMask.TexelCount * 4 + dirt.Length + 2]; var cursor = 0;
                    foreach (var face in new[] { CoinSurfaceFace.Front, CoinSurfaceFace.Back }) for (var i = 0; i < owner.SurfaceMask.TexelCount; i++)
                    { data[cursor++] = owner.SurfaceMask.GetDirt(face, i); data[cursor++] = owner.SurfaceMask.GetPolish(face, i); }
                    foreach (var item in dirt) data[cursor++] = item.CurrentDurability;
                    data[cursor++] = surfaceEvents; data[cursor] = removalEvents; return data;
                }
                finally { owner.CleaningApplied -= Changed; cleaner.DirtRemoved -= Removed; }
            }
        }

        public static string BeginOutput()
        {
            RequirePaused(); if (outputFeedback != null) throw new InvalidOperationException("Output already pending");
            outputFeedback = GameObject.Find("CleaningController").GetComponent<CleaningAudioFeedback>();
            outputSource = Get<AudioSource>(outputFeedback, "frictionSource"); outputFeedback.enabled = false;
            outputSource.clip = Get<AudioClip>(outputFeedback, "dryClothClip"); outputSource.volume = .12f;
            sourcePeak = listenerPeak = 0f; sourceSquare = listenerSquare = 0d; outputReads = 0;
            outputStart = EditorApplication.timeSinceStartup; dspStart = AudioSettings.dspTime; OutputResult = "Running";
            EditorApplication.update += ReadOutput; EditorApplication.isPaused = false; outputSource.Play();
            return "Output started: real fixed source with observer temporarily disabled to isolate native transport, no synthetic input approval.";
        }
        private static void ReadOutput()
        {
            try
            {
                if (!Application.isPlaying || outputSource == null) throw new InvalidOperationException("Output interrupted");
                outputSource.GetOutputData(outputSamples, 0); foreach (var x in outputSamples) { sourcePeak = Mathf.Max(sourcePeak, Mathf.Abs(x)); sourceSquare += (double)x * x; }
                AudioListener.GetOutputData(outputSamples, 0); foreach (var x in outputSamples) { listenerPeak = Mathf.Max(listenerPeak, Mathf.Abs(x)); listenerSquare += (double)x * x; }
                outputReads++;
                if (EditorApplication.timeSinceStartup - outputStart < .65d) return;
                OutputResult = $"S2-04b OUTPUT reads={outputReads}, dspDelta={AudioSettings.dspTime - dspStart:G7}, sourcePeak={sourcePeak:G7}, listenerPeak={listenerPeak:G7}, " +
                    $"sourceRms={Math.Sqrt(sourceSquare / (outputReads * outputSamples.Length)):G7}, listenerRms={Math.Sqrt(listenerSquare / (outputReads * outputSamples.Length)):G7}, playing={outputSource.isPlaying}, volume={outputSource.volume}. " +
                    DescribeAudioEnvironment() + (sourcePeak == 0f || listenerPeak == 0f ? "; ZERO output, transport verification incomplete." : "; nonzero native output.") +
                    " Native transport probe, not physical input/hearing.";
                FinishOutput();
            }
            catch (Exception ex) { OutputResult = "S2-04b OUTPUT unavailable: " + ex.Message; FinishOutput(); }
        }
        private static void FinishOutput()
        {
            EditorApplication.update -= ReadOutput;
            if (outputSource != null) { outputSource.Stop(); outputSource.volume = 0; }
            if (Application.isPlaying) EditorApplication.isPaused = true;
            if (outputFeedback != null) outputFeedback.enabled = true;
            outputFeedback = null; outputSource = null;
            Debug.Log(OutputResult);
        }

        public static string BeginDestruction()
        {
            RequirePaused(); if (probe != null) throw new InvalidOperationException("Probe pending");
            var controller = GameObject.Find("CleaningController"); var original = controller.GetComponent<CleaningAudioFeedback>();
            probeOwner = controller.GetComponent<CleaningCompletionController>(); expectedSubscribers = SubscriberCount(probeOwner, "RoundCompleted");
            probe = UnityEngine.Object.Instantiate(controller.transform.Find("CleaningAudio").gameObject); probe.name = "AudioLifecycleProbe"; probe.SetActive(false);
            var clone = probe.AddComponent<CleaningAudioFeedback>(); var so = new SerializedObject(clone); var source = new SerializedObject(original);
            foreach (var name in new[] { "surfacePointer", "toolSelection", "surfaceContamination", "objectDirtCleaner", "completion", "dryClothClip", "smallBrushClip", "polishingClothClip", "removalClip", "completionClip" })
                so.FindProperty(name).objectReferenceValue = source.FindProperty(name).objectReferenceValue;
            foreach (var pair in new[] { ("frictionSource", "Friction"), ("removalSource", "Removal"), ("completionSource", "Completion") })
                so.FindProperty(pair.Item1).objectReferenceValue = probe.transform.Find(pair.Item2).GetComponent<AudioSource>();
            so.ApplyModifiedPropertiesWithoutUndo(); probeObjects = probe.GetComponentsInChildren<Transform>(true).Select(x => (UnityEngine.Object)x.gameObject).Append(clone).ToArray();
            probe.SetActive(true); if (SubscriberCount(probeOwner, "RoundCompleted") != expectedSubscribers + 1) throw new InvalidOperationException("Probe subscribe failed");
            UnityEngine.Object.Destroy(probe); return "Audio destruction begun; separate Editor Step2 then VerifyDestruction.";
        }
        public static string VerifyDestruction()
        {
            RequirePaused(); if (probeObjects == null || probeObjects.Any(x => x != null) || SubscriberCount(probeOwner, "RoundCompleted") != expectedSubscribers ||
                UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 3) throw new InvalidOperationException("Probe source/subscription remains");
            probe = null; probeObjects = null; probeOwner = null;
            return "S2-04b DESTRUCTION PASS objects=5, remainingAudioSources=3, completion subscription restored.";
        }
        private static void Channels(CoinSurfaceContamination owner, float dirt, float polish)
        { foreach (var face in new[] { CoinSurfaceFace.Front, CoinSurfaceFace.Back }) for (var i = 0; i < owner.SurfaceMask.TexelCount; i++) { owner.SurfaceMask.TrySetDirt(face, i, dirt); owner.SurfaceMask.TrySetPolish(face, i, polish); } owner.UploadSurfaceMasks(); }
        private static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, Private).GetValue(owner);
        private static void Call(object owner, string method) => owner.GetType().GetMethod(method, Private).Invoke(owner, null);
        private static int SubscriberCount(object owner, string field) => ((Delegate)owner.GetType().GetField(field, Private).GetValue(owner))?.GetInvocationList().Length ?? 0;
        private static void Check(bool ok, string label, ref int checks) { if (!ok) throw new InvalidOperationException("S2-04b AUDIO FAIL: " + label); checks++; }
        private static void RequirePaused() { if (!Application.isPlaying || !EditorApplication.isPaused || SceneManager.GetActiveScene().path != "Assets/Scenes/CleaningRoom.unity") throw new InvalidOperationException("새 CleaningRoom Play를 Pause하세요."); }
    }
}
