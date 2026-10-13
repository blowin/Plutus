using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain.Minifier;

namespace Plutus.Domain;

public class BundleWriter(
    IFileInfoDetailProvider fileInfoDetailProvider,
    IMarkdownLanguageProvider markdownLanguageProvider,
    ICodeMinifier codeMinifier,
    Encoding encoding)
{
    public int MaxConcurrentFileReads { get; set; } = 8;

    public async ValueTask<List<BundleEntry>> WriteAsync(
        List<(IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode)> roots,
        IFileInfo output,
        FileSize maxFileSize)
    {
        fileInfoDetailProvider.CreateDirectoryForFile(output);

        var entries = roots.SelectMany(e => e.ProjectNode.ExtractBundleEntries()).ToList();
        entries.Sort((a, b) => string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase));

        var totalSize = entries.Sum(x => x.File.Length);

        await using var writer = fileInfoDetailProvider.CreateWriterForFile(output, encoding);

        writer.WriteLine("# Project Bundle");
        writer.WriteLine();
        var rootStr = roots.Count > 1 ? "Roots" : "Root";
        writer.WriteLine($"- {rootStr}:");
        foreach (var root in roots)
        {
            writer.WriteLine($"\t `{root.OriginalPath}`");
        }
        writer.WriteLine($"- Output: `{output.PhysicalPath!}`");
        writer.WriteLine($"- Files included: `{entries.Count}`");
        writer.WriteLine($"- Total size: `{totalSize.HumanSize()}`");
        writer.WriteLine($"- Max file size: `{maxFileSize}`");
        writer.WriteLine();

        writer.WriteLine("## File List");
        writer.WriteLine();
        writer.WriteLine("```text");
        foreach (var entry in entries)
        {
            writer.WriteLine($"{entry.RelativePath} [{entry.File.Length.HumanSize()}]");
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

        using var semaphore = new SemaphoreSlim(MaxConcurrentFileReads);

        // ReSharper disable once AccessToDisposedClosure
        var processingTasks = entries.Select(e => ReadTextSafeAsync(semaphore, e, maxFileSize));
        ProcessedFileBlock[] processedBlocks = await Task.WhenAll(processingTasks).ConfigureAwait(false);
        foreach (var block in processedBlocks)
        {
            await writer.WriteAsync(block.ContentBlock).ConfigureAwait(false);
        }

        return entries;
    }

    private async Task<ProcessedFileBlock> ReadTextSafeAsync(SemaphoreSlim semaphore, BundleEntry entry, FileSize maxFileSize)
    {
        await semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var sb = new StringBuilder(1024);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine($"## FILE: `{entry.RelativePath}`");
            sb.AppendLine();

            if (entry.File.Length > maxFileSize)
            {
                sb.AppendLine("```text");
                sb.AppendLine($"[skipped: file is larger than max-file-size ({entry.File.Length.HumanSize()} > {maxFileSize})]");
                sb.AppendLine("```");
                return new ProcessedFileBlock(entry, sb.ToString());
            }

            var (content, reason) = await ReadTextSafeAsync(entry.File).ConfigureAwait(false);
            if (content is null)
            {
                sb.AppendLine("```text");
                sb.AppendLine($"[skipped: {reason}]");
                sb.AppendLine("```");
                return new ProcessedFileBlock(entry, sb.ToString());
            }

            var fence = ChooseFence(content);
            var language = markdownLanguageProvider.GetLanguage(entry.File.Name);
            content = codeMinifier.Minify(content, language);

            sb.AppendLine($"{fence}{language}");
            sb.Append(content);

            if (content.Length == 0 || content[^1] != '\n')
            {
                sb.AppendLine();
            }
            sb.AppendLine(fence);

            return new ProcessedFileBlock(entry, sb.ToString());
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<(string? Content, string? Reason)> ReadTextSafeAsync(IFileInfo file)
    {
        try
        {
            // Streaming check of the first 8 KB for \0 characters (Binary detection optimization)
            await using (var fs = file.CreateReadStream())
            {
                var buffer = new byte[8192];
                var bytesRead = await fs.ReadAsync(buffer).ConfigureAwait(false);
                for (var i = 0; i < bytesRead; i++)
                {
                    if (buffer[i] == 0)
                    {
                        return (null, "binary file");
                    }
                }
            }

            // Safely extract the entire byte array only after verifying that it is text.

            byte[] bytes;
            await using (var ms = new MemoryStream())
            {
                await using (var fs = file.CreateReadStream())
                {
                    await fs.CopyToAsync(ms).ConfigureAwait(false);
                }
                bytes = ms.ToArray();
            }

            if (bytes.Length == 0)
            {
                return (string.Empty, null);
            }

            return (encoding.GetString(bytes), null);
        }
        catch (Exception ex)
        {
            return (null, $"unreadable: {ex.Message}");
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

    private record ProcessedFileBlock(BundleEntry Entry, string ContentBlock);
}
