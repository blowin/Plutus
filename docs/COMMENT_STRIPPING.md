# Comment stripping

`--strip-comments` removes comments from source files before they are bundled. This document states exactly what
the feature is, which languages and comment forms it covers, what it guarantees, and where it stops.

## What it is — and what it is not

| | |
|---|---|
| **It is** | A language-aware *lexical scanner*: a single-character-at-a-time state machine driven by a per-language table of comment and literal forms (`Plutus.Domain.Comments.LanguageCommentSyntaxTable`). |
| **It is not** | A parser, an AST pass, a grammar-driven lexer, or ANTLR token-channel processing. No grammar is generated, no token stream exists, and no syntax tree is built. |
| **It is also not** | A formatter. Stripping comments changes nothing else: whitespace, indentation, and line endings survive byte for byte. |

The project also ships a real token-channel path for grammars it maintains. That path is separate, is selected
explicitly, and — as of today — has no grammar to run on. See
[ANTLR grammar inventory](ANTLR_GRAMMAR_INVENTORY.md) and [Strategy selection](#strategy-selection-and-observability).

## Supported languages

A language is stripped only if it appears in this table. Anything else — YAML, Lua, Elixir, Markdown, Vue, Svelte,
and every unlisted language — is passed through untouched rather than scanned with another language's rules.

| Language | Identifiers | Line comments | Block comments | Documentation comments | Literals kept intact |
|---|---|---|---|---|---|
| C# | `csharp` | `//` | `/* */` | `///`, `/** */` | `"…"`, `'c'`, `@"…"`, `$"…"`, `$@"…"`, `@$"…"`, `"""…"""` |
| Java | `java` | `//` | `/* */` | `/** */` | `"…"`, `'c'`, `"""…"""` |
| Kotlin | `kotlin`, `kts` | `//` | `/* */` (nested) | `/** */` | `"…"`, `"""…"""`, `'c'`, `${…}` holes |
| Scala | `scala` | `//` | `/* */` (nested) | `/** */` | `"…"`, `"""…"""`, `'c'` |
| Groovy | `groovy` | `//` | `/* */` | `/** */` | `'…'`, `"…"`, `'''…'''`, `"""…"""` |
| JavaScript / TypeScript | `javascript`, `typescript`, `jsx`, `tsx`, `mjs`, `cjs` | `//` | `/* */` | `/** */` | `'…'`, `"…"`, `` `…` ``, `${…}` holes, `/regex/flags` |
| Go | `go` | `//` | `/* */` | — (plain `//` is Go's doc form) | `"…"`, `'c'`, `` `…` `` |
| Rust | `rust` | `//` | `/* */` (nested) | `///`, `//!`, `/** */`, `/*! */` | `"…"`, `'c'`, lifetimes `'a`, `r"…"`, `r#"…"#`, `b"…"` |
| Swift | `swift` | `//` | `/* */` (nested) | `///`, `/** */` | `"…"`, `"""…"""`, `\(…)` holes |
| Dart | `dart` | `//` | `/* */` (nested) | `///`, `/** */` | `'…'`, `"…"`, `'''…'''`, `"""…"""`, `r'…'` |
| C | `c` | `//` | `/* */` | — | `"…"`, `'c'`, `L"…"`, `u8"…"`, `u"…"`, `U"…"` |
| C++ | `cpp` | `//` | `/* */` | — | as C (raw strings `R"(…)"` are a known limitation) |
| F# | `fsharp` | `//` | `(* *)` (nested) | `///`, `(** *)` | `"…"`, `@"…"`, `"""…"""`, `'c'` |
| Visual Basic | `vb` | `'` | — | — | `"…"` with `""` escapes |
| HTML | `html`, `htm` | — | `<!-- -->` | — | attribute values |
| XML | `xml`, `xaml`, `axaml`, `xsd`, `xslt`, `svg` | — | `<!-- -->` | — | attribute values, `<![CDATA[…]]>` |
| CSS | `css` | — | `/* */` | `/*! */` | `'…'`, `"…"` |
| SCSS / Sass / Less | `scss`, `sass`, `less` | `//` | `/* */` | `/*! */` | `'…'`, `"…"` |
| JSON with comments | `json`, `jsonc`, `json5` | `//` | `/* */` | — | `"…"` |
| SQL | `sql` | `--` | `/* */` (nested) | — | `'…'` with `''`, `N'…'`, `E'…'`, `X'…'`, `B'…'`, `"…"`, `[…]`, `$tag$…$tag$` |
| TOML | `toml` | `#` | — | — | `"…"`, `'…'`, `"""…"""`, `'''…'''` |
| INI | `ini`, `cfg`, `conf` (see note) | `#`, `;` at a word boundary | — | — | `'…'`, `"…"` |
| Dockerfile | `dockerfile` | `#` at a word boundary | — | — | `'…'`, `"…"` |
| Makefile | `makefile` | `#` at a word boundary | — | — | `'…'`, `"…"` |
| dotenv | `dotenv` | `#` at a word boundary | — | — | `'…'`, `"…"` |
| HCL / Terraform | `hcl`, `tf`, `tfvars` | `#`, `//` | `/* */` | — | `"…"`, `<<EOF … EOF` heredocs |
| GraphQL | `graphql`, `gql` (see note) | `#` | — | — | `"…"`, `"""…"""` |
| Protocol Buffers | `protobuf`, `proto` | `//` | `/* */` | — | `'…'`, `"…"` |
| Python | `python`, `py`, `pyi` | `#` | — | — | `'…'`, `"…"`, `'''…'''`, `"""…"""`, `r`/`b`/`u`/`f` prefixes, `f"{…}"` holes |
| Shell | `bash`, `sh`, `zsh`, `ksh` | `#` at a word boundary | — | — | `'…'`, `"…"`, `$'…'`, `$"…"`, `<<EOF … EOF` heredocs |
| PowerShell | `powershell`, `ps1`, `pwsh` | `#` | `<# #>` | — | `'…'`, `"…"` (backtick escape), `@" … "@`, `@' … '@` |
| Ruby | `ruby`, `rb` | `#` | `=begin` … `=end` | — | `'…'`, `"…"` with `#{…}`, `` `…` `` |
| PHP | `php` | `//`, `#` | `/* */` | `/** */` | `'…'`, `"…"`, `<<<EOT … EOT` heredoc/nowdoc |

Notes:

- The *identifiers* column is what `IMarkdownLanguageProvider` reports for a file extension. `.ini`/`.cfg`/`.conf`
  map to `ini`, and `.gql` maps to `graphql` in the language provider; the registry accepts the documented
  identifiers and their aliases.
- `#` **at a word boundary** means the prefix only starts a comment after whitespace or a separator, so
  `https://host/path#fragment`, `KEY=a#b`, and `echo a#b` stay values while `value # comment` is a comment.
- `json`/`jsonc` are scanned as JSON-with-comments: valid JSON contains no comment sequences, so the same rules
  remove comments from a JSONC file and leave strict JSON untouched.
- Python docstrings are *string expressions*, not comments, so comment stripping never removes them.

## Guarantees

Both guarantees below are enforced by tests (`Plutus.Test/Comments`), including over real-world fixtures.

**Comment-like text inside literals is preserved.** Every literal form in the table is copied verbatim, so
`"https://host//path"`, `@"C:\temp//raw"`, `` `// data` ``, `r#"// data"#`, `$body$ -- data $body$`,
`<<<EOT … EOT`, `value="<!-- kept -->"`, and `/<[/*]>/` all survive.

**Removing a comment cannot merge two tokens.** When a removed comment sits between two non-whitespace
characters, exactly one space is inserted:

```text
a/* comment */b     →  a b
a/*1*//*2*/b        →  a b
var x=1/*c*/+2;     →  var x=1 +2;
```

**Line endings and line count do not move.** A removed line comment keeps the terminator that ended its line. A
removed block comment re-emits every line break it contained:

```text
one();\n/* two\nthree\nfour */\nfive();\n   →   one();\n\n\n\nfive();\n
```

The result still has the same number of lines as the input, which keeps a bundle's line references and diffs
aligned with the original file. Both LF and CRLF inputs keep their own terminators; stripping never converts them.

**Malformed input degrades visibly, not silently.** An unterminated block comment is treated as a comment to end
of file (as a compiler does) and counted; a stray quote ends at the line break instead of swallowing the rest of
the file; both counts appear in the run report.

## Documentation comments

**The default is explicit: documentation comments are removed like any other comment.** The default does not
depend on the language, the file path, or the presence of a toolchain.

`--preserve-doc-comments` keeps them verbatim. Which forms count as documentation is declared per language in the
table above: `///`, `//!`, `/** */`, `/*! */`. `////` is an ordinary comment, and `/**/` is an empty one — neither
is treated as documentation.

```bash
plutus ./src --strip-comments --preserve-doc-comments   # keep API docs, drop the rest
```

For languages without a distinct documentation form (Go, Python, SQL, C, C++, Ruby, HTML, XML, CSS, the config
languages) the flag has no effect, because there is nothing to distinguish; a plain `//` or `#` comment that
documents a symbol is still removed.

## Interaction with `--minify`

| Flags | Comment handling | Whitespace handling |
|---|---|---|
| *(neither)* | unchanged | unchanged |
| `--strip-comments` | comments removed | untouched — every blank line, indent and trailing space stays |
| `--minify` | comments removed | whitespace-only lines dropped, result trimmed, plus language boilerplate (C# `#region`) |
| `--strip-comments --minify` | comments removed | as `--minify` — `--minify` is a superset, so the extra flag is redundant, not conflicting |
| `--preserve-doc-comments` alone | unchanged (warning on stderr) | unchanged |
| `--comment-strategy <…>` alone | unchanged (warning on stderr) | unchanged |

`--minify` uses the same scanner as `--strip-comments`; the only difference is the whitespace pass that follows.
Neither flag rewrites literal contents.

## Unknown languages

`MarkdownLanguageProvider` labels a file by its extension, and the scanner only acts on labels it knows. A file
whose language has no entry in the table — or no extension at all — is bundled unchanged. The run report lists the
languages that were processed, so an unexpectedly absent language is visible rather than assumed to be handled.

## Strategy selection and observability

```bash
plutus ./src --strip-comments                                  # default: --comment-strategy auto
plutus ./src --strip-comments --comment-strategy lexical        # force the character scanner
plutus ./src --strip-comments --comment-strategy antlr          # ANTLR token channels, or a hard error
```

`--comment-strategy` accepts `auto` (default), `lexical`, and `antlr`.

- `auto` — ANTLR token channels for languages a maintained grammar covers, lexical scanning for everything else.
  With no maintained grammar (today) this is lexical scanning, and the run says so.
- `lexical` — always the character scanner; the grammar inventory is not consulted, so a broken manifest cannot
  break the run.
- `antlr` — requires at least one project-maintained grammar. Without one the run fails with the probed manifest
  path instead of quietly falling back to lexical scanning. A manifest that exists but cannot be trusted (invalid
  JSON, unknown channel name, missing `.g4` file) fails the run for `auto` and `antlr` as well.

When comment processing runs, the run report states what actually happened:

```text
Source processing
  comments    strategy=lexical (requested: auto) — lexical scanning; no project-maintained grammar
              files=12, removed=37 (inline 20, block 12, documentation 5), documentation preserved=3, languages: csharp=9, javascript=3
```

A strategy that declines to transform a file (for example a grammar-backed lexer whose tokens do not tile the
input) is reported on stderr with its reason and counted as a fallback; the file is returned unchanged.

## Lexical scanning versus ANTLR token channels

| | Lexical scanning (implemented) | ANTLR token channels (not available: no maintained grammar) |
|---|---|---|
| Mechanism | Character state machine driven by a per-language syntax table | Generated lexer; tokens on the grammar's comment channels are dropped |
| Knows | Comment and literal forms, as declared in the table | Whatever the grammar declares: token types, channels, modes |
| Cost of a wrong guess | Comment-like text inside an unmodelled literal, or an unterminated literal, is reported through the counters | A stale grammar fails the tiling check and the file is left unchanged |
| Verification | Fixtures plus their language's own parser where one is installed (node, python, gcc) | Would be the grammar's own test suite |
| Current status | Shipped | The seam exists (`ICommentChannelLexer`, `AntlrCommentChannelStripper`), the inventory is empty |

Calling the shipped feature "AST-driven" or "ANTLR-channel-driven" would be wrong, and the code and CLI text avoid
those words. See [ANTLR grammar inventory](ANTLR_GRAMMAR_INVENTORY.md) for what would have to exist first.

## Known limitations

These are deliberate, stated so a bundle is not trusted beyond what was checked:

- **Comment directives are comments.** `//go:build`, `// nolint`, `# noqa`, `/*#__PURE__*/`, `// @ts-ignore`, and
  similar markers are removed with the surrounding comment. Use `--preserve-doc-comments` where the directive uses
  a documentation form, or exclude such files.
- **YAML is not stripped.** A `#` inside a block scalar (`|`, `>`) is content, and this scanner does not model
  indentation-defined scalars, so claiming YAML support would silently corrupt such files.
- **Lua, Elixir, Erlang, Haskell, R, MATLAB, Markdown, RST, batch, Vue, and Svelte are not stripped** (no entry in
  the table; files are bundled unchanged).
- **JSX/TSX text nodes are scanned as code.** A `//` inside JSX text would be treated as a comment; such text is
  usually inside braces or attributes, which are protected.
- **C++ raw strings (`R"delim(…)delim"`) and Groovy slashy strings (`/…/`) are not modelled**; their contents are
  scanned as code, so a comment marker inside one could be removed. Prefer `--exclude-files` for files that rely on
  them, or add the form to the table.
- **Dockerfile heredocs (`RUN <<EOF`)** are not modelled, so a `#` line inside one is treated as a comment.
- **Embedded languages are not followed.** An HTML `<script>` or `<style>` body is markup text, so a `//` inside it
  is left alone; strip such a file with the script's own language instead. Conversely, an HTML comment inside a PHP
  heredoc is protected, because the heredoc is one literal.
- **Comment removal is single-pass and non-semantic.** Nothing checks that the result still compiles, apart from
  the fixture tests that call an installed parser.

## Tests and verification

- `Plutus.Test/Comments/LexicalCommentStripperTests.cs` — token separation, line endings, literal protection per
  form, unknown languages, malformed input, documentation-comment defaults.
- `Plutus.Test/Comments/LanguageCommentCoverageTests.cs` — every canonical language must prove that its comment is
  removed and its literal is untouched, and that stripping is idempotent.
- `Plutus.Test/Comments/CommentStrippingInteractionTests.cs`, `CommentStrippingPlanTests.cs` — the flag matrix,
  strategy resolution, and the run report.
- `Plutus.Test/Comments/GrammarInventoryTests.cs`, `AntlrCommentChannelStripperTests.cs` — the ANTLR seam, including
  numeric and named channels and the failure modes.
- `Plutus.Test/Comments/CommentStrippingFixtureTests.cs` — real-world fixtures in
  `Plutus.Test/Fixtures/Comments/` (JavaScript, C#, Python, Rust, SQL, HTML, SCSS, PHP, Go, C), checked for literal
  preservation, stable line count, and idempotence. Where `node`, `python`, or `gcc` is on `PATH`, the stripped
  fixture is handed to it (`node --check`, `python -m py_compile`, `gcc -fsyntax-only`) so the result is accepted by
  a real parser, not only by this scanner. A missing tool skips that extra check instead of reporting a pass.
