namespace AJDock.App.ViewModels;

public sealed class WifiNetworkViewModel
{
    public WifiNetworkViewModel(string ssid, string signal, string security)
    {
        Ssid = ssid;
        Signal = signal;
        Security = security;
    }

    public string Ssid { get; }
    public string Signal { get; }
    public string Security { get; }
    public string Summary => string.IsNullOrWhiteSpace(Security) ? Signal : $"{Signal} · {Security}";
}
