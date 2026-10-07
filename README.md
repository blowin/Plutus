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

## Security Engineering

Plutus contains multi-tiered structural guardrails to prevent accidental exposure of cryptographic signatures, sensitive parameters, or infrastructure keys:

* **Junk Exclusions (`JunkDirectoryIgnoreMatcher`)**: Automatically intercepts and cuts out transient build files, operational runtimes, package trees, and storage logs (`node_modules`, `bin`, `obj`, `.git`, `venv`, `.vs`).
* **Leak Protection (`DangerousFileIgnoreMatcher`)**: Actively blocks localized credentials, token configuration stores, and encryption files (`.env`, `credentials.json`, `secrets.json`, `id_rsa`, `*.pem`, `*.key`).
* **Oversized Threshold Filtering**: Restricts injection paths based on size boundaries. Files exceeding the target threshold are cleanly mapped in the directory tree structure but omitted from the body text stream.

---

## Usage

```bash
plutus <project-path-or-url> [options]
```

### Options

* `-o, --output <path>`: Explicit destination path for the compiled Markdown asset. Defaults to `<root-directory-name>_bundle.md` within the current folder.
* `--max-file-size <size>`: Sets the boundary capacity threshold for code file aggregation (e.g., `512K`, `1M`, `4MB`). Defaults to `1M`.
* `--no-gitignore`: Disables scanning and parsing local root-level `.gitignore` tracking files.
* `--ignore <pattern>`: Injects user-defined custom gitignore-style exclusions via command-line arguments (can be repeated).
* `--no-default-excludes`: Disables built-in automated junk directory tracking filters.
* `--allow-dangerous-files`: Overrides the security matching protocols to force ingestion of secure asset blocks (`.env`, private identities).
*  `--exclude <mask>`: **Exclude directories by name or wildcard mask (e.g., `frontend`, `build_*`, `*test*`). Matches the directory name at any nesting level.**

### Operational Examples

**Compile local working environments with standard configurations:**
```bash
plutus .
```

**Ingest a public remote GitHub repository directly from the web:**
```bash
plutus "https://github.com"
```

**Ingest a nested GitLab group repository architecture:**
```bash
plutus "https://gitlab.com"
```

**Ingest and trim specific sub-browser tracks on Bitbucket:**
```bash
plutus "https://bitbucket.org" --max-file-size 250K
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
