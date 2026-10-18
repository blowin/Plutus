using Plutus.Domain.Diagnostics;

namespace Plutus.Infrastructure.Diagnostics;

/// <summary>
/// The console binding of <see cref="IRunOutput"/>: results go to standard output, warnings and errors to standard
/// error, and every message is framed here — which is what keeps the wording of a run identical no matter which
/// service is speaking.
/// </summary>
public sealed class TextWriterRunOutput(TextWriter? output, TextWriter? error) : IRunOutput
{
    // Console.Out and Console.Error are re-created when the output encoding changes, so they are read per write
    // instead of being captured when this instance is built.
    public void Info(string message) => (output ?? Console.Out).WriteLine(message);

    public void Warning(string message) => (error ?? Console.Error).WriteLine($"Warning: {message}");

    public void Error(string message) => (error ?? Console.Error).WriteLine($"Error: {message}");
}
