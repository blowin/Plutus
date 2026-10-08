using FluentAssertions;
using Plutus.Domain;

namespace Plutus.Test;

public class FileSizeTests
{
    [Theory]
    [InlineData("0", 0)]
    [InlineData("0B", 0)]
    [InlineData("1024", 1024)]
    [InlineData("512K", 512 * 1024)]
    [InlineData("1M", 1024 * 1024)]
    [InlineData("1.5MB", (long)(1.5 * 1024 * 1024))]
    [InlineData("2mb", 2 * 1024 * 1024)]
    [InlineData("1g", 1024L * 1024 * 1024)]
    [InlineData("1.2 GB", (long)(1.2 * 1024 * 1024 * 1024))]
    public void Parse_ValidStrings_ShouldReturnCorrectBytes(string input, long expectedBytes)
    {
        // Act
        var size = FileSize.Parse(input);

        // Assert
        size.Bytes.Should().Be(expectedBytes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-10K")]
    [InlineData("1M1K")]
    [InlineData("abc")]
    [InlineData("1.5.5MB")]
    [InlineData("1024XB")]
    public void Parse_InvalidStrings_ShouldThrowArgumentException(string input)
    {
        // Act
        Action action = () => FileSize.Parse(input);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromBytes_ShouldCreateCorrectInstance()
    {
        // Act
        var size = FileSize.FromBytes(2048);

        // Assert
        size.Bytes.Should().Be(2048);
    }

    [Fact]
    public void ImplicitOperatorLong_ShouldConvertAutomatically()
    {
        // Arrange
        var size = FileSize.FromBytes(5000);

        // Act
        long bytes = size; // Неявное приведение

        // Assert
        bytes.Should().Be(5000);
    }

    [Fact]
    public void ToString_ShouldFormatToHumanReadable()
    {
        // Arrange
        var size = FileSize.Parse("1.5MB");

        // Act
        var result = size.ToString();

        // Assert
        result.Should().Be("1.5 MB");
    }
}
