using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Entities;

internal sealed class Comet(V2 position, V2 velocity, int value, uint tint)
    : Body(position, velocity, 22)
{
    public const double TrailLength = 180;
    public readonly int Value = value;
    public readonly uint Tint = tint;
    public readonly double Lifetime = 15;
}
