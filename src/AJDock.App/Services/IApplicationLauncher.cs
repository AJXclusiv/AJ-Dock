using AJDock.Core.Models;

namespace AJDock.App.Services;

public interface IApplicationLauncher
{
    void Launch(PinnedApp app);
    void RunAsAdministrator(PinnedApp app);
    void OpenFileLocation(PinnedApp app);
}
