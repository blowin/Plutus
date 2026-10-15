using System.Text.Json;

namespace Plutus.Domain.Comments;

/// <summary>
/// One channel reference from a grammar manifest. A channel may be written numerically (<c>1</c>) or by name
/// (<c>"HIDDEN"</c>, or a grammar-declared name such as <c>"COMMENTS"</c>), and both forms resolve to the same
/// number before anything is stripped.
/// </summary>
public sealed record GrammarChannelRef(string Raw, int? Number, string? Name)
{
    /// <summary>
    /// ANTLR's built-in channels. <c>HIDDEN</c> is the one a comment lexer normally uses; a grammar may declare
    /// further names, which the manifest supplies through <c>namedChannels</c>.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> BuiltInChannels = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["DEFAULT_TOKEN_CHANNEL"] = 0,
        ["DEFAULT"] = 0,
        ["HIDDEN"] = 1,
    };

    /// <summary>Resolves the reference to a channel number, or <c>null</c> when the name is unknown.</summary>
    public int? Resolve(IReadOnlyDictionary<string, int> namedChannels)
    {
        if (Number is { } number)
        {
            return number;
        }

        if (Name is null)
        {
            return null;
        }

        if (namedChannels.TryGetValue(Name, out var declared))
        {
            return declared;
        }

        return BuiltInChannels.TryGetValue(Name, out var builtIn) ? builtIn : null;
    }
}

/// <summary>
/// One maintained grammar: which language it covers and which of its token types/channels carry comments.
/// </summary>
public sealed record GrammarInventoryEntry
{
    public required string Language { get; init; }

    public required string GrammarFile { get; init; }

    /// <summary>The generated lexer class the stripper would drive, when the project ships one.</summary>
    public string? LexerClass { get; init; }

    /// <summary>Token types that carry ordinary comments.</summary>
    public IReadOnlyList<string> CommentTokenTypes { get; init; } = [];

    /// <summary>Token types that carry documentation comments, kept when the caller asks for that.</summary>
    public IReadOnlyList<string> DocumentationTokenTypes { get; init; } = [];

    /// <summary>Channels whose tokens never reach the parser, written numerically or by name.</summary>
    public IReadOnlyList<GrammarChannelRef> CommentChannels { get; init; } = [];

    /// <summary>Grammar-declared channel names (<c>channels { COMMENTS }</c>), mapped to their numbers.</summary>
    public IReadOnlyDictionary<string, int> NamedChannels { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>
    /// The channel numbers to drop. A name that resolves to no number is reported rather than dropped from the
    /// list silently, because ignoring a channel would leave comments in the bundle.
    /// </summary>
    public IReadOnlyList<int> ResolveCommentChannelNumbers(out IReadOnlyList<string> unresolved)
    {
        var numbers = new List<int>(CommentChannels.Count);
        var unknown = new List<string>();
        foreach (var channel in CommentChannels)
        {
            var resolved = channel.Resolve(NamedChannels);
            if (resolved is { } number)
            {
                if (!numbers.Contains(number))
                {
                    numbers.Add(number);
                }
            }
            else
            {
                unknown.Add(channel.Raw);
            }
        }

        unresolved = unknown;
        return numbers;
    }

    /// <summary>Reads the inventory record from a manifest JSON object.</summary>
    internal static GrammarInventoryEntry? FromJson(JsonElement element)
    {
        var language = element.TryGetProperty("language", out var languageElement) ? languageElement.GetString() : null;
        var grammarFile = element.TryGetProperty("grammarFile", out var fileElement) ? fileElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(language) || string.IsNullOrWhiteSpace(grammarFile))
        {
            return null;
        }

        return new GrammarInventoryEntry
        {
            Language = language,
            GrammarFile = grammarFile,
            LexerClass = element.TryGetProperty("lexerClass", out var lexerElement) ? lexerElement.GetString() : null,
            CommentTokenTypes = ReadStringArray(element, "commentTokenTypes"),
            DocumentationTokenTypes = ReadStringArray(element, "documentationTokenTypes"),
            CommentChannels = ReadChannels(element),
            NamedChannels = ReadNamedChannels(element),
        };
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .ToList();
    }

    private static IReadOnlyList<GrammarChannelRef> ReadChannels(JsonElement element)
    {
        if (!element.TryGetProperty("commentChannels", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var channels = new List<GrammarChannelRef>();
        foreach (var item in array.EnumerateArray())
        {
            switch (item.ValueKind)
            {
                case JsonValueKind.Number when item.TryGetInt32(out var number):
                    channels.Add(new GrammarChannelRef(number.ToString(), number, null));
                    break;
                case JsonValueKind.String:
                    var name = item.GetString()!;
                    channels.Add(int.TryParse(name, out var parsed)
                        ? new GrammarChannelRef(name, parsed, null)
                        : new GrammarChannelRef(name, null, name));
                    break;
            }
        }

        return channels;
    }

    private static IReadOnlyDictionary<string, int> ReadNamedChannels(JsonElement element)
    {
        if (!element.TryGetProperty("namedChannels", out var named) || named.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var property in named.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var number))
            {
                map[property.Name] = number;
            }
        }

        return map;
    }
}
