using Plutus.Infrastructure.Git;

namespace Plutus.Test.Diffing;

/// <summary>
/// Deterministic <see cref="IGitCommandExecutor"/> double: every call is recorded and answered from a
/// pre-seeded queue in order. Calls beyond the seeded queue fail like git would, which lets a test prove
/// that a failing step stops the sequence.
/// </summary>
internal sealed class StubGitCommandExecutor : IGitCommandExecutor
{
    private readonly Queue<GitCommandResult> _responses = new();
    private readonly List<RecordedGitCall> _calls = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<RecordedGitCall> Calls
    {
        get
        {
            lock (_gate)
            {
                return _calls.ToArray();
            }
        }
    }

    public Task<GitCommandResult> RunAsync(string? workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _calls.Add(new RecordedGitCall(workingDirectory, arguments.ToArray()));

            var result = _responses.Count > 0
                ? _responses.Dequeue()
                : new GitCommandResult(128, string.Empty, "no canned response configured");

            return Task.FromResult(result);
        }
    }

    internal sealed record RecordedGitCall(string? WorkingDirectory, IReadOnlyList<string> Arguments);

    /// <summary>Answers with the given outputs in order; every one of them succeeds.</summary>
    public static StubGitCommandExecutor Succeeding(params string[] standardOutputs) =>
        Seeded(standardOutputs.Select(output => new GitCommandResult(0, output, string.Empty)).ToArray());

    public static StubGitCommandExecutor Seeded(params GitCommandResult[] results)
    {
        var executor = new StubGitCommandExecutor();
        foreach (var result in results)
        {
            executor._responses.Enqueue(result);
        }

        return executor;
    }

    public static GitCommandResult Success(string standardOutput) => new(0, standardOutput, string.Empty);

    public static GitCommandResult Failure(string standardError = "") => new(128, string.Empty, standardError);
}
