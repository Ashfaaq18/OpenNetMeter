using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenNetMeter.Avalonia.Services;

public static class UpdateChecker
{
    public static async Task<(Version? latestVersion, string? downloadUrl)> CheckForUpdatesAsync()
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "OpenNetMeter");

        using var response = await client.GetAsync($"https://api.github.com/repos/Ashfaaq18/OpenNetMeter/releases/latest");
        if (!response.IsSuccessStatusCode)
        {
            return (null, null);
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        JsonElement root = document.RootElement;

        if (!root.TryGetProperty("tag_name", out JsonElement tagElement))
        {
            return (null, null);
        }

        string? latestTag = tagElement.GetString();
        if (string.IsNullOrWhiteSpace(latestTag))
        {
            return (null, null);
        }

        string normalizedVersion = latestTag.Trim();
        if (normalizedVersion.StartsWith('v') || normalizedVersion.StartsWith('V'))
        {
            normalizedVersion = normalizedVersion[1..];
        }

        if (!Version.TryParse(normalizedVersion, out Version? latestVersion))
        {
            return (null, null);
        }

        string? downloadUrl = null;
        if (root.TryGetProperty("assets", out JsonElement assetsElement) &&
            assetsElement.ValueKind == JsonValueKind.Array &&
            assetsElement.GetArrayLength() > 0)
        {
            JsonElement firstAsset = assetsElement[0];
            if (firstAsset.TryGetProperty("browser_download_url", out JsonElement downloadUrlElement))
            {
                downloadUrl = downloadUrlElement.GetString();
            }
        }

        return (latestVersion, downloadUrl);
    }
}
