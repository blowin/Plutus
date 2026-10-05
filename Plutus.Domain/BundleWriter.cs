using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileProviders;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Domain;

public class BundleWriter(IFileInfoDetailProvider fileInfoDetailProvider, IIgnoreMatcher ignoreMatcher)
{
    private static readonly Dictionary<string, string> ExtensionToLanguage = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "csharp",
        [".csx"] = "csharp",
        [".py"] = "python",
        [".pyi"] = "python",
        [".js"] = "javascript",
        [".mjs"] = "javascript",
        [".cjs"] = "javascript",
        [".jsx"] = "jsx",
        [".ts"] = "typescript",
        [".tsx"] = "tsx",
        [".json"] = "json",
        [".jsonc"] = "json",
        [".yaml"] = "yaml",
        [".yml"] = "yaml",
        [".toml"] = "toml",
        [".ini"] = "ini",
        [".cfg"] = "ini",
        [".conf"] = "ini",
        [".md"] = "markdown",
        [".markdown"] = "markdown",
        [".rst"] = "rst",
        [".txt"] = "text",
        [".sql"] = "sql",
        [".sh"] = "bash",
        [".bash"] = "bash",
        [".zsh"] = "bash",
        [".ps1"] = "powershell",
        [".bat"] = "batch",
        [".cmd"] = "batch",
        [".html"] = "html",
        [".htm"] = "html",
        [".xml"] = "xml",
        [".xsd"] = "xml",
        [".css"] = "css",
        [".scss"] = "scss",
        [".sass"] = "sass",
        [".less"] = "less",
        [".vue"] = "vue",
        [".svelte"] = "svelte",
        [".go"] = "go",
        [".rs"] = "rust",
        [".java"] = "java",
        [".kt"] = "kotlin",
        [".kts"] = "kotlin",
        [".scala"] = "scala",
        [".c"] = "c",
        [".h"] = "c",
        [".cpp"] = "cpp",
        [".cc"] = "cpp",
        [".cxx"] = "cpp",
        [".hpp"] = "cpp",
        [".vb"] = "vb",
        [".fs"] = "fsharp",
        [".php"] = "php",
        [".rb"] = "ruby",
        [".swift"] = "swift",
        [".dart"] = "dart",
        [".ex"] = "elixir",
        [".exs"] = "elixir",
        [".erl"] = "erlang",
        [".hs"] = "haskell",
        [".lua"] = "lua",
        [".r"] = "r",
        [".m"] = "matlab",
        [".proto"] = "protobuf",
        [".graphql"] = "graphql",
        [".gql"] = "graphql",
        [".tf"] = "hcl",
        [".tfvars"] = "hcl",
        [".axaml"] = "xml",
        [".xaml"] = "xml",
        [".sln"] = "text",
    };

    public async Task WriteAsync(
        List<(IFileProvider Provider, string RootName, string OriginalPath)> roots,
        FileInfo output,
        List<BundleEntry> entries,
        long maxFileSize)
    {
        output.Directory?.Create();

        var totalSize = entries.Sum(x => x.File.Length);

        await using var writer = new StreamWriter(output.FullName, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        writer.WriteLine("# Project Bundle");
        writer.WriteLine();
        var rootStr = roots.Count > 1 ? "Roots" : "Root";
        writer.WriteLine($"- {rootStr}:");
        foreach (var root in roots)
        {
            writer.WriteLine($"\t `{root.OriginalPath}`");
        }
        writer.WriteLine($"- Output: `{output.FullName}`");
        writer.WriteLine($"- Files included: `{entries.Count}`");
        writer.WriteLine($"- Total size: `{totalSize.HumanSize()}`");
        writer.WriteLine($"- Max file size: `{maxFileSize.HumanSize()}`");
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

        var treeRenderer = new TreeRenderer(ignoreMatcher, fileInfoDetailProvider);
        foreach (var root in roots)
        {
            writer.WriteLine();
            writer.WriteLine("```text");
            foreach (var line in treeRenderer.RenderTree(root.Provider, root.RootName, output.FullName, maxFileSize))
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
                writer.WriteLine(
                    $"[skipped: file is larger than max-file-size ({entry.File.Length.HumanSize()} > {maxFileSize.HumanSize()})]")
                    ;
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
            var language = GetLanguage(entry.File.Name);

            writer.WriteLine($"{fence}{language}");
            await writer.WriteAsync(content).ConfigureAwait(false); ;

            if (content.Length == 0 || content[^1] != '\n')
            {
                writer.WriteLine();
            }

            writer.WriteLine(fence);
        }
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

    private static string GetLanguage(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (ExtensionToLanguage.TryGetValue(extension, out var language))
        {
            return language;
        }

        var name = fileName.ToLowerInvariant();

        if (name == "dockerfile" || name.StartsWith("dockerfile."))
        {
            return "dockerfile";
        }

        if (name == "makefile" || name.StartsWith("makefile."))
        {
            return "makefile";
        }

        if (name == "jenkinsfile")
        {
            return "groovy";
        }

        if (name == ".env.example")
        {
            return "dotenv";
        }

        return "text";
    }
}
