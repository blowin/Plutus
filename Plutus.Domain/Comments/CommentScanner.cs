namespace Plutus.Domain.Comments;

/// <summary>
/// The single-pass scanner behind <see cref="LexicalCommentStripper"/>. Every character is either copied through,
/// consumed as part of a literal, or consumed as a comment; nothing is rewritten, so whitespace, line endings and
/// literal contents reach the output byte for byte.
/// </summary>
internal sealed class CommentScanner(string source, LanguageCommentSyntax syntax, CommentStripOptions options)
{
    private static readonly HashSet<string> RegexPrefixKeywords = new(StringComparer.Ordinal)
    {
        "return", "typeof", "instanceof", "in", "of", "new", "delete", "void", "case", "do", "else", "yield",
        "await", "throw",
    };

    private readonly CommentRemovalBuffer _buffer = new();
    private readonly List<Frame> _frames = [new CodeFrame()];
    private int _position;
    private int _removedInline;
    private int _removedBlock;
    private int _removedDocumentation;
    private int _preservedDocumentation;
    private int _unterminated;

    public CommentStripResult Run()
    {
        if (syntax.Markup)
        {
            ScanMarkup();
        }
        else
        {
            ScanCode();

            // A literal still open when the source ends never saw its closing delimiter. Its text was copied
            // verbatim either way, but counting it makes the suspicious result visible.
            _unterminated += _frames.OfType<StringFrame>().Count();
        }

        return new CommentStripResult
        {
            Content = _buffer.ToString(),
            Strategy = CommentStripStrategy.Lexical,
            RemovedInlineComments = _removedInline,
            RemovedBlockComments = _removedBlock,
            RemovedDocumentationComments = _removedDocumentation,
            PreservedDocumentationComments = _preservedDocumentation,
            UnterminatedSequences = _unterminated,
        };
    }

    // ---- code mode -----------------------------------------------------------------------------------------

    private void ScanCode()
    {
        while (_position < source.Length)
        {
            if (_frames[^1] is StringFrame stringFrame)
            {
                ScanStringBody(stringFrame);
                continue;
            }

            ScanCodeStep((CodeFrame)_frames[^1]);
        }
    }

    private void ScanCodeStep(CodeFrame frame)
    {
        if (TryConsumeLineComment() || TryConsumeBlockComment() || TryConsumeString())
        {
            return;
        }

        if (syntax.RegexLiterals && TryConsumeRegexLiteral())
        {
            return;
        }

        var current = source[_position];
        if (frame.IsHole)
        {
            if (current == frame.OpenChar)
            {
                frame.Depth++;
            }
            else if (current == frame.CloseChar)
            {
                if (frame.Depth == 0)
                {
                    // The hole ends: the '}' belongs to the interpolation, and the string frame below resumes.
                    _frames.RemoveAt(_frames.Count - 1);
                    _buffer.Append(current);
                    _position++;
                    return;
                }

                frame.Depth--;
            }
        }

        _buffer.Append(current);
        _position++;
    }

    private bool TryConsumeLineComment()
    {
        foreach (var rule in syntax.LineComments)
        {
            if (!MatchesAt(source, _position, rule.Prefix))
            {
                continue;
            }

            if (rule.ForbiddenFollowingChar is { } forbidden && CharAt(_position + rule.Prefix.Length) == forbidden)
            {
                continue;
            }

            if (rule.RequiresTokenBoundary && !IsTokenBoundaryBefore(_position))
            {
                continue;
            }

            var end = _position + rule.Prefix.Length;
            while (end < source.Length && source[end] != '\n' && source[end] != '\r')
            {
                end++;
            }

            if (rule.Kind == CommentKind.Documentation && options.PreserveDocumentationComments)
            {
                _buffer.Append(source, _position, end - _position);
                _preservedDocumentation++;
            }
            else
            {
                Count(rule.Kind);

                // The terminator that ends the line is left in place, so no separator is needed and no line moves.
                _buffer.RemoveComment(source.AsSpan(_position, end - _position), CharAt(end));
            }

            _position = end;
            return true;
        }

        return false;
    }

    private bool TryConsumeBlockComment()
    {
        foreach (var rule in syntax.BlockComments)
        {
            if (!MatchesAt(source, _position, rule.Open))
            {
                continue;
            }

            if (rule.OpenRequiresLineStart && !IsAtLineStart(_position))
            {
                continue;
            }

            if (rule.ForbiddenFollowingChar is { } forbidden && CharAt(_position + rule.Open.Length) == forbidden)
            {
                continue;
            }

            var scan = _position + rule.Open.Length;
            var depth = 1;
            var end = -1;
            while (scan < source.Length)
            {
                if (rule.Nested && MatchesAt(source, scan, rule.Open))
                {
                    depth++;
                    scan += rule.Open.Length;
                    continue;
                }

                if (MatchesAt(source, scan, rule.Close))
                {
                    depth--;
                    scan += rule.Close.Length;
                    if (depth == 0)
                    {
                        end = scan;
                        break;
                    }

                    continue;
                }

                scan++;
            }

            if (end < 0)
            {
                // An unterminated comment is still a comment; the count is reported so a suspicious result shows.
                _unterminated++;
                end = source.Length;
            }

            if (rule.Kind == CommentKind.Documentation && options.PreserveDocumentationComments)
            {
                _buffer.Append(source, _position, end - _position);
                _preservedDocumentation++;
            }
            else
            {
                Count(rule.Kind);
                _buffer.RemoveComment(source.AsSpan(_position, end - _position), CharAt(end));
            }

            _position = end;
            return true;
        }

        return false;
    }

    private bool TryConsumeString()
    {
        foreach (var rule in syntax.Strings)
        {
            switch (rule.Kind)
            {
                case StringLiteralKind.Heredoc:
                    if (TryConsumeHeredoc(rule))
                    {
                        return true;
                    }

                    break;

                case StringLiteralKind.HereString:
                    if (TryConsumeHereString(rule))
                    {
                        return true;
                    }

                    break;

                case StringLiteralKind.DollarQuoted:
                    if (TryConsumeDollarQuoted(rule))
                    {
                        return true;
                    }

                    break;

                default:
                    if (TryOpenStringLiteral(rule))
                    {
                        return true;
                    }

                    break;
            }
        }

        return false;
    }

    /// <summary>Opens a delimited or quote-run literal and hands it to the string frame.</summary>
    private bool TryOpenStringLiteral(StringLiteralSyntax rule)
    {
        if (!TryMatchLiteralStart(rule, out var prefixLength, out var openerLength, out var closeText, out var escapesDisabled))
        {
            return false;
        }

        _buffer.Append(source, _position, prefixLength + openerLength);
        _position += prefixLength + openerLength;
        _frames.Add(new StringFrame(rule, escapesDisabled, closeText));
        return true;
    }

    private bool TryMatchLiteralStart(
        StringLiteralSyntax rule,
        out int prefixLength,
        out int openerLength,
        out string closeText,
        out bool escapesDisabled)
    {
        prefixLength = 0;
        openerLength = 0;
        closeText = rule.Close ?? rule.Open;
        escapesDisabled = false;

        if (rule.Kind == StringLiteralKind.RustRaw)
        {
            foreach (var prefix in rule.Prefixes)
            {
                if (!MatchesAt(source, _position, prefix) || IsIdentifierChar(CharAt(_position - 1)))
                {
                    continue;
                }

                var hashStart = _position + prefix.Length;
                var hashCount = 0;
                while (CharAt(hashStart + hashCount) == '#')
                {
                    hashCount++;
                }

                if (!MatchesAt(source, hashStart + hashCount, rule.Open))
                {
                    continue;
                }

                prefixLength = prefix.Length + hashCount;
                openerLength = rule.Open.Length;
                closeText = rule.Open + new string('#', hashCount);
                return true;
            }

            return false;
        }

        foreach (var prefix in EnumeratePrefixes(rule))
        {
            if (prefix.Length > 0)
            {
                if (!MatchesAt(source, _position, prefix))
                {
                    continue;
                }

                if (rule.RequiresTokenStart && IsIdentifierChar(CharAt(_position - 1)))
                {
                    continue;
                }
            }

            var delimiterStart = _position + prefix.Length;

            if (rule.Kind == StringLiteralKind.QuoteRun)
            {
                var quote = rule.Open[0];
                var runLength = 0;
                while (CharAt(delimiterStart + runLength) == quote)
                {
                    runLength++;
                }

                if (runLength < rule.MinQuoteRun)
                {
                    continue;
                }

                prefixLength = prefix.Length;
                openerLength = rule.MaximalQuoteRun ? runLength : rule.MinQuoteRun;
                closeText = new string(quote, openerLength);
                escapesDisabled = IsEscapesDisabled(rule, prefix);
                return true;
            }

            if (!MatchesAt(source, delimiterStart, rule.Open))
            {
                continue;
            }

            if (rule.CharLiteral && !IsWellFormedCharLiteral(source, delimiterStart, rule.Escape))
            {
                continue;
            }

            prefixLength = prefix.Length;
            openerLength = rule.Open.Length;
            closeText = rule.Close ?? rule.Open;
            escapesDisabled = IsEscapesDisabled(rule, prefix);
            return true;
        }

        return false;
    }

    private static bool IsEscapesDisabled(StringLiteralSyntax rule, string prefix) =>
        rule.PrefixDisablesEscapes && prefix.Contains('r', StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> EnumeratePrefixes(StringLiteralSyntax rule)
    {
        foreach (var prefix in rule.Prefixes.OrderByDescending(candidate => candidate.Length))
        {
            yield return prefix;
        }

        yield return string.Empty;
    }

    /// <summary>
    /// Accepts only a real single-character literal: a quote, one character or escape sequence, and a closing
    /// quote. Rust lifetimes (<c>&amp;'a str</c>) and stray apostrophes are therefore not treated as strings.
    /// </summary>
    private static bool IsWellFormedCharLiteral(string text, int start, string? escape)
    {
        var position = start + 1;
        if (position >= text.Length || text[position] is '\n' or '\r')
        {
            return false;
        }

        if (escape is { Length: > 0 } && MatchesAt(text, position, escape))
        {
            position += escape.Length;
            if (position >= text.Length)
            {
                return false;
            }

            if (text[position] == 'u' && position + 1 < text.Length && text[position + 1] == '{')
            {
                var close = text.IndexOf('}', position);
                if (close < 0 || close - start > 16)
                {
                    return false;
                }

                position = close + 1;
            }
            else
            {
                position++;
            }
        }
        else
        {
            position++;
        }

        return position < text.Length && text[position] == '\'';
    }

    private void ScanStringBody(StringFrame frame)
    {
        if (_position >= source.Length)
        {
            _frames.RemoveAt(_frames.Count - 1);
            _unterminated++;
            return;
        }

        var rule = frame.Rule;

        // Interpolation is checked before escapes because Swift's \( is both the escape character and the hole
        // opener. A doubled hole opener ({{ in C# and Python) is literal text, not a hole.
        if (rule.InterpolationOpen is { Length: > 0 } interpolation && MatchesAt(source, _position, interpolation))
        {
            var doubled = interpolation.Length == 1
                          && interpolation[0] == rule.HoleOpenChar
                          && MatchesAt(source, _position, new string(rule.HoleOpenChar, 2));
            if (!doubled)
            {
                _buffer.Append(source, _position, interpolation.Length);
                _position += interpolation.Length;
                _frames.Add(new CodeFrame
                {
                    IsHole = true,
                    OpenChar = rule.HoleOpenChar,
                    CloseChar = rule.HoleCloseChar,
                });
                return;
            }
        }

        if (!frame.EscapesDisabled && rule.Escape is { Length: > 0 } escape && MatchesAt(source, _position, escape))
        {
            var take = Math.Min(escape.Length + 1, source.Length - _position);
            _buffer.Append(source, _position, take);
            _position += take;
            return;
        }

        if (rule.DoubledDelimiterEscape
            && MatchesAt(source, _position, frame.CloseText)
            && MatchesAt(source, _position + frame.CloseText.Length, frame.CloseText))
        {
            _buffer.Append(source, _position, frame.CloseText.Length * 2);
            _position += frame.CloseText.Length * 2;
            return;
        }

        var current = source[_position];
        if (!rule.AllowsNewlines && current is '\n' or '\r')
        {
            // A stray quote must not swallow the rest of the file. Like a resynchronising compiler, the literal
            // ends at the line end and scanning resumes in code mode.
            _frames.RemoveAt(_frames.Count - 1);
            _unterminated++;
            return;
        }

        if (rule.Kind == StringLiteralKind.QuoteRun)
        {
            var quote = rule.Open[0];
            var runLength = 0;
            while (CharAt(_position + runLength) == quote)
            {
                runLength++;
            }

            if (runLength >= frame.CloseText.Length)
            {
                // The last quotes of the run are the closing delimiter; longer runs keep the extra quotes.
                _buffer.Append(source, _position, runLength);
                _position += runLength;
                _frames.RemoveAt(_frames.Count - 1);
                return;
            }
        }
        else if (MatchesAt(source, _position, frame.CloseText))
        {
            _buffer.Append(source, _position, frame.CloseText.Length);
            _position += frame.CloseText.Length;
            _frames.RemoveAt(_frames.Count - 1);
            return;
        }

        _buffer.Append(current);
        _position++;
    }

    /// <summary>
    /// Consumes a heredoc literal verbatim. The body is data — in shell and HCL a '#' inside it is not a
    /// comment, and in PHP a '//' inside it is not one either.
    /// </summary>
    private bool TryConsumeHeredoc(StringLiteralSyntax rule)
    {
        if (!MatchesAt(source, _position, rule.Open))
        {
            return false;
        }

        var scan = _position + rule.Open.Length;
        var indented = false;
        if (rule.Open == "<<" && CharAt(scan) == '-')
        {
            indented = true;
            scan++;
        }

        while (CharAt(scan) is ' ' or '\t')
        {
            scan++;
        }

        var quote = CharAt(scan) is '\'' or '"' ? CharAt(scan)!.Value : '\0';
        if (quote != '\0')
        {
            if (!rule.QuotedTag)
            {
                return false;
            }

            scan++;
        }

        var tagStart = scan;
        while (scan < source.Length && (char.IsLetterOrDigit(source[scan]) || source[scan] == '_'))
        {
            scan++;
        }

        // A tag has to be word-like and start with a letter or underscore, so arithmetic such as shell
        // $((1<<2)) is not mistaken for a heredoc whose body never ends.
        if (scan == tagStart || !(char.IsLetter(source[tagStart]) || source[tagStart] == '_'))
        {
            return false;
        }

        var tag = source[tagStart..scan];

        if (quote != '\0')
        {
            if (CharAt(scan) != quote)
            {
                return false;
            }

            scan++;
        }

        // Whatever follows the opener on its line belongs to the literal, so no comment detection happens there.
        while (scan < source.Length && source[scan] is not ('\n' or '\r'))
        {
            scan++;
        }

        var terminator = FindHeredocTerminator(tag, scan, indented || rule.IndentedTerminator);
        var end = terminator < 0 ? source.Length : terminator;
        if (terminator < 0)
        {
            _unterminated++;
        }

        _buffer.Append(source, _position, end - _position);
        _position = end;
        return true;
    }

    private int FindHeredocTerminator(string tag, int from, bool allowIndent)
    {
        var lineStart = from;
        while (lineStart < source.Length)
        {
            // Move to the first character after the current line's terminator.
            while (lineStart < source.Length && source[lineStart] is not ('\n' or '\r'))
            {
                lineStart++;
            }

            if (lineStart < source.Length)
            {
                lineStart += source[lineStart] == '\r' && CharAt(lineStart + 1) == '\n' ? 2 : 1;
            }

            var candidate = lineStart;
            if (allowIndent)
            {
                while (CharAt(candidate) is ' ' or '\t')
                {
                    candidate++;
                }
            }

            if (MatchesAt(source, candidate, tag) && !IsIdentifierChar(CharAt(candidate + tag.Length)))
            {
                return candidate + tag.Length;
            }
        }

        return -1;
    }

    private bool TryConsumeHereString(StringLiteralSyntax rule)
    {
        if (!MatchesAt(source, _position, rule.Open) || rule.Close is not { Length: > 0 } close)
        {
            return false;
        }

        // A PowerShell here-string opener must be the last thing on its line.
        var scan = _position + rule.Open.Length;
        while (CharAt(scan) is ' ' or '\t')
        {
            scan++;
        }

        if (scan < source.Length && source[scan] is not ('\n' or '\r'))
        {
            return false;
        }

        var searchFrom = scan;
        var end = -1;
        while (searchFrom <= source.Length - close.Length)
        {
            var found = source.IndexOf(close, searchFrom, StringComparison.Ordinal);
            if (found < 0)
            {
                break;
            }

            if (IsAtLineStart(found))
            {
                end = found + close.Length;
                break;
            }

            searchFrom = found + 1;
        }

        if (end < 0)
        {
            _unterminated++;
            end = source.Length;
        }

        _buffer.Append(source, _position, end - _position);
        _position = end;
        return true;
    }

    private bool TryConsumeDollarQuoted(StringLiteralSyntax rule)
    {
        if (!MatchesAt(source, _position, rule.Open))
        {
            return false;
        }

        var scan = _position + rule.Open.Length;
        var tagStart = scan;
        while (scan < source.Length && (char.IsLetterOrDigit(source[scan]) || source[scan] == '_'))
        {
            scan++;
        }

        if (CharAt(scan) != '$' || (scan > tagStart && char.IsDigit(source[tagStart])))
        {
            // '$1' is a parameter, not a dollar-quoted string.
            return false;
        }

        var tag = source[_position..(scan + 1)];
        var bodyStart = scan + 1;
        var body = bodyStart <= source.Length
            ? source.IndexOf(tag, bodyStart, StringComparison.Ordinal)
            : -1;

        var end = body < 0 ? source.Length : body + tag.Length;
        if (body < 0)
        {
            _unterminated++;
        }

        _buffer.Append(source, _position, end - _position);
        _position = end;
        return true;
    }

    /// <summary>
    /// Recognises a JavaScript regular-expression literal using the usual preceding-token heuristic, so
    /// <c>/[/*]/</c> is a literal rather than the start of a comment. Division is left alone.
    /// </summary>
    private bool TryConsumeRegexLiteral()
    {
        if (source[_position] != '/' || !RegexCanStartHere())
        {
            return false;
        }

        var scan = _position + 1;
        var inCharacterClass = false;
        var closed = false;

        while (scan < source.Length)
        {
            var current = source[scan];
            if (current is '\n' or '\r')
            {
                return false;
            }

            if (current == '\\')
            {
                scan += 2;
                continue;
            }

            if (inCharacterClass)
            {
                if (current == ']')
                {
                    inCharacterClass = false;
                }
            }
            else if (current == '[')
            {
                inCharacterClass = true;
            }
            else if (current == '/')
            {
                scan++;
                closed = true;
                break;
            }

            scan++;
        }

        if (!closed)
        {
            return false;
        }

        while (scan < source.Length && char.IsLetter(source[scan]))
        {
            scan++;
        }

        _buffer.Append(source, _position, scan - _position);
        _position = scan;
        return true;
    }

    private bool RegexCanStartHere()
    {
        var index = _position - 1;
        while (index >= 0 && char.IsWhiteSpace(source[index]))
        {
            index--;
        }

        if (index < 0)
        {
            return true;
        }

        var previous = source[index];
        if (previous is ')' or ']' or '}' or '"' or '\'' or '`' || char.IsDigit(previous))
        {
            return false;
        }

        if (!IsIdentifierChar(previous))
        {
            return true;
        }

        var start = index;
        while (start > 0 && IsIdentifierChar(source[start - 1]))
        {
            start--;
        }

        return RegexPrefixKeywords.Contains(source[start..(index + 1)]);
    }

    // ---- markup mode ---------------------------------------------------------------------------------------

    /// <summary>
    /// Markup is scanned as text plus tags. Comments are only recognised in text, while quoted attribute values
    /// inside a tag are copied verbatim, so <c>value="&lt;!-- not a comment --&gt;"</c> survives intact.
    /// </summary>
    private void ScanMarkup()
    {
        while (_position < source.Length)
        {
            if (TryConsumeBlockComment())
            {
                continue;
            }

            if (syntax.CdataSections && MatchesAt(source, _position, "<![CDATA["))
            {
                CopyThrough("]]>");
                continue;
            }

            if (source[_position] == '<' && LooksLikeTagStart(source, _position))
            {
                CopyTag();
                continue;
            }

            _buffer.Append(source[_position]);
            _position++;
        }
    }

    private static bool LooksLikeTagStart(string text, int index)
    {
        var next = index + 1 < text.Length ? text[index + 1] : '\0';
        return char.IsLetter(next) || next is '/' or '!' or '?';
    }

    private void CopyThrough(string terminator)
    {
        var found = source.IndexOf(terminator, _position, StringComparison.Ordinal);
        var end = found < 0 ? source.Length : found + terminator.Length;
        if (found < 0)
        {
            _unterminated++;
        }

        _buffer.Append(source, _position, end - _position);
        _position = end;
    }

    private void CopyTag()
    {
        while (_position < source.Length)
        {
            var current = source[_position];
            if (current is '"' or '\'')
            {
                _buffer.Append(current);
                _position++;
                while (_position < source.Length)
                {
                    var inner = source[_position];
                    _buffer.Append(inner);
                    _position++;
                    if (inner == current)
                    {
                        break;
                    }
                }

                continue;
            }

            _buffer.Append(current);
            _position++;
            if (current == '>')
            {
                return;
            }
        }
    }

    // ---- helpers -------------------------------------------------------------------------------------------

    private void Count(CommentKind kind)
    {
        switch (kind)
        {
            case CommentKind.Documentation:
                _removedDocumentation++;
                break;
            case CommentKind.Inline:
                _removedInline++;
                break;
            default:
                _removedBlock++;
                break;
        }
    }

    private char? CharAt(int index) => index >= 0 && index < source.Length ? source[index] : null;

    private static bool MatchesAt(string text, int index, string value)
    {
        if (index < 0 || value.Length == 0 || index + value.Length > text.Length)
        {
            return false;
        }

        return text.AsSpan(index, value.Length).SequenceEqual(value);
    }

    private static bool IsIdentifierChar(char? value) =>
        value is { } character && (char.IsLetterOrDigit(character) || character is '_' or '$');

    private bool IsAtLineStart(int index) =>
        index == 0 || source[index - 1] is '\n' or '\r';

    /// <summary>
    /// True when a boundary-sensitive prefix such as '#' may start a comment here. Shell, YAML and Makefiles
    /// treat <c>value#text</c> as one word and <c>value # text</c> as a value followed by a comment.
    /// </summary>
    private bool IsTokenBoundaryBefore(int index)
    {
        if (index == 0)
        {
            return true;
        }

        var previous = source[index - 1];
        return char.IsWhiteSpace(previous) || previous is ';' or '&' or '|' or '(' or ')' or '{' or '}' or ',' or ':' or '=';
    }

    private abstract class Frame;

    private sealed class CodeFrame : Frame
    {
        /// <summary>True while scanning an interpolation hole, which ends at its matching closing character.</summary>
        public bool IsHole { get; init; }

        public char OpenChar { get; init; } = '{';

        public char CloseChar { get; init; } = '}';

        public int Depth { get; set; }
    }

    private sealed class StringFrame(StringLiteralSyntax rule, bool escapesDisabled, string closeText) : Frame
    {
        public StringLiteralSyntax Rule { get; } = rule;

        public bool EscapesDisabled { get; } = escapesDisabled;

        public string CloseText { get; } = closeText;
    }
}
