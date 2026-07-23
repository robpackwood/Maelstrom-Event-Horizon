using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Entities;

internal abstract class Body(V2 position, V2 velocity, double radius)
{
    public V2 Position = position;
    public V2 Velocity = velocity;
    public double Radius = radius;
    public double Angle;
    public double Spin;
    public double Age;
    public bool Alive = true;
}
