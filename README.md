<div align="center">

# Plutus

**Turn a local project or a remote Git repository into a structured, AI-ready code bundle.**

Plutus collects project files, applies configurable filters, and writes a single Markdown, JSON, or XML artifact for LLM-assisted development, code review, debugging, and offline analysis.

</div>

---

## Why Plutus?

- **One artifact, whole context** — combine a project's file inventory, directory tree, and readable source files into a portable bundle.
- **Local and remote inputs** — process local directories or repositories hosted on GitHub, GitLab, and Bitbucket.
- **Control what goes in** — use `.gitignore`, built-in exclusions, and include/exclude patterns to focus on the relevant code.
- **Keep bundles manageable** — set a maximum file size and an optional estimated source-token budget; select whole files deterministically when needed.
- **Choose a format** — emit Markdown for people and LLMs, or JSON/XML for downstream tools.
- **Optional source minification** — remove comments and extra whitespace for supported languages before bundling.
- **Comment stripping that respects literals** — a language-aware scanner removes comments without touching string, raw-string, template, regex, or markup-attribute contents, without merging adjacent tokens, and without moving line endings.
- **Signature-only bundles** — replace implementation bodies with a structural terminator using a real parse tree, so a bundle can carry a project's API surface instead of its code.
- **Testable architecture** — scanning and bundling are built around file-provider abstractions, with a separate CLI, domain, and infrastructure layer.

## Quick start

### Requirements

- **.NET 10 SDK** to build and run from source.
- A local project directory, or a supported remote repository URL.

### Build and run from source

```bash
git clone https://github.com/<OWNER>/<REPOSITORY>.git
cd <REPOSITORY>
dotnet restore Plutus.sln
dotnet build Plutus.sln -c Release
dotnet run --project Plutus.CLI -- ./path/to/project
```

Replace `<OWNER>/<REPOSITORY>` with the actual GitHub repository path. The command writes a Markdown bundle to the current working directory by default. For a more predictable output path, specify `-o`:

```bash
dotnet run --project Plutus.CLI -- ./path/to/project -o ./artifacts/project-context.md
```

### Run the published executable

When a GitHub Release has been published, download the asset matching your platform from the repository's **Releases** page. The release workflow is configured to produce self-contained single-file assets for Windows x64, Linux x64, and macOS x64. Then run the executable with the same CLI options shown below.

## Examples

### Bundle the current directory

```bash
plutus .
```

### Bundle selected source files and omit test directories

```bash
plutus ./src --include-files "*.cs" --exclude-dirs "*Test*"
```

### Write JSON instead of Markdown

```bash
plutus ./src --format json --output ./artifacts/context.json
```

### Bundle a specific branch or commit

```bash
plutus "https://github.com/owner/repository" --branch "feature/new-parser"
plutus "https://github.com/owner/repository" --commit "a1b2c3d4e5f6789012345678901234567890abcd"
```

`--branch` and `--commit` are mutually exclusive. Commit hashes must contain 7–40 hexadecimal characters. Remote URLs should point to a supported repository; for a non-default branch or tag, pass `--branch` explicitly.

### Bundle only what changed

```bash
plutus . --diff-only                                    # just the diff: no file contents
plutus . --diff-only --diff-include-untracked            # also generate diffs for new files
plutus . --diff-from main -o ./review.md                 # changed files WITH their contents
plutus . --diff-from v1.0.0 --diff-to v1.1.0             # a specific commit or release range
plutus . --diff-only --max-tokens 60000 --token-overflow-policy select
```

`--diff-only` produces a review artifact rather than a source dump: the header, the list of changed paths and one fenced diff per file — file contents and the directory tree are omitted. `--diff-from <ref>` instead keeps the changed files' contents and narrows them relative to the merge base of that ref, which is what you want when handing a branch to a model; add `--diff-to <ref>` to compare two specific commits. Because `git diff` only sees tracked content, add `--diff-include-untracked` to cover files that were never `git add`ed; a real new-file diff is generated for each.

`--max-file-size` and `--max-tokens` apply to each diff independently, so one oversized change is reported and left out without discarding the rest of the change set. See the [CLI reference](docs/CLI.md#bundle-only-what-changed).

### Remove comments without reformatting the code

```bash
plutus ./src --strip-comments                                # comments only; line endings and indentation stay
plutus ./src --strip-comments --preserve-doc-comments         # keep /// and /** */ documentation
plutus ./src --minify                                         # comments plus whitespace collapsing
```

Both flags share one language-aware lexical scanner: comment-like text inside strings, raw strings, template
literals, regex literals, and markup attributes is preserved, removing a comment never joins two tokens, and a
stripped file keeps its line count. Documentation comments are removed by default — the default is explicit, not
implied by the language. `--comment-strategy antlr` requests real ANTLR token-channel processing and fails rather
than falling back while no grammar is maintained. Supported languages, guarantees, and limitations are in the
[comment-stripping guide](docs/COMMENT_STRIPPING.md).

### Bundle only the API surface

```bash
plutus ./src --signatures-only                              # declarations stay, implementation bodies become ';'
plutus ./src --signatures-only --strip-comments              # and no comments either
plutus ./src --signatures-only --minify --include-files "*.cs"
```

`--signatures-only` parses each supported file with its own parser (C# today) and replaces method bodies, `=>`
expression bodies and property accessor bodies with a structural terminator, keeping attributes, documentation
comments, generics and `where` constraints. A body is identified from the parse tree, never by matching braces in
text, and each replacement is applied as a token-span rewrite with conflict detection, so the file is either
rewritten exactly where intended or left alone with a reason. Languages without a provider are passed through
unchanged and counted in the run report. The result is a skeleton for reading, not compilable code — see the
[signature-extraction guide](docs/SIGNATURE_EXTRACTION.md).

### Keep the estimated source-token count within a budget

```bash
plutus ./src --max-tokens 100000 --token-overflow-policy select
plutus ./src --max-tokens 100000 --token-overflow-policy Select --token-selection-strategy LargestFirst
```

The default overflow policy is `fail`: if eligible source content exceeds the budget, Plutus reports the estimate and does not write the bundle. `warn` writes all eligible files and reports the overflow. `select` includes only whole files that fit. See the [token-budget guide](docs/TOKEN_BUDGET.md) for details and limitations.

## CLI reference

The full option reference, defaults, and filtering examples live in **[docs/CLI.md](docs/CLI.md)**. Useful options include:

| Option | Purpose |
|---|---|
| `-o`, `--output <path>` | Set the output file path. |
| `--format <md\|json\|xml>` | Select output format; default is `md`. |
| `--max-file-size <size>` | Skip files larger than the configured size; default is `1M`. |
| `--max-tokens <count>` | Enable the offline source-token estimate and budget policy. |
| `--token-overflow-policy <fail\|warn\|select>` | Decide what happens when the budget is exceeded. |
| `--token-selection-strategy <path-order\|largest-first>` | Choose deterministic whole-file selection for `select`. |
| `--exclude`, `--exclude-files`, `--exclude-dirs` | Exclude matching files and/or directories. |
| `--include-files`, `--include-dirs` | Restrict the bundle to matching paths. |
| `--diff-only` | Emit the change itself: changed paths and one fenced diff per file, no file contents. |
| `--diff-from <ref>`, `--diff-to <ref>` | Bundle changed files **with** their contents, relative to a ref or between two commits. |
| `--diff-include-untracked` | Also cover files git does not track yet, via generated new-file diffs. |
| `--no-gitignore` | Do not read the root `.gitignore`. |
| `--allow-dangerous-files` | Allow normally excluded sensitive-looking files. Use with care. |
| `-m`, `--minify` | Apply supported source minification before bundling. |
| `--strip-comments` | Remove comments only; whitespace and line endings stay as they are. |
| `--preserve-doc-comments` | Keep documentation comments (`///`, `/** */`, …) while stripping. Off by default. |
| `--comment-strategy <auto\|lexical\|antlr>` | Choose how comments are located; `antlr` requires a project-maintained grammar. |
| `--signatures-only` | Replace implementation bodies with a structural terminator, keeping declarations and signatures. |

## Output formats

- **Markdown (`md`)** — human-readable project metadata, file list, directory tree, and file contents in fenced code blocks. This is the default and the best starting point for LLM context sharing.
- **JSON (`json`)** — structured metadata, roots/tree nodes, and file records, including skipped-file status and token-estimate fields where applicable.
- **XML (`xml`)** — structured metadata, tree nodes, and file content elements. Content is represented using CDATA sections.

For format details and guidance on consuming the output programmatically, see [docs/OUTPUT_FORMATS.md](docs/OUTPUT_FORMATS.md).

## How it works

1. **Resolve input** — local paths are expanded; supported remote repository URLs are downloaded and extracted.
2. **Resolve the diff scope** — with `--diff-only`/`--diff-from`, ask git for the changed paths (plus untracked files when requested) and limit every later step to them.
3. **Build the project tree** — scan entries through file-provider abstractions and create a tree used by both the renderer and bundle writers.
4. **Filter paths** — apply the diff scope, default junk exclusions, `.gitignore` (unless disabled), and user include/exclude patterns.
5. **Read and process files** — skip oversized files, detect obvious binary content, decode text using the selected encoding, and optionally strip comments, extract signatures, or minify supported source languages.
6. **Apply token policy** — if `--max-tokens` is configured, estimate eligible source content and apply `fail`, `warn`, or deterministic `select` behavior.
7. **Serialize the bundle** — write Markdown, JSON, or XML; with `--diff-only`, omit file contents and emit the change itself.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for component responsibilities and the processing pipeline.

## Security and privacy

A bundle may contain source code, configuration, internal URLs, or secrets if those files are included. Plutus has built-in exclusions for sensitive-looking files, but **filters are not a substitute for reviewing the bundle before sharing it**. Avoid `--allow-dangerous-files` unless you deliberately need those files, and inspect the generated artifact before uploading it to an LLM or publishing it.

See [SECURITY.md](SECURITY.md) for reporting vulnerabilities and [docs/CLI.md](docs/CLI.md#sensitive-files) for the sensitive-file option.

## Development

```bash
dotnet restore Plutus.sln
dotnet format Plutus.sln --verify-no-changes --no-restore
dotnet build Plutus.sln -c Debug
dotnet test Plutus.sln -c Debug
```

The repository's GitHub Actions workflow runs restore, formatting verification, build, and tests on pushes and pull requests targeting `master`. Contributions should include focused tests for behavior changes; see [CONTRIBUTING.md](CONTRIBUTING.md).

## Documentation

- [CLI reference](docs/CLI.md)
- [Comment stripping: languages, guarantees, limitations](docs/COMMENT_STRIPPING.md)
- [Signature extraction: the structural pass](docs/SIGNATURE_EXTRACTION.md)
- [ANTLR grammar inventory and channel mapping](docs/ANTLR_GRAMMAR_INVENTORY.md)
- [Token budget behavior and estimator limitations](docs/TOKEN_BUDGET.md)
- [Output formats](docs/OUTPUT_FORMATS.md)
- [Architecture and pipeline](docs/ARCHITECTURE.md)
- [Contributing](CONTRIBUTING.md)
- [Security policy](SECURITY.md)

## License

Plutus is distributed under the MIT License. See [LICENSE](LICENSE).
