namespace Plutus.Domain;

public interface IMarkdownLanguageProvider
{
    string GetLanguage(string fileName);
}

public sealed class MarkdownLanguageProvider : IMarkdownLanguageProvider
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

    public string GetLanguage(string fileName)
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
