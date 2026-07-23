using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Entities;

internal sealed class Ship(V2 position) : Body(position, V2.Zero, BaseRadius)
{
    public const double BaseVisualScale = 1.3;
    private const double BaseRadius = 22.1;
    public double Shield = 67;
    public double Invulnerable = 2.5;
    public double SpawnShieldTime;
    public bool Shielding;
    public bool Thrusting;
    public bool Giant;
    public double VisualScale => BaseVisualScale * (Giant ? 2 : 1);

    public void SetGiant(bool giant)
    {
        Giant = giant;
        Radius = BaseRadius * (giant ? 2 : 1);
    }
}
