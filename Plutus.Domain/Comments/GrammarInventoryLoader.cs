namespace Plutus.Domain.Comments;

/// <summary>
/// Reads the grammar inventory from disk. Discovery is a service rather than a static entry point because it touches
/// the filesystem and the environment: a host decides when it happens, and a test points it at a tree of its own
/// through the file provider it was built with.
/// </summary>
public sealed class GrammarInventoryLoader(FileProviderAccess files)
{
    /// <summary>
    /// Reads the shipped manifest, or the one named by <see cref="GrammarInventory.ManifestPathVariable"/>. An
    /// explicitly named manifest that does not exist is reported as a load error: a typo must not look like
    /// "no grammars".
    /// </summary>
    public GrammarInventory Discover(string? manifestPath = null)
    {
        var variable = Environment.GetEnvironmentVariable(GrammarInventory.ManifestPathVariable);
        if (manifestPath is null && !string.IsNullOrWhiteSpace(variable))
        {
            return files.FileExists(variable)
                ? Load(variable)
                : GrammarInventory.Unusable(
                    variable,
                    $"'{GrammarInventory.ManifestPathVariable}' points at '{variable}', which does not exist");
        }

        var path = manifestPath ?? GrammarInventory.DefaultManifestPath;
        return files.FileExists(path) ? Load(path) : GrammarInventory.Unusable(path, null);
    }

    /// <summary>Reads one manifest and validates it against the files it names.</summary>
    public GrammarInventory Load(string manifestPath)
    {
        string json;
        try
        {
            json = files.ReadAllText(manifestPath);
        }
        catch (Exception exception)
        {
            return GrammarInventory.Unusable(manifestPath, $"cannot read grammar manifest '{manifestPath}': {exception.Message}");
        }

        return GrammarInventory.Parse(json, manifestPath, grammarFile => files.FileExists(grammarFile));
    }
}
