using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain.Minifier;

namespace Plutus.Domain.BundleWriters;

public class MarkdownBundleWriter(
    IFileInfoDetailProvider fileInfoDetailProvider,
    IMarkdownLanguageProvider markdownLanguageProvider,
    ICodeMinifier codeMinifier,
    Encoding encoding) : BaseBundleWriter(fileInfoDetailProvider, markdownLanguageProvider, codeMinifier, encoding)
{
    public override string FormatExtension => "md";

    protected override async Task WriteContentAsync(
        List<(IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode)> roots,
        IFileInfo output,
        List<ProcessedFileContext> files,
        FileSize maxFileSize)
    {
        var totalSize = files.Sum(x => x.Entry.File.Length);

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
        writer.WriteLine($"- Files included: `{files.Count}`");
        writer.WriteLine($"- Total size: `{totalSize.HumanSize()}`");
        writer.WriteLine($"- Max file size: `{maxFileSize}`");
        writer.WriteLine();

        writer.WriteLine("## File List");
        writer.WriteLine();
        writer.WriteLine("```text");
        foreach (var entry in files)
        {
            writer.WriteLine($"{entry.Entry.RelativePath} [{entry.Entry.File.Length.HumanSize()}]");
        }
        writer.WriteLine("```");

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

        foreach (var entry in files)
        {
            writer.WriteLine();
            writer.WriteLine("---");
            writer.WriteLine();
            writer.WriteLine($"## FILE: `{entry.Entry.RelativePath}`");
            writer.WriteLine();

            if (entry.Entry.File.Length > maxFileSize)
            {
                writer.WriteLine("```text");
                writer.WriteLine($"[skipped: file is larger than max-file-size ({entry.Entry.File.Length.HumanSize()} > {maxFileSize})]");
                writer.WriteLine("```");
                continue;
            }

            if (entry.IsSkipped)
            {
                writer.WriteLine("```text");
                writer.WriteLine($"[skipped: {entry.SkipReason}]");
                writer.WriteLine("```");
                continue;
            }

            var content = entry.Content ?? string.Empty;
            var fence = ChooseFence(content);
            writer.WriteLine($"{fence}{entry.Language}");
            await writer.WriteAsync(content).ConfigureAwait(false); ;

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
