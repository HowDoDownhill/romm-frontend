using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class IniSectionCopier
{
    public static int CopyMissingKeys(string configurationFilePath, string sourceSection, string targetSection, ICollection<string> excludedKeys)
    {
        if (!File.Exists(configurationFilePath) || sourceSection == targetSection)
        {
            return 0;
        }

        List<string> lines = File.ReadAllLines(configurationFilePath).ToList();
        List<(string Key, string Line)> sourceEntries = ReadSectionEntries(lines, sourceSection, out _);
        List<(string Key, string Line)> targetEntries = ReadSectionEntries(lines, targetSection, out int targetEnd);
        var targetKeys = targetEntries.Select(entry => entry.Key).ToHashSet();
        List<string> missingLines = sourceEntries
            .Where(entry => !targetKeys.Contains(entry.Key) && !excludedKeys.Contains(entry.Key))
            .Select(entry => entry.Line)
            .ToList();

        if (missingLines.Count == 0)
        {
            return 0;
        }

        if (targetEnd < 0)
        {
            lines.Add($"[{targetSection}]");
            lines.AddRange(missingLines);
        }

        else
        {
            lines.InsertRange(targetEnd, missingLines);
        }

        File.WriteAllLines(configurationFilePath, lines);
        return missingLines.Count;
    }

    private static List<(string Key, string Line)> ReadSectionEntries(List<string> lines, string sectionName, out int sectionEnd)
    {
        var entries = new List<(string Key, string Line)>();
        bool isInsideSection = false;
        sectionEnd = -1;

        for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            string trimmedLine = lines[lineIndex].Trim();

            if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
            {
                if (isInsideSection)
                {
                    break;
                }

                isInsideSection = trimmedLine.Substring(1, trimmedLine.Length - 2) == sectionName;

                if (isInsideSection)
                {
                    sectionEnd = lineIndex + 1;
                }

                continue;
            }

            if (!isInsideSection)
            {
                continue;
            }

            if (trimmedLine.Contains('='))
            {
                entries.Add((trimmedLine.Substring(0, trimmedLine.IndexOf('=')).Trim(), lines[lineIndex]));
                sectionEnd = lineIndex + 1;
            }
        }

        return entries;
    }
}
