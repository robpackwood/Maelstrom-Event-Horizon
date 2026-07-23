using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Entities;

internal sealed class HomingMine(V2 position, V2 velocity) : Body(position, velocity, 15)
{
    public int HitPoints = 5;
}
