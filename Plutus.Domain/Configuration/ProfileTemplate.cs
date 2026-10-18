using System.Text.Json;
using System.Text.Json.Serialization;
using Plutus.Domain.Comments;
using Plutus.Domain.Retry;
using Plutus.Domain.Tokens;

namespace Plutus.Domain.Configuration;

/// <summary>
/// The profile a new file starts from: every key the loader accepts, written out with the value a run would use
/// anyway. Nothing is commented out and nothing is hidden, so filling it in means editing values instead of learning
/// a schema — and because the starter values are the built-in defaults, generating the file cannot change what a
/// bundle contains.
/// <para>
/// The text is produced by serializing <see cref="Starter"/> with the same naming and enum spelling the loader reads,
/// so a key cannot exist in one and be missing from the other, and a test round-trips the result through the loader.
/// </para>
/// </summary>
public sealed class ProfileTemplate
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,

        // Absent and unset are different things in a profile: a key that is present with null states "not set", and
        // a user who wants to fill it in should see it.
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower),
            new TimeSpanConverter(),
        },
    };

    /// <summary>The values a run uses when nothing is stated.</summary>
    public static PlutusProfile Starter { get; } = new()
    {
        IncludeFiles = [],
        IncludeDirs = [],
        IncludeExtensions = [],
        Exclude = [],

        // The trees that --use-default-excludes already skips, stated explicitly because excluding them is what a
        // profile is usually for. Listing them changes no file that would have been bundled.
        ExcludeDirs =
        [
            "bin",
            "obj",
            "node_modules",
            "dist",
            ".vs"
        ],
        ExcludeFiles = [],
        UseGitignore = true,
        UseDefaultExcludes = true,
        AllowDangerousFiles = false,

        Output = null,
        Format = "md",
        Branch = "master",
        Commit = "bef25b4",
        Encoding = "utf-8",
        MaxFileSize = "1M",
        MaxTokens = null,
        TokenOverflowPolicy = TokenOverflowPolicy.Fail,
        TokenSelectionStrategy = TokenSelectionStrategy.PathOrder,
        MaxConcurrency = 8,
        IncludeFileContents = true,

        Minify = false,
        StripComments = false,
        PreserveDocComments = false,
        CommentStrategy = CommentStrategyRequest.Auto,
        SignaturesOnly = false,
        MaxDownloadSize = "100M",
        MaxExtractedSize = "500M",
        MaxArchiveFileCount = 10_000,
        MaxArchiveDepth = 16,
        RejectDuplicateArchivePaths = true,
        RejectSymlinksInArchives = true,
        Timeout = new TimeoutOptions
        {
            OperationTimeout = TimeSpan.FromSeconds(120),
            MaxRetries = 3,
            RetryBaseDelay = TimeSpan.FromMilliseconds(500),
            RetryMaxDelay = TimeSpan.FromSeconds(30),
        },
        IncludeDependencies = false,
    };

    public string Render() => JsonSerializer.Serialize(Starter, SerializerOptions) + Environment.NewLine;
}
