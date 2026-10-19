using Plutus.Domain.Retry;

namespace Plutus.Test.Retry;

public class TimeSpanParserTests
{
    [Theory]
    [InlineData("30s", 30)]
    [InlineData("500ms", 0.5)]
    [InlineData("1m", 60)]
    [InlineData("2m", 120)]
    [InlineData("1h", 3600)]
    [InlineData("2h", 7200)]
    [InlineData("1d", 86400)]
    [InlineData("1.5s", 1.5)]
    [InlineData("500.5ms", 0.5005)]
    [InlineData("90s", 90)]
    public void Parse_FriendlyFormats_ParsesCorrectly(string input, double expectedSeconds)
    {
        var result = TimeSpanParser.Parse(input);
        var expected = TimeSpan.FromSeconds(expectedSeconds);
        Assert.True(Math.Abs((result - expected).TotalSeconds) < 0.001,
            $"Expected {expected}, got {result}");
    }

    [Theory]
    [InlineData("PT30S", 30)]
    [InlineData("PT5M", 300)]
    [InlineData("PT2H", 7200)]
    [InlineData("P1D", 86400)]
    [InlineData("PT1H30M", 5400)]
    [InlineData("PT1H30M45S", 5445)]
    [InlineData("P1DT2H3M4S", 93784)]
    [InlineData("pt30s", 30)]
    [InlineData("Pt5m", 300)]
    public void Parse_ISO8601Durations_ParsesCorrectly(string input, double expectedSeconds)
    {
        var result = TimeSpanParser.Parse(input);
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), result);
    }

    [Theory]
    [InlineData("00:00:30", 30)]
    [InlineData("00:05:00", 300)]
    [InlineData("01:30:00", 5400)]
    [InlineData("1.02:03:04", 93784)]
    public void Parse_StandardFormats_ParsesCorrectly(string input, double expectedSeconds)
    {
        var result = TimeSpanParser.Parse(input);
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), result);
    }

    [Fact]
    public void Parse_EmptyInput_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => TimeSpanParser.Parse(""));
        Assert.Throws<ArgumentException>(() => TimeSpanParser.Parse(null!));
        Assert.Throws<ArgumentException>(() => TimeSpanParser.Parse("   "));
    }

    [Fact]
    public void Parse_InvalidInput_ThrowsFormatException()
    {
        var exception = Assert.Throws<FormatException>(() => TimeSpanParser.Parse("invalid"));
        Assert.Contains("Cannot parse 'invalid' as TimeSpan", exception.Message);
    }
}
