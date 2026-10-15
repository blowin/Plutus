namespace Plutus.Domain.Structure;

/// <summary>Controls how structural signature extraction rewrites bodies.</summary>
public sealed record SignatureExtractionOptions
{
    /// <summary>The default: bodies become <c>;</c>, everything else in the file is left alone.</summary>
    public static readonly SignatureExtractionOptions Default = new();

    public BodyTerminator Terminator { get; init; } = BodyTerminator.Semicolon;

    /// <summary>The text a stripped body is replaced with, derived from <see cref="Terminator"/>.</summary>
    public string TerminatorText => Terminator.ToText();
}
