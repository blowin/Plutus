namespace Plutus.Domain.Minifier.Languages;

/// <summary>SQL processor, including PostgreSQL dollar-quoted bodies and T-SQL bracket identifiers.</summary>
public sealed class SqlLanguageMinifier(SourceProcessingContext? context = null)
    : SyntaxLanguageMinifier([LanguageId.Sql], "SQL Minifier (Removes inline -- and block /*...*/ comments)", context),
      IMaintainedLanguageProcessor;
