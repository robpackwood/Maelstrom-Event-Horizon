using MaelstromEventHorizon.Application.Services.Contracts;

namespace MaelstromEventHorizon.Infrastructure.Randomness;

internal sealed class SeededRandomSource : IRandomSource
{
    private readonly Random random = new(391923);

    public int Next()
    {
        return random.Next();
    }

    public int Next(int maximum)
    {
        return random.Next(maximum);
    }

    public int Next(int minimum, int maximum)
    {
        return random.Next(minimum, maximum);
    }

    public double NextDouble()
    {
        return random.NextDouble();
    }
}
