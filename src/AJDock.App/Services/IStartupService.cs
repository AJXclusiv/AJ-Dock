namespace AJDock.App.Services;

public interface IStartupService
{
    bool IsEnabled();
    void SetEnabled(bool enabled);
}
