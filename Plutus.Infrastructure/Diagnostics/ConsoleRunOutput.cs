using Plutus.Domain.Diagnostics;

namespace Plutus.Infrastructure.Diagnostics;

/// <summary>
/// The console binding of <see cref="IRunOutput"/>: results go to standard output, warnings and errors to standard
/// error, and every message is framed here — which is what keeps the wording of a run identical no matter which
/// service is speaking.
/// </summary>
public sealed class ConsoleRunOutput(TextWriter? output = null, TextWriter? warn = null, TextWriter? error = null) : IRunOutput
{
    private readonly TextWriter _outWriter = output ?? Console.Out;
    private readonly TextWriter _warnWriter = warn ?? Console.Error;
    private readonly TextWriter _errorWriter = error ?? Console.Error;

    // Console.Out and Console.Error are re-created when the output encoding changes, so they are read per write
    // instead of being captured when this instance is built.
    public void Info(string message) => _outWriter.WriteLine(message);

    public void Warning(string message) => _warnWriter.WriteLine($"Warning: {message}");

    public void Error(string message) => _errorWriter.WriteLine($"Error: {message}");
}
