namespace MaelstromEventHorizon.Application.Services.Contracts;

internal interface IAppDataPathProvider
{
    string DirectoryPath { get; }
    string ReadPath(string filename);
    string WritePath(string filename);
}
