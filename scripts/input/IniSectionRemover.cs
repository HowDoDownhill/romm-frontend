using Godot;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public class IniSectionRemover
{
    public int RemoveMatchingSections(string configurationFilePath, string sectionNamePattern)
    {
        if (!File.Exists(configurationFilePath) || string.IsNullOrEmpty(sectionNamePattern))
        {
            return 0;
        }

        Regex sectionExpression;

        try
        {
            sectionExpression = new Regex(sectionNamePattern);
        }
        catch (System.ArgumentException patternFailure)
        {
            GD.PrintErr($"[InputLayer] remove_section_patterns entry '{sectionNamePattern}' is not a valid expression: {patternFailure.Message}");
            return 0;
        }

        string[] configurationLines = File.ReadAllLines(configurationFilePath);
        var keptLines = new List<string>();
        bool isInsideRemovedSection = false;
        int removedSectionCount = 0;

        foreach (string currentLine in configurationLines)
        {
            string trimmedLine = currentLine.Trim();

            if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
            {
                string sectionName = trimmedLine.Substring(1, trimmedLine.Length - 2);
                isInsideRemovedSection = sectionExpression.IsMatch(sectionName);

                if (isInsideRemovedSection)
                {
                    removedSectionCount++;
                    continue;
                }
            }

            if (!isInsideRemovedSection)
            {
                keptLines.Add(currentLine);
            }
        }

        if (removedSectionCount > 0)
        {
            File.WriteAllLines(configurationFilePath, keptLines);
        }

        return removedSectionCount;
    }
}
