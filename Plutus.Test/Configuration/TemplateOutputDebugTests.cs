using Plutus.Domain.Configuration;

namespace Plutus.Test.Configuration;

public class TemplateOutputDebugTests
{
    [Fact]
    public void ShowTemplateOutput()
    {
        var template = new ProfileTemplate();
        var rendered = template.Render();

        // Print the timeout section
        var lines = rendered.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var timeoutStart = Array.FindIndex(lines, l => l.Contains("\"timeout\""));

        Assert.True(timeoutStart >= 0, "timeout section not found");

        var output = string.Join("\n", lines.Skip(timeoutStart).Take(10));
        Console.WriteLine("Timeout section:\n" + output);

        // Check format
        Assert.Contains("\"operationTimeout\"", rendered);
        Assert.Contains("\"retryBaseDelay\"", rendered);
        Assert.Contains("\"retryMaxDelay\"", rendered);
    }
}
