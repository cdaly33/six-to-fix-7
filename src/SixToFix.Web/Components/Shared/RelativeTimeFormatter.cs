namespace SixToFix.Web.Components.Shared;

/// <summary>Formats a UTC timestamp as a human-readable relative string.</summary>
public static class RelativeTimeFormatter
{
    public static string ToRelative(DateTimeOffset utc)
    {
        var diff = DateTimeOffset.UtcNow - utc;
        if (diff.TotalSeconds < 60) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return utc.UtcDateTime.ToString("MMM d, yyyy");
    }
}
