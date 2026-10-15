using System.CommandLine;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Plutus.Domain;
using Plutus.Domain.Comments;
using Plutus.Domain.Configuration;
using Plutus.Domain.Diagnostics;
using Plutus.Domain.Diffing;
using Plutus.Domain.Structure;
using Plutus.Domain.Tokens;
using Plutus.Infrastructure;
using Plutus.Infrastructure.Diagnostics;

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

        // One output target for the whole run: services report through this, so no layer below decides how a run
        // reads, and the report of what happened is printed in exactly one place.
        IRunOutput output = new ConsoleRunOutput();

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

        var stripCommentsOption = new Option<bool>("--strip-comments")
        {
            Description = "Remove comments only, for languages with a maintained comment syntax. Whitespace, indentation and line endings are preserved, and a space is inserted when removing a comment would join two tokens."
        };

        var preserveDocCommentsOption = new Option<bool>("--preserve-doc-comments")
        {
            Description = "Keep documentation comments (///, //!, /** */, /*! */) when stripping or minifying. Off by default: documentation comments are removed like any other comment."
        };

        var commentStrategyOption = new Option<CommentStrategyRequest>("--comment-strategy")
        {
            Description = "How comments are located: auto (ANTLR token channels where a project-maintained grammar exists, lexical scanning elsewhere), lexical (language-aware character scanner), antlr (requires a project-maintained grammar; fails when none exists).",
            DefaultValueFactory = _ => CommentStrategyRequest.Auto
        };
        commentStrategyOption.AcceptOnlyFromAmong("auto", "lexical", "antlr");

        var signaturesOnlyOption = new Option<bool>("--signatures-only")
        {
            Description = "Reduce supported files to their signatures: a parse-tree pass replaces implementation bodies (method bodies, => expression bodies, property accessor bodies) with a structural terminator, keeping declarations, attributes, generics and constraints. Independent of --strip-comments and --minify."
        };

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
            Description = "Restrict the bundle to files changed relative to this branch, tag, or commit, using merge-base semantics, and keep their contents. Combine with --diff-to to compare two specific commits. Cannot be combined with --branch or --commit.",
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

        var diffToOption = new Option<string?>("--diff-to")
        {
            Description = "Upper revision of an explicit diff range. Together with --diff-from it compares the two commits directly, so a specific commit or range can be bundled.",
            Validators =
            {
                result =>
                {
                    var target = result.GetValueOrDefault<string?>();
                    if (target is not null && string.IsNullOrWhiteSpace(target))
                    {
                        result.AddError("Error: --diff-to requires a branch, tag, or commit name.");
                    }
                }
            }
        };

        var diffIncludeUntrackedOption = new Option<bool>("--diff-include-untracked")
        {
            Description = "With --diff-only or --diff-from, also cover files git does not track yet, excluding anything .gitignore covers. A real new-file diff is generated for them."
        };

        var profileOption = new Option<string?>("--profile")
        {
            Description = "Path to a .plutusrc profile to use instead of searching for one. Overrides the PLUTUS_PROFILE environment variable."
        };

        var noProfileOption = new Option<bool>("--no-profile")
        {
            Description = "Ignore any .plutusrc profile (and PLUTUS_PROFILE) and run with the command line and built-in defaults only."
        };

        var rootCommand = new RootCommand("Plutus — Project Bundler. Collects source files into a single Markdown document.")
        {
            projectPathsArgument,
            outputOption,
            profileOption,
            noProfileOption,
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
            stripCommentsOption,
            preserveDocCommentsOption,
            commentStrategyOption,
            signaturesOnlyOption,
            encodingOption,
            maxConcurrencyOption,
            formatOption,
            maxTokensOption,
            tokenOverflowPolicyOption,
            tokenSelectionStrategyOption,
            diffOnlyOption,
            diffFromOption,
            diffToOption,
            diffIncludeUntrackedOption,
        };

        rootCommand.SetAction(async parseResult =>
        {
            var branch = parseResult.GetValue(branchOption);
            var commit = parseResult.GetValue(commitOption);
            var maxTokens = parseResult.GetValue(maxTokensOption);
            var diffOnly = parseResult.GetValue(diffOnlyOption);
            var diffFrom = parseResult.GetValue(diffFromOption);
            var diffTo = parseResult.GetValue(diffToOption);
            var diffIncludeUntracked = parseResult.GetValue(diffIncludeUntrackedOption);
            var noProfile = parseResult.GetValue(noProfileOption);

            if (!string.IsNullOrWhiteSpace(branch) && !string.IsNullOrWhiteSpace(commit))
            {
                output.Error("branch and commit cannot be used together.");
                return 1;
            }

            if (!string.IsNullOrWhiteSpace(diffFrom) && (!string.IsNullOrWhiteSpace(branch) || !string.IsNullOrWhiteSpace(commit)))
            {
                output.Error("--diff-from cannot be used together with --branch or --commit.");
                return 1;
            }

            if (!string.IsNullOrWhiteSpace(diffTo) && string.IsNullOrWhiteSpace(diffFrom))
            {
                output.Error("--diff-to requires --diff-from to define the other end of the range.");
                return 1;
            }

            // Configuration is read through one file access for the whole process: the profile search, the grammar
            // inventory and the container all see the same filesystem, and none of them opens a file directly.
            var fileProviderDetail = new PhysicianPlutusFileInfo();
            var files = new FileProviderAccessFactory(fileProviderDetail).CreateForWorkingDirectory();
            var profileLoader = new PlutusProfileLoader(files);
            var grammarLoader = new GrammarInventoryLoader(files);

            // The profile is found before anything runs: a profile that cannot be trusted is a failure, not a
            // reason to fall back to the defaults without saying so.
            var profilePath = parseResult.GetValue(profileOption) ?? Environment.GetEnvironmentVariable(PlutusProfileLoader.PathVariable);
            if (noProfile && !string.IsNullOrWhiteSpace(profilePath))
            {
                output.Warning("--no-profile ignores --profile and the PLUTUS_PROFILE environment variable.");
            }

            var profileResult = profileLoader.Resolve(fileProviderDetail.GetCurrentDirectory(), profilePath, noProfile);
            if (!profileResult.Succeeded)
            {
                output.Error(profileResult.Error!);
                return 1;
            }

            // Every option that can also come from a profile is read as "what the user typed", so the resolver can
            // tell an explicit flag from an option that merely has a default.
            var request = new RunOptionsRequest
            {
                ProjectPath = parseResult.GetValue(projectPathsArgument)!,
                OutputPath = Explicit(parseResult, outputOption),
                UseGitignore = ExplicitValue(parseResult, noGitignoreOption) is { } noGitignore ? !noGitignore : null,
                Exclude = Explicit(parseResult, excludeOption),
                UseDefaultExcludes = ExplicitValue(parseResult, useDefaultExcludesOption),
                AllowDangerousFiles = ExplicitValue(parseResult, allowDangerousFilesOption),
                MaxFileSize = ExplicitValue(parseResult, maxSizeOption),
                ExcludeDirs = Explicit(parseResult, excludeDirsOption),
                ExcludeFiles = Explicit(parseResult, excludeFilesOption),
                IncludeDirs = Explicit(parseResult, includeDirsOption),
                IncludeFiles = Explicit(parseResult, includeFilesOption),
                Encoding = Explicit(parseResult, encodingOption),
                MaxConcurrency = ExplicitValue(parseResult, maxConcurrencyOption),
                Format = Explicit(parseResult, formatOption),
                MaxTokens = maxTokens,
                TokenOverflowPolicy = ExplicitValue(parseResult, tokenOverflowPolicyOption),
                TokenSelectionStrategy = ExplicitValue(parseResult, tokenSelectionStrategyOption),
                IncludeFileContents = diffOnly ? false : null,
                Minify = ExplicitValue(parseResult, minifyOption),
                StripComments = ExplicitValue(parseResult, stripCommentsOption),
                PreserveDocComments = ExplicitValue(parseResult, preserveDocCommentsOption),
                CommentStrategy = ExplicitValue(parseResult, commentStrategyOption),
                SignaturesOnly = ExplicitValue(parseResult, signaturesOnlyOption),
                Branch = branch,
                Commit = commit,
                Diff = diffOnly || !string.IsNullOrWhiteSpace(diffFrom)
                    ? new DiffRequest
                    {
                        From = diffFrom,
                        To = diffTo,
                        Only = diffOnly,
                        IncludeUntracked = diffIncludeUntracked,
                    }
                    : null,
            };

            var resolution = new RunOptionsResolver().Resolve(request, profileResult.Profile, profileResult.Path);
            if (!resolution.Succeeded)
            {
                output.Error(resolution.Error!);
                return 1;
            }

            var options = resolution.Options!;

            // The two comment switches do nothing on their own; say so instead of accepting them in silence. The
            // values are the resolved ones, so a profile that sets them is reported the same way a flag is.
            if (!options.StripComments && !options.Minify)
            {
                if (options.PreserveDocumentationComments)
                {
                    output.Warning("--preserve-doc-comments has no effect without --strip-comments or --minify.");
                }

                if (options.CommentStrategy != CommentStrategyRequest.Auto)
                {
                    output.Warning("--comment-strategy has no effect without --strip-comments or --minify.");
                }
            }

            // Resolve the strategy before any work: asking for ANTLR token channels without a maintained grammar is
            // an error, and it must not quietly degrade into lexical scanning.
            if (options.StripComments || options.Minify)
            {
                if (!new CommentStrippingPlanner().TryResolve(options.CommentStrategy, grammarLoader.Discover(), out var resolved, out var planError))
                {
                    output.Error(planError!);
                    return 1;
                }

                options = options with { CommentStripPlan = resolved };
            }

            try
            {
                return await RunAppAsync(options, output, files);
            }
            catch (Exception ex)
            {
                output.Error(ex.Message);
                return 1;
            }
        });

        var result = rootCommand.Parse(args);
        return await Task.FromResult(await result.InvokeAsync()).ConfigureAwait(false);
    }

    /// <summary>
    /// A reference-typed option as the user typed it, or <c>null</c> when only the option's default filled it in.
    /// This is what lets a profile apply to everything the command line did not state.
    /// </summary>
    private static T? Explicit<T>(ParseResult parseResult, Option<T> option) where T : class? =>
        parseResult.GetResult(option) is { Implicit: false } ? parseResult.GetValue(option) : null;

    /// <summary>The same for a value-typed option, including flags such as <c>--minify</c>.</summary>
    private static T? ExplicitValue<T>(ParseResult parseResult, Option<T> option) where T : struct =>
        parseResult.GetResult(option) is { Implicit: false } ? parseResult.GetValue(option) : null;

    private static async Task<int> RunAppAsync(RunAppOptions options, IRunOutput output, FileProviderAccess files)
    {
        Console.OutputEncoding = options.TargetEncoding;
        Console.InputEncoding = options.TargetEncoding;

        var sc = new ServiceCollection();
        sc.AddInfrastructureServices(options, output, files);
        await using var serviceProvider = sc.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        await using var scope = serviceProvider.CreateAsyncScope();

        var exitCode = await scope.ServiceProvider.GetRequiredService<BundleAppRunner>().RunAsync(options);

        scope.ServiceProvider.GetRequiredService<IRunReportWriter>().Write();

        return exitCode;
    }
}
