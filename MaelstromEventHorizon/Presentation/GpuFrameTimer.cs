using Vortice.Direct3D9;

namespace MaelstromEventHorizon.Presentation;

/// <summary>Collects asynchronous Direct3D timestamp queries without stalling the render thread.</summary>
internal sealed class GpuFrameTimer : IDisposable
{
    private readonly Sample[] samples;
    private readonly IDirect3DQuery9 frequency;
    private Sample? active;
    private int nextSample;
    private ulong ticksPerSecond;
    private double latestMilliseconds;
    private bool hasLatest;

    private GpuFrameTimer(IDirect3DDevice9 device)
    {
        frequency = device.CreateQuery(QueryType.TimestampFreq);
        frequency.Issue(Issue.End);
        samples =
        [
            new(device.CreateQuery(QueryType.Timestamp), device.CreateQuery(QueryType.Timestamp)),
            new(device.CreateQuery(QueryType.Timestamp), device.CreateQuery(QueryType.Timestamp)),
            new(device.CreateQuery(QueryType.Timestamp), device.CreateQuery(QueryType.Timestamp))
        ];
    }

    internal static GpuFrameTimer? TryCreate(IDirect3DDevice9 device)
    {
        try
        {
            return new GpuFrameTimer(device);
        }
        catch
        {
            return null;
        }
    }

    internal void Begin()
    {
        CollectReadySamples();
        Sample sample = samples[nextSample];
        nextSample = (nextSample + 1) % samples.Length;

        if (sample.Pending)
        {
            active = null;
            return;
        }

        sample.Start.Issue(Issue.End);
        active = sample;
    }

    internal void End()
    {
        if (active is null)
        {
            return;
        }

        active.End.Issue(Issue.End);
        active.Pending = true;
        active = null;
    }

    internal bool TryGetLatest(out double milliseconds)
    {
        CollectReadySamples();
        milliseconds = latestMilliseconds;
        return hasLatest;
    }

    public void Dispose()
    {
        foreach (Sample sample in samples)
        {
            sample.End.Dispose();
            sample.Start.Dispose();
        }

        frequency.Dispose();
    }

    private void CollectReadySamples()
    {
        if (ticksPerSecond == 0)
        {
            frequency.GetData(out ticksPerSecond, false);
        }

        if (ticksPerSecond == 0)
        {
            return;
        }

        foreach (Sample sample in samples)
        {
            if (!sample.Pending || !sample.Start.GetData(out ulong start, false) || !sample.End.GetData(out ulong end, false))
            {
                continue;
            }

            sample.Pending = false;
            latestMilliseconds = (end - start) * 1000.0 / ticksPerSecond;
            hasLatest = true;
        }
    }

    private sealed class Sample(IDirect3DQuery9 start, IDirect3DQuery9 end)
    {
        internal readonly IDirect3DQuery9 Start = start;
        internal readonly IDirect3DQuery9 End = end;
        internal bool Pending;
    }
}
