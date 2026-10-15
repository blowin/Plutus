using System.CommandLine;
using Plutus.Domain;
using Plutus.Domain.Comments;
using Plutus.Domain.Configuration;
using Plutus.Domain.Diagnostics;
using Plutus.Infrastructure;

namespace Plutus.CLI;

/// <summary>
/// The default command: bundle the given project paths. It is a command object like <c>init</c>, so both live next to
/// each other in the entry point, and its action is a short sequence of named steps — read what was stated, resolve
/// the profile, resolve the comment strategy, run — instead of one long lambda.
/// </summary>
internal static class BundleCommand
{
    public static RootCommand Create(IRunOutput output)
    {
        var options = new BundleOptions().WithAliases();
        var command = new RootCommand("Plutus — Project Bundler. Collects source files into a single Markdown document.");
        command.Add(options.ProjectPath);
        foreach (var option in options.Options())
        {
            command.Add(option);
        }

        // Subcommands, not options: another job with its own options and exit codes.
        command.Subcommands.Add(InitCommand.Create(output));

        command.SetAction(parseResult => RunAsync(new CommandLineReader(parseResult), options, output));
        return command;
    }

    private static async Task<int> RunAsync(CommandLineReader reader, BundleOptions options, IRunOutput output)
    {
        var branch = reader.Value(options.Branch);
        var commit = reader.Value(options.Commit);
        var diffFrom = reader.Value(options.DiffFrom);
        var diffOnly = reader.Value(options.DiffOnly);

        if (Validate(branch, commit, diffFrom, reader.Value(options.DiffTo), output) is { } failure)
        {
            return failure;
        }

        // Configuration is read through one file access for the whole process: the profile search, the grammar
        // inventory and the container all see the same filesystem, and none of them opens a file directly.
        var fileProviderDetail = new PhysicianPlutusFileInfo();
        var files = new FileProviderAccessFactory(fileProviderDetail).CreateForWorkingDirectory();
        var profileLoader = new PlutusProfileLoader(files);

        var profileResult = ReadProfile(reader, options, profileLoader, fileProviderDetail, output);
        if (!profileResult.Succeeded)
        {
            output.Error(profileResult.Error!);
            return 1;
        }

        var resolution = new RunOptionsResolver().Resolve(
            Request(reader, options),
            profileResult.Profile,
            profileResult.Path);

        if (!resolution.Succeeded)
        {
            output.Error(resolution.Error!);
            return 1;
        }

        var resolved = resolution.Options!;
        WarnAboutIdleCommentOptions(resolved, output);

        // Resolve the strategy before any work: asking for ANTLR token channels without a maintained grammar is an
        // error, and it must not quietly degrade into lexical scanning.
        if (resolved.StripComments || resolved.Minify)
        {
            var grammarLoader = new GrammarInventoryLoader(files);
            if (!new CommentStrippingPlanner().TryResolve(resolved.CommentStrategy, grammarLoader.Discover(), out var plan, out var planError))
            {
                output.Error(planError!);
                return 1;
            }

            resolved = resolved with { CommentStripPlan = plan };
        }

        try
        {
            return await BundleRunHost.RunAsync(resolved, output, files);
        }
        catch (Exception exception)
        {
            output.Error(exception.Message);
            return 1;
        }
    }

    /// <summary>The combinations that cannot mean anything, refused before any work starts.</summary>
    private static int? Validate(string? branch, string? commit, string? diffFrom, string? diffTo, IRunOutput output)
    {
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

        return null;
    }

    /// <summary>
    /// Finds the profile before anything runs: a profile that cannot be trusted is a failure, not a reason to fall
    /// back to the defaults without saying so.
    /// </summary>
    private static ProfileLoadResult ReadProfile(
        CommandLineReader reader,
        BundleOptions options,
        PlutusProfileLoader loader,
        IFileInfoDetailProvider detailProvider,
        IRunOutput output)
    {
        var noProfile = reader.Value(options.NoProfile);
        var named = reader.Value(options.Profile) ?? Environment.GetEnvironmentVariable(PlutusProfileLoader.PathVariable);
        if (noProfile && !string.IsNullOrWhiteSpace(named))
        {
            output.Warning("--no-profile ignores --profile and the PLUTUS_PROFILE environment variable.");
        }

        return loader.Resolve(detailProvider.GetCurrentDirectory(), named, noProfile);
    }

    /// <summary>
    /// What the command line stated, with everything else left unstated so the profile can fill it in. A flag that is
    /// not listed here cannot come from a profile because another source already owns it.
    /// </summary>
    private static RunOptionsRequest Request(CommandLineReader reader, BundleOptions options)
    {
        var diffOnly = reader.Value(options.DiffOnly);
        var diffFrom = reader.Value(options.DiffFrom);

        return new RunOptionsRequest
        {
            ProjectPath = reader.Argument(options.ProjectPath),
            OutputPath = reader.Explicit(options.Output),
            UseGitignore = reader.ExplicitInverse(options.NoGitignore),
            Exclude = reader.Explicit(options.Exclude),
            UseDefaultExcludes = reader.ExplicitValue(options.UseDefaultExcludes),
            AllowDangerousFiles = reader.ExplicitValue(options.AllowDangerousFiles),
            MaxFileSize = reader.ExplicitValue(options.MaxFileSize),
            ExcludeDirs = reader.Explicit(options.ExcludeDirs),
            ExcludeFiles = reader.Explicit(options.ExcludeFiles),
            IncludeDirs = reader.Explicit(options.IncludeDirs),
            IncludeFiles = reader.Explicit(options.IncludeFiles),
            Encoding = reader.Explicit(options.Encoding),
            MaxConcurrency = reader.ExplicitValue(options.MaxConcurrency),
            Format = reader.Explicit(options.Format),
            MaxTokens = reader.Value(options.MaxTokens),
            TokenOverflowPolicy = reader.ExplicitValue(options.OverflowPolicy),
            TokenSelectionStrategy = reader.ExplicitValue(options.SelectionStrategy),
            IncludeFileContents = diffOnly ? false : null,
            Minify = reader.ExplicitValue(options.Minify),
            StripComments = reader.ExplicitValue(options.StripComments),
            PreserveDocComments = reader.ExplicitValue(options.PreserveDocComments),
            CommentStrategy = reader.ExplicitValue(options.CommentStrategy),
            SignaturesOnly = reader.ExplicitValue(options.SignaturesOnly),
            Branch = reader.Value(options.Branch),
            Commit = reader.Value(options.Commit),
            Diff = diffOnly || !string.IsNullOrWhiteSpace(diffFrom)
                ? new DiffRequest
                {
                    From = diffFrom,
                    To = reader.Value(options.DiffTo),
                    Only = diffOnly,
                    IncludeUntracked = reader.Value(options.DiffIncludeUntracked),
                }
                : null,
        };
    }

    /// <summary>
    /// The two comment switches do nothing on their own; saying so is better than accepting them in silence. The
    /// values are the resolved ones, so a profile that sets them is reported the same way a flag is.
    /// </summary>
    private static void WarnAboutIdleCommentOptions(RunAppOptions options, IRunOutput output)
    {
        if (options.StripComments || options.Minify)
        {
            return;
        }

        if (options.PreserveDocumentationComments)
        {
            output.Warning("--preserve-doc-comments has no effect without --strip-comments or --minify.");
        }

        if (options.CommentStrategy != CommentStrategyRequest.Auto)
        {
            output.Warning("--comment-strategy has no effect without --strip-comments or --minify.");
        }
    }
}
