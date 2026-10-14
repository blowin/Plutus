using System.Text;
using Plutus.Domain;

namespace Plutus.Test;

public class RunAppOptionsFactory
{
    public static RunAppOptions Create(FileSize maxFileSize, int? maxTokens = null, string policy = "", string strategy = "")
    {
        return new RunAppOptions
        {
            ProjectPath = ["root"],
            OutputPath = null,
            UseGitignore = true,
            ExcludePatterns = [],
            UseDefaultExcludes = true,
            AllowDangerous = false,
            MaxFileSize = maxFileSize,
            ExcludeDirPatterns = [],
            ExcludeFilePatterns = [],
            IncludeDirPatterns = [],
            IncludeFilePatterns = [],
            TargetEncoding = Encoding.UTF8,
            MaxConcurrency = 11,
            Format = "md",
            Branch = null,
            Commit = null,
            MaxTokens = maxTokens,
            TokenOverflowPolicy = policy,
            TokenSelectionStrategy = strategy,
        };
    }
}
