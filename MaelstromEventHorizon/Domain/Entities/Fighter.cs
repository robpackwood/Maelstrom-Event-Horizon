using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Entities;

internal sealed class Fighter(V2 position, V2 velocity, FighterKind kind)
    : Body(position, velocity, kind == FighterKind.Raider ? 27 : 18)
{
    public readonly FighterKind Kind = kind;
    public int HitPoints = kind == FighterKind.Raider ? 4 : 2;
    public double FireDelay = kind == FighterKind.Raider ? 2.1 : 1.45;
}
