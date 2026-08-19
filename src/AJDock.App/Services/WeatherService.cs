using System.Net.Http;
using System.Text.Json;

namespace AJDock.App.Services;

public sealed class WeatherService : IDisposable
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(4)
    };

    public async Task<string> GetCurrentWeatherTextAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("https://wttr.in/?format=j1", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return "☁ --°";
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var current = document.RootElement.GetProperty("current_condition")[0];
            var temp = current.GetProperty("temp_F").GetString();
            var condition = current.GetProperty("weatherDesc")[0].GetProperty("value").GetString();
            var code = current.TryGetProperty("weatherCode", out var weatherCode)
                ? weatherCode.GetString()
                : string.Empty;

            if (string.IsNullOrWhiteSpace(temp))
            {
                return "☁ --°";
            }

            return $"{WeatherGlyph(code, condition)} {temp}°";
        }
        catch
        {
            return "☁ --°";
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private static string WeatherGlyph(string? code, string? condition)
    {
        if (int.TryParse(code, out var weatherCode))
        {
            return weatherCode switch
            {
                113 => "☀",
                116 or 119 or 122 => "☁",
                143 or 248 or 260 => "≋",
                386 or 389 or 392 or 395 => "⚡",
                >= 176 and <= 359 => "☂",
                >= 368 and <= 395 => "❄",
                _ => "☁"
            };
        }

        var text = condition ?? string.Empty;
        if (text.Contains("sun", StringComparison.OrdinalIgnoreCase) || text.Contains("clear", StringComparison.OrdinalIgnoreCase))
        {
            return "☀";
        }

        if (text.Contains("snow", StringComparison.OrdinalIgnoreCase))
        {
            return "❄";
        }

        if (text.Contains("thunder", StringComparison.OrdinalIgnoreCase))
        {
            return "⚡";
        }

        if (text.Contains("rain", StringComparison.OrdinalIgnoreCase) || text.Contains("drizzle", StringComparison.OrdinalIgnoreCase))
        {
            return "☂";
        }

        if (text.Contains("fog", StringComparison.OrdinalIgnoreCase) || text.Contains("mist", StringComparison.OrdinalIgnoreCase))
        {
            return "≋";
        }

        return "☁";
    }
}
