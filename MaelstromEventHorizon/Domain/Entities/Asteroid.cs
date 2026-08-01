using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Entities;

internal sealed class Asteroid : Body
{
    public readonly bool ExitsArena;
    public readonly bool Mega;
    public readonly int Seed;

    public readonly int Size;
    public double BonusCurve;
    public bool EnteredArena;
    public int HitPoints;
    public bool Steel;

    public Asteroid(V2 position, V2 velocity, int size, bool steel, int seed, bool exitsArena = false,
        bool mega = false)
        : base(position, velocity, mega ? 105 : size switch { 3 => 35, 2 => 21, _ => 11 })
    {
        Size = size;
        Steel = steel;
        Seed = seed;
        ExitsArena = exitsArena;
        Mega = mega;
        Spin = (seed % 2 == 0 ? 1 : -1) * (.18 + seed % 11 * .025);
        HitPoints = mega ? 3 : steel ? 7 : 1;
    }
}
