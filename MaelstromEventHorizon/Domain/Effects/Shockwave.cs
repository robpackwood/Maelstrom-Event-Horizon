using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Effects;

internal sealed class Shockwave(V2 position, double lifetime, uint color, double maxRadius)
    : Body(position, V2.Zero, 0)
{
    public readonly double Lifetime = lifetime;
    public readonly uint Color = color;
    public readonly double MaxRadius = maxRadius;
}
