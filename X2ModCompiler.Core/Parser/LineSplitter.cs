using X2ModCompiler.Core.Core;

namespace X2ModCompiler.Core.Parser;

/// <summary>
/// Provides utilities for splitting raw configuration text into physical and logical lines.
/// Handles Unreal Engine 3 (XCOM 2) specific line-ending and continuation rules (using '\\').
/// </summary>
public static class LineSplitter
{
    /// <summary>
    /// Decomposes the input text into a sequence of <see cref="LineSpan"/> objects.
    /// Identifies physical line boundaries (\r, \n, \r\n) and detects the presence of continuation markers (\\).
    /// </summary>
    /// <param name="text">The raw source text to process.</param>
    /// <returns>A list of discovered <see cref="LineSpan"/> structures.</returns>
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
                int continuationIndex = GetContinuationIndex(text, lineStart, i);
                bool hasContinuation = continuationIndex >= 0;
                
                int lineEnd = i;
                if (hasContinuation)
                {
                    lineEnd = continuationIndex;
                    // Trim any whitespace immediately before the \\
                    while (lineEnd > lineStart && (text[lineEnd - 1] == ' ' || text[lineEnd - 1] == '\t'))
                        lineEnd--;
                }
                
                lines.Add(new LineSpan(lineStart, lineEnd, false, hasContinuation, lineNumber));
                
                lineStart = i + 1;
                lineNumber++;
            }
            else if (c == '\r')
            {
                // Check for continuation before line ending
                int continuationIndex = GetContinuationIndex(text, lineStart, i);
                bool hasContinuation = continuationIndex >= 0;
                
                int lineEnd = i;
                if (hasContinuation)
                {
                    lineEnd = continuationIndex;
                    // Trim any whitespace immediately before the \\
                    while (lineEnd > lineStart && (text[lineEnd - 1] == ' ' || text[lineEnd - 1] == '\t'))
                        lineEnd--;
                }
                
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
            int continuationIndex = GetContinuationIndex(text, lineStart, text.Length);
            bool hasContinuation = continuationIndex >= 0;
            
            int lineEnd = text.Length;
            if (hasContinuation)
            {
                lineEnd = continuationIndex;
                // Trim any whitespace immediately before the \\
                while (lineEnd > lineStart && (text[lineEnd - 1] == ' ' || text[lineEnd - 1] == '\t'))
                    lineEnd--;
            }

            lines.Add(new LineSpan(lineStart, lineEnd, false, hasContinuation, lineNumber));
        }

        return lines;
    }

    /// <summary>
    /// Searches for the '\\' continuation sequence at the end of a line segment.
    /// Accounts for optional trailing whitespace after the marker.
    /// </summary>
    /// <param name="text">The source text.</param>
    /// <param name="lineStart">The start index of the current line.</param>
    /// <param name="position">The end index of the current line segment.</param>
    /// <returns>The index of the first '\' in the sequence, or -1 if no continuation is found.</returns>
    private static int GetContinuationIndex(string text, int lineStart, int position)
    {
        if (position < lineStart + 2)
            return -1;

        int i = position - 1;
        // Skip trailing whitespace
        while (i >= lineStart && (text[i] == ' ' || text[i] == '\t'))
            i--;

        // Check for \\
        if (i >= lineStart + 1 && text[i] == '\\' && text[i - 1] == '\\')
            return i - 1; // Return the start index of the \\

        return -1;
    }

    /// <summary>
    /// Aggregates physical line spans into single logical lines by applying continuation merging rules.
    /// Concatenates continued lines with exactly two spaces as separation, as per XCOM 2 engine specifications.
    /// </summary>
    /// <param name="text">The raw source text.</param>
    /// <param name="lines">The list of <see cref="LineSpan"/> objects produced by <see cref="Split"/>.</param>
    /// <returns>
    /// A list of tuples containing the merged text, the original starting and ending line spans, 
    /// and a flag indicating if the file ended prematurely with a continuation marker.
    /// </returns>
    public static List<(string Text, LineSpan FirstLine, LineSpan LastLine, bool HasTrailingContinuation)> GetMergedLines(string text, List<LineSpan> lines)
    {
        var result = new List<(string, LineSpan, LineSpan, bool)>();
        int i = 0;

        while (i < lines.Count)
        {
            var firstLine = lines[i];
            LineSpan lastLine = firstLine;
            var mergedParts = new List<string>();
            bool hasTrailingContinuation = false;

            while (true)
            {
                var line = lines[i];
                lastLine = line;
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
            result.Add((mergedText, firstLine, lastLine, hasTrailingContinuation));
            i++;
        }

        return result;
    }
}
