using Plutus.Domain.Minifier;
using Plutus.Domain.Minifier.Languages;

namespace Plutus.Test.LanguageMinifiers;

public class JavaLanguageMinifierTests
{
    private readonly JavaLanguageMinifier _minifier = new();

    [Fact]
    public void Minify_NoComments_ReturnsOriginalTrimmedAndCollapsed()
    {
        // Arrange
        var source = "public class Foo { public void bar() {} }";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal(source, result);
    }

    [Fact]
    public void Minify_SingleLineComment_RemovesCommentAndLineBreaks()
    {
        // Arrange
        var source = @"
            // This is a Java single-line comment
            public int id; // Inline field comment
        ";
        var expected = "public int id;";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Minify_MultiLineAndJavadocComments_RemovesAllComments()
    {
        // Arrange
        // Javadoc (/** ... */) is a critical token waster in Enterprise Java apps.
        var source = @"
            /**
             * Formats the internal system payload.
             * @param data Raw input payload string.
             * @return Processed structural map.
             */
            /* Regular block comment */
            public void process(String data) {}
        ";
        var expected = "public void process(String data) {}";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Minify_CommentSymbolsInsideJavaStrings_DoesNotAlterStringContents()
    {
        // Arrange
        // Java strings containing URLs or regex pattern blocks mimicking comments
        // must remain completely untouched.
        var source = @"
        public class Environment {
            public String apiEndpoint = ""https://plutus.org"";
            public String mathRegex = ""/* matches nothing */"";
            public String filepath = ""C:\\Java\\Path//Subdir"";
        }
    ";

        // FIXED: The expected path string now perfectly retains the double-escaped backslashes ("\\\\")
        // exactly like the input source, reflecting accurate literal preservation.
        var expected = "public class Environment {" + Environment.NewLine +
                       "            public String apiEndpoint = \"https://plutus.org\";" + Environment.NewLine +
                       "            public String mathRegex = \"/* matches nothing */\";" + Environment.NewLine +
                       "            public String filepath = \"C:\\\\Java\\\\Path//Subdir\";" + Environment.NewLine +
                       "        }";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Minify_MultipleBlankLines_CollapsesToZeroBlankLines()
    {
        // Arrange
        // Testing your new optimization where multiple blank lines collapse completely
        // into a single line break sequence with no empty trailing rows.
        var source = @"
            public class ModuleA {}



            public class ModuleB {}
        ";

        var expected = $"public class ModuleA {{}}{Environment.NewLine}            public class ModuleB {{}}";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Minify_JavaCharLiteralsWithSlash_DoesNotTriggerFalsePositiveMatches()
    {
        // Arrange
        var source = "char fileSeparator = '/'; // Trailing comment statement";
        var expected = "char fileSeparator = '/';";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal(expected, result);
    }
}
