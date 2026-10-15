# ANTLR grammar inventory

This page is the project's answer to "do we have real grammar support for comment stripping?" It records which
grammars Plutus maintains, how their comment token types and channels are mapped, and what the token-channel
stripper requires. Recording it — including the empty case — is what keeps the lexical scanner from being described
as grammar-driven.

## Current inventory

| Language | Grammar file | Generated lexer | Comment token types | Documentation token types | Comment channels |
|---|---|---|---|---|---|
| *none* | — | — | — | — | — |

**Plutus maintains no ANTLR grammar today.** There is no `.g4` file, no generated lexer class, and no ANTLR
runtime dependency in `Plutus.Domain`, `Plutus.Infrastructure`, or `Plutus.CLI`. Consequently:

- `--comment-strategy auto` selects lexical scanning and says so in the run report;
- `--comment-strategy antlr` fails with the probed manifest path rather than falling back silently;
- the token-channel code path is reachable only in tests, through a stub lexer, and would be reachable in
  production only for a language listed in a manifest that ships with the application.

The inventory is discovered at runtime, not hard-coded:

1. `PLUTUS_GRAMMAR_MANIFEST` — an explicit manifest path, useful while developing a grammar. A path that does not
   exist is a hard error, so a typo cannot look like "we ship no grammars".
2. `<application directory>/grammars/grammar-manifest.json` — the manifest a release would ship.

## Manifest schema

```json
{
  "grammars": [
    {
      "language": "sql",
      "grammarFile": "SqlLexer.g4",
      "lexerClass": "Plutus.Domain.Comments.Antlr.SqlLexer",
      "commentTokenTypes": ["SIMPLE_COMMENT", "BRACKETED_COMMENT"],
      "documentationTokenTypes": ["DOC_COMMENT"],
      "commentChannels": [1, "HIDDEN", "COMMENTS"],
      "namedChannels": { "COMMENTS": 2 }
    }
  ]
}
```

| Field | Meaning |
|---|---|
| `language` | The identifier `IMarkdownLanguageProvider` reports, e.g. `sql`. Lookup is case-insensitive. |
| `grammarFile` | The maintained grammar, relative to the manifest. It must exist: a manifest that describes grammars the build does not ship is rejected. |
| `lexerClass` | The generated lexer the stripper would drive. Reported in messages; the code resolves lexers through `ICommentChannelLexer`. |
| `commentTokenTypes` | Token type names that carry ordinary comments. |
| `documentationTokenTypes` | Token type names that carry documentation comments, kept when documentation comments are preserved. |
| `commentChannels` | Channels whose tokens are dropped. Written numerically (`1`) or by name (`"HIDDEN"`, `"COMMENTS"`). |
| `namedChannels` | Grammar-declared channel names, mapped to their numbers, for `channels { COMMENTS }` style declarations. |

A manifest is rejected as a whole — with a reason that reaches the user — when it is not valid JSON, has no
`grammars` array, has an entry without `language`/`grammarFile`, references a `grammarFile` that is not next to the
manifest, declares neither `commentChannels` nor `commentTokenTypes`, or names a channel that resolves to nothing.
An unresolved channel name is never dropped from the list silently: that would leave comments in the bundle while
the run claimed to have removed them.

## Channel mapping contract

Comments can be declared in three complementary ways, and all three are honoured:

1. **By token type** — `commentTokenTypes` / `documentationTokenTypes` match the symbolic name the lexer reports.
2. **By numeric channel** — `commentChannels: [1]`.
3. **By named channel** — `commentChannels: ["HIDDEN"]`, resolved in this order:
   - `namedChannels` from the manifest (grammar-declared channels such as `COMMENTS`),
   - then ANTLR's built-ins: `DEFAULT_TOKEN_CHANNEL` = `0`, `DEFAULT` = `0`, `HIDDEN` = `1`.

A token is dropped when its type name appears in either token-type list, when it is flagged as documentation by the
lexer, or when its numeric channel is one of the resolved comment channels. Where the two disagree, the channel
wins for dropping and the token type decides the *kind* (documentation versus ordinary), which is what the report
counts.

Because a channel can hide more than comments, a grammar that declares a channel here is stating that everything on
that channel is "not for the parser" — exactly the semantics of ANTLR's hidden channel.

## What the token-channel stripper requires

`AntlrCommentChannelStripper` (`Plutus.Domain.Comments`) drives an `ICommentChannelLexer`:

- **Tokens must tile the input.** Concatenated token texts must reproduce the source exactly once, in order, with
  matching `StartIndex` values. A mismatch means a stale or wrong grammar, so the file is returned unchanged with
  the reason recorded; nothing is rewritten from an incomplete token stream.
- **Removal keeps the same guarantees as lexical scanning.** Line breaks inside a removed token are re-emitted and a
  separator is inserted when the removal would join two tokens, so a text-level diff of the result stays readable.
- **A lexer that throws is caught.** The file is returned unchanged and the failure is reported.
- **Documentation tokens are kept only on request.** `--preserve-doc-comments` applies to `documentationTokenTypes`
  and to tokens the lexer itself flags as documentation.

## Why this matters

The distinction between the two mechanisms is user-visible, not cosmetic:

| | Lexical scanner | ANTLR token channels |
|---|---|---|
| Configuration | `LanguageCommentSyntaxRegistry` (hand-written table) | `.g4` grammar + manifest, generated lexer |
| What decides a comment | A prefix or delimiter in the table | The grammar's token type and channel |
| Failure mode | Reported counts (`unterminated`) plus fixtures | Tiling check failure, file returned unchanged |
| Currently available | Yes, for the languages in [COMMENT_STRIPPING.md](COMMENT_STRIPPING.md) | No grammar is maintained |

Adding a grammar means: ship the `.g4` and its generated lexer, ship `grammars/grammar-manifest.json`, implement
`ICommentChannelLexer` over that lexer, register it, and add the language to the inventory table above. Until all of
that exists, the honest description of `--strip-comments` is "language-aware lexical scanner".

## Related: the structural pass does not use ANTLR either

`--signatures-only` is a real parse-tree transformation, and it is worth being precise about what parses the file:

| | Comment stripping | Signature extraction |
|---|---|---|
| Mechanism | Hand-written lexical scanner | Roslyn syntax tree (`CSharpSyntaxWalker`) |
| Rewriting | Text spans with separator and line-ending rules | Token spans through `TokenStreamRewriter` |
| Why not ANTLR | A comment lexer needs no grammar | The C# grammar would have to be vendored, generated (a JRE toolchain) and kept current with the language — while Roslyn is the C# parser, ships as a package, and is error-tolerant by design |
| Seam | `ICommentChannelLexer` + `GrammarInventory` | `ISignatureSkeletonProvider` + `ITokenStreamRewriter` |

Both seams accept a grammar-backed implementation: an ANTLR lexer satisfies `ICommentChannelLexer`, and an ANTLR
parse-tree visitor plus ANTLR's own `TokenStreamRewriter` satisfies `ISignatureSkeletonProvider` — so adding a
maintained grammar for a language would light up both paths without changing the pipeline. See
[Signature extraction](SIGNATURE_EXTRACTION.md).
