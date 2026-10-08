using Plutus.Domain.Minifier.Languages;

namespace Plutus.Test.LanguageMinifiers;

public class StyleLanguageMinifierTests
{
    private readonly StyleLanguageMinifier _minifier = new();

    [Fact]
    public void Minify_CssComments_RemovesStandardBlockComments()
    {
        // Arrange
        var source = @"
            /* Global typography stylesheet structure */
            body {
                font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                margin: 0; /* Clear boundary spacing defaults */
                padding: 0;
            }
        ";
        var expected = "body {" + Environment.NewLine +
                       "                font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;" + Environment.NewLine +
                       "                margin: 0; " + Environment.NewLine +
                       "                padding: 0;" + Environment.NewLine +
                       "            }";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal((expected), (result));
    }

    [Fact]
    public void Minify_ScssComments_RemovesPreprocessorInlineAndBlockComments()
    {
        // Arrange
        // Advanced preprocessors utilize double slashes for non-compiled pipeline statements.
        var source = @"
            // Target corporate layout configuration variables
            $primary-color: #4bbc25;

            /* Structural card container layout element */
            .card-element {
                background: url('https://plutus.io'); // Inline background asset
                border-radius: 8px;
            }
        ";
        var expected = "$primary-color: #4bbc25;" + Environment.NewLine +
                       "            .card-element {" + Environment.NewLine +
                       "                background: url('https://plutus.io'); " + Environment.NewLine +
                       "                border-radius: 8px;" + Environment.NewLine +
                       "            }";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal((expected), (result));
    }

    [Fact]
    public void Minify_CommentSymbolsInsideStrings_LeavesAssetsIntact()
    {
        // Arrange
        // String components nested inside CSS selectors or variables can contain comment symbols
        // which must be ignored by the engine.
        var source = @"
            .mock-selector::before {
                content: ""/* this looks like a comment but is structural pseudo-text */"";
                background-image: url(""data:image/svg+xml;charset=utf8,%3Csvg //mock-path%3E"");
            }
        ";
        var expected = ".mock-selector::before {" + Environment.NewLine +
                       "                content: \"/* this looks like a comment but is structural pseudo-text */\";" + Environment.NewLine +
                       "                background-image: url(\"data:image/svg+xml;charset=utf8,%3Csvg //mock-path%3E\");" + Environment.NewLine +
                       "            }";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal((expected), (result));
    }
}
