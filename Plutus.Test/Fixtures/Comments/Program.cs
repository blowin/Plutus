using System;

#region Widgets
namespace Fixture.Widgets;

/// <summary>
/// Renders widgets. This documentation comment is removed by default.
/// </summary>
public sealed class WidgetRenderer
{
    private const string Route = @"C:\temp//raw\path";

    private const string Json = """
        { "url": "https://example.test", "note": "// not a comment" }
        """;

    /* A block comment. */
    public string Render(int count)
    {
        var label = $"count={count /* inline */}";
        char separator = '/';                                    // a character literal
        return label + separator + Route + Json;
    }
}
#endregion
