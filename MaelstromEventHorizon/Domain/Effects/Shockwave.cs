using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Effects;

internal sealed class Shockwave(V2 position, double lifetime, uint color, double maxRadius)
    : Body(position, V2.Zero, 0)
{
    public uint Color = color;
    public double Lifetime = lifetime;
    public double MaxRadius = maxRadius;

    internal Shockwave Reset(V2 position, double lifetime, uint color, double maxRadius)
    {
        Position = position;
        Lifetime = lifetime;
        Color = color;
        MaxRadius = maxRadius;
        Age = 0;
        Angle = 0;
        Spin = 0;
        Alive = true;
        return this;
    }
}
