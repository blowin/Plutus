using FluentAssertions;
using Plutus.Domain;
using Plutus.Domain.Minifier;
using Plutus.Domain.Structure;
using Plutus.Infrastructure.Structure;

namespace Plutus.Test.Structure;

/// <summary>
/// Keeps the structural pass documented: the CLI flag exists, the guide names the supported language and the
/// guarantees, and the documentation distinguishes a parse-tree pass from the lexical comment scanner.
/// </summary>
public class SignatureExtractionDocumentationTests
{
    [Fact]
    public void Guide_ExistsAndStatesTheMechanismAndTheLanguage()
    {
        var guide = ReadDocument("SIGNATURE_EXTRACTION.md");

        guide.Should().Contain("--signatures-only");
        guide.Should().Contain("roslyn-csharp");
        guide.Should().Contain("TokenStreamRewriter");
        guide.Should().Contain("Idempotent");
        guide.Should().Contain("not compilable code");
    }

    [Fact]
    public void Guide_ListsEveryLanguageWithAProvider()
    {
        var guide = ReadDocument("SIGNATURE_EXTRACTION.md");

        foreach (var provider in TestSourceProcessing.Providers())
        {
            guide.Should().Contain($"`{provider.Language.Value}`", "the supported-language table must list {provider.Language}");
            guide.Should().Contain(provider.Name, "the table must name the provider that answers");
        }
    }

    [Fact]
    public void Guide_DistinguishesTheStructuralPassFromLexicalCommentScanning()
    {
        var guide = ReadDocument("SIGNATURE_EXTRACTION.md");

        guide.Should().Contain("parse tree");
        guide.Should().NotContain("ANTLR Parse Tree Visitor");
        guide.Should().Contain("token span");
    }

    [Fact]
    public void Guides_StaySeparateSoEachPassIsDescribedHonestly()
    {
        var comments = ReadDocument("COMMENT_STRIPPING.md");

        // The comment scanner must not be described as grammar- or AST-driven, and the structural pass must not be
        // described as lexical scanning.
        comments.Should().Contain("lexical scanner");
        comments.Should().Contain("A parser, an AST pass");
    }

    [Fact]
    public void CliReferenceAndReadme_DocumentTheFlagAndLinkTheGuide()
    {
        ReadDocument("CLI.md").Should().Contain("--signatures-only");

        var readme = File.ReadAllText(Path.Combine(RepositoryRoot(), "README.md"));
        readme.Should().Contain("docs/SIGNATURE_EXTRACTION.md");
        readme.Should().Contain("--signatures-only");
    }

    private static string ReadDocument(string fileName) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", fileName));

    /// <summary>Finds the repository root by walking up from the test output directory.</summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Plutus.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The repository root could not be located from the test output directory.");
    }
}
