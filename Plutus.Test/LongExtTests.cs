using FluentAssertions;
using Plutus.Domain;

namespace Plutus.Test;

public class LongExtTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1.0 MB")]
    [InlineData(1073741824, "1.0 GB")]
    public void HumanSize_ShouldReturnCorrectlyFormattedString(long bytes, string expected)
    {
        // Act
        var result = bytes.HumanSize();

        // Assert
        result.Should().Be(expected);
    }
}
