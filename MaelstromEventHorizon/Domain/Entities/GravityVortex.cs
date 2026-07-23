using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Entities;

internal sealed class GravityVortex(V2 position) : Body(position, V2.Zero, 43)
{
    public double Lifetime = 14;
}
