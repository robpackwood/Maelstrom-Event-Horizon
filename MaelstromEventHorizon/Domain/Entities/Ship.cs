using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Entities;

internal sealed class Ship(V2 position) : Body(position, V2.Zero, BaseRadius)
{
    public const double BaseVisualScale = 1.3;
    private const double BaseRadius = 22.1;
    private const double GiantScale = 1.5;
    public bool Giant;
    public double Invulnerable = 2.5;
    public double Shield = 67;
    public bool Shielding;
    public double SpawnShieldTime;
    public bool Thrusting;
    public double VisualScale => BaseVisualScale * (Giant ? GiantScale : 1);

    public void SetGiant(bool giant)
    {
        Giant = giant;
        Radius = BaseRadius * (giant ? GiantScale : 1);
    }
}
