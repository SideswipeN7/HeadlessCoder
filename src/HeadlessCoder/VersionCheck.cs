namespace HeadlessCoder;

/// <summary>Version-comparison helpers for the GitHub Releases update check.</summary>
public static class VersionCheck
{
    /// <summary>
    /// True when the release <paramref name="tag"/> (e.g. "v1.2.0") is a higher
    /// version than the running app's <paramref name="current"/> version.
    /// </summary>
    public static bool IsNewer(string current, string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return false;
        string t = tag.TrimStart('v', 'V').Trim();
        return Version.TryParse(Pad(current), out var cv) &&
               Version.TryParse(Pad(t), out var lv) && lv > cv;

        static string Pad(string v)
        {
            // Version.TryParse needs at least major.minor.
            var parts = v.Split('-', '+')[0]; // drop pre-release / build metadata
            return parts.Contains('.') ? parts : parts + ".0";
        }
    }
}
