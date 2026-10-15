namespace Plutus.Domain;

/// <summary>
/// A language identifier carried as a value instead of as a loose string. The value is normalized when it is
/// created, so ordinal equality is also case-insensitive equality and no comparer has to be threaded through every
/// dictionary and lookup.
/// <para>
/// This type is also the single place where the canonical spellings live: the comment syntax table, the minifiers,
/// the structural providers and the language provider all refer to these members instead of repeating literals.
/// </para>
/// </summary>
public readonly record struct LanguageId
{
    private readonly string? _value;

    private LanguageId(string value) => _value = value;

    /// <summary>The normalized identifier, or an empty string for <c>default</c>.</summary>
    public string Value => _value ?? string.Empty;

    public bool IsEmpty => string.IsNullOrEmpty(_value);

    public static LanguageId Parse(string? value) => TryParse(value, out var id)
        ? id
        : throw new ArgumentException("A language identifier cannot be empty.", nameof(value));

    public static bool TryParse(string? value, out LanguageId id)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            id = default;
            return false;
        }

        id = new LanguageId(value.Trim().ToLowerInvariant());
        return true;
    }

    /// <summary>Parses when possible and yields <c>default</c> otherwise, for call sites with a fallback path.</summary>
    public static LanguageId From(string? value) => TryParse(value, out var id) ? id : default;

    public override string ToString() => Value;

    // ---- canonical identifiers ----------------------------------------------------------------------------

    public static readonly LanguageId CSharp = Parse("csharp");
    public static readonly LanguageId Java = Parse("java");
    public static readonly LanguageId Kotlin = Parse("kotlin");
    public static readonly LanguageId Scala = Parse("scala");
    public static readonly LanguageId Groovy = Parse("groovy");
    public static readonly LanguageId JavaScript = Parse("javascript");
    public static readonly LanguageId TypeScript = Parse("typescript");
    public static readonly LanguageId Jsx = Parse("jsx");
    public static readonly LanguageId Tsx = Parse("tsx");
    public static readonly LanguageId Go = Parse("go");
    public static readonly LanguageId Rust = Parse("rust");
    public static readonly LanguageId Swift = Parse("swift");
    public static readonly LanguageId Dart = Parse("dart");
    public static readonly LanguageId C = Parse("c");
    public static readonly LanguageId Cpp = Parse("cpp");
    public static readonly LanguageId FSharp = Parse("fsharp");
    public static readonly LanguageId VisualBasic = Parse("vb");
    public static readonly LanguageId Html = Parse("html");
    public static readonly LanguageId Xml = Parse("xml");
    public static readonly LanguageId Css = Parse("css");
    public static readonly LanguageId Scss = Parse("scss");
    public static readonly LanguageId Sass = Parse("sass");
    public static readonly LanguageId Less = Parse("less");
    public static readonly LanguageId Json = Parse("json");
    public static readonly LanguageId Sql = Parse("sql");
    public static readonly LanguageId Toml = Parse("toml");
    public static readonly LanguageId Ini = Parse("ini");
    public static readonly LanguageId Dockerfile = Parse("dockerfile");
    public static readonly LanguageId Makefile = Parse("makefile");
    public static readonly LanguageId Dotenv = Parse("dotenv");
    public static readonly LanguageId Hcl = Parse("hcl");
    public static readonly LanguageId GraphQl = Parse("graphql");
    public static readonly LanguageId Protobuf = Parse("protobuf");
    public static readonly LanguageId Python = Parse("python");
    public static readonly LanguageId Shell = Parse("bash");
    public static readonly LanguageId PowerShell = Parse("powershell");
    public static readonly LanguageId Ruby = Parse("ruby");
    public static readonly LanguageId Php = Parse("php");
    public static readonly LanguageId Batch = Parse("batch");
    public static readonly LanguageId Markdown = Parse("markdown");
    public static readonly LanguageId PlainText = Parse("text");

    // ---- aliases referenced by the syntax table -----------------------------------------------------------

    public static readonly LanguageId Mjs = Parse("mjs");
    public static readonly LanguageId Cjs = Parse("cjs");
    public static readonly LanguageId Kts = Parse("kts");
    public static readonly LanguageId Htm = Parse("htm");
    public static readonly LanguageId Xaml = Parse("xaml");
    public static readonly LanguageId Axaml = Parse("axaml");
    public static readonly LanguageId Xsd = Parse("xsd");
    public static readonly LanguageId Xslt = Parse("xslt");
    public static readonly LanguageId Svg = Parse("svg");
    public static readonly LanguageId Jsonc = Parse("jsonc");
    public static readonly LanguageId Json5 = Parse("json5");
    public static readonly LanguageId Yml = Parse("yml");
    public static readonly LanguageId Cfg = Parse("cfg");
    public static readonly LanguageId Conf = Parse("conf");
    public static readonly LanguageId Tf = Parse("tf");
    public static readonly LanguageId Tfvars = Parse("tfvars");
    public static readonly LanguageId GraphQlShort = Parse("gql");
    public static readonly LanguageId Proto = Parse("proto");
    public static readonly LanguageId Py = Parse("py");
    public static readonly LanguageId Pyi = Parse("pyi");
    public static readonly LanguageId Sh = Parse("sh");
    public static readonly LanguageId Zsh = Parse("zsh");
    public static readonly LanguageId Ksh = Parse("ksh");
    public static readonly LanguageId Ps1 = Parse("ps1");
    public static readonly LanguageId Pwsh = Parse("pwsh");
    public static readonly LanguageId RubyShort = Parse("rb");
    public static readonly LanguageId VisualBasicFull = Parse("visualbasic");

    /// <summary>
    /// Every identifier this type defines. Tables elsewhere (the comment syntax registry, the minifier catalog) are
    /// verified against this list, so a language cannot be invented in a table without being declared here.
    /// </summary>
    public static readonly IReadOnlyList<LanguageId> All =
    [
        CSharp, Java, Kotlin, Kts, Scala, Groovy,
        JavaScript, TypeScript, Jsx, Tsx, Mjs, Cjs,
        Go, Rust, Swift, Dart, C, Cpp, FSharp, VisualBasic, VisualBasicFull,
        Html, Htm, Xml, Xaml, Axaml, Xsd, Xslt, Svg,
        Css, Scss, Sass, Less,
        Json, Jsonc, Json5, Sql, Toml, Ini, Cfg, Conf, Yml, Dockerfile, Makefile, Dotenv,
        Hcl, Tf, Tfvars, GraphQl, GraphQlShort, Protobuf, Proto,
        Python, Py, Pyi, Shell, Sh, Zsh, Ksh, PowerShell, Ps1, Pwsh, Ruby, RubyShort, Php,
        Batch, Markdown, PlainText,
    ];
}
