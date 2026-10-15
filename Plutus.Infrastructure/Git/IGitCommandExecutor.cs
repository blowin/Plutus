namespace Plutus.Infrastructure.Git;

/// <summary>Outcome of one git invocation: exit code, standard output and standard error.</summary>
public readonly record struct GitCommandResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}

/// <summary>
/// Runs git commands through the local shell wrapper so callers stay independent of process plumbing.
/// </summary>
public interface IGitCommandExecutor
{
    Task<GitCommandResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
}
