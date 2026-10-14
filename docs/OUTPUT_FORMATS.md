# Output formats

Choose the format with `--format`; the default is `md`. If `--output` is omitted, Plutus derives a filename from the first input root and the selected format extension in the current working directory.

## Markdown (`md`)

Best for reading, sharing with an LLM, or attaching to an issue/review. The bundle includes:

1. Header metadata such as input root, output path, file count/size, and maximum file size.
2. A file list with sizes and token estimates when available.
3. A rendered directory tree with statuses for files that are skipped or omitted.
4. A section for each eligible source file, using a fenced code block and detected language label.

The code fence is chosen to avoid being closed by backticks inside the source file.

## JSON (`json`)

JSON output contains top-level `Metadata`, `Roots`, and `Files` values. Metadata describes aggregate information and optional token-budget fields. Tree nodes expose names, relative paths, whether they are directories, status, and child nodes. File records include path, size, language, skip status/reason, content when included, and an estimated token count when available.

Consumers should tolerate absent optional fields and treat `IsSkipped`/`SkipReason` as part of the file contract rather than assuming every file record has content.

## XML (`xml`)

XML output contains `Metadata`, `Roots`, and `Files` elements. Directory/tree nodes use `Directory` and `File` elements with attributes for names and status. File records carry path, size, and language attributes. Included content is stored in a `Content` element using CDATA; skipped records carry skipped/reason attributes instead.

CDATA has XML-specific constraints; downstream tools should parse the document with a standard XML parser rather than treating it as plain text.

## General notes

- The directory tree can contain nodes that do not have embedded source content. A visible node does not imply its contents were included.
- Oversized, binary, unreadable, or token-budget-omitted files should be interpreted from status/skip metadata.
- Output files can contain sensitive source code. Review them before sharing externally.
