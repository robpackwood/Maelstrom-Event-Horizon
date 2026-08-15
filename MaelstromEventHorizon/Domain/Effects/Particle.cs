using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Effects;

internal sealed class Particle(V2 position, V2 velocity, double lifetime, uint color, double size)
    : Body(position, velocity, size)
{
    public uint Color = color;
    public double Lifetime = lifetime;
    public double StartSize = size;
    public bool ShipExplosion;

    internal Particle Reset(V2 position, V2 velocity, double lifetime, uint color, double size, bool shipExplosion = false)
    {
        Position = position;
        Velocity = velocity;
        Radius = size;
        Lifetime = lifetime;
        Color = color;
        StartSize = size;
        Age = 0;
        Angle = 0;
        Spin = 0;
        ShipExplosion = shipExplosion;
        Alive = true;
        return this;
    }
}
