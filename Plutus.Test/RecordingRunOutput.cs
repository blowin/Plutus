using Plutus.Domain.Diagnostics;

namespace Plutus.Test;

/// <summary>
/// An <see cref="IRunOutput"/> that keeps every message, so tests can assert on the output of a run without a
/// console. Warnings and errors are recorded with the prefix the console implementation would add, which is how the
/// framing itself is covered.
/// </summary>
public sealed class RecordingRunOutput : IRunOutput
{
    private readonly List<string> _lines = [];
    private readonly List<string> _warnings = [];
    private readonly List<string> _errors = [];

    /// <summary>Everything written through <see cref="Info"/>, in order.</summary>
    public IReadOnlyList<string> Lines => _lines;

    public IReadOnlyList<string> Warnings => _warnings;

    public IReadOnlyList<string> Errors => _errors;

    public void Info(string message) => _lines.Add(message);

    public void Warning(string message) => _warnings.Add($"Warning: {message}");

    public void Error(string message) => _errors.Add($"Error: {message}");
}
