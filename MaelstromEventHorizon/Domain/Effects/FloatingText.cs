using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Effects;

internal sealed class FloatingText(V2 position, string text, uint color)
{
    public V2 Position = position;
    public readonly string Text = text;
    public readonly uint Color = color;
    public double Age;
    public readonly double Lifetime = 1.65;
    public bool Alive => Age < Lifetime;
}
