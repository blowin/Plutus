namespace Plutus.Domain;

public static class LongExt
{
    public static string HumanSize(this long self)
    {
        double value = self;
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var unitIndex = 0;

        while (value >= 1024.0 && unitIndex < units.Length - 1)
        {
            value /= 1024.0;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{(long)value} {units[unitIndex]}"
            : $"{value:F1} {units[unitIndex]}";
    }
}

