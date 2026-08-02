using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Entities;

internal sealed class Shot(V2 position, V2 velocity, bool enemy, double lifetime)
    : Body(position, velocity, enemy ? 4 : 3)
{
    public bool BossShot;
    public int Damage = 1;
    public bool Enemy = enemy;
    public Asteroid? LastPiercedAsteroid;
    public bool Laser;
    public double Lifetime = lifetime;
    public int PowerLevel;
    public double RiftDelay = -1;
    public bool Sludge;
    public bool SludgeVomit;
    public double SplitAge = -1;
    public uint Tint;

    internal Shot Reset(V2 position, V2 velocity, bool enemy, double lifetime)
    {
        Position = position;
        Velocity = velocity;
        Radius = enemy ? 4 : 3;
        Enemy = enemy;
        Lifetime = lifetime;
        Age = 0;
        Angle = 0;
        Spin = 0;
        Alive = true;
        BossShot = false;
        Tint = 0;
        LastPiercedAsteroid = null;
        Laser = false;
        Sludge = false;
        SludgeVomit = false;
        SplitAge = -1;
        Damage = 1;
        PowerLevel = 0;
        RiftDelay = -1;
        return this;
    }
}
