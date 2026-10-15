using Plutus.Domain.Comments;
using Plutus.Domain.Minifier.Languages;

namespace Plutus.Domain.Minifier;

/// <summary>
/// Builds the set of language processors for a run: one entry per language in
/// <see cref="LanguageCommentSyntaxRegistry"/>, each carrying the strategy that answers for it. A language backed
/// by a maintained ANTLR grammar gets the token-channel stripper; every other language gets lexical scanning.
/// </summary>
public static class LanguageMinifierCatalog
{
    public static IReadOnlyList<ILanguageMinifier> Create(
        CommentStripOptions? options = null,
        SourceProcessingMode mode = SourceProcessingMode.Minify,
        ICommentStripObserver? observer = null,
        CommentStrippingPlan? plan = null,
        GrammarInventory? inventory = null,
        IReadOnlyDictionary<string, ICommentChannelLexer>? channelLexers = null)
    {
        var grammarBacked = plan?.GrammarBackedLanguages ?? [];
        var minifiers = new List<ILanguageMinifier>();

        foreach (var language in LanguageCommentSyntaxRegistry.Languages)
        {
            var stripper = ResolveStripper(language, grammarBacked, inventory, channelLexers);
            minifiers.Add(Create(language, options, mode, stripper, observer));
        }

        return minifiers;
    }

    /// <summary>
    /// Picks the stripper for one language. ANTLR token channels are used only when the plan names the language
    /// and the inventory ships both a grammar and a lexer for it; anything else is lexical scanning.
    /// </summary>
    public static ICommentStripper ResolveStripper(
        string language,
        IReadOnlyCollection<string> grammarBackedLanguages,
        GrammarInventory? inventory,
        IReadOnlyDictionary<string, ICommentChannelLexer>? channelLexers)
    {
        var isGrammarBacked = grammarBackedLanguages.Contains(language, StringComparer.OrdinalIgnoreCase);
        if (isGrammarBacked
            && inventory is not null && inventory.TryGet(language, out var grammar)
            && channelLexers is not null && channelLexers.TryGetValue(language, out var lexer))
        {
            return new AntlrCommentChannelStripper(grammar, lexer);
        }

        return LexicalCommentStripper.Instance;
    }

    /// <summary>Description shown for a language that has no dedicated minifier class.</summary>
    public static string Describe(string language, SourceProcessingMode mode)
    {
        var displayName = LanguageCommentSyntaxRegistry.Find(language)?.DisplayName ?? language;
        return mode == SourceProcessingMode.StripComments
            ? $"{displayName} Comment Stripper (lexical scanner)"
            : $"{displayName} Minifier (lexical comment scanner, collapses blank lines)";
    }

    private static ILanguageMinifier Create(
        string language,
        CommentStripOptions? options,
        SourceProcessingMode mode,
        ICommentStripper stripper,
        ICommentStripObserver? observer) => language switch
        {
            "csharp" => new CSharpLanguageMinifier(options, mode, stripper, observer),
            "go" => new GoLanguageMinifier(options, mode, stripper, observer),
            "java" => new JavaLanguageMinifier(options, mode, stripper, observer),
            "php" => new PhpLanguageMinifier(options, mode, stripper, observer),
            "rust" => new RustLanguageMinifier(options, mode, stripper, observer),
            "sql" => new SqlLanguageMinifier(options, mode, stripper, observer),
            "javascript" or "typescript" or "jsx" or "tsx" or "mjs" or "cjs" =>
                new JavaScriptLanguageMinifier(language, options, mode, stripper, observer),
            "css" or "scss" or "sass" or "less" => new StyleLanguageMinifier(language, options, mode, stripper, observer),
            "html" or "htm" => new HtmlLanguageMinifier(language, options, mode, stripper, observer),
            _ => new SyntaxLanguageMinifier(language, Describe(language, mode), options, mode, stripper, observer),
        };
}
