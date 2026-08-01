using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Entities;

internal sealed class AlienBoss : Body
{
    public readonly int Encounter;

    public readonly AlienBossKind Kind;
    public readonly int MaxHitPoints;
    public double AttackTimer;
    public int HitPoints;
    public double HurtFlash;
    public double Phase;
    public double SpecialTimer;

    public AlienBoss(V2 position, AlienBossKind kind, int encounter)
        : base(position, V2.Zero, kind switch
        {
            AlienBossKind.SludgeMaw => 76,
            AlienBossKind.EyeTyrant => 70,
            AlienBossKind.BoneBroodmother => 82,
            AlienBossKind.DreadHarvester => 78,
            AlienBossKind.SolarWarden => 74,
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
            AlienBossKind.DreadHarvester => 26,
            AlienBossKind.SolarWarden => 23,
            _ => 22
        };
        HitPoints = MaxHitPoints = baseHealth + encounter * 4;
        AttackTimer = 3.4;
        SpecialTimer = 5;
    }
}
