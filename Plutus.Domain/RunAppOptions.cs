using System.Text;
using Plutus.Domain.Comments;
using Plutus.Domain.Configuration;
using Plutus.Domain.Diffing;
using Plutus.Domain.RemoteRepository;
using Plutus.Domain.Structure;
using Plutus.Domain.Tokens;

namespace Plutus.Domain;

public record RunAppOptions
{
    public required List<string> ProjectPath { get; init; }
    public required string? OutputPath { get; init; }
    public required bool UseGitignore { get; init; }
    public required List<string> ExcludePatterns { get; init; }
    public required bool UseDefaultExcludes { get; init; }
    public required bool AllowDangerous { get; init; }
    public required FileSize MaxFileSize { get; init; }
    public required List<string> ExcludeDirPatterns { get; init; }
    public required List<string> ExcludeFilePatterns { get; init; }
    public required List<string> IncludeDirPatterns { get; init; }
    public required List<string> IncludeFilePatterns { get; init; }
    public bool Minify { get; init; }

    /// <summary>
    /// Remove comments without touching whitespace or line endings (<c>--strip-comments</c>). <see cref="Minify"/>
    /// is a superset, so when both are set the whitespace pass runs as well.
    /// </summary>
    public bool StripComments { get; init; }

    /// <summary>
    /// Keep documentation comments when stripping or minifying (<c>--preserve-doc-comments</c>). Off by default:
    /// documentation comments are removed like any other comment unless this is set.
    /// </summary>
    public bool PreserveDocumentationComments { get; init; }

    /// <summary>What <c>--comment-strategy</c> asked for, before it is resolved against the grammar inventory.</summary>
    public CommentStrategyRequest CommentStrategy { get; init; } = CommentStrategyRequest.Auto;

    /// <summary>
    /// Reduce every supported file to its signatures (<c>--signatures-only</c>): a parse-tree pass replaces
    /// implementation bodies with a structural terminator. Independent of the comment flags.
    /// </summary>
    public bool SignaturesOnly { get; init; }

    /// <summary>How the structural pass writes a stripped body. Defaults to a semicolon.</summary>
    public SignatureExtractionOptions SignatureOptions { get; init; } = SignatureExtractionOptions.Default;

    /// <summary>
    /// The strategy resolved for this run, or <c>null</c> when no comment processing was requested. Resolving it
    /// once keeps the CLI error message and the pipeline in agreement, and makes the choice reportable.
    /// </summary>
    public CommentStrippingPlan? CommentStripPlan { get; init; }

    public required Encoding TargetEncoding { get; init; }
    public required int MaxConcurrency { get; init; }
    public required string Format { get; init; }
    public required string? Branch { get; init; }
    public required string? Commit { get; init; }
    public TokenBudgetOptions? TokenBudget { get; init; }

    /// <summary>When set, the bundle is restricted to files changed relative to this diff scope.</summary>
    public GitDiffOptions? Diff { get; init; }

    /// <summary>
    /// The <c>.plutusrc</c> profile this run was configured from, when one was found, together with the keys it
    /// supplied. It makes the configuration of a run reportable instead of implicit.
    /// </summary>
    public ProfileProvenance? Profile { get; init; }

    /// <summary>
    /// When <c>false</c> the bundle carries only metadata, the changed-path list and the raw diff, which is
    /// the "review the change, not the source" mode selected by <c>--diff-only</c>.
    /// </summary>
    public bool IncludeFileContents { get; init; } = true;

    /// <summary>Archive extraction limits and behaviors for remote repository downloads.</summary>
    public ArchiveExtractionOptions ArchiveExtraction { get; init; } = ArchiveExtractionOptions.Default;
}
