using Plutus.Domain.Minifier.Languages;

namespace Plutus.Test.LanguageMinifiers;

public class SqlLanguageMinifierTests
{
    private readonly SqlLanguageMinifier _minifier = new();

    [Fact]
    public void Minify_SqlComments_RemovesDashAndBlockComments()
    {
        // Arrange
        var source = @"
            -- Fetch active records
            SELECT id, name FROM users
            WHERE status = 'active'; /* filter clause */
        ";
        var expected = "SELECT id, name FROM users" + Environment.NewLine +
                       "            WHERE status = 'active';";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Minify_SqlStrings_PreservesEscapedQuotesAndCommentSymbols()
    {
        // Arrange
        // SQL strings escape single quotes using two single quotes ('').
        var source = @"
            SELECT * FROM configurations
            WHERE description = 'De-serialize -- boundary configs and ''/* comment mock */'' tokens';
        ";
        var expected = "SELECT * FROM configurations" + Environment.NewLine +
                       "            WHERE description = 'De-serialize -- boundary configs and ''/* comment mock */'' tokens';";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal(expected, result);
    }
}
