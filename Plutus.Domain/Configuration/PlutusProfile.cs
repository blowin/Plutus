using System.Text.Json.Serialization;
using Plutus.Domain.Comments;
using Plutus.Domain.Tokens;

namespace Plutus.Domain.Configuration;

/// <summary>
/// A <c>.plutusrc</c> profile: the defaults a repository wants every run to start from, so a long command line full
/// of exclusion targets can be replaced by a checked-in file.
/// <para>
/// Every member is optional, because the file states only what it wants to change: <c>null</c> means "not mentioned
/// in the profile", which is what lets an explicit command-line flag win over it without guessing.
/// </para>
/// </summary>
public sealed record PlutusProfile
{
    // ---- path rules -----------------------------------------------------------------------------------------

    /// <summary>Inclusive file patterns, as if repeated <c>--include-files</c>.</summary>
    public IReadOnlyList<string>? IncludeFiles { get; init; }

    /// <summary>Inclusive directory patterns, as if repeated <c>--include-dirs</c>.</summary>
    public IReadOnlyList<string>? IncludeDirs { get; init; }

    /// <summary>
    /// Extensions to restrict the bundle to, in the usual spelling (<c>.cs</c>, <c>ts</c>, <c>*.razor</c>). They are
    /// the most common inclusive filter, so they have their own key instead of being written as patterns.
    /// </summary>
    public IReadOnlyList<string>? IncludeExtensions { get; init; }

    /// <summary>Patterns excluding files and directories, as if repeated <c>--exclude</c>.</summary>
    public IReadOnlyList<string>? Exclude { get; init; }

    /// <summary>Patterns excluding directories, as if repeated <c>--exclude-dirs</c>.</summary>
    public IReadOnlyList<string>? ExcludeDirs { get; init; }

    /// <summary>Patterns excluding files, as if repeated <c>--exclude-files</c>.</summary>
    public IReadOnlyList<string>? ExcludeFiles { get; init; }

    /// <summary>Read root <c>.gitignore</c> files. Equivalent to the absence of <c>--no-gitignore</c>.</summary>
    public bool? UseGitignore { get; init; }

    /// <summary>Use the built-in junk exclusions. Equivalent to <c>--use-default-excludes</c>.</summary>
    public bool? UseDefaultExcludes { get; init; }

    /// <summary>Include files normally treated as dangerous. Equivalent to <c>--allow-dangerous-files</c>.</summary>
    public bool? AllowDangerousFiles { get; init; }

    // ---- output preferences ---------------------------------------------------------------------------------

    /// <summary>
    /// The bundle path. A relative path is resolved against the profile's own directory, so a checked-in profile
    /// writes next to the repository it describes rather than wherever the command happened to run.
    /// </summary>
    public string? Output { get; init; }

    /// <summary>Bundle format (<c>md</c>, <c>json</c>, <c>xml</c>). Equivalent to <c>--format</c>.</summary>
    public string? Format { get; init; }

    /// <summary>Text encoding name. Equivalent to <c>--encoding</c>.</summary>
    public string? Encoding { get; init; }

    /// <summary>Largest file to include, e.g. <c>512K</c>. Equivalent to <c>--max-file-size</c>.</summary>
    public string? MaxFileSize { get; init; }

    /// <summary>Source-token budget. Equivalent to <c>--max-tokens</c>.</summary>
    public int? MaxTokens { get; init; }

    /// <summary>Budget overflow behavior. Equivalent to <c>--token-overflow-policy</c>.</summary>
    public TokenOverflowPolicy? TokenOverflowPolicy { get; init; }

    /// <summary>Selection strategy for the <c>select</c> policy. Equivalent to <c>--token-selection-strategy</c>.</summary>
    public TokenSelectionStrategy? TokenSelectionStrategy { get; init; }

    /// <summary>Concurrent file-processing tasks. Equivalent to <c>--max-concurrency</c>.</summary>
    public int? MaxConcurrency { get; init; }

    /// <summary>Carry file contents or only the change itself. Equivalent to the absence of <c>--diff-only</c>.</summary>
    public bool? IncludeFileContents { get; init; }

    // ---- source processing ----------------------------------------------------------------------------------

    /// <summary>Remove comments and collapse whitespace. Equivalent to <c>--minify</c>.</summary>
    public bool? Minify { get; init; }

    /// <summary>Remove comments only. Equivalent to <c>--strip-comments</c>.</summary>
    public bool? StripComments { get; init; }

    /// <summary>Keep documentation comments while stripping or minifying. Equivalent to <c>--preserve-doc-comments</c>.</summary>
    public bool? PreserveDocComments { get; init; }

    /// <summary>How comments are located. Equivalent to <c>--comment-strategy</c>.</summary>
    public CommentStrategyRequest? CommentStrategy { get; init; }

    /// <summary>Reduce files to signatures. Equivalent to <c>--signatures-only</c>.</summary>
    public bool? SignaturesOnly { get; init; }
}
