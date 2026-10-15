using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain.Diffing;
using Plutus.Domain.Minifier;
using Plutus.Domain.Tokens;
using Plutus.Domain.Tokens.Applier;

namespace Plutus.Domain.BundleWriters;

public class MarkdownBundleWriter(
    IFileInfoDetailProvider fileInfoDetailProvider,
    IMarkdownLanguageProvider markdownLanguageProvider,
    ICodeMinifier codeMinifier,
    Encoding encoding,
    BundleLimits bundleLimits) : BaseBundleWriter(fileInfoDetailProvider, markdownLanguageProvider, codeMinifier, encoding, bundleLimits)
{
    public override string FormatExtension => "md";

    protected override async Task WriteContentAsync(List<(IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode)> roots,
        IFileInfo output,
        List<ProcessedFileContext> files,
        TokenBudgetSummary? tokenBudgetSummary,
        GitDiffContext? gitDiff)
    {
        var includedFiles = files.Where(x => !x.IsSkipped).ToList();
        var totalSize = includedFiles.Sum(x => x.Entry.File.Length);

        await using var writer = FileInfoDetailProvider.CreateWriterForFile(output, Encoding);

        writer.WriteLine("# Project Bundle");
        writer.WriteLine();
        var rootStr = roots.Count > 1 ? "Roots" : "Root";
        writer.WriteLine($"- {rootStr}:");
        foreach (var root in roots)
        {
            writer.WriteLine($"\t `{root.OriginalPath}`");
        }
        writer.WriteLine($"- Output: `{output.PhysicalPath!}`");
        writer.WriteLine($"- Files included: `{includedFiles.Count}`");
        if (IncludeFileContents)
        {
            writer.WriteLine($"- Total size: `{totalSize.HumanSize()}`");
        }

        writer.WriteLine($"- Max file size: `{Limits.MaxFileSize}`");
        if (tokenBudgetSummary is { } tokenSummary)
        {
            writer.WriteLine($"- Token estimate method: `{tokenSummary.EstimatorMethod}`");
            writer.WriteLine($"- Estimated source tokens: `{tokenSummary.CandidateTokens}`; selected: `{tokenSummary.SelectedTokens}`; budget: `{tokenSummary.MaxTokens}`; policy: `{tokenSummary.OverflowPolicy.ToString().ToLowerInvariant()}`; strategy: `{tokenSummary.SelectionStrategy}`");
        }
        writer.WriteLine();

        writer.WriteLine(IncludeFileContents ? "## File List" : "## Changed Files");
        writer.WriteLine();
        writer.WriteLine("```text");
        foreach (var entry in files)
        {
            var tokenSuffix = entry.EstimatedTokens.HasValue ? $"; ~{entry.EstimatedTokens.Value} tokens" : string.Empty;

            // In diff mode the contents are absent, so the list states why a change was left out instead of
            // giving it a diff block whose only content would be that reason.
            var exclusion = gitDiff is null
                ? null
                : BundleDiff.DescribeExclusion(gitDiff.TrackedChunks.Concat(gitDiff.UntrackedChunks).ToArray(), entry.Entry.RelativePath);

            writer.WriteLine(IncludeFileContents
                ? $"{entry.Entry.RelativePath} [{entry.Entry.File.Length.HumanSize()}{tokenSuffix}]"
                : $"{entry.Entry.RelativePath}{exclusion}");
        }
        writer.WriteLine("```");

        if (IncludeFileContents)
        {
            WriteDirectoryTree(writer, roots);
            await WriteFileContentsAsync(writer, files).ConfigureAwait(false);
        }

        if (gitDiff is not null)
        {
            writer.WriteLine();
            writer.WriteLine("---");
            writer.WriteLine();
            BundleDiff.WriteSection(writer, gitDiff, "Bundle scope is limited to", IncludeFileContents);
        }
    }

    private static void WriteDirectoryTree(
        TextWriter writer,
        List<(IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode)> roots)
    {
        writer.WriteLine();
        writer.WriteLine("## Directory Tree");

        var treeRenderer = new TreeRenderer();
        foreach (var node in roots)
        {
            writer.WriteLine();
            writer.WriteLine("```text");
            foreach (var line in treeRenderer.RenderTree(node.ProjectNode))
            {
                writer.WriteLine(line);
            }
            writer.WriteLine("```");
        }
    }

    private async Task WriteFileContentsAsync(TextWriter writer, List<ProcessedFileContext> files)
    {
        foreach (var entry in files)
        {
            if (entry.IsSkipped)
            {
                continue;
            }

            writer.WriteLine();
            writer.WriteLine("---");
            writer.WriteLine();
            writer.WriteLine($"## FILE: `{entry.Entry.RelativePath}`");
            writer.WriteLine();

            var content = entry.Content ?? string.Empty;
            var fence = ChooseFence(content);
            writer.WriteLine($"{fence}{entry.Language}");
            await writer.WriteAsync(content).ConfigureAwait(false);

            if (content.Length == 0 || content[^1] != '\n')
            {
                writer.WriteLine();
            }

            writer.WriteLine(fence);
        }
    }

    private static string ChooseFence(string content)
    {
        var maxRun = 0;
        foreach (Match match in Regex.Matches(content, "`+"))
        {
            maxRun = Math.Max(maxRun, match.Length);
        }
        return new string('`', Math.Max(4, maxRun + 1));
    }
}
