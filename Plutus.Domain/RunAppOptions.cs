using System.Text;
using Plutus.Domain.Diffing;
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
    public required Encoding TargetEncoding { get; init; }
    public required int MaxConcurrency { get; init; }
    public required string Format { get; init; }
    public required string? Branch { get; init; }
    public required string? Commit { get; init; }
    public TokenBudgetOptions? TokenBudget { get; init; }

    /// <summary>When set, the bundle is restricted to files changed relative to this diff scope.</summary>
    public GitDiffOptions? Diff { get; init; }
}
