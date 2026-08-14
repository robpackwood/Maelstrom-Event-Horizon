using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Entities;

internal sealed class Asteroid : Body
{
    public readonly bool ExitsArena;
    public readonly bool Colossal;
    public readonly bool Mega;
    public readonly int Seed;

    public readonly int Size;
    public double BonusCurve;
    public bool EnteredArena;
    public int HitPoints;
    public bool Steel;

    public Asteroid(
        V2 position, V2 velocity, int size, bool steel, int seed, bool exitsArena = false, bool mega = false,
        bool colossal = false)
        : base(position, velocity, colossal ? 105 * 1.3 : mega ? 105 : size switch { 3 => 35, 2 => 21, _ => 11 })
    {
        Size = size;
        Steel = steel;
        Seed = seed;
        ExitsArena = exitsArena;
        Colossal = colossal;
        Mega = mega || colossal;
        Spin = (seed % 2 == 0 ? 1 : -1) * (.18 + seed % 11 * .025);
        HitPoints = colossal ? 6 : mega ? 3 : steel ? 7 : 1;
    }
}
