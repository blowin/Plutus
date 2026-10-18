using System.Text;
using Plutus.Domain.ArchiveExtractor;
using Plutus.Domain.Comments;
using Plutus.Domain.Diffing;
using Plutus.Domain.Structure;
using Plutus.Domain.Tokens;

namespace Plutus.Domain.Configuration;

/// <summary>
/// Merges the command line with the profile and the built-in defaults, in that order:
/// <list type="number">
/// <item><description>an explicit flag wins over everything;</description></item>
/// <item><description>a key stated by the profile wins over the built-in default;</description></item>
/// <item><description>otherwise the default of the option applies.</description></item>
/// </list>
/// A flag replaces the profile's value for the same key rather than adding to it, so the command line is never
/// silently mixed with a preset the caller cannot see; the run report states which keys came from where.
/// <para>
/// The resolver is a service: it holds the defaults, so a host that wants different ones (an embedding application,
/// a test) constructs it with them instead of having no say.
/// </para>
/// </summary>
public sealed class RunOptionsResolver
{
    private static readonly FileSize DefaultMaxFileSize = FileSize.Parse("1M");

    private const string DefaultFormat = "md";
    private const string DefaultEncoding = "utf-8";
    private const int DefaultMaxConcurrency = 8;

    /// <summary>Builds the options of a run from what the command line asked and what the profile states.</summary>
    public RunOptionsResolution Resolve(RunOptionsRequest request, PlutusProfile? profile = null, string? profilePath = null)
    {
        var provenance = new Provenance(profilePath);
        try
        {
            return new RunOptionsResolution { Options = Build(request, profile, provenance) };
        }
        catch (ConfigurationException exception)
        {
            return new RunOptionsResolution { Error = exception.Message };
        }
    }

    private static RunAppOptions Build(RunOptionsRequest request, PlutusProfile? profile, Provenance provenance)
    {
        var maxFileSize = ResolveMaxFileSize(request, profile, provenance);
        var maxTokens = request.MaxTokens ?? profile?.MaxTokens;
        var tokenBudget = maxTokens is { } limit
            ? new TokenBudgetOptions(
                limit,
                ResolveValue("tokenOverflowPolicy", request.TokenOverflowPolicy, profile?.TokenOverflowPolicy, TokenOverflowPolicy.Fail, provenance),
                ResolveValue("tokenSelectionStrategy", request.TokenSelectionStrategy, profile?.TokenSelectionStrategy, TokenSelectionStrategy.PathOrder, provenance))
            : null;

        var diff = request.Diff is { } diffRequest
            ? new GitDiffOptions
            {
                Target = string.IsNullOrWhiteSpace(diffRequest.From) ? null : diffRequest.From,
                TargetTo = string.IsNullOrWhiteSpace(diffRequest.To) ? null : diffRequest.To,
                IncludeUntracked = diffRequest.IncludeUntracked,

                // The same limits the content path uses, so a diff cannot bypass them.
                MaxFileSize = maxFileSize,
                TokenBudget = tokenBudget,
            }
            : null;

        return new RunAppOptions
        {
            ProjectPath = [.. request.ProjectPath],
            OutputPath = ResolveOutput(request.OutputPath, profile, provenance),
            UseGitignore = ResolveValue("useGitignore", request.UseGitignore, profile?.UseGitignore, true, provenance),
            ExcludePatterns = ResolveList("exclude", request.Exclude, profile?.Exclude, provenance),
            UseDefaultExcludes = ResolveValue("useDefaultExcludes", request.UseDefaultExcludes, profile?.UseDefaultExcludes, true, provenance),
            AllowDangerous = ResolveValue("allowDangerousFiles", request.AllowDangerousFiles, profile?.AllowDangerousFiles, false, provenance),
            MaxFileSize = maxFileSize,
            ExcludeDirPatterns = ResolveList("excludeDirs", request.ExcludeDirs, profile?.ExcludeDirs, provenance),
            ExcludeFilePatterns = ResolveList("excludeFiles", request.ExcludeFiles, profile?.ExcludeFiles, provenance),
            IncludeDirPatterns = ResolveList("includeDirs", request.IncludeDirs, profile?.IncludeDirs, provenance),
            IncludeFilePatterns = ResolveIncludeFilePatterns(request, profile, provenance),

            // The order of the passes is fixed by the pipeline; these flags only say which of them run.
            Minify = ResolveValue("minify", request.Minify, profile?.Minify, false, provenance),
            StripComments = ResolveValue("stripComments", request.StripComments, profile?.StripComments, false, provenance),
            PreserveDocumentationComments = ResolveValue("preserveDocComments", request.PreserveDocComments, profile?.PreserveDocComments, false, provenance),
            CommentStrategy = ResolveValue("commentStrategy", request.CommentStrategy, profile?.CommentStrategy, CommentStrategyRequest.Auto, provenance),
            SignaturesOnly = ResolveValue("signaturesOnly", request.SignaturesOnly, profile?.SignaturesOnly, false, provenance),
            SignatureOptions = request.SignatureOptions ?? SignatureExtractionOptions.Default,

            TargetEncoding = ResolveEncoding(request.Encoding, profile?.Encoding, provenance),
            MaxConcurrency = ResolveValue("maxConcurrency", request.MaxConcurrency, profile?.MaxConcurrency, DefaultMaxConcurrency, provenance),
            Format = ResolveString("format", request.Format, profile?.Format, DefaultFormat, provenance),
            Branch = request.Branch,
            Commit = request.Commit,
            TokenBudget = tokenBudget,
            Diff = diff,
            IncludeFileContents = ResolveValue("includeFileContents", request.IncludeFileContents, profile?.IncludeFileContents, true, provenance),
            Profile = provenance.Build(),
            ArchiveExtraction = ResolveArchiveExtraction(request.ArchiveExtraction, profile, provenance),
        };
    }

    private static FileSize ResolveMaxFileSize(RunOptionsRequest request, PlutusProfile? profile, Provenance provenance)
    {
        if (request.MaxFileSize is { } fromCommandLine)
        {
            provenance.OverrideIfStated("maxFileSize", profile?.MaxFileSize is not null);
            return fromCommandLine;
        }

        if (profile?.MaxFileSize is not { } stated)
        {
            return DefaultMaxFileSize;
        }

        try
        {
            var size = FileSize.Parse(stated);
            provenance.Apply("maxFileSize", differsFromDefault: size != DefaultMaxFileSize);
            return size;
        }
        catch (Exception exception)
        {
            throw new ConfigurationException($"profile '{provenance.Path}': 'maxFileSize' {exception.Message}");
        }
    }

    /// <summary>
    /// Inclusive file filters come from two keys — patterns and extensions — and they are one list to the scanner. A
    /// command-line filter replaces both, because the profile stated the whole set it wanted included.
    /// </summary>
    private static List<string> ResolveIncludeFilePatterns(RunOptionsRequest request, PlutusProfile? profile, Provenance provenance)
    {
        if (request.IncludeFiles is { Count: > 0 } fromCommandLine)
        {
            provenance.OverrideIfStated("includeFiles", profile?.IncludeFiles is { Count: > 0 });
            provenance.OverrideIfStated("includeExtensions", profile?.IncludeExtensions is { Count: > 0 });
            return [.. fromCommandLine];
        }

        var patterns = new List<string>();
        if (profile?.IncludeFiles is { Count: > 0 } stated)
        {
            provenance.Apply("includeFiles", differsFromDefault: true);
            patterns.AddRange(stated);
        }

        if (profile?.IncludeExtensions is { Count: > 0 } extensions)
        {
            provenance.Apply("includeExtensions", differsFromDefault: true);
            patterns.AddRange(extensions.Select(ToIncludePattern));
        }

        return patterns;
    }

    /// <summary>
    /// A profile's relative output path belongs to the repository the profile describes, not to the directory the
    /// command happened to run in.
    /// </summary>
    private static string? ResolveOutput(string? fromCommandLine, PlutusProfile? profile, Provenance provenance)
    {
        if (!string.IsNullOrWhiteSpace(fromCommandLine))
        {
            provenance.OverrideIfStated("output", !string.IsNullOrWhiteSpace(profile?.Output));
            return fromCommandLine;
        }

        if (string.IsNullOrWhiteSpace(profile?.Output))
        {
            return null;
        }

        provenance.Apply("output", differsFromDefault: true);
        var directory = provenance.Path is null
            ? null
            : System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(provenance.Path));

        return directory is null ? profile.Output : System.IO.Path.Combine(directory, profile.Output);
    }

    /// <summary>Turns an extension into the inclusive pattern that selects it at any depth.</summary>
    private static string ToIncludePattern(string extension)
    {
        var trimmed = extension.Trim();
        if (trimmed.StartsWith('*'))
        {
            return trimmed;
        }

        return trimmed.StartsWith('.') ? $"*{trimmed}" : $"*.{trimmed}";
    }

    private static Encoding ResolveEncoding(string? fromCommandLine, string? fromProfile, Provenance provenance)
    {
        var name = fromCommandLine ?? fromProfile ?? DefaultEncoding;
        provenance.OverrideIfStated("encoding", fromCommandLine is not null && fromProfile is not null);
        provenance.ApplyIf(
            "encoding",
            fromCommandLine is null && fromProfile is not null,
            differsFromDefault: fromProfile is not null && !string.Equals(fromProfile, DefaultEncoding, StringComparison.OrdinalIgnoreCase));

        try
        {
            return Encoding.GetEncoding(name);
        }
        catch (ArgumentException)
        {
            throw new ConfigurationException(
                fromCommandLine is null && fromProfile is not null
                    ? $"profile '{provenance.Path}': 'encoding' is '{name}', which this platform does not know"
                    : $"Unsupported encoding name '{name}'. Examples: utf-8, windows-1251.");
        }
    }

    private static T ResolveValue<T>(string key, T? fromCommandLine, T? fromProfile, T fallback, Provenance provenance)
        where T : struct
    {
        if (fromCommandLine is { } explicitValue)
        {
            provenance.OverrideIfStated(key, fromProfile is not null);
            return explicitValue;
        }

        if (fromProfile is { } profileValue)
        {
            provenance.Apply(key, differsFromDefault: !EqualityComparer<T>.Default.Equals(profileValue, fallback));
            return profileValue;
        }

        return fallback;
    }

    private static string ResolveString(string key, string? fromCommandLine, string? fromProfile, string fallback, Provenance provenance)
    {
        if (!string.IsNullOrWhiteSpace(fromCommandLine))
        {
            provenance.OverrideIfStated(key, !string.IsNullOrWhiteSpace(fromProfile));
            return fromCommandLine;
        }

        if (!string.IsNullOrWhiteSpace(fromProfile))
        {
            provenance.Apply(key, differsFromDefault: !string.Equals(fromProfile, fallback, StringComparison.OrdinalIgnoreCase));
            return fromProfile;
        }

        return fallback;
    }

    private static List<string> ResolveList(
        string key,
        IReadOnlyList<string>? fromCommandLine,
        IReadOnlyList<string>? fromProfile,
        Provenance provenance)
    {
        if (fromCommandLine is { Count: > 0 })
        {
            provenance.OverrideIfStated(key, fromProfile is { Count: > 0 });
            return [.. fromCommandLine];
        }

        if (fromProfile is { Count: > 0 })
        {
            provenance.Apply(key, differsFromDefault: true);
            return [.. fromProfile];
        }

        return [];
    }

    private static ArchiveExtractionOptions ResolveArchiveExtraction(
        ArchiveExtractionOptions? fromCommandLine,
        PlutusProfile? profile,
        Provenance provenance)
    {
        // Resolve each field: CLI wins over profile wins over default
        var defaultOptions = ArchiveExtractionOptions.Default;

        var maxDownloadSize = ResolveFileSize(
            "maxDownloadSize",
            fromCommandLine?.MaxDownloadSize,
            profile?.MaxDownloadSize,
            defaultOptions.MaxDownloadSize!,
            provenance);

        var maxExtractedSize = ResolveFileSize(
            "maxExtractedSize",
            fromCommandLine?.MaxExtractedSize,
            profile?.MaxExtractedSize,
            defaultOptions.MaxExtractedSize!,
            provenance);

        var maxFileCount = ResolveNullableValue(
            "maxFileCount",
            fromCommandLine?.MaxFileCount,
            profile?.MaxArchiveFileCount,
            defaultOptions.MaxFileCount.GetValueOrDefault(),
            provenance);

        var maxDepth = ResolveNullableValue(
            "maxDepth",
            fromCommandLine?.MaxDepth,
            profile?.MaxArchiveDepth,
            defaultOptions.MaxDepth.GetValueOrDefault(),
            provenance);

        var rejectDuplicatePaths = ResolveNullableValue(
            "rejectDuplicatePaths",
            fromCommandLine?.RejectDuplicatePaths,
            profile?.RejectDuplicateArchivePaths,
            defaultOptions.RejectDuplicatePaths.GetValueOrDefault(),
            provenance);

        var rejectSymlinks = ResolveNullableValue(
            "rejectSymlinks",
            fromCommandLine?.RejectSymlinks,
            profile?.RejectSymlinksInArchives,
            defaultOptions.RejectSymlinks.GetValueOrDefault(),
            provenance);

        return new ArchiveExtractionOptions
        {
            MaxDownloadSize = maxDownloadSize,
            MaxExtractedSize = maxExtractedSize,
            MaxFileCount = maxFileCount,
            MaxDepth = maxDepth,
            RejectDuplicatePaths = rejectDuplicatePaths,
            RejectSymlinks = rejectSymlinks,
        };
    }

    private static FileSize? ResolveFileSize(
        string key,
        FileSize? fromCommandLine,
        string? fromProfile,
        FileSize? fallback,
        Provenance provenance)
    {
        if (fromCommandLine is { } explicitValue)
        {
            provenance.OverrideIfStated(key, !string.IsNullOrWhiteSpace(fromProfile));
            return explicitValue;
        }

        if (!string.IsNullOrWhiteSpace(fromProfile))
        {
            try
            {
                var parsed = FileSize.Parse(fromProfile);
                provenance.Apply(key, differsFromDefault: fallback is null || parsed != fallback);
                return parsed;
            }
            catch (Exception exception)
            {
                throw new ConfigurationException($"profile '{provenance.Path}': 'maxDownloadSize' / 'maxExtractedSize' {exception.Message}");
            }
        }

        return null;
    }

    private static T? ResolveNullableValue<T>(string key, T? fromCommandLine, T? fromProfile, T fallback, Provenance provenance)
        where T : struct
    {
        if (fromCommandLine is { } explicitValue)
        {
            provenance.OverrideIfStated(key, fromProfile is not null);
            return explicitValue;
        }

        if (fromProfile is { } profileValue)
        {
            provenance.Apply(key, differsFromDefault: !EqualityComparer<T>.Default.Equals(profileValue, fallback));
            return profileValue;
        }

        return null;
    }

    /// <summary>Collects which profile keys were used, and which an explicit flag replaced.</summary>
    private sealed class Provenance(string? path)
    {
        private readonly List<string> _applied = [];
        private readonly List<string> _defaulted = [];
        private readonly List<string> _overridden = [];

        public string? Path { get; } = path;

        /// <summary>
        /// Records a key the profile supplied. A value equal to the built-in default is recorded separately: the
        /// report is about what decided the run, and a starter profile that restates every default would otherwise
        /// bury the keys that matter.
        /// </summary>
        public void Apply(string key, bool differsFromDefault = true)
        {
            if (Path is null)
            {
                return;
            }

            var bucket = differsFromDefault ? _applied : _defaulted;
            if (!bucket.Contains(key))
            {
                bucket.Add(key);
            }
        }

        public void ApplyIf(string key, bool condition, bool differsFromDefault = true)
        {
            if (condition)
            {
                Apply(key, differsFromDefault);
            }
        }

        public void OverrideIfStated(string key, bool statedByProfile)
        {
            if (Path is not null && statedByProfile && !_overridden.Contains(key))
            {
                _overridden.Add(key);
            }
        }

        public ProfileProvenance? Build() =>
            Path is null
                ? null
                : new ProfileProvenance
                {
                    Path = Path,
                    AppliedKeys = _applied,
                    DefaultedKeys = _defaulted,
                    OverriddenKeys = _overridden,
                };
    }
}
