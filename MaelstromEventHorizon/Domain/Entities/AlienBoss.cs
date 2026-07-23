using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Entities;

internal sealed class AlienBoss : Body
{
    public AlienBoss(V2 position, AlienBossKind kind, int encounter)
        : base(position, V2.Zero, kind switch
        {
            AlienBossKind.SludgeMaw => 76,
            AlienBossKind.EyeTyrant => 70,
            AlienBossKind.BoneBroodmother => 82,
            _ => 73
        })
    {
        Kind = kind;
        Encounter = encounter;
        int baseHealth = kind switch
        {
            AlienBossKind.SludgeMaw => 18,
            AlienBossKind.EyeTyrant => 20,
            AlienBossKind.BoneBroodmother => 24,
            _ => 22
        };
        HitPoints = MaxHitPoints = baseHealth + encounter * 4;
        AttackTimer = 3.4;
        SpecialTimer = 5;
    }

    public readonly AlienBossKind Kind;
    public readonly int Encounter;
    public int HitPoints;
    public readonly int MaxHitPoints;
    public double AttackTimer;
    public double SpecialTimer;
    public double HurtFlash;
    public double Phase;
}
