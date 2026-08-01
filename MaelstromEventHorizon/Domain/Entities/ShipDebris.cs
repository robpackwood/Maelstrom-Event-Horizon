using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Entities;

internal sealed class ShipDebris(V2 position, V2 velocity, int kind, double angle, double spin)
    : Body(position, velocity, 8)
{
    public int Kind = kind;
    public readonly double Lifetime = 1.9;

    public ShipDebris Initialize()
    {
        Angle = angle;
        Spin = spin;
        return this;
    }

    internal ShipDebris Reset(V2 position, V2 velocity, int kind, double newAngle, double newSpin)
    {
        Position = position;
        Velocity = velocity;
        Kind = kind;
        Angle = newAngle;
        Spin = newSpin;
        Age = 0;
        Alive = true;
        return this;
    }
}
