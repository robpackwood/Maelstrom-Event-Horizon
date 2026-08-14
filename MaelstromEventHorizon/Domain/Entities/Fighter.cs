using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Entities;

internal sealed class Fighter(V2 position, V2 velocity, FighterKind kind, bool elite = false)
    : Body(position, velocity, (kind == FighterKind.Raider ? 27 : 18) * (elite ? 1.12 : 1))
{
    public readonly FighterKind Kind = kind;
    public readonly bool Elite = elite;
    public double FireDelay = kind == FighterKind.Raider ? 2.1 : 1.45;
    public int HitPoints = (kind == FighterKind.Raider ? 4 : 2) + (elite ? 2 : 0);
}
