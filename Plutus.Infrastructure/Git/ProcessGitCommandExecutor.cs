using System.Diagnostics;
using System.Text;

namespace Plutus.Infrastructure.Git;

/// <summary>
/// Starts the <c>git</c> executable once per call and captures its text output. No shell is involved,
/// so arguments are passed verbatim and cannot be re-interpreted by a command interpreter.
/// </summary>
public sealed class ProcessGitCommandExecutor : IGitCommandExecutor
{
    private const string Executable = "git";

    public async Task<GitCommandResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo(Executable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new GitCommandResult(process.ExitCode, (await standardOutput.ConfigureAwait(false)).Trim(), (await standardError.ConfigureAwait(false)).Trim());
    }
}
