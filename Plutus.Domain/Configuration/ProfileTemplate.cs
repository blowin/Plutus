namespace Plutus.Domain.Configuration;

/// <summary>
/// The commented profile template a user starts from. Every key is present but commented out, so generating the file
/// cannot change what a run does — it only puts the options, their command-line equivalents and their defaults in
/// front of the person who has to fill them in.
/// <para>
/// A test asserts that the template mentions every key the loader accepts, so a new option cannot be added without
/// showing up here.
/// </para>
/// </summary>
public sealed class ProfileTemplate
{
    public string Render() => Template;

    private const string Template = """
        // Plutus configuration profile.
        //
        // A profile states the defaults of a repository, so the usual command line stops being a list of exclusions:
        // Plutus uses the nearest .plutusrc in the working directory or any parent up to the repository root, unless
        // --profile <path> names one explicitly or --no-profile disables the search.
        //
        // Every key below is commented out, so this file changes nothing until you uncomment what you need. Comments
        // and trailing commas are allowed. An explicit command-line flag always wins over this file:
        //
        //     explicit flag  >  this file  >  built-in default
        //
        // What a profile cannot set: the project paths, --branch/--commit, and the diff scope (--diff-only,
        // --diff-from, --diff-to): those say what is being bundled, not how. See docs/CONFIGURATION.md.

        {
          // ---- path rules -----------------------------------------------------------------------------------

          // Patterns excluding files, as if repeated --exclude-files.
          // "excludeFiles": ["*.g.cs", "*.Designer.cs"],

          // Patterns excluding directories, as if repeated --exclude-dirs: the usual generated and vendored trees.
          // "excludeDirs": ["bin", "obj", "node_modules", "dist", ".vs"],

          // Patterns excluding files and directories alike, as if repeated --exclude.
          // "exclude": ["*.log"],

          // Inclusive patterns: when one is set, only matching files are bundled, as with --include-files/--include-dirs.
          // "includeFiles": ["src/**"],
          // "includeDirs": ["src", "tests"],

          // Inclusive extensions: the friendly form of an inclusive filter. Each entry selects that extension at any
          // depth, so this is the same as --include-files "*.cs" "*.razor".
          // "includeExtensions": [".cs", ".razor"],

          // Read root .gitignore files (the inverse of --no-gitignore). Default: true.
          // "useGitignore": true,

          // Use the built-in junk exclusions (--use-default-excludes). Default: true.
          // "useDefaultExcludes": true,

          // Include files whose names look sensitive, such as .env or keys (--allow-dangerous-files). Default: false.
          // "allowDangerousFiles": false,

          // ---- output preferences ---------------------------------------------------------------------------

          // Bundle path (--output). A relative path is resolved against this file's directory, not the command's.
          // "output": "artifacts/context.md",

          // Bundle format: "md", "json" or "xml" (--format). Default: "md".
          // "format": "md",

          // Text encoding used for reading and writing (--encoding). Default: "utf-8".
          // "encoding": "utf-8",

          // Largest file to include, for example "512K" or "2M" (--max-file-size). Default: "1M".
          // "maxFileSize": "2M",

          // Source-token budget, checked against an offline estimate (--max-tokens). Default: disabled.
          // "maxTokens": 200000,

          // What happens when the budget is exceeded: "fail", "warn" or "select" (--token-overflow-policy).
          // Default: "fail".
          // "tokenOverflowPolicy": "select",

          // Which whole files "select" keeps: "path-order" or "largest-first" (--token-selection-strategy).
          // Default: "path-order".
          // "tokenSelectionStrategy": "path-order",

          // Concurrent file reads (--max-concurrency). Default: 8.
          // "maxConcurrency": 8,

          // Carry file contents, or only the change itself (the inverse of --diff-only). Default: true.
          // "includeFileContents": true,

          // ---- source processing ----------------------------------------------------------------------------

          // Remove comments and collapse whitespace-only lines (--minify).
          // "minify": true,

          // Remove comments only, keeping whitespace, indentation and line endings (--strip-comments).
          // "stripComments": true,

          // Keep documentation comments while stripping or minifying (--preserve-doc-comments). Default: false.
          // "preserveDocComments": true,

          // How comments are located: "auto", "lexical" or "antlr" (--comment-strategy). Default: "auto".
          // "commentStrategy": "auto",

          // Reduce supported files to their signatures (--signatures-only).
          // "signaturesOnly": false
        }
        """;
}
