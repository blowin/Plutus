using FluentAssertions;
using Plutus.Domain;
using Plutus.Domain.Comments;

namespace Plutus.Test.Comments;

/// <summary>
/// The grammar inventory is what keeps ANTLR support honest: it is empty for this repository, a broken manifest
/// fails loudly, and channel names resolve to numbers before anything is stripped.
/// </summary>
public class GrammarInventoryTests
{
    private const string ValidManifest = """
        {
          "grammars": [
            {
              "language": "sql",
              "grammarFile": "SqlLexer.g4",
              "lexerClass": "Plutus.Domain.Comments.Antlr.SqlLexer",
              "commentTokenTypes": ["SIMPLE_COMMENT", "BRACKETED_COMMENT"],
              "documentationTokenTypes": ["DOC_COMMENT"],
              "commentChannels": [1, "HIDDEN", "COMMENTS"],
              "namedChannels": { "COMMENTS": 2 }
            }
          ]
        }
        """;

    [Fact]
    public void Discover_WithoutAManifest_ReportsAnEmptyInventoryAndWhereItLooked()
    {
        var inventory = GrammarInventory.Discover(Path.Combine(Path.GetTempPath(), "plutus-missing", "grammar-manifest.json"));

        inventory.HasGrammars.Should().BeFalse();
        inventory.LoadError.Should().BeNull();
        inventory.Describe().Should().Contain("none").And.Contain("probed");
    }

    [Fact]
    public void Parse_ResolvesNumericAndNamedChannels()
    {
        var inventory = GrammarInventory.Parse(ValidManifest);

        inventory.LoadError.Should().BeNull();
        var entry = inventory.Entries.Should().ContainSingle().Subject;

        entry.Language.Should().Be(LanguageId.Sql);
        entry.LexerClass.Should().Contain("SqlLexer");
        entry.ResolveCommentChannelNumbers(out var unresolved).Should().Equal(1, 2);
        unresolved.Should().BeEmpty();
    }

    [Fact]
    public void Parse_KeepsTheWrittenFormOfEveryChannel()
    {
        var entry = GrammarInventory.Parse(ValidManifest).Entries.Single();

        entry.CommentChannels.Select(channel => channel.Raw).Should().Equal("1", "HIDDEN", "COMMENTS");
        entry.CommentChannels[0].Number.Should().Be(1);
        entry.CommentChannels[1].Name.Should().Be("HIDDEN");
        entry.CommentChannels[1].Resolve(entry.NamedChannels).Should().Be(1, "HIDDEN is an ANTLR built-in channel");
        entry.CommentChannels[2].Resolve(entry.NamedChannels).Should().Be(2, "grammar-declared channels come from the manifest");
    }

    [Fact]
    public void Parse_UnknownChannelName_IsALoadErrorInsteadOfASilentDrop()
    {
        var inventory = GrammarInventory.Parse(
            """
            { "grammars": [ { "language": "sql", "grammarFile": "SqlLexer.g4", "commentChannels": ["NOWHERE"] } ] }
            """);

        inventory.HasGrammars.Should().BeFalse();
        inventory.LoadError.Should().Contain("NOWHERE");
    }

    [Fact]
    public void Parse_GrammarWithoutACommentDeclaration_IsALoadError()
    {
        var inventory = GrammarInventory.Parse(
            """
            { "grammars": [ { "language": "sql", "grammarFile": "SqlLexer.g4" } ] }
            """);

        inventory.LoadError.Should().Contain("commentChannels");
    }

    [Fact]
    public void Parse_ManifestWithoutAFileForTheGrammar_IsALoadError()
    {
        var directory = Directory.CreateTempSubdirectory("plutus-grammar-");
        var manifestPath = Path.Combine(directory.FullName, "grammar-manifest.json");
        File.WriteAllText(manifestPath, ValidManifest);

        var inventory = GrammarInventory.Load(manifestPath);

        inventory.LoadError.Should().Contain("not next to the manifest");
        directory.Delete(recursive: true);
    }

    [Fact]
    public void Parse_ManifestWithTheGrammarFileOnDisk_IsAccepted()
    {
        var directory = Directory.CreateTempSubdirectory("plutus-grammar-");
        var manifestPath = Path.Combine(directory.FullName, "grammar-manifest.json");
        File.WriteAllText(Path.Combine(directory.FullName, "SqlLexer.g4"), "lexer grammar SqlLexer;");
        File.WriteAllText(manifestPath, ValidManifest);

        var inventory = GrammarInventory.Load(manifestPath);

        inventory.LoadError.Should().BeNull();
        inventory.TryGet("SQL", out var entry).Should().BeTrue("language lookup is case-insensitive");
        entry.GrammarFile.Should().Be("SqlLexer.g4");
        directory.Delete(recursive: true);
    }

    [Fact]
    public void Parse_InvalidJson_IsReported()
    {
        GrammarInventory.Parse("{ not json").LoadError.Should().Contain("not valid JSON");
    }

    [Fact]
    public void Parse_ManifestWithoutTheGrammarsArray_IsReported()
    {
        GrammarInventory.Parse("{}").LoadError.Should().Contain("grammars");
    }

    [Fact]
    public void Discover_EnvironmentVariablePointingNowhere_IsAnErrorNotAnEmptyInventory()
    {
        var variableBackup = Environment.GetEnvironmentVariable(GrammarInventory.ManifestPathVariable);
        try
        {
            Environment.SetEnvironmentVariable(GrammarInventory.ManifestPathVariable, Path.Combine(Path.GetTempPath(), "plutus-missing-manifest.json"));

            var inventory = GrammarInventory.Discover();

            inventory.HasGrammars.Should().BeFalse();
            inventory.LoadError.Should().Contain(GrammarInventory.ManifestPathVariable);
        }
        finally
        {
            Environment.SetEnvironmentVariable(GrammarInventory.ManifestPathVariable, variableBackup);
        }
    }

    [Fact]
    public void DefaultManifestPath_PointsNextToTheApplication()
    {
        GrammarInventory.DefaultManifestPath.Should().Be(
            Path.Combine(AppContext.BaseDirectory, "grammars", "grammar-manifest.json"));
    }

    [Fact]
    public void Description_ListsLanguageGrammarAndChannels()
    {
        var inventory = GrammarInventory.Parse(ValidManifest);

        inventory.Describe().Should().Be("sql (SqlLexer.g4, channel(s) 1/HIDDEN/COMMENTS)");
    }
}
