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
    /// One random Transformers name followed by a number — short and easy to type,
    /// e.g. "Bumblebee742".
    /// </summary>
    public static string Generate()
    {
        string name = Names[RandomNumberGenerator.GetInt32(Names.Length)];
        int number = RandomNumberGenerator.GetInt32(100, 1000); // 3 digits
        return name + number;
    }
}
