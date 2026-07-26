using Godot;

public static class DownloadProgressDisplay
{
    public static bool HasKnownTotal(long currentBytes, long totalBytes)
    {
        return totalBytes > 0 && currentBytes <= totalBytes;
    }

    public static void ApplyTo(ProgressBar progressBar, long currentBytes, long totalBytes)
    {
        if (progressBar == null)
        {
            return;
        }

        if (!HasKnownTotal(currentBytes, totalBytes))
        {
            progressBar.Indeterminate = true;
            progressBar.MaxValue = 1;
            progressBar.Value = 0;
            return;
        }

        progressBar.Indeterminate = false;
        progressBar.MaxValue = totalBytes;
        progressBar.Value = currentBytes;
    }

    public static string DescribeProgress(long currentBytes, long totalBytes)
    {
        if (!HasKnownTotal(currentBytes, totalBytes))
        {
            return $"Downloading... {FormatBytes(currentBytes)}";
        }

        return $"Downloading... {FormatBytes(currentBytes)} / {FormatBytes(totalBytes)}";
    }

    public static string FormatBytes(long byteCount)
    {
        if (byteCount < 0)
        {
            return "unknown";
        }

        string[] unitSuffixes = { "B", "KB", "MB", "GB", "TB" };
        double scaledValue = byteCount;
        int unitIndex = 0;

        while (scaledValue >= 1024.0 && unitIndex < unitSuffixes.Length - 1)
        {
            scaledValue /= 1024.0;
            unitIndex++;
        }

        if (unitIndex == 0)
        {
            return $"{byteCount} B";
        }

        return $"{scaledValue:F1} {unitSuffixes[unitIndex]}";
    }
}
