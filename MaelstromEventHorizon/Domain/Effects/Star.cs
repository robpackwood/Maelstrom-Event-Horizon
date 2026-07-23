using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Effects;

internal sealed class Star(V2 position, double depth, double phase)
{
    public V2 Position = position;
    public readonly double Depth = depth;
    public readonly double Phase = phase;
}
