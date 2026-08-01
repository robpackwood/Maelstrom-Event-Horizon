using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Entities;

internal sealed class Pickup(V2 position, V2 velocity, PickupKind kind, int value = 0)
    : Body(position, velocity,
        kind == PickupKind.RescueShip ? 22 :
        kind is PickupKind.TimeFreeze or PickupKind.SmartBomb or PickupKind.RicochetArena ? 19 : 15)
{
    public readonly PickupKind Kind = kind;

    public readonly double Lifetime = kind == PickupKind.Canister ? 16 :
        kind == PickupKind.RescueShip ? 18 :
        kind is PickupKind.TimeFreeze or PickupKind.SmartBomb or PickupKind.RicochetArena ? 13 : 10;

    public readonly int Value = value;
}
