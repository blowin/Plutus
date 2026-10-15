namespace Plutus.Domain.Minifier.Languages;

/// <summary>
/// JavaScript/TypeScript processor, serving the whole JS family. The lexical scanner protects template literals
/// (including comments inside <c>${...}</c> holes, which are code) and regular-expression literals such as
/// <c>/[/*]/</c>; the language is passed per call, so one processor answers for every dialect.
/// </summary>
public sealed class JavaScriptLanguageMinifier(SourceProcessingContext? context = null)
    : SyntaxLanguageMinifier(
        [LanguageId.JavaScript, LanguageId.TypeScript, LanguageId.Jsx, LanguageId.Tsx, LanguageId.Mjs, LanguageId.Cjs],
        "JavaScript/TypeScript Minifier (Removes standard comments, protects template literals)",
        context),
      IMaintainedLanguageProcessor;
