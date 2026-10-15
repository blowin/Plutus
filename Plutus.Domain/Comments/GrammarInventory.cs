using System.Text.Json;

namespace Plutus.Domain.Comments;

/// <summary>
/// The grammars this project actually maintains. ANTLR-channel stripping is only ever offered for a language
/// listed here: an empty inventory means ANTLR is unavailable, and that is reported instead of being replaced by
/// lexical scanning without saying so.
/// <para>
/// The inventory is read from a manifest next to the application (<c>grammars/grammar-manifest.json</c>) and
/// every entry is checked against the filesystem, so a manifest that describes grammars the build does not ship
/// fails loudly. See <c>docs/ANTLR_GRAMMAR_INVENTORY.md</c> for the schema and the channel-mapping contract.
/// </para>
/// </summary>
public sealed class GrammarInventory
{
    private readonly Dictionary<string, GrammarInventoryEntry> _byLanguage;

    private GrammarInventory(IReadOnlyList<GrammarInventoryEntry> entries, string? manifestPath, string? loadError)
    {
        Entries = entries;
        ManifestPath = manifestPath;
        LoadError = loadError;
        _byLanguage = entries.ToDictionary(entry => entry.Language, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>No maintained grammars. This is the state of the repository today.</summary>
    public static GrammarInventory Empty { get; } = new([], null, null);

    public IReadOnlyList<GrammarInventoryEntry> Entries { get; }

    /// <summary>The manifest that was read, or the one that was probed and not found.</summary>
    public string? ManifestPath { get; }

    /// <summary>Non-null when a manifest exists but cannot be trusted. A run must not continue past this.</summary>
    public string? LoadError { get; }

    public bool HasGrammars => Entries.Count > 0;

    public static string DefaultManifestPath =>
        Path.Combine(AppContext.BaseDirectory, "grammars", "grammar-manifest.json");

    /// <summary>
    /// Environment variable that points at a grammar manifest, so a developer can try a grammar without rebuilding
    /// the application or copying files next to the binary.
    /// </summary>
    public const string ManifestPathVariable = "PLUTUS_GRAMMAR_MANIFEST";

    /// <summary>
    /// Reads the shipped manifest, or the one named by <see cref="ManifestPathVariable"/>. An explicitly named
    /// manifest that does not exist is reported as a load error: a typo must not look like "no grammars".
    /// </summary>
    public static GrammarInventory Discover(string? manifestPath = null)
    {
        var variable = Environment.GetEnvironmentVariable(ManifestPathVariable);
        if (manifestPath is null && !string.IsNullOrWhiteSpace(variable))
        {
            return File.Exists(variable)
                ? Load(variable)
                : new GrammarInventory([], variable, $"'{ManifestPathVariable}' points at '{variable}', which does not exist");
        }

        var path = manifestPath ?? DefaultManifestPath;
        return File.Exists(path) ? Load(path) : new GrammarInventory([], path, null);
    }

    public static GrammarInventory Load(string manifestPath)
    {
        string json;
        try
        {
            json = File.ReadAllText(manifestPath);
        }
        catch (Exception exception)
        {
            return new GrammarInventory([], manifestPath, $"cannot read grammar manifest '{manifestPath}': {exception.Message}");
        }

        return Parse(json, manifestPath);
    }

    /// <summary>Parses a manifest and validates every entry against the filesystem.</summary>
    public static GrammarInventory Parse(string json, string? manifestPath = null)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            return new GrammarInventory([], manifestPath, $"grammar manifest is not valid JSON: {exception.Message}");
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("grammars", out var grammars) || grammars.ValueKind != JsonValueKind.Array)
            {
                return new GrammarInventory([], manifestPath, "grammar manifest has no 'grammars' array");
            }

            var entries = new List<GrammarInventoryEntry>();
            var errors = new List<string>();

            foreach (var element in grammars.EnumerateArray())
            {
                var entry = GrammarInventoryEntry.FromJson(element);
                if (entry is null)
                {
                    errors.Add("a grammar entry is missing 'language' or 'grammarFile'");
                    continue;
                }

                var channels = entry.ResolveCommentChannelNumbers(out var unresolved);
                if (unresolved.Count > 0)
                {
                    errors.Add($"grammar '{entry.Language}' references unknown channel(s): {string.Join(", ", unresolved)}");
                    continue;
                }

                if (channels.Count == 0 && entry.CommentTokenTypes.Count == 0)
                {
                    errors.Add($"grammar '{entry.Language}' declares neither commentChannels nor commentTokenTypes");
                    continue;
                }

                if (manifestPath is { } path && !GrammarFileExists(path, entry.GrammarFile))
                {
                    errors.Add($"grammar '{entry.Language}' points at '{entry.GrammarFile}', which is not next to the manifest");
                    continue;
                }

                entries.Add(entry);
            }

            var error = errors.Count == 0 ? null : string.Join("; ", errors);
            return new GrammarInventory(entries, manifestPath, error);
        }
    }

    private static bool GrammarFileExists(string manifestPath, string grammarFile)
    {
        if (Path.IsPathRooted(grammarFile))
        {
            return File.Exists(grammarFile);
        }

        var manifestDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
        return manifestDirectory is not null && File.Exists(Path.Combine(manifestDirectory, grammarFile));
    }

    public bool TryGet(string language, out GrammarInventoryEntry entry) => _byLanguage.TryGetValue(language, out entry!);

    /// <summary>Human-readable inventory, used in the message that explains why ANTLR is unavailable.</summary>
    public string Describe()
    {
        if (LoadError is { } error)
        {
            return $"grammar manifest problem: {error}";
        }

        if (Entries.Count == 0)
        {
            return $"none (probed '{ManifestPath ?? DefaultManifestPath}')";
        }

        return string.Join(", ", Entries.Select(Describe));
    }

    private static string Describe(GrammarInventoryEntry entry)
    {
        var channels = entry.ResolveCommentChannelNumbers(out _);
        var names = entry.CommentChannels.Select(channel => channel.Name ?? channel.Raw);
        var channelText = channels.Count == 0 ? "no channel" : $"channel(s) {string.Join("/", names)}";
        return $"{entry.Language} ({entry.GrammarFile}, {channelText})";
    }
}
