using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using RTSWorld.Presentation.Cameras;
using Unity.Profiling;
using UnityEngine;

namespace RTSWorld.Units.Presentation
{
    public sealed partial class RtsInteractionDemo
    {
        private void Start()
        {
            if (!environmentBenchmark) return;
            var arguments = System.Environment.GetCommandLineArgs();
            int position = Array.IndexOf(arguments, "-rts-environment-benchmark");
            if (position < 0 || position + 1 >= arguments.Length) return;
            string path = arguments[position + 1];
            StartCoroutine(MeasureEnvironment(csv =>
            {
                File.WriteAllText(path, csv);
                Application.Quit();
            }));
        }

        public IEnumerator MeasureEnvironment(Action<string> completed)
        {
            if (!environmentBenchmark) throw new InvalidOperationException("Use EnvironmentBenchmarkDemo.");
            if (Session == null || materials == null || materials[0] == null)
                throw new InvalidOperationException("A cena de benchmark não inicializou a apresentação.");
            int oldSync = QualitySettings.vSyncCount;
            int oldRate = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            var output = new StringBuilder();
            output.AppendLine($"# {Application.unityVersion}, {SystemInfo.graphicsDeviceType}, {SystemInfo.graphicsDeviceName}, " +
                $"{View.pixelWidth}x{View.pixelHeight}, {QualitySettings.names[QualitySettings.GetQualityLevel()]}, VSync=0, target=-1, seed={streaming.Seed}");
            output.AppendLine("units,art,density,shadows,ground,zoom,fps,frame_ms,p95_ms,presentation_ms,stream_ms,ready_ms,stream_peak_ms,chunks,placements,lod0,lod1,lod2,ground_instances,main_ms,render_ms,gpu_ms,draw_calls,batches,instanced_batches,setpass,triangles,vertices,memory_bytes");
            using var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            using var render = ProfilerRecorder.StartNew(ProfilerCategory.Render, "CPU Render Thread Frame Time", 1);
            using var gpu = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", 1);
            using var calls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
            using var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1);
            using var instances = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Instanced Batches Count", 1);
            using var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1);
            using var triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1);
            using var vertices = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count", 1);
            using var memory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory", 1);
            var counters = new[] { main, render, gpu, calls, batches, instances, setPass, triangles, vertices, memory };

            try
            {
                VisualMode = VisualBackend.Instanced;
                VisualCulling = VisualLod = VisualAnimation = true;
                foreach (int count in new[] { 100, 500, 1000, 5000 })
                {
                    Rebuild(count);
                    for (int density = 1; density <= 3; density++)
                        yield return Measure(count, true, density, true, true, 0);
                    if (count != 5000) continue;
                    yield return Measure(count, false, 2, true, true, 0);
                    yield return Measure(count, true, 2, false, true, 0);
                    yield return Measure(count, true, 2, true, false, 0);
                    yield return Measure(count, true, 2, true, true, -6);
                    yield return Measure(count, true, 2, true, true, -14);
                }

                rig.ResetView();
                yield return Ready();
                double panStart = Time.realtimeSinceStartupAsDouble;
                double panPeak = 0, panFramePeak = 0;
                long initialGenerated = streaming.TotalGenerated;
                rig.Advance(new RtsCameraInputFrame(Vector2.right), 10, View.pixelHeight);
                rig.Motion.Stop();
                yield return null;
                for (int i = 0; i < 600 && (streaming.PendingCount != 0 || visualObserver.WaitingForCoverage); i++)
                {
                    panPeak = Math.Max(panPeak, streaming.LastStepMilliseconds);
                    panFramePeak = Math.Max(panFramePeak, Time.unscaledDeltaTime * 1000);
                    yield return null;
                }
                if (streaming.PendingCount != 0 || visualObserver.WaitingForCoverage)
                    throw new TimeoutException("Pan não terminou durante o benchmark.");
                output.AppendLine(FormattableString.Invariant(
                    $"# pan_ms={(Time.realtimeSinceStartupAsDouble-panStart)*1000:F3}, pan_peak_stream_ms={panPeak:F3}, pan_peak_frame_ms={panFramePeak:F3}, jump_chunks={streaming.TotalGenerated-initialGenerated}"));
                completed(output.ToString());
            }
            finally
            {
                QualitySettings.vSyncCount = oldSync;
                Application.targetFrameRate = oldRate;
            }

            IEnumerator Ready()
            {
                yield return null;
                for (int i = 0; i < 600 && (streaming.PendingCount != 0 || visualObserver.WaitingForCoverage); i++)
                    yield return null;
                if (streaming.PendingCount != 0 || visualObserver.WaitingForCoverage)
                    throw new TimeoutException("Streaming não terminou durante o benchmark.");
            }

            IEnumerator Measure(int count, bool art, int density, bool shadows, bool ground, float zoom)
            {
                EnvironmentArt = art;
                GroundDensity = density;
                VisualShadows = shadows;
                GroundCover = ground;
                rig.ResetView();
                if (zoom != 0)
                    rig.Advance(new RtsCameraInputFrame(Vector2.zero, zoomSteps: zoom), 2, View.pixelHeight);
                double readyStart = Time.realtimeSinceStartupAsDouble;
                yield return Ready();
                double readyMilliseconds = (Time.realtimeSinceStartupAsDouble - readyStart) * 1000;
                for (int i = 0; i < 18; i++) yield return null;
                const int samples = 120;
                var frames = new double[samples];
                var totals = new long[counters.Length];
                double frameSum = 0, presentation = 0, stream = 0;
                for (int i = 0; i < samples; i++)
                {
                    yield return null;
                    frames[i] = Time.unscaledDeltaTime * 1000;
                    frameSum += frames[i];
                    presentation += PresentationMilliseconds;
                    stream += streaming.LastStepMilliseconds;
                    for (int j = 0; j < counters.Length; j++)
                        if (counters[j].Valid) totals[j] += counters[j].LastValue;
                }
                Array.Sort(frames);
                var stats = VisualStatistics;
                if (Session.Count != count) throw new InvalidOperationException("População de benchmark incompleta.");
                output.Append(FormattableString.Invariant(
                    $"{count},{art},{density},{shadows},{ground},{rig.Motion.Zoom:F0},{samples*1000/frameSum:F3},{frameSum/samples:F3},{frames[113]:F3},{presentation/samples:F4},{stream/samples:F4},{readyMilliseconds:F3},{streaming.PeakStepMilliseconds:F3},{streaming.ActiveCount},{VisualResourceCount},{stats.EnvironmentNear},{stats.EnvironmentMid},{stats.EnvironmentFar},{stats.GroundInstances}"));
                for (int j = 0; j < counters.Length; j++)
                {
                    output.Append(',');
                    output.Append(totals[j] > 0 ? (totals[j] * (j < 3 ? 1e-6 : 1) / samples)
                        .ToString("F3", CultureInfo.InvariantCulture) : "NOT_RUN");
                }
                output.AppendLine();
            }
        }
    }
}
