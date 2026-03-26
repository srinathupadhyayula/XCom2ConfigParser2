using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using X2ModCompiler.Compilation;

namespace X2ModCompiler.Utilities;

/// <summary>
/// Formats data as aligned text tables for console output.
/// </summary>
public static class TableFormatter
{
    /// <summary>
    /// Formats a collection of objects as a table with aligned columns.
    /// </summary>
    /// <typeparam name="T">The type of objects to format.</typeparam>
    /// <param name="items">The items to format.</param>
    /// <param name="columnWidths">Optional column widths. If null, widths are auto-calculated.</param>
    /// <returns>A formatted table string.</returns>
    public static string Format<T>(IEnumerable<T> items, int[]? columnWidths = null)
    {
        var itemList = items.ToList();
        if (!itemList.Any())
        {
            return string.Empty;
        }

        var properties = typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p.CanRead && p.PropertyType.IsPrimitive || p.PropertyType == typeof(string) || p.PropertyType == typeof(TimeSpan) || p.PropertyType == typeof(double) || p.PropertyType == typeof(decimal))
            .ToList();

        if (!properties.Any())
        {
            return string.Empty;
        }

        // Calculate column widths
        var columnNames = properties.Select(p => p.Name).ToList();
        var widths = columnWidths ?? CalculateColumnWidths(itemList, properties);

        // Build header
        var header = new StringBuilder();
        for (int i = 0; i < columnNames.Count; i++)
        {
            header.Append(columnNames[i].PadRight(widths[i])).Append("  ");
        }

        // Build rows
        var rows = new StringBuilder();
        foreach (var item in itemList)
        {
            var row = new StringBuilder();
            for (int i = 0; i < properties.Count; i++)
            {
                var value = properties[i].GetValue(item);
                var formattedValue = FormatValue(value);
                row.Append(formattedValue.PadRight(widths[i])).Append("  ");
            }
            rows.AppendLine(row.ToString().TrimEnd());
        }

        return $"{header}{Environment.NewLine}{rows}";
    }

    /// <summary>
    /// Formats build timing records for display.
    /// </summary>
    /// <param name="timings">The timing records to format.</param>
    /// <param name="totalDuration">The total build duration.</param>
    /// <returns>A formatted timing report.</returns>
    public static string FormatTimings(IEnumerable<BuildTimingRecord> timings, TimeSpan totalDuration)
    {
        var timingList = timings.ToList();
        var accountedTime = timingList.Sum(t => t.Seconds);
        
        // Add total and unaccounted time
        var extendedTimings = timingList.Concat(new[]
        {
            new BuildTimingRecord("Total Duration", totalDuration.TotalSeconds, "INFO"),
            new BuildTimingRecord("Unaccounted Time", totalDuration.TotalSeconds - accountedTime, "INFO")
        }).ToList();

        // Sort by duration descending
        var sortedTimings = extendedTimings.OrderByDescending(t => t.Seconds).ToList();

        // Calculate percentages
        var formattedTimings = sortedTimings.Select(t => new
        {
            t.Description,
            Time = $"{t.Seconds:F2}s",
            Share = t.Seconds > 0 && totalDuration.TotalSeconds > 0 
                ? $"{(t.Seconds / totalDuration.TotalSeconds):P1}" 
                : "N/A"
        }).ToList();

        return Format(formattedTimings);
    }

    private static int[] CalculateColumnWidths<T>(IEnumerable<T> items, List<System.Reflection.PropertyInfo> properties)
    {
        var widths = properties.Select(p => p.Name.Length).ToArray();
        var itemList = items.ToList();

        foreach (var property in properties)
        {
            var index = properties.IndexOf(property);
            var maxLength = itemList
                .Select(item => property.GetValue(item))
                .Select(value => FormatValue(value).Length)
                .DefaultIfEmpty(0)
                .Max();

            widths[index] = Math.Max(widths[index], maxLength);
        }

        return widths;
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            TimeSpan ts => ts.TotalSeconds.ToString("F2"),
            double d => d.ToString("F2"),
            decimal dm => dm.ToString("F2"),
            _ => value.ToString() ?? string.Empty
        };
    }
}
