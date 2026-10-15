namespace Plutus.Domain.Diagnostics;

/// <summary>
/// Where a run says what it did. Services report here instead of writing to the console, so a run has exactly one
/// output target that the host chooses: the CLI binds it to the console, a test binds it to a recorder, and a
/// library caller binds it to nothing.
/// <para>
/// Callers pass a plain message — no <c>Error:</c> or <c>Warning:</c> prefix — because how a message is framed is a
/// presentation decision and belongs to the implementation. That is what keeps the wording of a run uniform no
/// matter which service is speaking.
/// </para>
/// </summary>
public interface IRunOutput
{
    /// <summary>Progress or a result the user is expected to read.</summary>
    void Info(string message);

    /// <summary>Something was skipped, degraded or ignored; the run continues.</summary>
    void Warning(string message);

    /// <summary>Something failed, so the current step is abandoned or the run stops.</summary>
    void Error(string message);
}

/// <summary>An output that discards everything, used when a caller does not collect run output.</summary>
public sealed class NullRunOutput : IRunOutput
{
    public static readonly IRunOutput Instance = new NullRunOutput();

    public void Info(string message)
    {
    }

    public void Warning(string message)
    {
    }

    public void Error(string message)
    {
    }
}
