using Godot;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public class SectionDeviceIndexRewriter
{
    public const string IndentedSectionStyle = "bml";

    public int RewriteSection(string configurationFilePath, string targetSection, string devicePattern, string deviceReplacement, string requiredKeyPrefix = null, string sectionStyle = null)
    {
        if (!File.Exists(configurationFilePath) || string.IsNullOrEmpty(devicePattern))
        {
            return 0;
        }

        Regex deviceExpression;

        try
        {
            deviceExpression = new Regex(devicePattern);
        }
        catch (System.ArgumentException patternFailure)
        {
            GD.PrintErr($"[InputLayer] device_binding_pattern '{devicePattern}' is not a valid expression: {patternFailure.Message}");
            return 0;
        }

        string[] configurationLines = File.ReadAllLines(configurationFilePath);
        var rewrittenLines = new List<string>();
        bool isInsideTargetSection = false;
        int rewrittenBindingCount = 0;

        foreach (string currentLine in configurationLines)
        {
            string trimmedLine = currentLine.Trim();

            if (sectionStyle == IndentedSectionStyle)
            {
                if (trimmedLine.Length > 0 && !char.IsWhiteSpace(currentLine[0]))
                {
                    isInsideTargetSection = trimmedLine.Split(':')[0].Trim() == targetSection;
                    rewrittenLines.Add(currentLine);
                    continue;
                }
            }

            else if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
            {
                isInsideTargetSection = trimmedLine.Substring(1, trimmedLine.Length - 2) == targetSection;
                rewrittenLines.Add(currentLine);
                continue;
            }

            bool describesRequiredKey = string.IsNullOrEmpty(requiredKeyPrefix) || trimmedLine.StartsWith(requiredKeyPrefix);

            if (!isInsideTargetSection || !describesRequiredKey || !deviceExpression.IsMatch(currentLine))
            {
                rewrittenLines.Add(currentLine);
                continue;
            }

            string rewrittenLine = deviceExpression.Replace(currentLine, deviceReplacement);

            if (rewrittenLine != currentLine)
            {
                rewrittenBindingCount++;
            }

            rewrittenLines.Add(rewrittenLine);
        }

        if (rewrittenBindingCount > 0)
        {
            File.WriteAllLines(configurationFilePath, rewrittenLines);
        }

        return rewrittenBindingCount;
    }
}
