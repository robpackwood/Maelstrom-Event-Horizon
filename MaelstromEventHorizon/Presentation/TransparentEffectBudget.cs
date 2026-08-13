using MaelstromEventHorizon.Application;

namespace MaelstromEventHorizon.Presentation;

internal sealed class TransparentEffectBudget
{
    private const double PlayfieldArea = GameEngine.Width * GameEngine.Height;
    private double remainingCoverage = double.MaxValue;

    internal void Reset(int visualQuality)
    {
        remainingCoverage = PlayfieldArea * (visualQuality switch { 0 => .28, 1 => .68, _ => 1.2 });
    }

    internal double ReserveDisk(double radius)
    {
        return ReserveSquareRoot(Math.Min(PlayfieldArea, Math.PI * radius * radius));
    }

    internal double ReserveRing(double radius, double thickness)
    {
        double coverage = Math.Min(PlayfieldArea, Math.PI * 2 * radius * Math.Max(.5, thickness));

        if (coverage <= remainingCoverage)
        {
            remainingCoverage -= coverage;
            return 1;
        }

        double scale = coverage == 0 ? 1 : remainingCoverage / coverage;
        remainingCoverage = 0;
        return scale >= .3 ? scale : 0;
    }

    internal GlowDetail ReserveGlow(double radius, int layers)
    {
        double scale = ReserveSquareRoot(Math.Min(PlayfieldArea, Math.PI * Math.Pow(radius + layers * 4, 2)) * layers);
        return scale == 0 ? GlowDetail.Hidden : new(Math.Max(.55, scale), Math.Max(1, (int)Math.Ceiling(layers * scale)));
    }

    private double ReserveSquareRoot(double coverage)
    {
        if (coverage <= remainingCoverage)
        {
            remainingCoverage -= coverage;
            return 1;
        }

        double scale = coverage == 0 ? 1 : Math.Sqrt(remainingCoverage / coverage);
        remainingCoverage = 0;
        return scale >= .35 ? scale : 0;
    }

    internal readonly record struct GlowDetail(double RadiusScale, int Layers)
    {
        internal static readonly GlowDetail Hidden = new(0, 0);
    }
}
