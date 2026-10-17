using System.Collections.Frozen;

namespace Plutus.Domain.Comments;

/// <summary>
/// The comment and literal forms of every language this project claims to strip comments from. A language that
/// is absent here has no entry, and the lexical stripper therefore leaves its files untouched instead of applying
/// another language's rules to them.
/// <para>
/// The table is a service, not a static: services take it as a dependency, which is what lets a test exercise the
/// scanner against a table of its own, and the CLI/stripper composition resolve the one the container holds.
/// </para>
/// </summary>
public interface ILanguageCommentSyntaxTable
{
    /// <summary>Every accepted language, aliases included, in ordinal order.</summary>
    IReadOnlyList<LanguageId> Languages { get; }

    /// <summary>The same identifiers as their canonical spellings, for diagnostics and documentation tests.</summary>
    IReadOnlyList<string> LanguageNames { get; }

    /// <summary>The canonical definitions only. Used by tests and by the documentation matrix.</summary>
    IReadOnlyCollection<LanguageCommentSyntax> Definitions { get; }

    bool TryGet(LanguageId language, out LanguageCommentSyntax syntax);

    bool TryGet(string? language, out LanguageCommentSyntax syntax);

    LanguageCommentSyntax? Find(LanguageId language);

    LanguageCommentSyntax? Find(string? language);

    bool IsSupported(LanguageId language);

    bool IsSupported(string? language);
}

/// <summary>
/// The maintained comment and literal forms of every supported language. The table is the single source of truth for
/// the language matrix in <c>docs/COMMENT_STRIPPING.md</c>, and it is covered by tests: every language listed here
/// must prove that it removes its own comments and keeps comment-like text inside its literals.
/// </summary>
public sealed class LanguageCommentSyntaxTable : ILanguageCommentSyntaxTable
{
    private readonly IReadOnlyList<LanguageCommentSyntax> _canonical;
    private readonly FrozenDictionary<string, LanguageCommentSyntax> _byLanguage;
    private readonly IReadOnlyList<string> _orderedNames;

    public LanguageCommentSyntaxTable()
    {
        _canonical = Build();

        var map = new Dictionary<string, LanguageCommentSyntax>(StringComparer.OrdinalIgnoreCase);
        foreach (var syntax in _canonical)
        {
            map[syntax.Language] = syntax;
            foreach (var alias in syntax.Aliases)
            {
                map[alias] = syntax;
            }
        }

        _byLanguage = map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _orderedNames = map.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
        Languages = _orderedNames.Select(LanguageId.Parse).ToArray();
    }

    public IReadOnlyList<LanguageId> Languages { get; }

    public IReadOnlyList<string> LanguageNames => _orderedNames;

    public IReadOnlyCollection<LanguageCommentSyntax> Definitions => _canonical;

    public bool TryGet(LanguageId language, out LanguageCommentSyntax syntax) =>
        _byLanguage.TryGetValue(language.Value, out syntax!);

    public bool TryGet(string? language, out LanguageCommentSyntax syntax)
    {
        if (!string.IsNullOrWhiteSpace(language) && _byLanguage.TryGetValue(language, out var found))
        {
            syntax = found;
            return true;
        }

        syntax = null!;
        return false;
    }

    public LanguageCommentSyntax? Find(LanguageId language) => TryGet(language, out var syntax) ? syntax : null;

    public LanguageCommentSyntax? Find(string? language) => TryGet(language, out var syntax) ? syntax : null;

    public bool IsSupported(LanguageId language) => _byLanguage.ContainsKey(language.Value);

    public bool IsSupported(string? language) => TryGet(language, out _);

    // ---- table construction -------------------------------------------------------------------------------

    private static IReadOnlyList<LanguageCommentSyntax> Build() =>
    [
        // ---------------------------------------------------------------- C# ---
        new LanguageCommentSyntax
        {
            Language = "csharp",
            DisplayName = "C#",
            LineComments = [DocLine("///", '/'), Line("//")],
            BlockComments = [DocBlock("/**", "*/", forbidden: '/'), Block("/*", "*/")],
            Strings =
            [
                // Raw string literals: three or more quotes, closed by a run of the same length. Interpolated
                // raw strings ($$""") are covered by the same rule, so their holes are copied rather than parsed.
                QuoteRun('"', escape: null, maximal: true),

                // Prefixed forms close on the quote alone: the prefix is only part of the opener.
                Delim("$@\"", close: "\"", doubled: true, newlines: true, interpolation: "{"),
                Delim("@$\"", close: "\"", doubled: true, newlines: true, interpolation: "{"),
                Delim("$\"", close: "\"", interpolation: "{"),
                Delim("@\"", close: "\"", doubled: true, newlines: true),
                Delim("\""),
                Delim("'", charLiteral: true),
            ],
        },

        // --------------------------------------------------------- Java / JVM ---
        new LanguageCommentSyntax
        {
            Language = "java",
            DisplayName = "Java",
            LineComments = [Line("//")],
            BlockComments = [DocBlock("/**", "*/", forbidden: '/'), Block("/*", "*/")],
            Strings =
            [
                QuoteRun('"', escape: "\\"),
                Delim("\""),
                Delim("'", charLiteral: true),
            ],
        },
        new LanguageCommentSyntax
        {
            Language = "kotlin",
            DisplayName = "Kotlin",
            LineComments = [Line("//")],
            BlockComments = [DocBlock("/**", "*/", nested: true, forbidden: '/'), Block("/*", "*/", nested: true)],
            Strings =
            [
                QuoteRun('"', escape: null),
                Delim("\"", interpolation: "${"),
                Delim("'", charLiteral: true),
            ],
            Aliases = ["kts"],
        },
        new LanguageCommentSyntax
        {
            Language = "scala",
            DisplayName = "Scala",
            LineComments = [Line("//")],
            BlockComments = [DocBlock("/**", "*/", nested: true, forbidden: '/'), Block("/*", "*/", nested: true)],
            Strings =
            [
                QuoteRun('"', escape: null),
                Delim("\"", interpolation: "${"),
                Delim("'", charLiteral: true),
            ],
        },
        new LanguageCommentSyntax
        {
            Language = "groovy",
            DisplayName = "Groovy",
            LineComments = [Line("//")],
            BlockComments = [DocBlock("/**", "*/", forbidden: '/'), Block("/*", "*/")],
            Strings =
            [
                QuoteRun('"', escape: "\\", interpolation: "${"),
                QuoteRun('\'', escape: "\\"),
                Delim("\"", escape: "\\", interpolation: "${"),
                Delim("'", escape: "\\"),
            ],
        },

        // -------------------------------------------------- JavaScript family ---
        new LanguageCommentSyntax
        {
            Language = "javascript",
            DisplayName = "JavaScript/TypeScript",
            LineComments = [Line("//")],
            BlockComments = [DocBlock("/**", "*/", forbidden: '/'), Block("/*", "*/")],
            Strings =
            [
                // Template literals may span lines and interpolate code; a comment inside ${...} is a comment.
                Delim("`", escape: "\\", newlines: true, interpolation: "${"),
                Delim("\""),
                Delim("'"),
            ],
            RegexLiterals = true,
            Aliases = ["typescript", "jsx", "tsx", "mjs", "cjs"],
        },

        // ------------------------------------------------------------ Systems ---
        new LanguageCommentSyntax
        {
            Language = "go",
            DisplayName = "Go",
            LineComments = [Line("//")],
            BlockComments = [Block("/*", "*/")],
            Strings =
            [
                Delim("`", escape: null, newlines: true),
                Delim("\""),
                Delim("'", charLiteral: true),
            ],
        },
        new LanguageCommentSyntax
        {
            Language = "rust",
            DisplayName = "Rust",
            LineComments = [DocLine("///", '/'), DocLine("//!"), Line("//")],
            BlockComments =
            [
                // Rust block comments nest, so /* /* */ */ is one comment and must be removed as one.
                DocBlock("/**", "*/", nested: true, forbidden: '/'),
                DocBlock("/*!", "*/", nested: true),
                Block("/*", "*/", nested: true),
            ],
            Strings =
            [
                RustRaw(),
                Delim("\"", prefixes: ["b", "c"]),
                // Lifetimes ('a) look like an opening character literal; a bounded scan refuses them.
                Delim("'", charLiteral: true),
            ],
        },
        new LanguageCommentSyntax
        {
            Language = "c",
            DisplayName = "C",
            LineComments = [Line("//")],
            BlockComments = [Block("/*", "*/")],
            Strings =
            [
                Delim("\"", prefixes: ["u8", "u", "U", "L"]),
                Delim("'", prefixes: ["u8", "u", "U", "L"], charLiteral: true),
            ],
        },
        new LanguageCommentSyntax
        {
            Language = "cpp",
            DisplayName = "C++",
            LineComments = [Line("//")],
            BlockComments = [Block("/*", "*/")],
            Strings =
            [
                Delim("\"", prefixes: ["u8", "u", "U", "L"]),
                Delim("'", prefixes: ["u8", "u", "U", "L"], charLiteral: true),
            ],
        },
        new LanguageCommentSyntax
        {
            Language = "swift",
            DisplayName = "Swift",
            LineComments = [DocLine("///", '/'), Line("//")],
            BlockComments = [DocBlock("/**", "*/", nested: true, forbidden: '/'), Block("/*", "*/", nested: true)],
            Strings =
            [
                QuoteRun('"', escape: "\\"),
                // \( ) interpolation is checked before the escape, which is why the opener includes the slash.
                Delim("\"", interpolation: "\\(", holeOpen: '(', holeClose: ')'),
            ],
        },
        new LanguageCommentSyntax
        {
            Language = "dart",
            DisplayName = "Dart",
            LineComments = [DocLine("///", '/'), Line("//")],
            BlockComments = [DocBlock("/**", "*/", nested: true, forbidden: '/'), Block("/*", "*/", nested: true)],
            Strings =
            [
                QuoteRun('"', prefixes: ["r"], prefixDisablesEscapes: true),
                QuoteRun('\'', prefixes: ["r"], prefixDisablesEscapes: true),
                Delim("\"", interpolation: "${", prefixes: ["r"], prefixDisablesEscapes: true),
                Delim("'", interpolation: "${", prefixes: ["r"], prefixDisablesEscapes: true),
            ],
        },
        new LanguageCommentSyntax
        {
            Language = "fsharp",
            DisplayName = "F#",
            LineComments = [DocLine("///", '/'), Line("//")],
            BlockComments = [DocBlock("(**", "*)", nested: true, forbidden: '*'), Block("(*", "*)", nested: true)],
            Strings =
            [
                QuoteRun('"', escape: "\\"),
                Delim("@\"", close: "\"", doubled: true, newlines: true),
                Delim("\""),
                Delim("'", charLiteral: true),
            ],
        },
        new LanguageCommentSyntax
        {
            Language = "vb",
            DisplayName = "Visual Basic",
            LineComments = [Line("'")],
            Strings = [Delim("\"", escape: null, doubled: true)],
        },

        // ------------------------------------------------------- Markup / data ---
        new LanguageCommentSyntax
        {
            Language = "html",
            DisplayName = "HTML",
            Markup = true,
            BlockComments = [Block("<!--", "-->")],
            Aliases = ["htm"],
        },
        new LanguageCommentSyntax
        {
            Language = "xml",
            DisplayName = "XML",
            Markup = true,
            CdataSections = true,
            BlockComments = [Block("<!--", "-->")],
            Aliases = ["xaml", "axaml", "xsd", "xslt", "svg"],
        },
        new LanguageCommentSyntax
        {
            Language = "css",
            DisplayName = "CSS",
            BlockComments = [DocBlock("/*!", "*/"), Block("/*", "*/")],
            Strings = [Delim("\""), Delim("'")],
        },
        new LanguageCommentSyntax
        {
            Language = "scss",
            DisplayName = "SCSS",
            LineComments = [Line("//")],
            BlockComments = [DocBlock("/*!", "*/"), Block("/*", "*/")],
            Strings = [Delim("\""), Delim("'")],
        },
        new LanguageCommentSyntax
        {
            Language = "sass",
            DisplayName = "Sass",
            LineComments = [Line("//")],
            BlockComments = [DocBlock("/*!", "*/"), Block("/*", "*/")],
            Strings = [Delim("\""), Delim("'")],
        },
        new LanguageCommentSyntax
        {
            Language = "less",
            DisplayName = "Less",
            LineComments = [Line("//")],
            BlockComments = [DocBlock("/*!", "*/"), Block("/*", "*/")],
            Strings = [Delim("\""), Delim("'")],
        },
        new LanguageCommentSyntax
        {
            Language = "json",
            DisplayName = "JSON",
            LineComments = [Line("//")],
            BlockComments = [Block("/*", "*/")],
            Strings = [Delim("\"")],
            Aliases = ["jsonc", "json5"],
        },
        new LanguageCommentSyntax
        {
            Language = "sql",
            DisplayName = "SQL",
            LineComments = [Line("--")],
            // T-SQL and MariaDB nest block comments; the standard does not, but nesting is the safe reading.
            BlockComments = [Block("/*", "*/", nested: true)],
            Strings =
            [
                DollarQuoted(),
                Delim("'", escape: null, doubled: true, prefixes: ["N", "E", "X", "B", "U&"]),
                Delim("\"", escape: null, doubled: true),
                Delim("[", "]", escape: null),
            ],
        },
        // YAML is deliberately absent. A '#' inside a block scalar (| or >) is content, not a comment, and this
        // scanner does not model indentation-defined scalars, so claiming YAML support would corrupt such files.
        new LanguageCommentSyntax
        {
            Language = "toml",
            DisplayName = "TOML",
            LineComments = [Line("#")],
            Strings =
            [
                QuoteRun('"', escape: "\\"),
                QuoteRun('\'', escape: null),
                Delim("\""),
                Delim("'", escape: null),
            ],
        },
        new LanguageCommentSyntax
        {
            Language = "ini",
            DisplayName = "INI",
            // Inline comments are not universal in INI, so only a '#' or ';' at a word boundary counts.
            LineComments = [Line("#", boundary: true), Line(";", boundary: true)],
            Strings = [Delim("\""), Delim("'")],
        },
        new LanguageCommentSyntax
        {
            Language = "dockerfile",
            DisplayName = "Dockerfile",
            LineComments = [Line("#", boundary: true)],
            Strings = [Delim("\""), Delim("'", escape: null)],
        },
        new LanguageCommentSyntax
        {
            Language = "makefile",
            DisplayName = "Makefile",
            LineComments = [Line("#", boundary: true)],
            Strings = [Delim("\""), Delim("'", escape: null)],
        },
        new LanguageCommentSyntax
        {
            Language = "dotenv",
            DisplayName = "dotenv",
            // Most dotenv parsers only treat '# comment' as a comment after whitespace, so KEY=a#b stays a value.
            LineComments = [Line("#", boundary: true)],
            Strings = [Delim("\""), Delim("'", escape: null)],
        },
        new LanguageCommentSyntax
        {
            Language = "hcl",
            DisplayName = "HCL",
            LineComments = [Line("#"), Line("//")],
            BlockComments = [Block("/*", "*/")],
            Strings = [Heredoc("<<"), Delim("\"", interpolation: "${")],
            Aliases = ["tf", "tfvars"],
        },
        new LanguageCommentSyntax
        {
            Language = "graphql",
            DisplayName = "GraphQL",
            LineComments = [Line("#")],
            Strings = [QuoteRun('"', escape: "\\"), Delim("\"")],
        },
        new LanguageCommentSyntax
        {
            Language = "protobuf",
            DisplayName = "Protocol Buffers",
            LineComments = [Line("//")],
            BlockComments = [Block("/*", "*/")],
            Strings = [Delim("\""), Delim("'")],
            Aliases = ["proto"],
        },

        // ----------------------------------------------------------- Scripting ---
        new LanguageCommentSyntax
        {
            Language = "python",
            DisplayName = "Python",
            LineComments = [Line("#")],
            // Docstrings are string expressions, not comments, so they are never removed by comment stripping.
            Strings =
            [
                QuoteRun('"', prefixes: FStringPrefixes, prefixDisablesEscapes: false, interpolation: "{"),
                QuoteRun('\'', prefixes: FStringPrefixes, prefixDisablesEscapes: false, interpolation: "{"),
                QuoteRun('"', prefixes: RawStringPrefixes, prefixDisablesEscapes: true),
                QuoteRun('\'', prefixes: RawStringPrefixes, prefixDisablesEscapes: true),
                Delim("\"", prefixes: FStringPrefixes, interpolation: "{"),
                Delim("'", prefixes: FStringPrefixes, interpolation: "{"),
                Delim("\"", prefixes: RawStringPrefixes, prefixDisablesEscapes: true),
                Delim("'", prefixes: RawStringPrefixes, prefixDisablesEscapes: true),
                Delim("\""),
                Delim("'"),
            ],
            Aliases = ["py", "pyi"],
        },
        new LanguageCommentSyntax
        {
            Language = "bash",
            DisplayName = "Shell",
            // 'echo a#b' prints a#b; only a '#' at a word boundary starts a comment.
            LineComments = [Line("#", boundary: true)],
            Strings =
            [
                // A heredoc body is shell data, so a '#' inside it is not a comment.
                Heredoc("<<"),
                Delim("$'", close: "'", escape: "\\"),
                Delim("$\"", close: "\"", escape: "\\"),
                Delim("'", escape: null),
                Delim("\"", escape: "\\"),
            ],
            Aliases = ["sh", "zsh", "ksh"],
        },
        new LanguageCommentSyntax
        {
            Language = "powershell",
            DisplayName = "PowerShell",
            LineComments = [Line("#")],
            BlockComments = [Block("<#", "#>")],
            Strings =
            [
                HereString("@\"", "\"@"),
                HereString("@'", "'@"),
                // PowerShell escapes with a backtick, not a backslash.
                Delim("\"", escape: "`", doubled: true),
                Delim("'", escape: null, doubled: true),
            ],
            Aliases = ["ps1", "pwsh"],
        },
        new LanguageCommentSyntax
        {
            Language = "ruby",
            DisplayName = "Ruby",
            LineComments = [Line("#")],
            BlockComments = [Block("=begin", "=end", openRequiresLineStart: true)],
            Strings = [Delim("\"", escape: "\\", interpolation: "#{"), Delim("'", escape: "\\"), Delim("`", escape: "\\")],
            Aliases = ["rb"],
        },
        new LanguageCommentSyntax
        {
            Language = "php",
            DisplayName = "PHP",
            LineComments = [Line("//"), Line("#")],
            BlockComments = [DocBlock("/**", "*/", forbidden: '/'), Block("/*", "*/")],
            Strings =
            [
                // Heredoc and nowdoc bodies hold arbitrary text, including comment markers.
                Heredoc("<<<", quotedTag: true, indentedTerminator: true),
                Delim("\"", escape: "\\"),
                Delim("'", escape: "\\"),
            ],
        },
    ];

    private static readonly IReadOnlyList<string> FStringPrefixes = ["fr", "rf", "f", "F"];
    private static readonly IReadOnlyList<string> RawStringPrefixes = ["rb", "br", "r", "R", "b", "B", "u", "U"];

    private static LineCommentSyntax Line(string prefix, bool boundary = false) => new(prefix, CommentKind.Inline, boundary);

    private static LineCommentSyntax DocLine(string prefix, char? forbidden = null) =>
        new(prefix, CommentKind.Documentation, ForbiddenFollowingChar: forbidden);

    private static BlockCommentSyntax Block(string open, string close, bool nested = false, bool openRequiresLineStart = false) =>
        new(open, close, CommentKind.Block, nested, openRequiresLineStart);

    private static BlockCommentSyntax DocBlock(string open, string close, bool nested = false, char? forbidden = null) =>
        new(open, close, CommentKind.Documentation, nested, ForbiddenFollowingChar: forbidden);

    private static StringLiteralSyntax Delim(
        string open,
        string? close = null,
        string? escape = "\\",
        bool doubled = false,
        bool newlines = false,
        string? interpolation = null,
        char holeOpen = '{',
        char holeClose = '}',
        IReadOnlyList<string>? prefixes = null,
        bool prefixDisablesEscapes = false,
        bool charLiteral = false) => new()
        {
            Kind = StringLiteralKind.Delimited,
            Open = open,
            Close = close,
            Escape = escape,
            DoubledDelimiterEscape = doubled,
            AllowsNewlines = newlines,
            InterpolationOpen = interpolation,
            HoleOpenChar = holeOpen,
            HoleCloseChar = holeClose,
            Prefixes = prefixes ?? [],
            PrefixDisablesEscapes = prefixDisablesEscapes,
            RequiresTokenStart = prefixes is { Count: > 0 },
            CharLiteral = charLiteral,
        };

    private static StringLiteralSyntax QuoteRun(
        char quote,
        string? escape = "\\",
        bool maximal = false,
        IReadOnlyList<string>? prefixes = null,
        bool prefixDisablesEscapes = false,
        string? interpolation = null) => new()
        {
            Kind = StringLiteralKind.QuoteRun,
            Open = quote.ToString(),
            Escape = escape,
            AllowsNewlines = true,
            MinQuoteRun = 3,
            MaximalQuoteRun = maximal,
            Prefixes = prefixes ?? [],
            PrefixDisablesEscapes = prefixDisablesEscapes,
            RequiresTokenStart = prefixes is { Count: > 0 },
            InterpolationOpen = interpolation,
        };

    private static StringLiteralSyntax RustRaw() => new()
    {
        Kind = StringLiteralKind.RustRaw,
        Open = "\"",
        Escape = null,
        AllowsNewlines = true,
        Prefixes = ["br", "rb", "r"],
        RequiresTokenStart = true,
    };

    private static StringLiteralSyntax Heredoc(string marker, bool quotedTag = false, bool indentedTerminator = false) => new()
    {
        Kind = StringLiteralKind.Heredoc,
        Open = marker,
        Escape = null,
        AllowsNewlines = true,
        QuotedTag = quotedTag,
        IndentedTerminator = indentedTerminator,
    };

    private static StringLiteralSyntax HereString(string open, string close) => new()
    {
        Kind = StringLiteralKind.HereString,
        Open = open,
        Close = close,
        Escape = null,
        AllowsNewlines = true,
        CloseRequiresLineStart = true,
    };

    private static StringLiteralSyntax DollarQuoted() => new()
    {
        Kind = StringLiteralKind.DollarQuoted,
        Open = "$",
        Escape = null,
        AllowsNewlines = true,
    };
}
