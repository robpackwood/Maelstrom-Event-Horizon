using MaelstromEventHorizon.Domain.Math;

namespace MaelstromEventHorizon.Domain.Effects;

internal sealed class FloatingText(V2 position, string text, uint color)
{
    public V2 Position = position;
    public string Text = text;
    public uint Color = color;
    public double Age;
    public double Lifetime = 1.65;
    public bool Alive => Age < Lifetime;

    internal FloatingText Reset(V2 position, string text, uint color)
    {
        Position = position; Text = text; Color = color; Age = 0; Lifetime = 1.65;
        return this;
    }
}
