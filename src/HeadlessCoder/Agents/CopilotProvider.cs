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

    // Copilot records each session under ~/.copilot/session-state/<id>/events.jsonl.
    protected override ICliHistoryStore? HistoryStore { get; } = new CopilotSessionStore();

    // Models Copilot CLI can route to (leave "Default" = auto). Adjust as GitHub adds models.
    protected override IReadOnlyList<AgentOption> ModelOptions { get; } = new AgentOption[]
    {
        new("gpt-5", "GPT-5"),
        new("gpt-5-mini", "GPT-5 mini"),
        new("claude-sonnet-4.5", "Claude Sonnet 4.5"),
        new("claude-sonnet-4", "Claude Sonnet 4"),
        new("o3", "o3"),
    };

    protected override IReadOnlyList<AgentOption> PermissionModeOptions { get; } = new AgentOption[]
    {
        new("default", "Default"),
        new("bypassPermissions", "Allow all tools"),
    };

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
