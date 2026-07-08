using HeadlessCoder.Claude;

namespace HeadlessCoder.Agents;

/// <summary>
/// Google Gemini CLI (https://github.com/google-gemini/gemini-cli). Driven in
/// non-interactive mode: `gemini -p "&lt;prompt&gt;"`.
/// </summary>
public sealed class GeminiProvider : GenericCliProvider
{
    public override string Id => "gemini";
    public override string DisplayName => "Gemini CLI";
    protected override string ExecutableName => "gemini";

    protected override string? ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini");

    protected override string InstallHint =>
        "Install with `npm install -g @google/gemini-cli`, then run `gemini` once to authenticate.";

    protected override IEnumerable<string> BuildArgs(SendMessageRequest request)
    {
        var args = new List<string> { "--prompt", request.Message };
        if (request.PermissionMode == "bypassPermissions")
            args.Add("--yolo"); // auto-approve tool actions
        if (!string.IsNullOrWhiteSpace(request.Model))
        {
            args.Add("--model");
            args.Add(request.Model!);
        }
        return args;
    }
}
