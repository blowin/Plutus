# Plutus
Plutus is a high-performance, architecturally pure .NET 10 command-line interface (CLI) utility designed to scan local directories or remote repositories and consolidate source files into a single structured Markdown document.

Optimized for **Large Language Models (LLMs)** context window injection, project auditing, and offline code reviews, Plutus generates an integrated project capsule containing visual directory hierarchies, complete file lists, and accurately syntax-highlighted code fences.

---

## Technical Highlights & Architecture

1. **Plutus.CLI**: Handles command-line invocation arguments, input normalization, and overall workflow orchestration via `App.cs`.
2. **Plutus.Domain**: Contains the invariant business rules, the unified `ProjectScanner` graph engine, text encoding detectors, and formatting logic. It operates purely on Microsoft's `IFileProvider` and `IFileInfo` abstractions.
3. **Plutus.Infrastructure**: Implements concrete structural bridges. It wraps physical platform disk drivers via specialized file providers and translates public cloud repository URLs into automated download workflows.

---

## Core Features

* **Multi-Platform Remote Ingestion**: Pass raw repository links from **GitHub**, **GitLab**, or **Bitbucket**. The infrastructure automatically maps branches, sanitizes URL paths, follows web redirects, handles nested source segments, and extracts the code into sandboxed execution containers.
* **Unified Single-Pass Scanning**: Replaced redundant multi-run tree traversals with a single-pass hierarchical execution tree (`ProjectScanner`). It populates an immutable in-memory graph (`ProjectNode`) that feeds both the code compiler and the visual layout renderer simultaneously.
* **High-Fidelity Virtualization**: Completely isolated from the `System.IO` footprint. It executes interchangeably against physical hard drives or in-memory virtual arrays, facilitating fully non-destructive isolated unit testing.
* **Smart Text/Binary Demuxing**: Inspects the preliminary byte arrays of file streams dynamically. It handles complex character maps (UTF-8, Windows-1251) seamlessly while flagging and isolating corrupted nodes or binary objects (images, compiled artifacts) to protect LLM token budget capacities.

---

## Smart Filtering Engine (Git-Style)

Plutus contains a multi-tiered, highly flexible filtering system designed to either restrict or expand your target codebase context window using native Git wildcard syntax:

###  Exclusions (Black-lists)
* **Built-in Junk Exclusions**: Automatically cuts out transient build files, operational runtimes, package trees, and storage logs (`node_modules`, `bin`, `obj`, `.git`, `venv`, `.vs`).
* **Leak Protection**: Actively blocks localized credentials, token configuration stores, and encryption files (`.env`, `credentials.json`, `secrets.json`, `id_rsa`, `*.pem`, `*.key`).
* **Custom Targets**: Exclude generic patterns, strict files, or strict directory trees via CLI flags.

### Inverted Inclusions (White-lists)
* **Strict Targeting**: Force the compiler to completely drop everything *unless* it matches specific files or directory module pathways. Ideal for omitting heavy client-side assets when auditing backend source patterns.

---

## Usage

```bash
plutus <project-path-or-url> [options]
```

### Options

* `-o, --output <path>`: Explicit destination path for the compiled Markdown asset. Defaults to `<root-directory-name>_bundle.md` within the current folder.
* `--max-file-size <size>`: Sets the boundary capacity threshold for code file aggregation (e.g., `512K`, `1M`, `4MB`). Defaults to `1M`.
* `--no-gitignore`: Disables scanning and parsing local root-level `.gitignore` tracking files.
* `--exclude-files <mask|`| `List<string>` | Gitignore-style wildcard patterns targeting strictly **FILES** for exclusion. |
* `--exclude-dirs <mask|` | `List<string>` | Gitignore-style wildcard patterns targeting strictly **DIRECTORIES** for exclusion. |
* `--include-files <mask|`| `List<string>` | Inverted filter: explicitly isolates processing strictly to matching **FILES**. |
* `--include-dirs <mask|` | `List<string>` | Inverted filter: explicitly restricts scanning paths strictly to matching **DIRECTORIES**. |
* `--no-default-excludes`: Disables built-in automated junk directory tracking filters.
* `--allow-dangerous-files`: Overrides the security matching protocols to force ingestion of secure asset blocks (`.env`, private identities).
*  `--exclude <mask>`: **Exclude directories by name or wildcard mask (e.g., `frontend`, `build_*`, `*test*`). Matches the directory name at any nesting level.**
* `-m, --minify`: Minifies source code before bundling by completely removing all single-line, block, and documentation comments, stripping out region directives, and collapsing all blank or whitespace-only lines. This optimizes the layout into a tight, high-density structural format, saving 20% to 40% of the token budget when injecting code into Large Language Models (LLMs). It features safe multi-language support for C#, Java, Go, JavaScript, TypeScript, PHP, Rust, SQL, CSS/SCSS, and HTML/XML, ensuring embedded string literals, URLs, and Data-URIs remain fully preserved.

### Operational Examples

**Compile local working environments with standard configurations:**

```bash
plutus .
```

**Isolate processing strictly to C# code sheets while ignoring Test projects:**
```bash
plutus . --include-files "*.cs" --exclude-dirs "*Test*"
```

**Ingest a public remote GitHub repository and bundle only the documentation folder:**
```bash
plutus "https://github.com" --include-dirs "docs/"
```

**Ingest a nested GitLab group architecture down to a 250K threshold map:**
```bash
plutus "https://gitlab.com" --max-file-size 250K
```

---

## Extending the Engine: Custom Remote Cloud Providers

Due to the Strategy Pattern decoupling layout, adding new remote Git platforms requires zero modification to the core engine. Simply inherit from `BaseRemoteRepository` inside the Infrastructure project layer:

```csharp
public sealed class GiteaRemoteRepository : BaseRemoteRepository
{
    protected override string PlatformPrefix => "gitea";

    public override bool IsSupportedPath(string path) =>
        path.StartsWith("https://gitea.com", StringComparison.OrdinalIgnoreCase);

    protected override string BuildZipUrl(string path) =>
        path.TrimEnd('/') + "/archive/master.zip";
}
```

---

## License

## License

Copyright (c) 2026 blowin

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
