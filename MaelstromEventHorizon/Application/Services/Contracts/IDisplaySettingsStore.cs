namespace MaelstromEventHorizon.Application.Services.Contracts;

internal interface IDisplaySettingsStore
{
    DisplayPreferences Load();
    void Save(DisplayPreferences preferences);
}
