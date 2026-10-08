using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain.Minifier;

namespace Plutus.Domain;

public class BundleWriter(IFileInfoDetailProvider fileInfoDetailProvider, IMarkdownLanguageProvider markdownLanguageProvider, ICodeMinifier codeMinifier)
{
    public async ValueTask<List<BundleEntry>> WriteAsync(
        List<(IFileProvider Provider, string RootName, string OriginalPath, ProjectNode ProjectNode)> roots,
        IFileInfo output,
        FileSize maxFileSize)
    {
        fileInfoDetailProvider.CreateDirectoryForFile(output);

        var entries = roots.SelectMany(e => e.ProjectNode.ExtractBundleEntries()).ToList();
        entries.Sort((a, b) => string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase));

        var totalSize = entries.Sum(x => x.File.Length);

        await using var writer = fileInfoDetailProvider.CreateWriterForFile(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

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

        foreach (var entry in entries)
        {
            writer.WriteLine();
            writer.WriteLine("---");
            writer.WriteLine();
            writer.WriteLine($"## FILE: `{entry.RelativePath}`");
            writer.WriteLine();

            if (entry.File.Length > maxFileSize)
            {
                writer.WriteLine("```text");
                writer.WriteLine($"[skipped: file is larger than max-file-size ({entry.File.Length.HumanSize()} > {maxFileSize})]");
                writer.WriteLine("```");
                continue;
            }

            var (content, reason) = await ReadTextSafeAsync(entry.File).ConfigureAwait(false);

            if (content is null)
            {
                writer.WriteLine("```text");
                writer.WriteLine($"[skipped: {reason}]");
                writer.WriteLine("```");
                continue;
            }

            var fence = ChooseFence(content);

            var language = markdownLanguageProvider.GetLanguage(entry.File.Name);
            content = codeMinifier.Minify(content, language);

            writer.WriteLine($"{fence}{language}");
            await writer.WriteAsync(content).ConfigureAwait(false); ;

            if (content.Length == 0 || content[^1] != '\n')
            {
                writer.WriteLine();
            }

            writer.WriteLine(fence);
        }

        return entries;
    }

    private static async Task<(string? Content, string? Reason)> ReadTextSafeAsync(IFileInfo file)
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

            // try UTF-8
            try
            {
                var strictUtf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);
                return (strictUtf8.GetString(bytes), null);
            }
            catch
            {
                // Ignore use fallback
            }

            // try Windows-1251
            try
            {
                var windows1251 = Encoding.GetEncoding(1251);
                return (windows1251.GetString(bytes), null);
            }
            catch
            {
                // Ignore use fallback
            }

            return (Encoding.UTF8.GetString(bytes), null);
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
}
