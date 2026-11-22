using System.Text.Json;

namespace Hangbar.App.Net;

/// <summary>
/// Spike configuration: client/app/hangbar.local.json (git-ignored), overridden by environment variables
/// HANGBAR_SUPABASE_URL, HANGBAR_SUPABASE_KEY (publishable key), HANGBAR_RELAY_URL.
/// </summary>
public sealed record HangbarConfig(string? SupabaseUrl, string? SupabaseKey, string? RelayUrl)
{
    public static HangbarConfig Load(string projectDir)
    {
        string? url = null, key = null, relay = null;
        var path = Path.Combine(projectDir, "hangbar.local.json");
        if (File.Exists(path))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var r = doc.RootElement;
            url = r.TryGetProperty("supabaseUrl", out var u) ? u.GetString() : null;
            key = r.TryGetProperty("supabaseKey", out var k) ? k.GetString() : null;
            relay = r.TryGetProperty("relayUrl", out var x) ? x.GetString() : null;
        }
        return new HangbarConfig(
            System.Environment.GetEnvironmentVariable("HANGBAR_SUPABASE_URL") ?? url,
            System.Environment.GetEnvironmentVariable("HANGBAR_SUPABASE_KEY") ?? key,
            System.Environment.GetEnvironmentVariable("HANGBAR_RELAY_URL") ?? relay);
    }

    public bool CanSignIn => !string.IsNullOrWhiteSpace(SupabaseUrl) && !string.IsNullOrWhiteSpace(SupabaseKey);
}
