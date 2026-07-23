namespace MaelstromEventHorizon.Application.Services.Contracts;

internal interface IAssetProvider
{
    string PathFor(params string[] segments);
}
