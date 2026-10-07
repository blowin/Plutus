using System.Globalization;
using System.Text.RegularExpressions;

namespace Plutus.Domain;

public readonly record struct FileSize
{
    private static readonly Regex SizeRegex = new(@"^([0-9]+(?:\.[0-9]+)?)\s*(B|KB|MB|GB|TB|K|M|G|T)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public long Bytes { get; }

    private FileSize(long bytes)
    {
        if (bytes < 0)
        {
            throw new ArgumentException("Size cannot be negative.");
        }
        Bytes = bytes;
    }

    /// <summary>
    /// Factory method for parsing from a string (e.g., "512K", "1.5MB", "1024")
    /// </summary>
    public static FileSize Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Size string cannot be empty.");
        }

        var raw = value.Trim().ToUpperInvariant();
        var match = SizeRegex.Match(raw);

        if (!match.Success)
        {
            throw new ArgumentException($"Invalid size: '{value}'. Examples: 512K, 1M, 2MB, 1024.");
        }

        var number = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var unit = match.Groups[2].Value;

        if (string.IsNullOrEmpty(unit))
        {
            unit = "B";
        }

        var multiplier = unit switch
        {
            "B" => 1L,
            "KB" or "K" => 1024L,
            "MB" or "M" => 1024L * 1024L,
            "GB" or "G" => 1024L * 1024L * 1024L,
            "TB" or "T" => 1024L * 1024L * 1024L * 1024L,
            _ => throw new ArgumentException($"Unsupported size unit: {unit}")
        };

        return new FileSize((long)(number * multiplier));
    }

    /// <summary>
    /// Factory method for creating directly from long
    /// </summary>
    public static FileSize FromBytes(long bytes) => new(bytes);

    /// <summary>
    /// Implicit conversion to long for backward compatibility during calculations
    /// </summary>
    public static implicit operator long(FileSize size) => size.Bytes;

    public override string ToString() => Bytes.HumanSize();
}
