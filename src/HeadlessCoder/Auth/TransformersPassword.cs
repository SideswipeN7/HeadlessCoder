using System.Security.Cryptography;

namespace HeadlessCoder.Auth;

/// <summary>
/// Generates a memorable access password from unique Transformers character names,
/// e.g. "Grimlock-Mirage-Kup-07".
/// </summary>
public static class TransformersPassword
{
    // Single-word Autobot/Decepticon names (unique), safe to use verbatim in a password.
    private static readonly string[] Names =
    {
        "Optimus", "Bumblebee", "Megatron", "Starscream", "Soundwave", "Shockwave",
        "Ratchet", "Ironhide", "Jazz", "Wheeljack", "Prowl", "Sideswipe", "Sunstreaker",
        "Grimlock", "Arcee", "Blaster", "Cliffjumper", "Mirage", "Windcharger",
        "Bluestreak", "Skywarp", "Thundercracker", "Devastator", "Bonecrusher", "Hound",
        "Trailbreaker", "Smokescreen", "Perceptor", "Blurr", "Kup", "Springer",
        "Galvatron", "Cyclonus", "Scourge", "Rodimus", "Jetfire", "Warpath", "Tracks",
        "Inferno", "Grapple", "Hoist", "Beachcomber", "Seaspray", "Powerglide", "Cosmos",
        "Gears", "Huffer", "Brawn", "Slag", "Sludge", "Snarl", "Swoop", "Predaking",
        "Bruticus", "Menasor", "Superion", "Defensor", "Metroplex", "Trypticon", "Jetstorm",
    };

    /// <summary>All unique names available to the generator.</summary>
    public static IReadOnlyList<string> AllNames => Names;

    /// <summary>
    /// Picks <paramref name="count"/> distinct names at random and joins them with '-',
    /// followed by a two-digit number for a little extra entropy.
    /// </summary>
    public static string Generate(int count = 3)
    {
        count = Math.Clamp(count, 2, Names.Length);

        // Fisher–Yates shuffle of indices using a cryptographic RNG.
        int[] idx = Enumerable.Range(0, Names.Length).ToArray();
        for (int i = idx.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (idx[i], idx[j]) = (idx[j], idx[i]);
        }

        var picked = idx.Take(count).Select(k => Names[k]);
        int suffix = RandomNumberGenerator.GetInt32(10, 100);
        return string.Join('-', picked) + "-" + suffix;
    }
}
