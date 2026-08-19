using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AJDock.App.Services;

public sealed class NetworkStatusService
{
    private DateTimeOffset _lastSampleAt = DateTimeOffset.MinValue;
    private long _lastBytesReceived;
    private long _lastBytesSent;
    private DateTimeOffset _lastNameRefreshAt = DateTimeOffset.MinValue;
    private string _connectionName = "Network";
    private string _adapterName = "No active connection";
    private string _wirelessAdapterName = "Wi-Fi";
    private bool _isWirelessEnabled;

    public NetworkStatusSnapshot GetSnapshot()
    {
        var now = DateTimeOffset.UtcNow;
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(IsActiveInterface)
            .ToList();

        var received = interfaces.Sum(item => SafeStats(item).BytesReceived);
        var sent = interfaces.Sum(item => SafeStats(item).BytesSent);
        var elapsed = Math.Max(0.001, (now - _lastSampleAt).TotalSeconds);
        var downloadBytesPerSecond = _lastSampleAt == DateTimeOffset.MinValue
            ? 0
            : Math.Max(0, (received - _lastBytesReceived) / elapsed);
        var uploadBytesPerSecond = _lastSampleAt == DateTimeOffset.MinValue
            ? 0
            : Math.Max(0, (sent - _lastBytesSent) / elapsed);

        _lastSampleAt = now;
        _lastBytesReceived = received;
        _lastBytesSent = sent;

        if (now - _lastNameRefreshAt > TimeSpan.FromSeconds(12))
        {
            _lastNameRefreshAt = now;
            RefreshConnectionName(interfaces);
        }

        return new NetworkStatusSnapshot(
            _connectionName,
            _adapterName,
            FormatRate(downloadBytesPerSecond),
            FormatRate(uploadBytesPerSecond),
            _wirelessAdapterName,
            _isWirelessEnabled);
    }

    public async Task<string> RunDownloadSpeedTestAsync(CancellationToken cancellationToken = default)
    {
        var speedtestCliResult = await TryRunOoklaSpeedtestAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(speedtestCliResult))
        {
            return speedtestCliResult;
        }

        var fastComResult = await TryRunFastComSpeedTestAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(fastComResult))
        {
            return fastComResult;
        }

        return "Could not reach Fast.com";
    }

    public async Task<IReadOnlyList<WifiNetworkInfo>> GetAvailableWifiNetworksAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("netsh", "wlan show networks mode=bssid")
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            });

            if (process is null)
            {
                return [];
            }

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return ParseWifiNetworks(output);
        }
        catch
        {
            return [];
        }
    }

    public void ConnectWifiNetwork(string ssid)
    {
        if (string.IsNullOrWhiteSpace(ssid))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("netsh", $"wlan connect name=\"{ssid}\" ssid=\"{ssid}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
        catch
        {
            // The network may not have a saved profile; the Windows network flyout is the fallback.
        }
    }

    private static async Task<string?> TryRunOoklaSpeedtestAsync(CancellationToken cancellationToken)
    {
        var executable = FindOnPath("speedtest.exe") ?? FindOnPath("speedtest");
        if (string.IsNullOrWhiteSpace(executable))
        {
            return null;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(45));

            using var process = Process.Start(new ProcessStartInfo(executable, "--format=json --accept-license --accept-gdpr")
            {
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            });

            if (process is null)
            {
                return null;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            if (process.ExitCode != 0)
            {
                return null;
            }

            var output = await outputTask;
            using var document = JsonDocument.Parse(output);
            var root = document.RootElement;
            var download = root.GetProperty("download").GetProperty("bandwidth").GetDouble() * 8 / 1_000_000;
            var upload = root.TryGetProperty("upload", out var uploadElement)
                ? uploadElement.GetProperty("bandwidth").GetDouble() * 8 / 1_000_000
                : 0;
            var ping = root.TryGetProperty("ping", out var pingElement)
                ? pingElement.GetProperty("latency").GetDouble()
                : 0;

            return upload > 0
                ? $"Speedtest {download:0}↓ {upload:0}↑ Mbps · {ping:0} ms"
                : $"Speedtest {download:0} Mbps";
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<WifiNetworkInfo> ParseWifiNetworks(string output)
    {
        var networks = new List<WifiNetworkInfo>();
        string? ssid = null;
        string signal = string.Empty;
        string authentication = string.Empty;

        foreach (var rawLine in output.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var line = rawLine.Trim();
            var ssidMatch = Regex.Match(line, @"^SSID\s+\d+\s*:\s*(?<ssid>.+)$", RegexOptions.IgnoreCase);
            if (ssidMatch.Success)
            {
                AddNetwork();
                ssid = ssidMatch.Groups["ssid"].Value.Trim();
                signal = string.Empty;
                authentication = string.Empty;
                continue;
            }

            var authMatch = Regex.Match(line, @"^Authentication\s*:\s*(?<auth>.+)$", RegexOptions.IgnoreCase);
            if (authMatch.Success)
            {
                authentication = authMatch.Groups["auth"].Value.Trim();
                continue;
            }

            var signalMatch = Regex.Match(line, @"^Signal\s*:\s*(?<signal>.+)$", RegexOptions.IgnoreCase);
            if (signalMatch.Success && string.IsNullOrWhiteSpace(signal))
            {
                signal = signalMatch.Groups["signal"].Value.Trim();
            }
        }

        AddNetwork();
        return networks
            .GroupBy(network => network.Ssid, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(network => ParseSignal(network.Signal)).First())
            .OrderByDescending(network => ParseSignal(network.Signal))
            .Take(8)
            .ToList();

        void AddNetwork()
        {
            if (!string.IsNullOrWhiteSpace(ssid))
            {
                networks.Add(new WifiNetworkInfo(ssid, signal, authentication));
            }
        }
    }

    private static int ParseSignal(string signal)
    {
        var match = Regex.Match(signal, @"\d+");
        return match.Success && int.TryParse(match.Value, out var value) ? value : 0;
    }

    private static async Task<string?> TryRunFastComSpeedTestAsync(CancellationToken cancellationToken)
    {
        const string token = "YXNkZmFzZGxmbnNkYWZoYXNkZmhrYWxm";
        const int urlCount = 5;
        const int maxBytesPerTarget = 10 * 1024 * 1024;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            using var response = await client.GetAsync(
                $"https://api.fast.com/netflix/speedtest/v2?https=true&token={token}&urlCount={urlCount}",
                timeout.Token);
            response.EnsureSuccessStatusCode();

            await using var jsonStream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var document = await JsonDocument.ParseAsync(jsonStream, cancellationToken: timeout.Token);
            var targets = document.RootElement
                .GetProperty("targets")
                .EnumerateArray()
                .Select(target => target.GetProperty("url").GetString())
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Take(urlCount)
                .ToList();

            if (targets.Count == 0)
            {
                return null;
            }

            var stopwatch = Stopwatch.StartNew();
            var tasks = targets.Select(url => DownloadSampleAsync(client, url!, maxBytesPerTarget, timeout.Token)).ToArray();
            var bytes = (await Task.WhenAll(tasks)).Sum();
            stopwatch.Stop();

            if (bytes <= 0 || stopwatch.Elapsed.TotalSeconds <= 0)
            {
                return null;
            }

            var megabitsPerSecond = bytes * 8d / 1_000_000d / stopwatch.Elapsed.TotalSeconds;
            return $"Fast.com {megabitsPerSecond:0.0} Mbps";
        }
        catch
        {
            return null;
        }
    }

    private static async Task<long> DownloadSampleAsync(HttpClient client, string url, int maxBytes, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[64 * 1024];
            var totalBytes = 0L;
            while (totalBytes < maxBytes)
            {
                var requested = (int)Math.Min(buffer.Length, maxBytes - totalBytes);
                var read = await stream.ReadAsync(buffer.AsMemory(0, requested), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                totalBytes += read;
            }

            return totalBytes;
        }
        catch
        {
            return 0;
        }
    }

    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim(), fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // Ignore invalid PATH entries.
            }
        }

        return null;
    }

    private static bool IsActiveInterface(NetworkInterface networkInterface)
    {
        return networkInterface.OperationalStatus == OperationalStatus.Up
            && networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback
            && networkInterface.NetworkInterfaceType != NetworkInterfaceType.Tunnel;
    }

    private static IPv4InterfaceStatistics SafeStats(NetworkInterface networkInterface)
    {
        try
        {
            return networkInterface.GetIPv4Statistics();
        }
        catch
        {
            return EmptyIPv4InterfaceStatistics.Instance;
        }
    }

    private void RefreshConnectionName(IReadOnlyList<NetworkInterface> interfaces)
    {
        var primary = interfaces
            .OrderByDescending(item => item.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            .ThenByDescending(item => SafeStats(item).BytesReceived + SafeStats(item).BytesSent)
            .FirstOrDefault();
        var wireless = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(item => item.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);

        _adapterName = primary?.Name ?? "No active connection";
        _wirelessAdapterName = wireless?.Name ?? "Wi-Fi";
        _isWirelessEnabled = wireless?.OperationalStatus == OperationalStatus.Up;
        _connectionName = primary is null
            ? "Offline"
            : primary.NetworkInterfaceType == NetworkInterfaceType.Wireless80211
                ? TryGetWifiSsid() ?? primary.Name
                : primary.Name;
    }

    private static string? TryGetWifiSsid()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("netsh", "wlan show interfaces")
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            });

            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(800))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort; the next sample can try again.
                }
            }

            var match = Regex.Match(output, @"^\s*SSID\s*:\s*(?<ssid>.+?)\s*$", RegexOptions.Multiline);
            return match.Success ? match.Groups["ssid"].Value.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static string FormatRate(double bytesPerSecond)
    {
        if (bytesPerSecond >= 1024 * 1024)
        {
            return $"{bytesPerSecond / 1024 / 1024:0.0} MB/s";
        }

        if (bytesPerSecond >= 1024)
        {
            return $"{bytesPerSecond / 1024:0.0} KB/s";
        }

        return $"{bytesPerSecond:0} B/s";
    }

    private sealed class EmptyIPv4InterfaceStatistics : IPv4InterfaceStatistics
    {
        public static readonly EmptyIPv4InterfaceStatistics Instance = new();
        public override long BytesReceived => 0;
        public override long BytesSent => 0;
        public override long IncomingPacketsDiscarded => 0;
        public override long IncomingPacketsWithErrors => 0;
        public override long IncomingUnknownProtocolPackets => 0;
        public override long NonUnicastPacketsReceived => 0;
        public override long NonUnicastPacketsSent => 0;
        public override long OutgoingPacketsDiscarded => 0;
        public override long OutgoingPacketsWithErrors => 0;
        public override long OutputQueueLength => 0;
        public override long UnicastPacketsReceived => 0;
        public override long UnicastPacketsSent => 0;
    }
}

public sealed record NetworkStatusSnapshot(
    string ConnectionName,
    string AdapterName,
    string DownloadRate,
    string UploadRate,
    string WirelessAdapterName,
    bool IsWirelessEnabled);

public sealed record WifiNetworkInfo(string Ssid, string Signal, string Security);
