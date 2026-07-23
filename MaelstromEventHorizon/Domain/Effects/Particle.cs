using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Effects;

internal sealed class Particle(V2 position, V2 velocity, double lifetime, uint color, double size)
    : Body(position, velocity, size)
{
    public readonly double Lifetime = lifetime;
    public readonly uint Color = color;
    public readonly double StartSize = size;
}
