using Plutus.Domain.Minifier.Languages;

namespace Plutus.Test.LanguageMinifiers;

public class GoLanguageMinifierTests
{
    private readonly GoLanguageMinifier _minifier = new();

    [Fact]
    public void Minify_GoComments_RemovesBlockAndLineComments()
    {
        // Arrange
        var source = @"
            package main
            // Line comment here
            import ""fmt""
            /* Block comment
               spanning lines */
            func main() {
                fmt.Println(""Hello"") // Inline comment
            }
        ";
        var expected = "package main" + Environment.NewLine +
                       "            import \"fmt\"" + Environment.NewLine +
                       "            func main() {" + Environment.NewLine +
                       "                fmt.Println(\"Hello\") " + Environment.NewLine +
                       "            }";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Minify_GoRawStrings_PreservesInternalCommentSymbols()
    {
        // Arrange
        var source = @"
            package main
            var raw = `
                // This is a string literal, not a comment
                /* block mock */
            `
        ";
        var expected = "package main" + Environment.NewLine +
                       "            var raw = `" + Environment.NewLine +
                       "                // This is a string literal, not a comment" + Environment.NewLine +
                       "                /* block mock */" + Environment.NewLine +
                       "            `";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal(expected, result);
    }
}
