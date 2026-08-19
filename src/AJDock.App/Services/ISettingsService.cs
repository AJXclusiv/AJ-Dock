using AJDock.Core.Models;

namespace AJDock.App.Services;

public interface ISettingsService
{
    string SettingsPath { get; }
    DockSettings Load();
    void Save(DockSettings settings);
}
