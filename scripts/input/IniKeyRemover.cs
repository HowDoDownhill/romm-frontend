using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public static class IniKeyRemover
{
    public static int RemoveMatchingKeys(string configurationFilePath, string sectionName, string keyPattern)
    {
        if (!File.Exists(configurationFilePath))
        {
            return 0;
        }

        var keyExpression = new Regex(keyPattern);
        var keptLines = new List<string>();
        bool isInsideSection = false;
        int removedKeyCount = 0;

        foreach (string currentLine in File.ReadAllLines(configurationFilePath))
        {
            string trimmedLine = currentLine.Trim();

            if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
            {
                isInsideSection = trimmedLine.Substring(1, trimmedLine.Length - 2) == sectionName;
            }

            else if (isInsideSection && trimmedLine.Contains('=') && keyExpression.IsMatch(trimmedLine.Substring(0, trimmedLine.IndexOf('=')).Trim()))
            {
                removedKeyCount++;
                continue;
            }

            keptLines.Add(currentLine);
        }

        if (removedKeyCount > 0)
        {
            File.WriteAllLines(configurationFilePath, keptLines);
        }

        return removedKeyCount;
    }
}
