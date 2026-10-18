using Plutus.Domain.ArchiveExtractor;
using Plutus.Domain.Comments;
using Plutus.Domain.Structure;
using Plutus.Domain.Tokens;

namespace Plutus.Domain.Configuration;

/// <summary>
/// What every source of configuration asked for, with <c>null</c> wherever a source said nothing. The command line
/// fills it from the flags the user actually typed, the resolver merges it with a profile, and the defaults live in
/// the resolver — so "not asked" and "asked for the default" stay distinguishable.
/// </summary>
public sealed record RunOptionsRequest
{
    /// <summary>The paths to bundle. Always from the command line: a profile cannot decide what is being bundled.</summary>
    public required IReadOnlyList<string> ProjectPath { get; init; }

    public string? OutputPath { get; init; }

    public bool? UseGitignore { get; init; }

    public IReadOnlyList<string>? Exclude { get; init; }

    public bool? UseDefaultExcludes { get; init; }

    public bool? AllowDangerousFiles { get; init; }

    public FileSize? MaxFileSize { get; init; }

    public IReadOnlyList<string>? ExcludeDirs { get; init; }

    public IReadOnlyList<string>? ExcludeFiles { get; init; }

    public IReadOnlyList<string>? IncludeDirs { get; init; }

    public IReadOnlyList<string>? IncludeFiles { get; init; }

    public string? Encoding { get; init; }

    public int? MaxConcurrency { get; init; }

    public string? Format { get; init; }

    public int? MaxTokens { get; init; }

    public TokenOverflowPolicy? TokenOverflowPolicy { get; init; }

    public TokenSelectionStrategy? TokenSelectionStrategy { get; init; }

    public bool? IncludeFileContents { get; init; }

    public bool? Minify { get; init; }

    public bool? StripComments { get; init; }

    public bool? PreserveDocComments { get; init; }

    public CommentStrategyRequest? CommentStrategy { get; init; }

    public bool? SignaturesOnly { get; init; }

    /// <summary>How the structural pass writes a stripped body. Comes from the command line.</summary>
    public SignatureExtractionOptions? SignatureOptions { get; init; }

    /// <summary>Remote revision, which cannot come from a profile: it identifies what is being bundled.</summary>
    public string? Branch { get; init; }

    public string? Commit { get; init; }

    /// <summary>The diff scope asked for on the command line, if any.</summary>
    public DiffRequest? Diff { get; init; }

    /// <summary>Archive extraction limits and behaviors for remote repository downloads.</summary>
    public ArchiveExtractionOptions? ArchiveExtraction { get; init; }
}

/// <summary>A diff scope as requested, before the resolved limits are attached to it.</summary>
public sealed record DiffRequest
{
    public string? From { get; init; }

    public string? To { get; init; }

    /// <summary>True for <c>--diff-only</c>: the change itself instead of the source.</summary>
    public bool Only { get; init; }

    public bool IncludeUntracked { get; init; }
}

/// <summary>
/// Which profile keys took effect, and which the command line overrode. It is what the run report prints, so a
/// profile that stopped applying is visible instead of mysterious.
/// </summary>
public sealed record ProfileProvenance
{
    public required string Path { get; init; }

    /// <summary>Keys the profile set to something other than the built-in default, so they decided the run.</summary>
    public IReadOnlyList<string> AppliedKeys { get; init; } = [];

    /// <summary>Keys the profile set to the value a run would use anyway.</summary>
    public IReadOnlyList<string> DefaultedKeys { get; init; } = [];

    /// <summary>Keys the profile set and an explicit flag replaced.</summary>
    public IReadOnlyList<string> OverriddenKeys { get; init; } = [];
}

/// <summary>The resolved run, or the reason the sources could not be merged.</summary>
public sealed record RunOptionsResolution
{
    public RunAppOptions? Options { get; init; }

    public string? Error { get; init; }

    public bool Succeeded => Error is null;
}
