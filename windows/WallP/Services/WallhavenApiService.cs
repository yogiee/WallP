using System.Net;
using System.Net.Http;
using System.Text.Json;
using WallP.Models;

namespace WallP.Services;

public sealed class WallhavenApiService
{
    private const string BaseUrl = "https://wallhaven.cc/api/v1";

    // A 3-digit SFW/Sketchy/NSFW mask. The API defaults to "100" (SFW only) when
    // omitted, which silently drops sketchy/NSFW images the user added to their
    // own collection. "111" requests everything; NSFW is only returned when the
    // supplied API key permits it, so SFW-only keys are unaffected.
    private const string DefaultPurity = "111";

    private readonly AppSettings _settings;
    private readonly HttpClient _http;

    public WallhavenApiService(AppSettings settings)
    {
        _settings = settings;
        _http = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl + "/"),
            Timeout = TimeSpan.FromSeconds(30),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("WallP/1.0 (Windows)");
    }

    public async Task<List<WallhavenCollection>> FetchCollectionsAsync(
        string username, CancellationToken ct = default)
    {
        var path = string.IsNullOrWhiteSpace(username) ? "collections" : $"collections/{username}";
        var response = await SendAsync<WallhavenCollectionsResponse>(path, ct);
        return response.Data;
    }

    public async Task<WallhavenSearchResponse> FetchCollectionWallpapersAsync(
        string username, int collectionId, int page = 1,
        string purity = DefaultPurity, CancellationToken ct = default)
    {
        var path = $"collections/{username}/{collectionId}?page={page}&purity={purity}";
        return await SendAsync<WallhavenSearchResponse>(path, ct);
    }

    // maxPages is only a runaway-safety ceiling — the loop already stops at the
    // collection's real lastPage. At 24 results/page (the API default) 50 pages
    // covers 1,200 images, comfortably above the largest cache limit (1,000).
    // The old value of 10 capped every collection at 240 images.
    public async Task<List<WallhavenWallpaper>> FetchAllCollectionWallpapersAsync(
        string username, int collectionId, int maxPages = 50,
        string purity = DefaultPurity, CancellationToken ct = default)
    {
        var all = new List<WallhavenWallpaper>();
        var page = 1;

        while (page <= maxPages)
        {
            var response = await FetchCollectionWallpapersAsync(username, collectionId, page, purity, ct);
            all.AddRange(response.Data);

            if (page >= response.Meta.LastPage) break;
            page++;

            // Wallhaven rate limit: 45 req/min — pacing keeps us well under.
            await Task.Delay(200, ct);
        }

        return all;
    }

    public async Task<byte[]> DownloadImageAsync(string url, CancellationToken ct = default)
    {
        using var request = BuildRequest(HttpMethod.Get, url, useBaseAddress: false);
        using var response = await _http.SendAsync(request, ct);
        ValidateResponse(response);
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<bool> ValidateApiKeyAsync(CancellationToken ct = default)
    {
        try
        {
            using var request = BuildRequest(HttpMethod.Get, "settings");
            using var response = await _http.SendAsync(request, ct);
            return response.StatusCode == HttpStatusCode.OK;
        }
        catch
        {
            return false;
        }
    }

    private async Task<T> SendAsync<T>(string path, CancellationToken ct)
    {
        using var request = BuildRequest(HttpMethod.Get, path);
        using var response = await _http.SendAsync(request, ct);
        ValidateResponse(response);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var result = await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: ct);
        return result ?? throw new WallhavenException("Empty response from Wallhaven.");
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string url, bool useBaseAddress = true)
    {
        var uri = useBaseAddress ? new Uri(url, UriKind.Relative) : new Uri(url, UriKind.Absolute);
        var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            request.Headers.Add("X-API-Key", _settings.ApiKey);
        }
        return request;
    }

    private static void ValidateResponse(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new WallhavenException("Invalid API key. Check your key in Settings."),
            (HttpStatusCode)429 => new WallhavenException("Rate limited by Wallhaven. Try again in a minute."),
            HttpStatusCode.NotFound => new WallhavenException("Resource not found on Wallhaven."),
            _ => new WallhavenException($"HTTP error {(int)response.StatusCode}"),
        };
    }
}

public sealed class WallhavenException : Exception
{
    public WallhavenException(string message) : base(message) { }
}
