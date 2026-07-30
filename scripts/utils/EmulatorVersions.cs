using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class EmulatorVersions
{
    public static int Compare(string leftVersion, string rightVersion)
    {
        var leftNumbers = ExtractNumbers(leftVersion);
        var rightNumbers = ExtractNumbers(rightVersion);

        if (leftNumbers.Count == 0 || rightNumbers.Count == 0)
        {
            return 0;
        }

        for (int partIndex = 0; partIndex < Math.Max(leftNumbers.Count, rightNumbers.Count); partIndex++)
        {
            long leftPart = partIndex < leftNumbers.Count ? leftNumbers[partIndex] : 0;
            long rightPart = partIndex < rightNumbers.Count ? rightNumbers[partIndex] : 0;

            if (leftPart != rightPart)
            {
                return leftPart < rightPart ? -1 : 1;
            }
        }

        return 0;
    }

    public static bool AreSame(string leftVersion, string rightVersion)
    {
        return !string.IsNullOrEmpty(leftVersion)
            && !string.IsNullOrEmpty(rightVersion)
            && string.Equals(leftVersion, rightVersion, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDowngrade(string candidateVersion, string installedVersion)
    {
        return !string.IsNullOrEmpty(candidateVersion)
            && !string.IsNullOrEmpty(installedVersion)
            && !AreSame(candidateVersion, installedVersion)
            && Compare(candidateVersion, installedVersion) < 0;
    }

    private static List<long> ExtractNumbers(string versionLabel)
    {
        var numbers = new List<long>();

        if (string.IsNullOrEmpty(versionLabel))
        {
            return numbers;
        }

        foreach (Match numberMatch in Regex.Matches(versionLabel, @"\d+"))
        {
            if (long.TryParse(numberMatch.Value, out long parsedNumber))
            {
                numbers.Add(parsedNumber);
            }
        }

        return numbers;
    }
}
