namespace MaelstromEventHorizon.Application.Services.Contracts;

internal interface IRandomSource
{
    int Next();
    int Next(int maximum);
    int Next(int minimum, int maximum);
    double NextDouble();
}
