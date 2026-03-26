using System.Text.RegularExpressions;
using X2ModCompiler.Core.Core;

namespace X2ModCompiler.Core.Parser;

/// <summary>
/// Provides logic for tokenizing raw configuration lines into high-level <see cref="Directive"/> objects.
/// Handles section headers, key-value pairs, and unrecognized content while respecting multiline continuations.
/// </summary>
public static class DirectiveTokenizer
{
    // Section header: [ObjectName] or [ObjectName ClassName]
    private static readonly Regex SectionHeaderRegex =
        new(@"^\[([A-Za-z][A-Za-z0-9_]*(?:[ \t.]+[A-Za-z][A-Za-z0-9_]*)?)\]\s*(;.*)?$");

    // Property name: MyProp, MyProp[0], MyProp(1)
    private static readonly Regex PropertyNameRegex =
        new(@"^[A-Za-z][A-Za-z0-9_]*(?:\[(?:0|[1-9][0-9]*)\]|\((?:0|[1-9][0-9]*)\))?$");

    /// <summary>
    /// Tokenizes a collection of merged lines into high-level directives.
    /// </summary>
    /// <param name="text">The full source text of the configuration file.</param>
    /// <param name="mergedLines">A list of lines already processed for multiline continuations.</param>
    /// <returns>A list of <see cref="Directive"/> objects representing the logical structure of the file.</returns>
    public static List<Directive> Tokenize(string text, List<(string Text, LineSpan FirstLine, LineSpan LastLine, bool HasTrailingContinuation)> mergedLines)
    {
        var directives = new List<Directive>();

        foreach (var (lineText, firstSpan, lastSpan, hasTrailingContinuation) in mergedLines)
        {
            string trimmed = lineText.TrimStart();
            int leadingWhitespace = lineText.Length - trimmed.Length;
            int lineStart = firstSpan.Start + leadingWhitespace;
            int lineEnd = lastSpan.End; // Full span from start of first line to end of last line

            // Empty line
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            // Comment line (starts with ;)
            if (trimmed.StartsWith(';'))
                continue;

            // Slash-slash comment (error)
            if (trimmed.StartsWith("//"))
            {
                var span = new Span(lineStart, lineStart + trimmed.Length);
                directives.Add(Directive.UnknownDirective(new Unknown(span)));
                continue;
            }

            // Section header
            var headerMatch = SectionHeaderRegex.Match(trimmed);
            if (headerMatch.Success)
            {
                // Check for trailing space after ]
                int closeBracketPos = trimmed.IndexOf(']');
                if (closeBracketPos >= 0 && closeBracketPos + 1 < trimmed.Length)
                {
                    char nextChar = trimmed[closeBracketPos + 1];
                    if (nextChar != ';' && !char.IsWhiteSpace(nextChar) || 
                        (nextChar == ' ' || nextChar == '\t'))
                    {
                        // Check if it's just whitespace before comment or end
                        string afterBracket = trimmed.Substring(closeBracketPos + 1);
                        string trimmedAfterBracket = afterBracket.TrimStart();
                        if (!string.IsNullOrEmpty(afterBracket.Trim()) && !trimmedAfterBracket.StartsWith(";"))
                        {
                            // Malformed header
                            var span = new Span(lineStart, lineEnd);
                            directives.Add(Directive.Section(new SectionHeader(span, new Span(lineStart + 1, lineStart + trimmed.LastIndexOf(']')))));
                            continue;
                        }
                    }
                }

                var fullSpan = new Span(lineStart, lineEnd);
                var objectNameSpan = new Span(lineStart + 1, lineStart + closeBracketPos);
                directives.Add(Directive.Section(new SectionHeader(fullSpan, objectNameSpan)));
                continue;
            }

            // Check for malformed header (starts with [ but doesn't match pattern)
            if (trimmed.StartsWith("["))
            {
                var span = new Span(lineStart, lineStart + trimmed.Length);
                directives.Add(Directive.Section(new SectionHeader(span, new Span(lineStart + 1, lineStart + trimmed.Length - 1))));
                continue;
            }

            // KVP: [op]property=value
            int eqPos = trimmed.IndexOf('=');
            if (eqPos > 0)
            {
                string beforeEq = trimmed.Substring(0, eqPos);
                string afterEq = trimmed.Substring(eqPos + 1);

                // Parse operation prefix
                KvpOperation operation = KvpOperation.Set;
                int nameStart = 0;
                if (beforeEq.Length > 0)
                {
                    char firstChar = beforeEq[0];
                    switch (firstChar)
                    {
                        case '+':
                            operation = KvpOperation.InsertUnique;
                            nameStart = 1;
                            break;
                        case '.':
                            operation = KvpOperation.Insert;
                            nameStart = 1;
                            break;
                        case '-':
                            operation = KvpOperation.Remove;
                            nameStart = 1;
                            break;
                        case '!':
                            operation = KvpOperation.Clear;
                            nameStart = 1;
                            break;
                    }
                }

                string propertyName = beforeEq.Substring(nameStart).Trim();
                string value = afterEq;
                int commentPos = value.IndexOf(';');
                if (commentPos >= 0)
                {
                    // Make sure it's not inside a string
                    string beforeComment = value.Substring(0, commentPos);
                    int quoteCount = beforeComment.Count(c => c == '"');
                    if (quoteCount % 2 == 0)
                    {
                        value = beforeComment;
                    }
                }
                value = value.Trim();

                int propStart = lineStart + lineText.IndexOf(beforeEq) + nameStart;
                int propEnd = propStart + propertyName.Length;
                
                int valueStart = lineStart + eqPos + 1;
                for (int j = 0; j < afterEq.Length && char.IsWhiteSpace(afterEq[j]); j++)
                    valueStart++;

                var kvp = new Kvp(
                    new Span(lineStart, lineEnd),
                    new Span(propStart, propEnd),
                    new Span(valueStart, lineEnd),
                    value,
                    operation
                );
                directives.Add(Directive.KvpDirective(kvp));
                continue;
            }

            // Unknown directive
            var unknownSpan = new Span(lineStart, lineEnd);
            directives.Add(Directive.UnknownDirective(new Unknown(unknownSpan)));
        }

        return directives;
    }

    /// <summary>
    /// Maps a single prefix character to its corresponding <see cref="KvpOperation"/>.
    /// </summary>
    /// <param name="c">The character prefix from a property assignment.</param>
    /// <returns>The identified <see cref="KvpOperation"/>.</returns>
    public static KvpOperation ParseOperationPrefix(char c)
    {
        return c switch
        {
            '+' => KvpOperation.InsertUnique,
            '.' => KvpOperation.Insert,
            '-' => KvpOperation.Remove,
            '!' => KvpOperation.Clear,
            _ => KvpOperation.Set
        };
    }
}
