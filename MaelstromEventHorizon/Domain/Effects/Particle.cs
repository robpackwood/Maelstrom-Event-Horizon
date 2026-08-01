using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Effects;

internal sealed class Particle(V2 position, V2 velocity, double lifetime, uint color, double size)
    : Body(position, velocity, size)
{
    public double Lifetime = lifetime;
    public uint Color = color;
    public double StartSize = size;

    internal Particle Reset(V2 position, V2 velocity, double lifetime, uint color, double size)
    {
        Position = position; Velocity = velocity; Radius = size; Lifetime = lifetime; Color = color; StartSize = size;
        Age = 0; Angle = 0; Spin = 0; Alive = true;
        return this;
    }
}
