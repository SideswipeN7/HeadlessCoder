using HeadlessCoder.Claude;

namespace HeadlessCoder.Agents;

/// <summary>
/// Google Antigravity CLI (https://antigravity.google/docs/cli-using), the `agy`
/// command. Driven in non-interactive mode: `agy -p "&lt;prompt&gt;"`.
/// </summary>
public sealed class AntigravityProvider : GenericCliProvider
{
    public override string Id => "antigravity";
    public override string DisplayName => "Antigravity CLI";
    protected override string ExecutableName => "agy";

    // agy shares Gemini's home dir; its own config/state lives under antigravity-cli/.
    protected override string? ConfigDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".gemini", "antigravity-cli");

    protected override string InstallHint =>
        "Install the Antigravity CLI (`agy`) from https://antigravity.google, then run `agy` once to authenticate.";

    protected override IReadOnlyList<AgentOption> ModelOptions { get; } = new AgentOption[]
    {
        new("gemini-3-pro", "Gemini 3 Pro"),
        new("gemini-2.5-pro", "Gemini 2.5 Pro"),
        new("gemini-2.5-flash", "Gemini 2.5 Flash"),
    };

    protected override IEnumerable<string> BuildArgs(SendMessageRequest request)
    {
        var args = new List<string> { "--prompt", request.Message };
        if (request.PermissionMode == "bypassPermissions")
            args.Add("--yes"); // auto-approve tool actions
        if (!string.IsNullOrWhiteSpace(request.Model))
        {
            args.Add("--model");
            args.Add(request.Model!);
        }
        return args;
    }
}
