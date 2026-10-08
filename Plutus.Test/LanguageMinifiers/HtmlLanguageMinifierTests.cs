using Plutus.Domain.Minifier.Languages;

namespace Plutus.Test.LanguageMinifiers;

public class HtmlLanguageMinifierTests
{
    private readonly HtmlLanguageMinifier _minifier = new();

    [Fact]
    public void Minify_HtmlComments_RemovesBlockComments()
    {
        // Arrange
        // HTML comments often contain structural notes or legacy code blocks.
        // They waste a massive amount of tokens and must be fully eliminated.
        var source = @"
            <!DOCTYPE html>
            <!-- Corporate layout navigation header block -->
            <html lang=""en"">
            <head>
                <meta charset=""UTF-8"">
                <!--
                   Multi-line comment block
                   with technical metadata notes
                -->
                <title>Plutus Console</title>
            </head>
            <body>
                <h1>Application Active</h1> <!-- Content note boundary -->
            </body>
            </html>
        ";
        var expected = "<!DOCTYPE html>" + Environment.NewLine +
                       "            <html lang=\"en\">" + Environment.NewLine +
                       "            <head>" + Environment.NewLine +
                       "                <meta charset=\"UTF-8\">" + Environment.NewLine +
                       "                <title>Plutus Console</title>" + Environment.NewLine +
                       "            </head>" + Environment.NewLine +
                       "            <body>" + Environment.NewLine +
                       "                <h1>Application Active</h1> " + Environment.NewLine +
                       "            </body>" + Environment.NewLine +
                       "            </html>";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal((expected), (result));
    }

    [Fact]
    public void Minify_CommentSymbolsInsideAttributes_LeavesMarkupIntact()
    {
        // Arrange
        // If an attribute text value contains character sequences resembling comments,
        // the engine must ignore them to prevent interface breakage.
        var source = @"
            <div class=""container"">
                <input type=""text"" value=""<!-- value mock data -->"" name=""payload"">
            </div>
        ";
        var expected = "<div class=\"container\">" + Environment.NewLine +
                       "                <input type=\"text\" value=\"<!-- value mock data -->\" name=\"payload\">" + Environment.NewLine +
                       "            </div>";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal((expected), (result));
    }
}
