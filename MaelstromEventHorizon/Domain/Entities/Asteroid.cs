using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Entities;

internal sealed class Asteroid : Body
{
    public Asteroid(V2 position, V2 velocity, int size, bool steel, int seed, bool exitsArena = false)
        : base(position, velocity, size switch { 3 => 35, 2 => 21, _ => 11 })
    {
        Size = size;
        Steel = steel;
        Seed = seed;
        ExitsArena = exitsArena;
        Spin = (seed % 2 == 0 ? 1 : -1) * (.18 + seed % 11 * .025);
        HitPoints = steel ? 7 : 1;
    }

    public readonly int Size;
    public bool Steel;
    public readonly int Seed;
    public int HitPoints;
    public readonly bool ExitsArena;
    public bool EnteredArena;
    public double BonusCurve;
}
