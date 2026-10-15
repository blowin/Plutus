using System.Text;

namespace Plutus.Domain.Comments;

/// <summary>
/// Accumulates transformed source while comments are removed. Both strippers share it, so removing a comment is
/// defined once: newlines inside the removed span survive, and a single space is inserted when the removal would
/// otherwise join the characters on either side into one token.
/// </summary>
internal sealed class CommentRemovalBuffer
{
    private readonly StringBuilder _output = new();

    public int Length => _output.Length;

    public void Append(ReadOnlySpan<char> text) => _output.Append(text);

    public void Append(char value) => _output.Append(value);

    public void Append(string source, int start, int length) => _output.Append(source, start, length);

    /// <summary>
    /// Removes one comment. Line terminators inside it are re-emitted verbatim so the line count and the line
    /// endings of the file do not move; if nothing was re-emitted and the removal would touch non-whitespace on
    /// both sides, one space is inserted so two tokens cannot merge.
    /// </summary>
    /// <param name="comment">The comment text, excluding any delimiter that is also being removed.</param>
    /// <param name="nextChar">The character that follows the comment in the source, or <c>null</c> at end of file.</param>
    public void RemoveComment(ReadOnlySpan<char> comment, char? nextChar)
    {
        var reemittedLineBreak = false;
        for (var i = 0; i < comment.Length; i++)
        {
            var current = comment[i];
            if (current == '\n')
            {
                _output.Append('\n');
                reemittedLineBreak = true;
                continue;
            }

            if (current != '\r')
            {
                continue;
            }

            if (i + 1 < comment.Length && comment[i + 1] == '\n')
            {
                _output.Append("\r\n");
                i++;
            }
            else
            {
                _output.Append('\r');
            }

            reemittedLineBreak = true;
        }

        if (reemittedLineBreak || nextChar is not { } next || char.IsWhiteSpace(next) || _output.Length == 0)
        {
            return;
        }

        if (!char.IsWhiteSpace(_output[^1]))
        {
            _output.Append(' ');
        }
    }

    public override string ToString() => _output.ToString();
}
