using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Entities;

internal sealed class Shot(V2 position, V2 velocity, bool enemy, double lifetime)
    : Body(position, velocity, enemy ? 4 : 3)
{
    public readonly bool Enemy = enemy;
    public readonly double Lifetime = lifetime;
    public bool BossShot;
    public uint Tint;
    public bool Sludge;
    public bool SludgeVomit;
    public double SplitAge = -1;
    public int Damage = 1;
    public int PowerLevel;
}
