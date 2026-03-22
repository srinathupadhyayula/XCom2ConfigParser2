using XCom2ConfigParser2.Core;

namespace XCom2ConfigParser2.Parser;

/// <summary>
/// Splits text into lines, handling line endings and continuation characters.
/// </summary>
public static class LineSplitter
{
    /// <summary>
    /// Splits the text into lines with continuation handling.
    /// Lines ending with \\ are merged with the next line.
    /// </summary>
    public static List<LineSpan> Split(string text)
    {
        var lines = new List<LineSpan>();
        int lineStart = 0;
        int lineNumber = 1;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            
            // Check for line ending
            if (c == '\n')
            {
                // Check for continuation before line ending
                bool hasContinuation = HasContinuationBefore(text, lineStart, i);
                int lineEnd = hasContinuation ? i - 2 : i; // End before \\ if continuation
                
                lines.Add(new LineSpan(lineStart, lineEnd, false, hasContinuation, lineNumber));
                
                lineStart = i + 1;
                lineNumber++;
            }
            else if (c == '\r')
            {
                // Check for continuation before line ending
                bool hasContinuation = HasContinuationBefore(text, lineStart, i);
                int lineEnd = hasContinuation ? i - 2 : i; // End before \\ if continuation
                
                lines.Add(new LineSpan(lineStart, lineEnd, false, hasContinuation, lineNumber));
                
                // Handle \r\n
                if (i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                
                lineStart = i + 1;
                lineNumber++;
            }
        }

        // Handle last line (no line ending)
        if (lineStart < text.Length)
        {
            bool hasContinuation = HasContinuationBefore(text, lineStart, text.Length);
            int lineEnd = hasContinuation ? text.Length - 2 : text.Length;
            lines.Add(new LineSpan(lineStart, lineEnd, false, hasContinuation, lineNumber));
        }

        return lines;
    }

    private static bool HasContinuationBefore(string text, int lineStart, int position)
    {
        if (position < lineStart + 2)
            return false;

        int i = position - 1;
        // Skip trailing whitespace
        while (i >= lineStart && (text[i] == ' ' || text[i] == '\t'))
            i--;

        // Check for \\
        return i >= lineStart + 1 && text[i] == '\\' && text[i - 1] == '\\';
    }

    /// <summary>
    /// Gets merged lines with continuation applied.
    /// Returns tuples of (merged line text, start span, has trailing continuation error).
    /// </summary>
    public static List<(string Text, LineSpan FirstSpan, bool HasTrailingContinuation)> GetMergedLines(string text, List<LineSpan> lines)
    {
        var result = new List<(string, LineSpan, bool)>();
        int i = 0;

        while (i < lines.Count)
        {
            var firstLine = lines[i];
            var mergedParts = new List<string>();
            bool hasTrailingContinuation = false;

            while (true)
            {
                var line = lines[i];
                string lineText = line.Extract(text).TrimEnd();
                mergedParts.Add(lineText);

                if (line.HasContinuation)
                {
                    // Check if next line exists
                    if (i + 1 >= lines.Count)
                    {
                        hasTrailingContinuation = true;
                        break;
                    }
                    i++;
                }
                else
                {
                    break;
                }
            }

            // Join with two spaces as per spec
            string mergedText = string.Join("  ", mergedParts);
            result.Add((mergedText, firstLine, hasTrailingContinuation));
            i++;
        }

        return result;
    }
}
