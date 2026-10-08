using Plutus.Domain.Minifier.Languages;

namespace Plutus.Test.LanguageMinifiers;

public class JavaScriptLanguageMinifierTests
{
    private readonly JavaScriptLanguageMinifier _minifier = new();

    [Fact]
    public void Minify_JsComments_RemovesBlockAndLineComments()
    {
        // Arrange
        var source = @"
            // Initialize target application module
            const core = () => {
                /* Block setup
                   parameters */
                console.log(""Plutus active""); // Inline alert
            };
        ";
        var expected = "const core = () => {" + Environment.NewLine +
                       "                console.log(\"Plutus active\"); " + Environment.NewLine +
                       "            };";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal((expected), (result));
    }

    [Fact]
    public void Minify_TemplateLiterals_PreservesInternalCommentSymbols()
    {
        // Arrange
        // Modern JavaScript uses backticks (`) for multi-line string templates.
        // The minifier must protect everything inside from being stripped.
        var source = @"
            const template = `
                // This is a literal path inside a template string
                const internalUrl = 'https://localhost/api'; /* metadata block */
            `;
        ";
        var expected = @"const template = `
                // This is a literal path inside a template string
                const internalUrl = 'https://localhost/api'; /* metadata block */
            `;";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal((expected), (result));
    }

    [Fact]
    public void Minify_CommentSymbolsInsideStandardStrings_DoesNotAlterContents()
    {
        // Arrange
        var source = @"
            const config = {
                url: ""https://github.com"",
                regexMock: ""/* stay untouched */"",
                escapedPath: ""C:\\Node\\Project//src""
            };
        ";
        var expected = "const config = {" + Environment.NewLine +
                       "                url: \"https://github.com\"," + Environment.NewLine +
                       "                regexMock: \"/* stay untouched */\"," + Environment.NewLine +
                       "                escapedPath: \"C:\\\\Node\\\\Project//src\"" + Environment.NewLine +
                       "            };";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal((expected), (result));
    }
}
