using System.Collections.Generic;
using System.IO;

public class IndentedConfigurationUpdater
{
    private const string ChildIndent = "  ";

    public void UpdateValue(string configurationFilePath, string targetSection, string targetKey, string stringValue)
    {
        if (!File.Exists(configurationFilePath))
        {
            return;
        }

        string[] configurationLines = File.ReadAllLines(configurationFilePath);
        var updatedLines = new List<string>();
        bool isInsideTargetSection = false;
        bool hasUpdatedTargetKey = false;

        foreach (string currentLine in configurationLines)
        {
            string trimmedLine = currentLine.Trim();
            bool opensSection = trimmedLine.Length > 0 && !char.IsWhiteSpace(currentLine[0]);

            if (opensSection)
            {
                if (isInsideTargetSection && !hasUpdatedTargetKey)
                {
                    updatedLines.Add($"{ChildIndent}{targetKey}: {stringValue}");
                    hasUpdatedTargetKey = true;
                }

                isInsideTargetSection = trimmedLine.Split(':')[0].Trim() == targetSection;
                updatedLines.Add(currentLine);
                continue;
            }

            if (isInsideTargetSection && !hasUpdatedTargetKey && DescribesKey(trimmedLine, targetKey))
            {
                updatedLines.Add($"{ChildIndent}{targetKey}: {stringValue}");
                hasUpdatedTargetKey = true;
                continue;
            }

            updatedLines.Add(currentLine);
        }

        if (isInsideTargetSection && !hasUpdatedTargetKey)
        {
            updatedLines.Add($"{ChildIndent}{targetKey}: {stringValue}");
            hasUpdatedTargetKey = true;
        }

        if (hasUpdatedTargetKey)
        {
            File.WriteAllLines(configurationFilePath, updatedLines);
        }
    }

    private static bool DescribesKey(string trimmedLine, string targetKey)
    {
        return trimmedLine.StartsWith(targetKey) && trimmedLine.Substring(targetKey.Length).TrimStart().StartsWith(":");
    }
}
