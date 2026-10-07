using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SqueakSqueak.EditorValidation
{
    /// <summary>외부 자료 없이 자체 합성한 임시 PCM WAV. 기존 파일을 덮어쓰지 않습니다.</summary>
    public static class CleaningAudioClipGenerator
    {
        public const string Folder = "Assets/Audio/Prototype";
        private const int Rate = 44100;
        [MenuItem("Squeak Squeak/Audio/Generate Missing Prototype Clips")]
        public static void Generate()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Audio")) AssetDatabase.CreateFolder("Assets", "Audio");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Audio", "Prototype");
            Write("DryCloth", .8f, 260, 1050, 17, true);
            Write("SmallBrush", .8f, 450, 2200, 29, true);
            Write("PolishingCloth", .8f, 180, 700, 43, true);
            Write("DirtRemoval", .16f, 140, 1400, 61, false);
            Write("RoundComplete", .55f, 0, 0, 0, false);
        }
        private static void Write(string name, float seconds, int low, int high, int seed, bool loop)
        {
            var path = Folder + "/" + name + ".wav";
            if (File.Exists(path)) return;
            var samples = (int)Math.Round(seconds * Rate); var data = new double[samples];
            var random = new System.Random(seed);
            // 정수 주기 배음으로 짧은 루프의 경계 위상/기울기를 연결합니다. 고정 seed로 재현됩니다.
            for (var harmonic = 0; harmonic < (seed == 0 ? 3 : 96); harmonic++)
            {
                var frequency = seed == 0 ? new[] { 392d, 523.25d, 659.25d }[harmonic] : Math.Round((low + random.NextDouble() * (high - low)) * seconds) / seconds;
                var phase = seed == 0 ? 0d : random.NextDouble() * Math.PI * 2d;
                for (var i = 0; i < samples; i++) data[i] += Math.Sin(2d * Math.PI * frequency * i / Rate + phase);
            }
            var peak = 0d;
            for (var i = 0; i < samples; i++)
            {
                var t = (double)i / Rate;
                var envelope = loop ? 1d : Math.Min(1d, t / .012d) * Math.Min(1d, (seconds - t) / .035d) * Math.Exp(-t * (seed == 0 ? 2d : 12d));
                data[i] *= Math.Max(0d, envelope); peak = Math.Max(peak, Math.Abs(data[i]));
            }
            // 처음/마지막은 원샷에만0. 루프는 주기 파형을 유지하고 Source 음량 attack으로 시작합니다.
            if (!loop) data[0] = data[samples - 1] = 0d;
            using (var writer = new BinaryWriter(File.Open(path, FileMode.CreateNew)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(Rate); writer.Write(Rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
                for (var i = 0; i < samples; i++) writer.Write((short)Math.Round(data[i] / peak * .28d * short.MaxValue));
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate; settings.sampleRateOverride = Rate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings; importer.forceToMono = true; importer.loadInBackground = false;
            importer.SaveAndReimport();
        }
    }
}
