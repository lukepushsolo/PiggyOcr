using System.Text.Json;

namespace PiggyOcr.App.Services;

public static class UpdateService
{
    public const string ReleasesUrl = "https://github.com/lukepushsolo/PiggyOcr/releases";

    public static async Task<string?> GetLatestTagAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("PiggyOcr");
            string json = await http.GetStringAsync(
                "https://api.github.com/repos/lukepushsolo/PiggyOcr/releases/latest");
            using JsonDocument document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("tag_name", out JsonElement tag)
                ? tag.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    public static bool IsNewer(string? tag, Version current)
    {
        if (string.IsNullOrWhiteSpace(tag)) return false;
        string trimmed = tag.TrimStart('v', 'V');
        return Version.TryParse(trimmed, out Version? parsed) && parsed > current;
    }
}
