using System;
using System.Collections.Generic;
using System.IO;

public class FlatConfigurationUpdater
{
    public void UpdateValue(string configurationFilePath, string targetKey, string stringValue)
    {
        if (!File.Exists(configurationFilePath))
        {
            return;
        }

        string[] configurationLines = File.ReadAllLines(configurationFilePath);
        var updatedConfigurationLines = new List<string>();
        bool hasUpdatedTargetKey = false;

        foreach (string currentLine in configurationLines)
        {
            if (!hasUpdatedTargetKey && DescribesKey(currentLine, targetKey))
            {
                updatedConfigurationLines.Add($"{targetKey} = {stringValue}");
                hasUpdatedTargetKey = true;
                continue;
            }

            updatedConfigurationLines.Add(currentLine);
        }

        if (!hasUpdatedTargetKey)
        {
            updatedConfigurationLines.Add($"{targetKey} = {stringValue}");
        }

        File.WriteAllLines(configurationFilePath, updatedConfigurationLines);
    }

    private static bool DescribesKey(string configurationLine, string targetKey)
    {
        string trimmedLine = configurationLine.TrimStart();

        if (trimmedLine.StartsWith("#") || !trimmedLine.StartsWith(targetKey, StringComparison.Ordinal))
        {
            return false;
        }

        return trimmedLine.Substring(targetKey.Length).TrimStart().StartsWith("=", StringComparison.Ordinal);
    }
}
