using System.Net.Http;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AJDock.App.Services;

public sealed class ArtworkLookupService : IDisposable
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };
    private readonly Dictionary<string, ImageSource?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<ImageSource?> FindArtworkAsync(string artist, string track)
    {
        var key = $"{artist}::{track}";
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        try
        {
            var query = Uri.EscapeDataString($"{artist} {track}");
            using var response = await _httpClient.GetAsync($"https://itunes.apple.com/search?term={query}&media=music&limit=1");
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            var results = document.RootElement.GetProperty("results");
            if (results.GetArrayLength() == 0)
            {
                _cache[key] = null;
                return null;
            }

            var url = results[0].TryGetProperty("artworkUrl100", out var artwork)
                ? artwork.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(url))
            {
                _cache[key] = null;
                return null;
            }

            url = url.Replace("100x100bb", "600x600bb", StringComparison.OrdinalIgnoreCase);
            using var imageResponse = await _httpClient.GetAsync(url);
            imageResponse.EnsureSuccessStatusCode();
            await using var imageStream = await imageResponse.Content.ReadAsStreamAsync();

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = imageStream;
            image.EndInit();
            image.Freeze();
            _cache[key] = image;
            return image;
        }
        catch
        {
            _cache[key] = null;
            return null;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
