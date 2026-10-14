using System.CommandLine;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Plutus.Domain;
using Plutus.Domain.Diffing;
using Plutus.Domain.Tokens;
using Plutus.Infrastructure;

namespace Plutus.CLI;

/// <summary>
/// Project bundler: collects source files into a single Markdown document.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var projectPathsArgument = new Argument<List<string>>("project-path")
        {
            Description = "Path to the target project directory or GitHub repository URL.",
            Arity = ArgumentArity.OneOrMore,
        };

        var outputOption = new Option<string?>("--output")
        {
            Description = "Output bundle file. Default: <folder-name>_bundle.md"
        };
        outputOption.Aliases.Add("-o");

        var maxSizeOption = new Option<FileSize>("--max-file-size")
        {
            Description = "Max file size to include (e.g., 512K, 1M, 2MB).",
            DefaultValueFactory = _ => FileSize.Parse("1M"),
            CustomParser = r =>
            {
                if (r.Tokens.Count != 1)
                {
                    r.AddError($"{r.Argument.Name} should have 1 file size");
                    return default;
                }

                try
                {
                    return FileSize.Parse(r.Tokens.First().Value);
                }
                catch (Exception e)
                {
                    r.AddError(e.Message);
                    return default;
                }
            }
        };

        var branchOption = new Option<string?>("--branch")
        {
            Description = "Remote branch or tag to download. Cannot be used together with --commit."
        };

        var commitOption = new Option<string?>("--commit")
        {
            Description = "Specific remote commit hash to download. Cannot be used together with --branch.",
            Validators =
            {
                result =>
                {
                    var commit = result.GetValueOrDefault<string?>() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(commit) && !System.Text.RegularExpressions.Regex.IsMatch(commit, "^(?:[0-9a-fA-F]{7,40})$"))
                    {
                        result.AddError("Error: --commit must be a hexadecimal Git commit hash (7–40 characters).");
                    }
                }
            }
        };

        var noGitignoreOption = new Option<bool>("--no-gitignore")
        {
            Description = "Do not read root .gitignore"
        };

        var excludeOption = new Option<List<string>>("--exclude")
        {
            Description = "Global gitignore-style wildcard patterns to exclude BOTH files and directories. Can be repeated",
            AllowMultipleArgumentsPerToken = true
        };
        excludeOption.Aliases.Add("-e");

        var excludeFilesOption = new Option<List<string>>("--exclude-files")
        {
            Description = "Gitignore-style wildcard patterns targeting strictly FILES for exclusion. Useful for ignoring specific file names or extensions across the entire project graph",
            AllowMultipleArgumentsPerToken = true
        };

        var excludeDirsOption = new Option<List<string>>("--exclude-dirs")
        {
            Description = "Gitignore-style wildcard patterns targeting strictly DIRECTORIES for exclusion. Matches the target directory name or absolute tree path at any nesting level",
            AllowMultipleArgumentsPerToken = true
        };

        var includeFilesOption = new Option<List<string>>("--include-files")
        {
            Description = "Inverted gitignore-style filters. Explicitly isolates and restricts processing strictly to FILES that match these patterns. When active, all other unmatched files are omitted by default",
            AllowMultipleArgumentsPerToken = true
        };

        var includeDirsOption = new Option<List<string>>("--include-dirs")
        {
            Description = "Inverted gitignore-style filters. Explicitly restricts scanning paths strictly to DIRECTORIES that match these patterns. Allows you to whitelist and isolate processing to specific module trees",
            AllowMultipleArgumentsPerToken = true
        };

        var useDefaultExcludesOption = new Option<bool>("--use-default-excludes")
        {
            Description = "Use default built-in junk exclusions.",
            DefaultValueFactory = _ => true
        };

        var allowDangerousFilesOption = new Option<bool>("--allow-dangerous-files")
        {
            Description = "Include files normally treated as dangerous (.env, keys, etc.)."
        };

        var minifyOption = new Option<bool>("--minify")
        {
            Description = "Minify source code (remove comments and extra whitespace) for supported languages before bundling."
        };
        minifyOption.Aliases.Add("-m");

        var encodingOption = new Option<string>("--encoding")
        {
            Description = "Target text encoding for reading, writing, and logging (e.g., utf-8, windows-1251, koi8-r).",
            DefaultValueFactory = _ => "utf-8"
        };
        var maxConcurrencyOption = new Option<int>("--max-concurrency")
        {
            Description = "Limit maximum concurrent file processing I/O tasks bound by a semaphore.",
            DefaultValueFactory = _ => 8,
            Validators =
            {
                result =>
                {
                    var value = result.GetValueOrDefault<int>();
                    if (value < 1)
                    {
                        result.AddError($"The {result.Option.Name} option must be at least 1.");
                    }
                }
            }
        };
        var maxTokensOption = new Option<int?>("--max-tokens")
        {
            Description = "Target source-token budget using an offline heuristic."
        };
        maxTokensOption.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<int?>();
            if (value is <= 0)
            {
                result.AddError("--max-tokens must be greater than zero.");
            }
        });

        var tokenOverflowPolicyOption = new Option<TokenOverflowPolicy>("--token-overflow-policy")
        {
            Description = "Budget overflow behavior: fail, warn, or select.",
            DefaultValueFactory = _ => TokenOverflowPolicy.Fail
        };

        var tokenSelectionStrategyOption = new Option<TokenSelectionStrategy>("--token-selection-strategy")
        {
            Description = "Deterministic file selection strategy for policy 'select': path-order or largest-first.",
            DefaultValueFactory = _ => TokenSelectionStrategy.PathOrder,
        };

        var formatOption = new Option<string>("--format")
        {
            Description = "Target serialization configuration format architecture (md, json, xml).",
            DefaultValueFactory = _ => "md"
        };

        var diffOnlyOption = new Option<bool>("--diff-only")
        {
            Description = "Emit the change itself instead of the source: the bundle keeps its header, the list of changed paths and the raw .diff, and omits all file contents. A raw .diff section is appended."
        };

        var diffFromOption = new Option<string?>("--diff-from")
        {
            Description = "Restrict the bundle to files changed relative to this branch, tag, or commit, using merge-base semantics, and keep their contents. Cannot be combined with --branch or --commit.",
            Validators =
            {
                result =>
                {
                    var target = result.GetValueOrDefault<string?>();
                    if (target is not null && string.IsNullOrWhiteSpace(target))
                    {
                        result.AddError("Error: --diff-from requires a branch, tag, or commit name.");
                    }
                }
            }
        };

        var diffIncludeUntrackedOption = new Option<bool>("--diff-include-untracked")
        {
            Description = "With --diff-only or --diff-from, also cover files git does not track yet, excluding anything .gitignore covers. A real new-file diff is generated for them."
        };

        var rootCommand = new RootCommand("Plutus — Project Bundler. Collects source files into a single Markdown document.")
        {
            projectPathsArgument,
            outputOption,
            maxSizeOption,
            noGitignoreOption,
            branchOption,
            commitOption,
            excludeOption,
            excludeFilesOption,
            excludeDirsOption,
            includeFilesOption,
            includeDirsOption,
            useDefaultExcludesOption,
            allowDangerousFilesOption,
            minifyOption,
            encodingOption,
            maxConcurrencyOption,
            formatOption,
            maxTokensOption,
            tokenOverflowPolicyOption,
            tokenSelectionStrategyOption,
            diffOnlyOption,
            diffFromOption,
            diffIncludeUntrackedOption,
        };

        rootCommand.SetAction(async parseResult =>
        {
            var branch = parseResult.GetValue(branchOption);
            var commit = parseResult.GetValue(commitOption);
            var encodingName = parseResult.GetValue(encodingOption)!;
            var maxTokens = parseResult.GetValue(maxTokensOption);
            var diffOnly = parseResult.GetValue(diffOnlyOption);
            var diffFrom = parseResult.GetValue(diffFromOption);
            var diffIncludeUntracked = parseResult.GetValue(diffIncludeUntrackedOption);

            if (!string.IsNullOrWhiteSpace(branch) && !string.IsNullOrWhiteSpace(commit))
            {
                Console.Error.WriteLine("Error: branch and commit cannot be used together.");
                return 1;
            }

            if (!string.IsNullOrWhiteSpace(diffFrom) && (!string.IsNullOrWhiteSpace(branch) || !string.IsNullOrWhiteSpace(commit)))
            {
                Console.Error.WriteLine("Error: --diff-from cannot be used together with --branch or --commit.");
                return 1;
            }

            Encoding targetEncoding;
            try
            {
                targetEncoding = Encoding.GetEncoding(encodingName);
            }
            catch (ArgumentException)
            {
                Console.Error.WriteLine($"Error: Unsupported encoding name '{encodingName}'. Examples: utf-8, windows-1251.");
                return 1;
            }

            var options = new RunAppOptions
            {
                ProjectPath = parseResult.GetValue(projectPathsArgument)!,
                OutputPath = parseResult.GetValue(outputOption),
                UseGitignore = !parseResult.GetValue(noGitignoreOption),
                ExcludePatterns = parseResult.GetValue(excludeOption) ?? new List<string>(),
                UseDefaultExcludes = parseResult.GetValue(useDefaultExcludesOption),
                AllowDangerous = parseResult.GetValue(allowDangerousFilesOption),
                MaxFileSize = parseResult.GetValue(maxSizeOption)!,
                ExcludeDirPatterns = parseResult.GetValue(excludeDirsOption) ?? new List<string>(),
                ExcludeFilePatterns = parseResult.GetValue(excludeFilesOption) ?? new List<string>(),
                IncludeDirPatterns = parseResult.GetValue(includeDirsOption) ?? new List<string>(),
                IncludeFilePatterns = parseResult.GetValue(includeFilesOption) ?? new List<string>(),
                Minify = parseResult.GetValue(minifyOption),
                TargetEncoding = targetEncoding,
                MaxConcurrency = parseResult.GetValue(maxConcurrencyOption),
                Format = parseResult.GetValue(formatOption)!,
                Branch = branch,
                Commit = commit,
                TokenBudget = maxTokens is { } tokenLimit
                    ? new TokenBudgetOptions(
                        tokenLimit,
                        parseResult.GetValue(tokenOverflowPolicyOption)!,
                        parseResult.GetValue(tokenSelectionStrategyOption)!)
                    : null,
                Diff = diffOnly || !string.IsNullOrWhiteSpace(diffFrom)
                    ? new GitDiffOptions
                    {
                        Target = string.IsNullOrWhiteSpace(diffFrom) ? null : diffFrom,
                        IncludeUntracked = diffIncludeUntracked
                    }
                    : null,
                IncludeFileContents = !diffOnly,
            };

            try
            {
                return await RunAppAsync(options);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        });

        var result = rootCommand.Parse(args);
        return await Task.FromResult(await result.InvokeAsync()).ConfigureAwait(false);
    }

    private static async Task<int> RunAppAsync(RunAppOptions options)
    {
        Console.OutputEncoding = options.TargetEncoding;
        Console.InputEncoding = options.TargetEncoding;

        var sc = new ServiceCollection();
        sc.AddInfrastructureServices(options);
        await using var serviceProvider = sc.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        await using var scope = serviceProvider.CreateAsyncScope();

        var runner = scope.ServiceProvider.GetRequiredService<BundleAppRunner>();
        return await runner.RunAsync(options);
    }
}
