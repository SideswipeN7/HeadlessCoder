using HeadlessCoder.Claude;

namespace HeadlessCoder.Agents;

// Additional agent CLIs, driven as one-shot headless prompts (best-effort flags;
// each is verified only for detection, not for exact invocation — adjust BuildArgs
// once the CLI is installed). All subclass GenericCliProvider.

/// <summary>Aider (https://aider.chat) — `aider --message "…"`.</summary>
public sealed class AiderProvider : GenericCliProvider
{
    public override string Id => "aider";
    public override string DisplayName => "Aider";
    protected override string ExecutableName => "aider";
    protected override string? ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aider");
    protected override string InstallHint =>
        "Install with `python -m pip install aider-install && aider-install`, then run `aider` once.";

    protected override IEnumerable<string> BuildArgs(SendMessageRequest r)
    {
        var a = new List<string> { "--message", r.Message, "--no-auto-commits" };
        if (r.PermissionMode == "bypassPermissions") a.Add("--yes-always");
        if (!string.IsNullOrWhiteSpace(r.Model)) { a.Add("--model"); a.Add(r.Model!); }
        return a;
    }
}

/// <summary>OpenAI Codex CLI — `codex exec "…"` (non-interactive).</summary>
public sealed class CodexProvider : GenericCliProvider
{
    public override string Id => "codex";
    public override string DisplayName => "Codex CLI";
    protected override string ExecutableName => "codex";
    protected override string? ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
    protected override string InstallHint =>
        "Install with `npm install -g @openai/codex`, then run `codex` once to sign in.";

    protected override IEnumerable<string> BuildArgs(SendMessageRequest r)
    {
        var a = new List<string> { "exec", r.Message };
        if (!string.IsNullOrWhiteSpace(r.Model)) { a.Add("-m"); a.Add(r.Model!); }
        return a;
    }
}

/// <summary>opencode (https://opencode.ai) — `opencode run "…"`.</summary>
public sealed class OpencodeProvider : GenericCliProvider
{
    public override string Id => "opencode";
    public override string DisplayName => "opencode";
    protected override string ExecutableName => "opencode";
    protected override string? ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "opencode");
    protected override string InstallHint =>
        "Install with `npm install -g opencode-ai` (or `curl -fsSL https://opencode.ai/install | bash`).";

    protected override IEnumerable<string> BuildArgs(SendMessageRequest r)
    {
        var a = new List<string> { "run", r.Message };
        if (!string.IsNullOrWhiteSpace(r.Model)) { a.Add("-m"); a.Add(r.Model!); }
        return a;
    }
}

/// <summary>Cursor CLI agent — `cursor-agent -p "…"`.</summary>
public sealed class CursorProvider : GenericCliProvider
{
    public override string Id => "cursor";
    public override string DisplayName => "Cursor Agent";
    protected override string ExecutableName => "cursor-agent";
    protected override string? ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cursor");
    protected override string InstallHint =>
        "Install with `curl https://cursor.com/install -fsS | bash`, then run `cursor-agent` once to sign in.";

    protected override IEnumerable<string> BuildArgs(SendMessageRequest r)
    {
        var a = new List<string> { "-p", r.Message };
        if (!string.IsNullOrWhiteSpace(r.Model)) { a.Add("-m"); a.Add(r.Model!); }
        return a;
    }
}

/// <summary>DeepSeek CLI — `deepseek --prompt "…"` (best-effort).</summary>
public sealed class DeepSeekProvider : GenericCliProvider
{
    public override string Id => "deepseek";
    public override string DisplayName => "DeepSeek CLI";
    protected override string ExecutableName => "deepseek";
    protected override string? ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".deepseek");
    protected override string InstallHint =>
        "Install a DeepSeek CLI (e.g. `npm install -g deepseek-cli`), then set your DEEPSEEK_API_KEY.";

    protected override IEnumerable<string> BuildArgs(SendMessageRequest r)
    {
        var a = new List<string> { "--prompt", r.Message };
        if (!string.IsNullOrWhiteSpace(r.Model)) { a.Add("--model"); a.Add(r.Model!); }
        return a;
    }
}

/// <summary>Qwen Code (a Gemini-CLI fork) — `qwen --prompt "…"`.</summary>
public sealed class QwenProvider : GenericCliProvider
{
    public override string Id => "qwen";
    public override string DisplayName => "Qwen Code";
    protected override string ExecutableName => "qwen";
    protected override string? ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".qwen");
    protected override string InstallHint =>
        "Install with `npm install -g @qwen-code/qwen-code`, then run `qwen` once to authenticate.";

    // Qwen Code inherits Gemini CLI's on-disk chat store: ~/.qwen/tmp/<project>/chats/*.jsonl
    protected override ICliHistoryStore? HistoryStore { get; } = new GeminiFamilySessionStore(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".qwen", "tmp"));

    protected override IEnumerable<string> BuildArgs(SendMessageRequest r)
    {
        var a = new List<string> { "--prompt", r.Message };
        if (r.PermissionMode == "bypassPermissions") a.Add("--yolo");
        if (!string.IsNullOrWhiteSpace(r.Model)) { a.Add("--model"); a.Add(r.Model!); }
        return a;
    }
}
