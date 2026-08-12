using System.Diagnostics;

namespace MaelstromEventHorizon.Presentation;

internal sealed class FrameTimingProfiler
{
    private const double Smoothing = .14;

    internal double SimulationMilliseconds { get; private set; }
    internal double GpuPlayfieldMilliseconds { get; private set; }
    internal double WpfOverlayMilliseconds { get; private set; }
    internal double WpfFallbackMilliseconds { get; private set; }
    internal bool HasHardwareGpuTiming { get; private set; }

    internal void RecordSimulation(long startedAt) =>
        SimulationMilliseconds = Smooth(SimulationMilliseconds, ElapsedMilliseconds(startedAt));

    internal void RecordGpuPlayfield(double milliseconds, bool hardwareTiming)
    {
        GpuPlayfieldMilliseconds = Smooth(GpuPlayfieldMilliseconds, milliseconds);
        HasHardwareGpuTiming |= hardwareTiming;
    }

    internal void RecordWpfOverlay(long startedAt) =>
        WpfOverlayMilliseconds = Smooth(WpfOverlayMilliseconds, ElapsedMilliseconds(startedAt));

    internal void RecordWpfFallback(long startedAt) =>
        WpfFallbackMilliseconds = Smooth(WpfFallbackMilliseconds, ElapsedMilliseconds(startedAt));

    internal void ClearGpuPlayfield()
    {
        GpuPlayfieldMilliseconds = 0;
        HasHardwareGpuTiming = false;
    }

    internal static double ElapsedMilliseconds(long startedAt) =>
        (Stopwatch.GetTimestamp() - startedAt) * 1000.0 / Stopwatch.Frequency;

    private static double Smooth(double current, double sample) =>
        current == 0 ? sample : current + (sample - current) * Smoothing;
}
