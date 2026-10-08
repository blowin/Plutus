using Plutus.Domain.Minifier.Languages;

namespace Plutus.Test.LanguageMinifiers;

public class RustLanguageMinifierTests
{
    private readonly RustLanguageMinifier _minifier = new();

    [Fact]
    public void Minify_RustComments_RemovesLineDocAndBlockComments()
    {
        // Arrange
        var source = @"
            /// Documenting the outer module mapping
            fn calculate() -> i32 {
                // Internal sequence computation
                let mut x = 5; /* step adjustment */
                x
            }
        ";
        var expected = "fn calculate() -> i32 {" + Environment.NewLine +
                       "                let mut x = 5; " + Environment.NewLine +
                       "                x" + Environment.NewLine +
                       "            }";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Minify_RustStringsAndLifetimes_PreservesValidSyntax()
    {
        // Arrange
        // Rust string testing alongside tick lifetime tokens ('a) which shouldn't trip up string rules.
        var source = @"
            pub struct Parser<'a> {
                pub source: &'a str,
            }
            const PATH: &str = ""https://crates.io""; // Target repository index url
        ";
        var expected = "pub struct Parser<'a> {" + Environment.NewLine +
                       "                pub source: &'a str," + Environment.NewLine +
                       "            }" + Environment.NewLine +
                       "            const PATH: &str = \"https://crates.io\";";

        // Act
        var result = _minifier.Minify(source);

        // Assert
        Assert.Equal(expected, result);
    }
}
