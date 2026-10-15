namespace Plutus.Domain.Comments;

/// <summary>
/// The resolved answer to "how will comments be removed in this run", including the languages a maintained
/// grammar covers. It is produced once per run and printed, so the strategy is observable rather than implied.
/// </summary>
public sealed record CommentStrippingPlan
{
    public required CommentStrategyRequest Request { get; init; }

    /// <summary>The strategy used for languages without a maintained grammar.</summary>
    public required CommentStripStrategy DefaultStrategy { get; init; }

    /// <summary>Why this strategy was chosen; shown in the run report.</summary>
    public required string Rationale { get; init; }

    /// <summary>Languages that will use ANTLR token channels because a maintained grammar covers them.</summary>
    public IReadOnlyList<LanguageId> GrammarBackedLanguages { get; init; } = [];

    public CommentStripOptions ToOptions(bool preserveDocumentationComments) => new()
    {
        Request = Request,
        PreserveDocumentationComments = preserveDocumentationComments,
        Strategy = DefaultStrategy,
    };
}

/// <summary>
/// Resolves the requested strategy against the grammars the project actually maintains. It is a service rather than
/// a static helper so a host can inject it and a test can substitute it, and so the resolution has one owner.
/// </summary>
public sealed class CommentStrippingPlanner
{
    /// <summary>
    /// Resolves the plan, or fails with a reason. A broken manifest is a failure even for <c>auto</c>: falling
    /// back to lexical scanning because a grammar could not be read is exactly the silent downgrade this feature
    /// is meant to prevent.
    /// </summary>
    public bool TryResolve(CommentStrategyRequest request, GrammarInventory inventory, out CommentStrippingPlan plan, out string? error)
    {
        plan = new CommentStrippingPlan
        {
            Request = request,
            DefaultStrategy = CommentStripStrategy.Lexical,
            Rationale = "lexical scanning",
        };
        error = null;

        // Explicit lexical scanning does not consult the inventory at all, so a broken manifest cannot break it.
        if (request == CommentStrategyRequest.Lexical)
        {
            plan = plan with { Rationale = "explicit lexical scanning requested" };
            return true;
        }

        if (inventory.LoadError is { } loadError)
        {
            error = $"the grammar inventory cannot be used: {loadError}";
            return false;
        }

        // LanguageId normalizes its value, so plain Distinct is already case-insensitive.
        var grammarLanguages = inventory.Entries.Select(entry => entry.Language).Distinct().ToList();

        switch (request)
        {
            case CommentStrategyRequest.Auto when inventory.HasGrammars:
                plan = plan with
                {
                    DefaultStrategy = CommentStripStrategy.Lexical,
                    Rationale = $"ANTLR token channels for [{string.Join(", ", grammarLanguages)}], lexical scanning for the rest",
                    GrammarBackedLanguages = grammarLanguages,
                };
                return true;

            case CommentStrategyRequest.Auto:
                // The probed manifest path belongs in the failure message the user sees when they ask for ANTLR, not
                // in the rationale of every run that simply scans lexically.
                plan = plan with { Rationale = "lexical scanning; no project-maintained grammar" };
                return true;

            case CommentStrategyRequest.Antlr when inventory.HasGrammars:
                plan = plan with
                {
                    DefaultStrategy = CommentStripStrategy.AntlrTokenChannel,
                    Rationale = $"ANTLR token channels for [{string.Join(", ", grammarLanguages)}]",
                    GrammarBackedLanguages = grammarLanguages,
                };
                return true;

            default:
                error = "--comment-strategy antlr requires at least one project-maintained ANTLR grammar, and this build ships none "
                        + $"({inventory.Describe()}). Provide a manifest and its .g4/lexer files, or use --comment-strategy lexical.";
                return false;
        }
    }
}
