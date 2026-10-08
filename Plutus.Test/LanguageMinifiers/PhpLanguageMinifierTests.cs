using Plutus.Domain.Minifier.Languages;

namespace Plutus.Test.LanguageMinifiers;

public class PhpLanguageMinifierTests
{
    private readonly PhpLanguageMinifier _minifier = new();

    [Fact]
    public void Minify_PhpComments_RemovesAllThreeCommentVariations()
    {
        // Arrange
        // PHP utilizes standard C++ comments combined with Unix shell hash (#) syntax patterns.
        var source = @"
            <?php
            # Shell script variant comment heading
            $endpoint = ""https://localhost:5001""; // Inline double slash parameter
            /*
               Multiline corporate block header
            */
            echo $endpoint;
        ";
        var expected = "<?php" + Environment.NewLine +
                       "            $endpoint = \"https://localhost:5001\"; " + Environment.NewLine +
                       "            echo $endpoint;";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Minify_PhpStrings_HandlesSingleAndDoubleQuotesSafely()
    {
        // Arrange
        var source = @"
            $query = 'SELECT * FROM items WHERE type = ""//mock""'; # Strip down text
            $html = ""<div class='/*css-override*/'>Content</div>"";
        ";
        var expected = "$query = 'SELECT * FROM items WHERE type = \"//mock\"'; " + Environment.NewLine +
                       "            $html = \"<div class='/*css-override*/'>Content</div>\";";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal(expected, result);
    }
}
