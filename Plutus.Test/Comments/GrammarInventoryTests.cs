using FluentAssertions;
using Plutus.Domain;
using Plutus.Domain.Comments;
using Plutus.Test.Fakes;

namespace Plutus.Test.Comments;

/// <summary>
/// The grammar inventory is what keeps ANTLR support honest: it is empty for this repository, a broken manifest
/// fails loudly, and channel names resolve to numbers before anything is stripped. The tree it reads lives in memory,
/// so no test creates a directory or a file.
/// </summary>
public class GrammarInventoryTests
{
    /// <summary>The host path the in-memory provider pretends to be rooted at; only string operations use it.</summary>
    private static readonly string Root = OperatingSystem.IsWindows() ? @"C:\plutus-grammar-tests" : "/plutus-grammar-tests";

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
        var inventory = Loader().Discover(At("missing/grammar-manifest.json"));

        inventory.HasGrammars.Should().BeFalse();
        inventory.LoadError.Should().BeNull();
        inventory.Describe().Should().Contain("none").And.Contain("probed");
    }

    [Fact]
    public void Parse_ResolvesNumericAndNamedChannels()
    {
        var inventory = GrammarInventory.Parse(ValidManifest, null, _ => true);

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
        var entry = GrammarInventory.Parse(ValidManifest, null, _ => true).Entries.Single();

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
            """,
            null,
            _ => true);

        inventory.HasGrammars.Should().BeFalse();
        inventory.LoadError.Should().Contain("NOWHERE");
    }

    [Fact]
    public void Parse_GrammarWithoutACommentDeclaration_IsALoadError()
    {
        var inventory = GrammarInventory.Parse(
            """
            { "grammars": [ { "language": "sql", "grammarFile": "SqlLexer.g4" } ] }
            """,
            null,
            _ => true);

        inventory.LoadError.Should().Contain("commentChannels");
    }

    [Fact]
    public void Load_ManifestWithoutAFileForTheGrammar_IsALoadError()
    {
        // The manifest is there, the lexer it names is not: the inventory must refuse to pretend it is.
        var loader = Loader(new InMemoryFileProvider().AddFile("grammars/grammar-manifest.json", ValidManifest));

        var inventory = loader.Load(At("grammars/grammar-manifest.json"));

        inventory.LoadError.Should().Contain("not next to the manifest");
    }

    [Fact]
    public void Load_ManifestWithTheGrammarFileBesideIt_IsAccepted()
    {
        var loader = Loader(new InMemoryFileProvider()
            .AddFile("grammars/grammar-manifest.json", ValidManifest)
            .AddFile("grammars/SqlLexer.g4", "lexer grammar SqlLexer;"));

        var inventory = loader.Load(At("grammars/grammar-manifest.json"));

        inventory.LoadError.Should().BeNull();
        inventory.TryGet("SQL", out var entry).Should().BeTrue("language lookup is case-insensitive");
        entry.GrammarFile.Should().Be("SqlLexer.g4");
    }

    [Fact]
    public void Load_ManifestWithAnAbsoluteGrammarPath_IsAccepted()
    {
        var lexer = At("elsewhere/SqlLexer.g4");
        var loader = Loader(new InMemoryFileProvider()
            .AddFile("elsewhere/SqlLexer.g4", "lexer grammar SqlLexer;")
            .AddFile("grammars/grammar-manifest.json", ValidManifest.Replace("SqlLexer.g4", lexer.Replace("\\", "\\\\"))));

        loader.Load(At("grammars/grammar-manifest.json")).LoadError.Should().BeNull();
    }

    [Fact]
    public void Parse_InvalidJson_IsReported()
    {
        GrammarInventory.Parse("{ not json", null, _ => true).LoadError.Should().Contain("not valid JSON");
    }

    [Fact]
    public void Parse_ManifestWithoutTheGrammarsArray_IsReported()
    {
        GrammarInventory.Parse("{}", null, _ => true).LoadError.Should().Contain("grammars");
    }

    [Fact]
    public void Discover_EnvironmentVariablePointingNowhere_IsAnErrorNotAnEmptyInventory()
    {
        var variableBackup = Environment.GetEnvironmentVariable(GrammarInventory.ManifestPathVariable);
        try
        {
            Environment.SetEnvironmentVariable(GrammarInventory.ManifestPathVariable, At("missing-manifest.json"));

            var inventory = Loader().Discover();

            inventory.HasGrammars.Should().BeFalse();
            inventory.LoadError.Should().Contain(GrammarInventory.ManifestPathVariable);
        }
        finally
        {
            Environment.SetEnvironmentVariable(GrammarInventory.ManifestPathVariable, variableBackup);
        }
    }

    [Fact]
    public void Discover_ReadsTheShippedManifestFromTheProvider()
    {
        var loader = Loader(new InMemoryFileProvider()
            .AddFile("grammars/grammar-manifest.json", ValidManifest)
            .AddFile("grammars/SqlLexer.g4", "lexer grammar SqlLexer;"));

        var inventory = loader.Load(At("grammars/grammar-manifest.json"));

        inventory.HasGrammars.Should().BeTrue();
        inventory.ManifestPath.Should().Be(At("grammars/grammar-manifest.json"));
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
        var inventory = GrammarInventory.Parse(ValidManifest, null, _ => true);

        inventory.Describe().Should().Be("sql (SqlLexer.g4, channel(s) 1/HIDDEN/COMMENTS)");
    }

    private static GrammarInventoryLoader Loader(InMemoryFileProvider? provider = null) =>
        new(new FileProviderAccess(provider ?? new InMemoryFileProvider(), Root));

    private static string At(string relativePath) =>
        Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
