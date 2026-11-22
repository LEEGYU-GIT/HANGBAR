namespace Hangbar.Core;

/// <summary>When to refresh the Supabase access token before (re)connecting.</summary>
public static class TokenRefresh
{
    public static readonly TimeSpan Margin = TimeSpan.FromSeconds(60);

    public static bool ShouldRefresh(DateTimeOffset expiresAt, DateTimeOffset now) => expiresAt - now < Margin;
}
