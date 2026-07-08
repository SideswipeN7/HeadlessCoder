using HeadlessCoder.Claude;

namespace HeadlessCoder.Agents;

/// <summary>
/// GitHub Copilot CLI (the agentic `copilot` command). Driven non-interactively
/// with `copilot -p "&lt;prompt&gt;"`.
/// </summary>
public sealed class CopilotProvider : GenericCliProvider
{
    public override string Id => "copilot";
    public override string DisplayName => "Copilot CLI";
    protected override string ExecutableName => "copilot";

    protected override string? ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot");

    protected override string InstallHint =>
        "Install with `npm install -g @github/copilot`, then run `copilot` once and `/login` to authenticate.";

    protected override IEnumerable<string> BuildArgs(SendMessageRequest request)
    {
        // Non-interactive prompt mode. Copilot prints the answer and exits.
        var args = new List<string> { "--prompt", request.Message };
        if (request.PermissionMode == "bypassPermissions")
            args.Add("--allow-all-tools");
        if (!string.IsNullOrWhiteSpace(request.Model))
        {
            args.Add("--model");
            args.Add(request.Model!);
        }
        return args;
    }
}
