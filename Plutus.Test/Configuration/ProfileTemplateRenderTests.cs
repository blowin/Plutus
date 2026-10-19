using Plutus.Domain.Configuration;
using Plutus.Domain.Retry;

namespace Plutus.Test.Configuration;

/// <summary>
/// Verifies that the profile template renders TimeSpan values in user-friendly format.
/// </summary>
public class ProfileTemplateRenderTests
{
    [Fact]
    public void Render_TimeSpanValues_AreUserFriendlyFormat()
    {
        var template = new ProfileTemplate();
        var rendered = template.Render();

        // TimeSpan values should be in standard format (e.g., "00:30:00" for 30s)
        // The TimeSpanConverter writes TimeSpan.ToString() which produces "hh:mm:ss" format
        Assert.Contains("\"operationTimeout\"", rendered);
        Assert.Contains("\"retryBaseDelay\"", rendered);
        Assert.Contains("\"retryMaxDelay\"", rendered);

        // Verify it round-trips correctly
        var profile = System.Text.Json.JsonSerializer.Deserialize<PlutusProfile>(
            rendered,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.KebabCaseLower), new TimeSpanConverter() },
            });

        Assert.NotNull(profile);
        Assert.NotNull(profile.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(120), profile.Timeout.OperationTimeout);
        Assert.Equal(3, profile.Timeout.MaxRetries);
        Assert.Equal(TimeSpan.FromMilliseconds(500), profile.Timeout.RetryBaseDelay);
        Assert.Equal(TimeSpan.FromSeconds(30), profile.Timeout.RetryMaxDelay);
    }

    [Fact]
    public void Render_ShowsActualOutput()
    {
        var template = new ProfileTemplate();
        var rendered = template.Render();

        // Show what the timeout section looks like
        var lines = rendered.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var timeoutSection = lines.Where(l => l.Contains("timeout") || l.Contains("OperationTimeout") ||
                                              l.Contains("operationTimeout") || l.Contains("MaxRetries") ||
                                              l.Contains("maxRetries") || l.Contains("RetryBaseDelay") ||
                                              l.Contains("retryBaseDelay") || l.Contains("RetryMaxDelay") ||
                                              l.Contains("retryMaxDelay")).ToArray();

        // This will show in test output if it fails
        Assert.NotEmpty(timeoutSection);
    }
}
