using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Entities;

internal abstract class Body(V2 position, V2 velocity, double radius)
{
    public double Age;
    public bool Alive = true;
    public double Angle;
    public V2 Position = position;
    public double Radius = radius;
    public double Spin;
    public V2 Velocity = velocity;
}
