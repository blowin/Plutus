using System;
using System.Globalization;
using System.Xml;

namespace Plutus.Domain.Retry;

/// <summary>
/// Parses and formats user-friendly TimeSpan strings, ISO 8601 durations, and standard formats.
/// </summary>
public static class TimeSpanParser
{
    /// <summary>
    /// Parses a TimeSpan from a user-friendly string format, ISO 8601, or standard format.
    /// </summary>
    /// <param name="input">The string representation of the duration (e.g., "30s", "pt30s", "00:00:30").</param>
    /// <returns>A TimeSpan equivalent to the duration specified in the input.</returns>
    /// <throws cref="ArgumentException">Thrown when input is null or whitespace.</throws>
    /// <throws cref="FormatException">Thrown when input is not in a recognized format.</throws>
    public static TimeSpan Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Input cannot be null or empty.", nameof(input));
        }

        var trimmed = input.Trim();

        // 1. Try standard TimeSpan.Parse (handles "00:00:30", "1.02:03:04", etc.)
        if (TimeSpan.TryParse(trimmed, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        // 2. Try ISO 8601 duration format (handles "PT30S", "pt30s", "Pt5m", "P1D", etc.)
        if (trimmed.StartsWith("P", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                // XmlConvert requires strictly uppercase 'P' and 'T' tokens, so we force upper invariant.
                string isoString = trimmed.ToUpperInvariant();
                return XmlConvert.ToTimeSpan(isoString);
            }
            catch (Exception ex)
            {
                throw new FormatException($"Invalid ISO 8601 duration: '{input}'.", ex);
            }
        }

        // 3. Try user-friendly format (handles "30s", "500ms", "1m", "2h", "1d")
        if (TryParseFriendlyFormat(trimmed, out result))
        {
            return result;
        }

        throw new FormatException(
            $"Cannot parse '{input}' as TimeSpan. Supported formats: '30s', '500ms', '1m', '2h', '1d', " +
            "'00:00:30', 'PT30S', 'P1D'.");
    }

    /// <summary>
    /// Converts a TimeSpan to a user-friendly string representation or an ISO 8601 duration string.
    /// </summary>
    /// <param name="timeSpan">The TimeSpan value to convert.</param>
    /// <param name="format">The target output format strategy.</param>
    /// <returns>A string representation of the TimeSpan.</returns>
    public static string ToString(TimeSpan timeSpan, TimeSpanFormat format = TimeSpanFormat.Friendly)
    {
        switch (format)
        {
            case TimeSpanFormat.Iso8601:
                // Returns standard uppercase ISO 8601 duration string (e.g., "PT30S")
                return XmlConvert.ToString(timeSpan);
            case TimeSpanFormat.Standard:
                // Returns invariant constant format (e.g., "00:00:30")
                return timeSpan.ToString("c", CultureInfo.InvariantCulture);
        }

        // Generate friendly format by finding the highest matching clean unit interval
        double totalMs = timeSpan.TotalMilliseconds;

        if (totalMs == 0)
        {
            return "0s";
        }

        // Check exact division from largest units down to smallest
        if (totalMs % (24 * 60 * 60 * 1000) == 0)
        {
            return $"{(int)timeSpan.TotalDays}d";
        }

        if (totalMs % (60 * 60 * 1000) == 0)
        {
            return $"{(int)timeSpan.TotalHours}h";
        }

        if (totalMs % (60 * 1000) == 0)
        {
            return $"{(int)timeSpan.TotalMinutes}m";
        }

        if (totalMs % 1000 == 0)
        {
            return $"{(int)timeSpan.TotalSeconds}s";
        }

        // Fallback to milliseconds if fractional second parts exist
        return $"{totalMs.ToString(CultureInfo.InvariantCulture)}ms";
    }

    private static bool TryParseFriendlyFormat(string input, out TimeSpan result)
    {
        result = TimeSpan.Zero;

        // Dynamically find the boundary between the numeric values and trailing unit letters
        int letterStartIndex = -1;
        for (int i = input.Length - 1; i >= 0; i--)
        {
            if (!char.IsLetter(input[i]))
            {
                letterStartIndex = i + 1;
                break;
            }
        }

        if (letterStartIndex <= 0 || letterStartIndex >= input.Length)
        {
            return false;
        }

        string numberPart = input[..letterStartIndex];
        string unitPart = input[letterStartIndex..].ToLowerInvariant();

        if (!double.TryParse(numberPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        switch (unitPart)
        {
            case "ms":
                result = TimeSpan.FromMilliseconds(value);
                return true;
            case "s":
                result = TimeSpan.FromSeconds(value);
                return true;
            case "m":
                result = TimeSpan.FromMinutes(value);
                return true;
            case "h":
                result = TimeSpan.FromHours(value);
                return true;
            case "d":
                result = TimeSpan.FromDays(value);
                return true;
            default:
                return false;
        }
    }
}

/// <summary>
/// Specifies the string format strategy for a TimeSpan object.
/// </summary>
public enum TimeSpanFormat
{
    Friendly,
    Iso8601,
    Standard
}
