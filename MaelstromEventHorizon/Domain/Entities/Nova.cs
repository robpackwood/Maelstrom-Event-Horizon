using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Entities;

internal sealed class Nova(V2 position) : Body(position, V2.Zero, 22)
{
    public const double Fuse = 6.2;
    public bool Detonated;
}
