using Plutus.Domain.Minifier;
using Plutus.Domain.Minifier.Languages;

namespace Plutus.Test.LanguageMinifiers;

public class CSharpLanguageMinifierTests
{
    private readonly CSharpLanguageMinifier _minifier = new();

    [Fact]
    public void Minify_NoComments_ReturnsOriginalTrimmed()
    {
        // Arrange
        var source = "public class Foo { public void Bar() {} }";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal(source, result);
    }

    [Fact]
    public void Minify_SingleLineComment_RemovesComment()
    {
        // Arrange
        var source = @"
            // This is a comment
            public int Id { get; set; } // Inline comment
        ";
        var expected = "public int Id { get; set; }";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Minify_MultiLineComment_RemovesComment()
    {
        // Arrange
        var source = @"
            /*
               Multi-line comment block
               with multiple description lines
            */
            public class Target {}
        ";
        var expected = "public class Target {}";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Minify_XmlDocumentationComments_RemovesDocumentation()
    {
        // Arrange
        var source = @"
            /// <summary>
            /// This is a sample class method description.
            /// </summary>
            /// <param name=""args"">Arguments mapping.</param>
            public void Execute(string args) {}
        ";
        var expected = "public void Execute(string args) {}";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Minify_CommentSymbolsInsideStringLiteral_DoesNotRemove()
    {
        // Arrange
        var source = @"
            public class WebConfig {
                public string Url = ""https://github.com"";
                public string Pattern = ""/* empty */"";
                public string Path = ""C:\\\\Folder//SubFolder"";
            }
        ";

        // Ожидаем, что строки внутри кавычек останутся нетронутыми
        var expected = source.Trim();

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Minify_CommentSymbolsInsideVerbatimString_DoesNotRemove()
    {
        // Arrange
        var source = @"
            public class VerbatimSample {
                public string Code = @""
                    // This looks like a comment but it is a string asset
                    var x = 10; /* block */
                "";
            }
        ";
        var expected = source.Trim();

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Minify_MultipleBlankLines_CollapsesToSingleBlankLine()
    {
        // Arrange
        var source = @"
            public class StepOne {}



            public class StepTwo {}
        ";

        // Ожидаем, что четыре переноса строки схлопнутся в один стандартный пустой разделитель строк
        var expected = $"public class StepOne {{}}\n            public class StepTwo {{}}";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Minify_CharLiteralsWithSlash_DoesNotTriggerCommentRemoval()
    {
        // Arrange
        var source = "char delimiter = '/'; // Simple comment";
        var expected = "char delimiter = '/';";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Minify_RawStringLiterals_DoesNotRemoveCommentSymbolsInside()
    {
        // Arrange
        // In C# 11+, raw string literals can contain any text including // or /* symbols.
        // The minifier must preserve them completely without alterations.
        var source = """"
                     public class RawStringContainer
                     {
                         public string JsonPayload = """
                         {
                             "url": "https://plutus.io",
                             "comment": "// This is not an actual code comment",
                             "documentation": "/* block example */"
                         }
                         """;
                     }
                     """";

        var expected = source.Trim();

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Minify_RegionDirectives_RemovesRegionAndEndregionTokens()
    {
        // Arrange
        // Regions are boilerplate grouping tools for IDEs.
        // They waste tokens for LLM context injection and should be stripped out.
        var source = @"
        public class TargetService
        {
            #region Core Methods
            public void Process()
            {
                // Action logic
                var active = true;
            }
            #endregion
        }
    ";

        // We expect the comments inside the method to be removed
        // and the region directives to be fully deleted.
        var expected = @"public class TargetService
        {
            public void Process()
            {
                var active = true;
            }
        }";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        // Using NormalizeLineEndings helper to prevent cross-platform CR/LF assertion issues.
        Assert.Equal(NormalizeLineEndings(expected), NormalizeLineEndings(result));
    }

    [Fact]
    public void Minify_SpacesInsideBlankLines_CleansSpacesAndCollapsesCorrectly()
    {
        // Arrange
        // This source contains trailing whitespaces on otherwise empty separating lines.
        // The engine must clean the whitespaces first and then collapse the vertical breaks.
        var source = "public class A {}\n    \n\t\npublic class B {}";

        var expected = $"public class A {{}}\npublic class B {{}}";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Helper method to standardize line breaks across different OS runtime configurations.
    /// </summary>
    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
    }
}
