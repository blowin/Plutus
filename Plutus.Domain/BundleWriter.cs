using System.Text;
using System.Text.RegularExpressions;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Domain;

public class BundleWriter(FileService fileService, IIgnoreMatcher ignoreMatcher)
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
        List<DirectoryInfo> roots,
        FileInfo output,
        List<BundleEntry> entries,
        long maxFileSize)
    {
        output.Directory?.Create();

        var totalSize = entries.Sum(x => x.Size);

        await using var writer = new StreamWriter(output.FullName, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        await writer.WriteLineAsync("# Project Bundle").ConfigureAwait(false);
        await writer.WriteLineAsync().ConfigureAwait(false);
        var rootStr = roots.Count > 1 ? "Roots" : "Root";
        await writer.WriteLineAsync($"- {rootStr}:").ConfigureAwait(false);
        foreach (DirectoryInfo root in roots)
        {
            await writer.WriteLineAsync($"\t `{root.FullName}`").ConfigureAwait(false);
        }
        await writer.WriteLineAsync($"- Output: `{output.FullName}`").ConfigureAwait(false);
        await writer.WriteLineAsync($"- Files included: `{entries.Count}`").ConfigureAwait(false);
        await writer.WriteLineAsync($"- Total size: `{totalSize.HumanSize()}`").ConfigureAwait(false);
        await writer.WriteLineAsync($"- Max file size: `{maxFileSize.HumanSize()}`").ConfigureAwait(false);
        await writer.WriteLineAsync().ConfigureAwait(false);

        await writer.WriteLineAsync("## File List").ConfigureAwait(false);
        await writer.WriteLineAsync().ConfigureAwait(false);
        await writer.WriteLineAsync("```text").ConfigureAwait(false);
        foreach (var entry in entries)
        {
            await writer.WriteLineAsync($"{entry.RelativePath} [{entry.Size.HumanSize()}]").ConfigureAwait(false);
        }
        await writer.WriteLineAsync("```").ConfigureAwait(false);

        await writer.WriteLineAsync().ConfigureAwait(false);
        await writer.WriteLineAsync("## Directory Tree").ConfigureAwait(false);

        foreach (DirectoryInfo root in roots)
        {
            await writer.WriteLineAsync().ConfigureAwait(false);
            await writer.WriteLineAsync("```text").ConfigureAwait(false);
            foreach (var line in new TreeRenderer(fileService, ignoreMatcher).RenderTree(root, output, maxFileSize))
            {
                await writer.WriteLineAsync(line).ConfigureAwait(false);
            }
            await writer.WriteLineAsync("```").ConfigureAwait(false);
        }

        foreach (var entry in entries)
        {
            await writer.WriteLineAsync().ConfigureAwait(false);
            await writer.WriteLineAsync("---").ConfigureAwait(false);
            await writer.WriteLineAsync().ConfigureAwait(false);
            await writer.WriteLineAsync($"## FILE: `{entry.RelativePath}`").ConfigureAwait(false);
            await writer.WriteLineAsync().ConfigureAwait(false);

            if (entry.Size > maxFileSize)
            {
                await writer.WriteLineAsync("```text").ConfigureAwait(false);
                await writer.WriteLineAsync(
                    $"[skipped: file is larger than max-file-size ({entry.Size.HumanSize()} > {maxFileSize.HumanSize()})]")
                    .ConfigureAwait(false);
                await writer.WriteLineAsync("```").ConfigureAwait(false);
                continue;
            }

            var (content, reason) = await ReadTextSafeAsync(entry.File).ConfigureAwait(false);

            if (content is null)
            {
                await writer.WriteLineAsync("```text").ConfigureAwait(false);
                await writer.WriteLineAsync($"[skipped: {reason}]").ConfigureAwait(false);
                await writer.WriteLineAsync("```").ConfigureAwait(false);
                continue;
            }

            var fence = ChooseFence(content);
            var language = GetLanguage(entry.File);

            await writer.WriteLineAsync($"{fence}{language}").ConfigureAwait(false);
            await writer.WriteAsync(content).ConfigureAwait(false);

            if (content.Length == 0 || content[^1] != '\n')
            {
                await writer.WriteLineAsync().ConfigureAwait(false);
            }

            await writer.WriteLineAsync(fence).ConfigureAwait(false);
        }
    }

    private static async Task<(string? Content, string? Reason)> ReadTextSafeAsync(FileInfo file)
    {
        byte[] bytes;

        try
        {
            bytes = await File.ReadAllBytesAsync(file.FullName).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return (null, $"unreadable: {ex.Message}");
        }

        if (bytes.Length == 0)
        {
            return (string.Empty, null);
        }

        // Check for binary content
        var probeLength = Math.Min(8192, bytes.Length);
        for (var i = 0; i < probeLength; i++)
        {
            if (bytes[i] == 0)
            {
                return (null, "binary file");
            }
        }

        // Try UTF-8
        try
        {
            var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            return (strictUtf8.GetString(bytes), null);
        }
        catch
        {
            // fallback below
        }

        // Try Windows-1251
        try
        {
            var windows1251 = Encoding.GetEncoding(1251);
            return (windows1251.GetString(bytes), null);
        }
        catch
        {
            // fallback below
        }

        // Last resort: UTF-8 with replacement
        return (Encoding.UTF8.GetString(bytes), null);
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

    private static string GetLanguage(FileInfo file)
    {
        if (ExtensionToLanguage.TryGetValue(file.Extension, out var language))
        {
            return language;
        }

        var name = file.Name.ToLowerInvariant();

        if (name == "dockerfile" || name.StartsWith("dockerfile.", StringComparison.OrdinalIgnoreCase))
        {
            return "dockerfile";
        }

        if (name == "makefile" || name.StartsWith("makefile.", StringComparison.OrdinalIgnoreCase))
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
