using System;
using Godot;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using FileAccess = Godot.FileAccess;
using DirAccess = Godot.DirAccess;

public interface IConfigurationUpdater
{
    bool CanHandle(string filePath);

    void UpdateValue(string filePath, string section, string key, string stringValue, object rawValue);
}

public class IniConfigurationUpdater : IConfigurationUpdater
{
    public bool CanHandle(string filePath)
    {
        return !filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
               !filePath.EndsWith(".bml", StringComparison.OrdinalIgnoreCase);
    }

    public string ReadValue(string configurationFilePath, string targetSection, string targetKey)
    {
        if (!System.IO.File.Exists(configurationFilePath))
        {
            return null;
        }

        bool isInsideTargetSection = false;

        foreach (string currentLine in System.IO.File.ReadAllLines(configurationFilePath))
        {
            string trimmedCurrentLine = currentLine.Trim();

            if (trimmedCurrentLine.StartsWith("[") && trimmedCurrentLine.EndsWith("]"))
            {
                string currentSection = trimmedCurrentLine.Substring(1, trimmedCurrentLine.Length - 2);
                isInsideTargetSection = (currentSection == targetSection);
                continue;
            }

            if (!isInsideTargetSection)
            {
                continue;
            }

            int separatorIndex = trimmedCurrentLine.IndexOf('=');

            if (separatorIndex > 0 && trimmedCurrentLine.Substring(0, separatorIndex).Trim() == targetKey)
            {
                return trimmedCurrentLine.Substring(separatorIndex + 1).Trim();
            }
        }

        return null;
    }

    public void UpdateValue(string configurationFilePath, string targetSection, string targetKey, string stringValue, object rawValue)
    {
        if (!System.IO.File.Exists(configurationFilePath))
        {
            return;
        }

        string[] configurationLines = System.IO.File.ReadAllLines(configurationFilePath);
        System.Collections.Generic.List<string> updatedConfigurationLines = new System.Collections.Generic.List<string>();
        bool isInsideTargetSection = false;
        bool hasUpdatedTargetKey = false;

        foreach (string currentLine in configurationLines)
        {
            string trimmedCurrentLine = currentLine.Trim();

            if (trimmedCurrentLine.StartsWith("[") && trimmedCurrentLine.EndsWith("]"))
            {
                if (isInsideTargetSection && !hasUpdatedTargetKey)
                {
                    updatedConfigurationLines.Add($"{targetKey} = {stringValue}");
                    hasUpdatedTargetKey = true;
                }

                string currentSection = trimmedCurrentLine.Substring(1, trimmedCurrentLine.Length - 2);
                isInsideTargetSection = (currentSection == targetSection);
                updatedConfigurationLines.Add(currentLine);
            }

            else if (isInsideTargetSection && !hasUpdatedTargetKey)
            {
                int equalsIndex = currentLine.IndexOf('=');

                if (equalsIndex != -1)
                {
                    string keyName = currentLine.Substring(0, equalsIndex).Trim();

                    if (keyName == targetKey)
                    {
                        updatedConfigurationLines.Add($"{keyName} = {stringValue}");
                        hasUpdatedTargetKey = true;
                    }

                    else
                    {
                        updatedConfigurationLines.Add(currentLine);
                    }
                }

                else
                {
                    updatedConfigurationLines.Add(currentLine);
                }
            }

            else
            {
                updatedConfigurationLines.Add(currentLine);
            }
        }

        if (isInsideTargetSection && !hasUpdatedTargetKey)
        {
            updatedConfigurationLines.Add($"{targetKey} = {stringValue}");
            hasUpdatedTargetKey = true;
        }

        else if (!hasUpdatedTargetKey)
        {
            updatedConfigurationLines.Add("");
            updatedConfigurationLines.Add($"[{targetSection}]");
            updatedConfigurationLines.Add($"{targetKey} = {stringValue}");
        }

        System.IO.File.WriteAllLines(configurationFilePath, updatedConfigurationLines);
    }
}

public class QtConfigurationUpdater : IConfigurationUpdater
{
    private readonly IniConfigurationUpdater iniUpdater = new IniConfigurationUpdater();

    public bool CanHandle(string filePath)
    {
        return Path.GetFileName(filePath).Equals("qt-config.ini", StringComparison.OrdinalIgnoreCase);
    }

    public void UpdateValue(string configurationFilePath, string targetSection, string targetKey, string stringValue, object rawValue)
    {
        iniUpdater.UpdateValue(configurationFilePath, targetSection, targetKey, stringValue, rawValue);
        iniUpdater.UpdateValue(configurationFilePath, targetSection, targetKey + "\\default", "false", "false");
    }
}

public class JsonConfigurationUpdater : IConfigurationUpdater
{
    public bool CanHandle(string filePath)
    {
        return filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    public void UpdateValue(string configurationFilePath, string targetSection, string targetKey, string stringValue, object rawValue)
    {
        string directory = Path.GetDirectoryName(configurationFilePath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        JsonNode jsonNode = null;

        if (System.IO.File.Exists(configurationFilePath))
        {
            try
            {
                string json = System.IO.File.ReadAllText(configurationFilePath);
                jsonNode = JsonNode.Parse(json);
            }

            catch { }
        }

        if (jsonNode == null)
        {
            jsonNode = new JsonObject();
        }

        var jsonObject = jsonNode.AsObject();

        if (!jsonObject.ContainsKey(targetSection))
        {
            jsonObject[targetSection] = new JsonObject();
        }

        var sectionObject = jsonObject[targetSection].AsObject();

        if (rawValue is bool boolVal)
        {
            sectionObject[targetKey] = boolVal;
        }

        else if (rawValue is string strVal)
        {
            if (int.TryParse(strVal, out int intVal))
            {
                sectionObject[targetKey] = intVal;
            }

            else
            {
                sectionObject[targetKey] = strVal;
            }
        }

        else if (rawValue is JsonElement elem)
        {
            if (elem.ValueKind == JsonValueKind.True)
            {
                sectionObject[targetKey] = true;
            }

            else if (elem.ValueKind == JsonValueKind.False)
            {
                sectionObject[targetKey] = false;
            }

            else if (elem.ValueKind == JsonValueKind.String)
            {
                sectionObject[targetKey] = elem.GetString();
            }

            else if (elem.ValueKind == JsonValueKind.Number)
            {
                sectionObject[targetKey] = elem.GetDouble();
            }
        }

        else if (rawValue is JsonArray jsonArray)
        {
            sectionObject[targetKey] = jsonArray;
        }

        System.IO.File.WriteAllText(configurationFilePath, jsonObject.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}

public class BmlConfigurationUpdater : IConfigurationUpdater
{
    class BmlLine
    {
        public string Text;
        public int Indent;
        public string Key;
        public bool IsParsed;
    }

    public bool CanHandle(string filePath)
    {
        return filePath.EndsWith(".bml", StringComparison.OrdinalIgnoreCase);
    }

    public void UpdateValue(string configurationFilePath, string targetSection, string targetKey, string stringValue, object rawValue)
    {
        string[] lines = System.IO.File.Exists(configurationFilePath) ? System.IO.File.ReadAllLines(configurationFilePath) : new string[0];

        var parsedLines = new System.Collections.Generic.List<BmlLine>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                parsedLines.Add(new BmlLine { Text = line, IsParsed = false });
                continue;
            }

            int indent = 0;

            while (indent < line.Length && line[indent] == ' ')
            {
                indent++;
            }

            string content = line.Substring(indent);

            if (content.StartsWith("//"))
            {
                parsedLines.Add(new BmlLine { Text = line, IsParsed = false });
                continue;
            }

            int colonIndex = content.IndexOf(':');
            string key = colonIndex >= 0 ? content.Substring(0, colonIndex).TrimEnd() : content.TrimEnd();

            parsedLines.Add(new BmlLine { Text = line, Indent = indent, Key = key, IsParsed = true });
        }

        string[] sectionPath = string.IsNullOrEmpty(targetSection) ? new string[0] : targetSection.Split('/');

        int currentLineIndex = 0;
        int currentIndent = 0;
        int parentIndent = -1;

        for (int i = 0; i < sectionPath.Length; i++)
        {
            string expectedSection = sectionPath[i];
            bool found = false;

            for (int j = currentLineIndex; j < parsedLines.Count; j++)
            {
                var pl = parsedLines[j];

                if (!pl.IsParsed)
                {
                    continue;
                }

                if (pl.Indent <= parentIndent)
                {
                    break;
                }

                if (pl.Indent == currentIndent && pl.Key == expectedSection)
                {
                    found = true;
                    currentLineIndex = j + 1;
                    parentIndent = currentIndent;

                    int childIndent = currentIndent + 2;

                    for (int next = currentLineIndex; next < parsedLines.Count; next++)
                    {
                        if (parsedLines[next].IsParsed)
                        {
                            if (parsedLines[next].Indent > currentIndent)
                            {
                                childIndent = parsedLines[next].Indent;
                            }

                            break;
                        }
                    }

                    currentIndent = childIndent;
                    break;
                }
            }

            if (!found)
            {
                for (int k = i; k < sectionPath.Length; k++)
                {
                    int insertAt = FindInsertPosition(parsedLines, currentLineIndex, parentIndent);
                    parsedLines.Insert(insertAt, new BmlLine
                    {
                        Text = new string(' ', currentIndent) + sectionPath[k],
                        Indent = currentIndent,
                        Key = sectionPath[k],
                        IsParsed = true
                    });
                    currentLineIndex = insertAt + 1;
                    parentIndent = currentIndent;
                    currentIndent += 2;
                }

                break;
            }
        }

        bool keyFound = false;

        for (int j = currentLineIndex; j < parsedLines.Count; j++)
        {
            var pl = parsedLines[j];

            if (!pl.IsParsed)
            {
                continue;
            }

            if (pl.Indent <= parentIndent)
            {
                break;
            }

            if (pl.Indent == currentIndent && pl.Key == targetKey)
            {
                pl.Text = new string(' ', currentIndent) + targetKey + ": " + stringValue;
                keyFound = true;
                break;
            }
        }

        if (!keyFound)
        {
            int insertAt = FindInsertPosition(parsedLines, currentLineIndex, parentIndent);
            parsedLines.Insert(insertAt, new BmlLine
            {
                Text = new string(' ', currentIndent) + targetKey + ": " + stringValue,
                Indent = currentIndent,
                Key = targetKey,
                IsParsed = true
            });
        }

        var outputLines = new System.Collections.Generic.List<string>();

        foreach(var pl in parsedLines)
        {
            outputLines.Add(pl.Text);
        }

        string directory = Path.GetDirectoryName(configurationFilePath);

        if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        System.IO.File.WriteAllLines(configurationFilePath, outputLines);
    }

    private int FindInsertPosition(System.Collections.Generic.List<BmlLine> parsedLines, int startIndex, int parentIndent)
    {
        int insertAt = startIndex;

        for (int j = startIndex; j < parsedLines.Count; j++)
        {
            if (parsedLines[j].IsParsed && parsedLines[j].Indent <= parentIndent)
            {
                break;
            }

            insertAt = j + 1;
        }

        return insertAt;
    }
}

public class EmulatorMeta
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("executable_name")]
    public Dictionary<string, string> ExecutableName { get; set; }

    [JsonPropertyName("executable_regex")]
    public Dictionary<string, string> ExecutableRegex { get; set; }

    [JsonPropertyName("emulator_dir_name")]
    public Dictionary<string, string> EmulatorDirName { get; set; }

    [JsonPropertyName("emulator_bios_path")]
    public Dictionary<string, string> EmulatorBiosPath { get; set; }

    [JsonPropertyName("relative_save_path")]
    public Dictionary<string, JsonElement> RelativeSavePath { get; set; }

    [JsonPropertyName("preserve_on_reinstall")]
    public List<string> PreserveOnReinstall { get; set; }

    [JsonPropertyName("sync_include")]
    public List<string> SyncInclude { get; set; }

    [JsonPropertyName("sync_save_path")]
    public Dictionary<string, JsonElement> SyncSavePath { get; set; }

    public List<string> GetSyncSavePatterns(string systemSlug)
    {
        var syncSavePatterns = new List<string>();

        if (SyncSavePath == null)
        {
            return syncSavePatterns;
        }

        if (!SyncSavePath.TryGetValue(systemSlug ?? "", out JsonElement syncSavePathElement)
            && !SyncSavePath.TryGetValue("default", out syncSavePathElement))
        {
            return syncSavePatterns;
        }

        if (syncSavePathElement.ValueKind == JsonValueKind.String)
        {
            syncSavePatterns.Add(syncSavePathElement.GetString());
        }

        else if (syncSavePathElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var syncSavePathArrayElement in syncSavePathElement.EnumerateArray())
            {
                if (syncSavePathArrayElement.ValueKind == JsonValueKind.String)
                {
                    syncSavePatterns.Add(syncSavePathArrayElement.GetString());
                }
            }
        }

        return syncSavePatterns;
    }


    [JsonPropertyName("launch_args_with_game")]
    public string LaunchArgsWithGame { get; set; }

    [JsonPropertyName("launch_args_without_game")]
    public string LaunchArgsWithoutGame { get; set; }

    [JsonPropertyName("launch_env")]
    public Dictionary<string, Dictionary<string, string>> LaunchEnv { get; set; }

    [JsonPropertyName("system_flags")]
    public Dictionary<string, JsonElement> SystemFlags { get; set; }

    [JsonPropertyName("system_cores")]
    public Dictionary<string, List<string>> SystemCores { get; set; }

    [JsonPropertyName("core_launch_arg")]
    public string CoreLaunchArg { get; set; }

    [JsonPropertyName("core_directory")]
    public string CoreDirectory { get; set; }

    [JsonPropertyName("core_file_name")]
    public JsonElement CoreFileName { get; set; }

    [JsonPropertyName("core_download_url")]
    public JsonElement CoreDownloadUrl { get; set; }

    public string ResolveSaveDirectoryForSystem(string systemSlug)
    {
        if (RelativeSavePath == null)
        {
            return null;
        }

        if (!RelativeSavePath.TryGetValue(systemSlug ?? "", out JsonElement savePathElement)
            && !RelativeSavePath.TryGetValue("default", out savePathElement))
        {
            return null;
        }

        if (savePathElement.ValueKind == JsonValueKind.String)
        {
            return savePathElement.GetString();
        }

        foreach (var savePathArrayElement in savePathElement.EnumerateArray())
        {
            if (savePathArrayElement.ValueKind == JsonValueKind.String)
            {
                return savePathArrayElement.GetString();
            }
        }

        return null;
    }

    public List<string> GetSelectableCores(string systemSlug)
    {
        if (SystemCores != null && !string.IsNullOrEmpty(systemSlug) && SystemCores.TryGetValue(systemSlug, out var selectableCores))
        {
            return selectableCores;
        }

        return new List<string>();
    }

    public string ResolveCoreRelativePath(string coreName, string operatingSystem)
    {
        string coreFileNameTemplate = EmulatorSettingField.ResolveOsScopedValue(CoreFileName, operatingSystem);

        if (string.IsNullOrEmpty(coreFileNameTemplate) || string.IsNullOrEmpty(coreName))
        {
            return null;
        }

        string coreFileName = coreFileNameTemplate.Replace("{core}", coreName);

        return string.IsNullOrEmpty(CoreDirectory) ? coreFileName : CoreDirectory + "/" + coreFileName;
    }

    public string ResolveCoreDownloadUrl(string coreName, string operatingSystem)
    {
        string coreDownloadUrlTemplate = EmulatorSettingField.ResolveOsScopedValue(CoreDownloadUrl, operatingSystem);

        if (string.IsNullOrEmpty(coreDownloadUrlTemplate) || string.IsNullOrEmpty(coreName))
        {
            return null;
        }

        return coreDownloadUrlTemplate.Replace("{core}", coreName);
    }

    public string ResolveCoreLaunchArgument(string coreName, string operatingSystem)
    {
        string coreRelativePath = ResolveCoreRelativePath(coreName, operatingSystem);

        return string.IsNullOrEmpty(CoreLaunchArg) || coreRelativePath == null
            ? null
            : CoreLaunchArg.Replace("{core_path}", coreRelativePath);
    }

    [JsonPropertyName("install_recipe")]
    public Dictionary<string, InstallRecipe> InstallRecipe { get; set; }

    [JsonPropertyName("settings_fields")]
    public List<EmulatorSettingField> SettingsFields { get; set; }

    [JsonPropertyName("controller_config")]
    public ControllerConfig ControllerConfig { get; set; }

    public List<string> GetSaveRelativePaths()
    {
        var savePaths = new List<string>();

        if (RelativeSavePath == null)
        {
            return savePaths;
        }

        foreach (var savePathEntry in RelativeSavePath.Values)
        {
            if (savePathEntry.ValueKind == JsonValueKind.String)
            {
                savePaths.Add(savePathEntry.GetString());
            }

            else if (savePathEntry.ValueKind == JsonValueKind.Array)
            {
                foreach (var savePathElement in savePathEntry.EnumerateArray())
                {
                    if (savePathElement.ValueKind == JsonValueKind.String)
                    {
                        savePaths.Add(savePathElement.GetString());
                    }
                }
            }
        }

        return savePaths;
    }

    public bool ShouldSyncSaveItem(string saveItemName)
    {
        if (SyncInclude == null)
        {
            return true;
        }

        foreach (string includePattern in SyncInclude)
        {
            string escapedPattern = "^" + System.Text.RegularExpressions.Regex.Escape(includePattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";

            if (System.Text.RegularExpressions.Regex.IsMatch(saveItemName ?? "", escapedPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public List<string> GetPreservePaths()
    {
        var preservePaths = GetSaveRelativePaths();

        if (PreserveOnReinstall != null)
        {
            foreach (var preservePath in PreserveOnReinstall)
            {
                if (!string.IsNullOrEmpty(preservePath))
                {
                    preservePaths.Add(preservePath);
                }
            }
        }

        return preservePaths;
    }
}

public class EmulatorSettingField
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("launch_arg_true")]
    public string LaunchArgTrue { get; set; }

    [JsonPropertyName("launch_arg_false")]
    public string LaunchArgFalse { get; set; }

    [JsonPropertyName("launch_arg_format")]
    public string LaunchArgFormat { get; set; }

    [JsonPropertyName("config_file_relative_path")]
    public JsonElement ConfigFileRelativePath { get; set; }

    [JsonPropertyName("config_section")]
    public JsonElement ConfigSection { get; set; }

    [JsonPropertyName("config_key")]
    public JsonElement ConfigKey { get; set; }

    [JsonPropertyName("options")]
    public Dictionary<string, string> Options { get; set; }

    [JsonPropertyName("default_value_bool")]
    public bool DefaultValueBool { get; set; }

    [JsonPropertyName("default_value_string")]
    public string DefaultValueString { get; set; }

    public string ResolveConfigFileRelativePath(string operatingSystem) => ResolveOsScopedValue(ConfigFileRelativePath, operatingSystem);

    public string ResolveConfigSection(string operatingSystem) => ResolveOsScopedValue(ConfigSection, operatingSystem);

    public string ResolveConfigKey(string operatingSystem) => ResolveOsScopedValue(ConfigKey, operatingSystem);

    public static string ResolveOsScopedValue(JsonElement fieldValue, string operatingSystem)
    {
        if (fieldValue.ValueKind == JsonValueKind.String)
        {
            return fieldValue.GetString();
        }

        if (fieldValue.ValueKind == JsonValueKind.Object
            && fieldValue.TryGetProperty(operatingSystem, out JsonElement osScopedValue)
            && osScopedValue.ValueKind == JsonValueKind.String)
        {
            return osScopedValue.GetString();
        }

        return null;
    }
}

public class InstallRecipe
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("repo")]
    public string Repo { get; set; }

    [JsonPropertyName("asset_regex")]
    public string AssetRegex { get; set; }

    [JsonPropertyName("extract")]
    public bool Extract { get; set; } = true;

    [JsonPropertyName("extract_folder_regex")]
    public string ExtractFolderRegex { get; set; }

    [JsonPropertyName("list_url")]
    public string ListUrl { get; set; }

    [JsonPropertyName("link_regex")]
    public string LinkRegex { get; set; }

    [JsonPropertyName("version_regex")]
    public string VersionRegex { get; set; }

    [JsonPropertyName("url_template")]
    public string UrlTemplate { get; set; }

    [JsonPropertyName("tag_regex")]
    public string TagRegex { get; set; }

    [JsonPropertyName("extra_downloads")]
    public List<ExtraDownload> ExtraDownloads { get; set; }
}

public class ExtraDownload
{
    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("destination")]
    public string Destination { get; set; }

    [JsonPropertyName("extract")]
    public bool Extract { get; set; } = true;
}

public class ReleaseOption
{
    public string VersionLabel { get; set; }

    public string AssetName { get; set; }

    public string DownloadUrl { get; set; }

    public string PublishedDate { get; set; }
}

public class ControllerConfig
{
    [JsonPropertyName("max_controllers")]
    public int MaxControllers { get; set; }

    [JsonPropertyName("config_file_relative_path")]
    public string ConfigFileRelativePath { get; set; }

    [JsonPropertyName("format")]
    public string Format { get; set; }

    [JsonPropertyName("platform_layout")]
    public Dictionary<string, string> PlatformLayout { get; set; }

    [JsonPropertyName("sdl_string_map")]
    public Dictionary<string, string> SdlStringMap { get; set; }

    [JsonPropertyName("controller_sections")]
    public List<ControllerSection> ControllerSections { get; set; }

    [JsonPropertyName("assignment_key_path")]
    public string AssignmentKeyPath { get; set; }

    [JsonPropertyName("assignment_template")]
    public string AssignmentTemplate { get; set; }

    [JsonPropertyName("enabled_key_path")]
    public string EnabledKeyPath { get; set; }
}

public class ControllerSection
{
    [JsonPropertyName("section_template")]
    public string SectionTemplate { get; set; }

    [JsonPropertyName("port_start")]
    public int PortStart { get; set; }

    [JsonPropertyName("device_key")]
    public string DeviceKey { get; set; }

    [JsonPropertyName("device_template")]
    public string DeviceTemplate { get; set; }

    [JsonPropertyName("device_disconnected")]
    public string DeviceDisconnected { get; set; }

    [JsonPropertyName("type_key")]
    public string TypeKey { get; set; }

    [JsonPropertyName("type_connected")]
    public string TypeConnected { get; set; }

    [JsonPropertyName("type_disconnected")]
    public string TypeDisconnected { get; set; }

    [JsonPropertyName("mappings")]
    public Dictionary<string, string> Mappings { get; set; }

    [JsonPropertyName("static_values")]
    public Dictionary<string, string> StaticValues { get; set; }
}

public partial class EmulatorManager : Node
{
    [Signal]
    public delegate void EmulatorInstallationCompletedEventHandler(string emulatorName, bool wasSuccessful);

    private string emulatorMapFilePath;
    private string executableMapFilePath;

    private Dictionary<string, List<string>> systemToEmulatorMap = new Dictionary<string, List<string>>();
    private readonly HashSet<string> installingEmulators = new HashSet<string>();
    private Process activeEmulatorProcess = null;
    private Game activeGame = null;
    private DateTime activeSessionStart;

    private AppInstance appInstance;
    private ControllerManager controllerManager;

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.emulatorManager = this;
        controllerManager = GetNode<ControllerManager>("/root/ControllerManager");

        InitializeFilePaths();
        LoadOrGenerateEmulatorMap();
        LinkInstalledEmulatorSaveDirectoriesIntoStore();
    }

    private void LinkInstalledEmulatorSaveDirectoriesIntoStore()
    {
        if (!Directory.Exists(appInstance.configManager.InstallScriptsPath))
        {
            return;
        }

        string currentOperatingSystem = OS.GetName().ToLower();

        foreach (string recipeDirectoryPath in Directory.GetDirectories(appInstance.configManager.InstallScriptsPath))
        {
            string emulatorName = new DirectoryInfo(recipeDirectoryPath).Name;

            if (!IsEmulatorInstalled(emulatorName))
            {
                continue;
            }

            SaveStore.LinkEmulatorSaveDirectories(appInstance, emulatorName, LoadEmulatorMetadataFromDisk(emulatorName), currentOperatingSystem, null);
        }
    }

    public override void _Process(double delta)
    {
        if (activeEmulatorProcess != null && activeEmulatorProcess.HasExited)
        {
            DateTime sessionEnd = DateTime.UtcNow;

            if (appInstance.saveSyncManager != null && activeGame != null)
            {
                _ = appInstance.saveSyncManager.SyncAfterExit(activeGame, activeSessionStart, sessionEnd);
            }

            activeEmulatorProcess = null;
            activeGame = null;
        }
    }

    private void InitializeFilePaths()
    {
        emulatorMapFilePath = Path.Combine(appInstance.configManager.EmulatorsPath, "EmulatorMap.json");
        executableMapFilePath = Path.Combine(appInstance.configManager.EmulatorsPath, "ExecutableMap.json");
    }

    private void LoadOrGenerateEmulatorMap()
    {
        if (!FileAccess.FileExists(emulatorMapFilePath) || !FileAccess.FileExists(executableMapFilePath))
        {
            GenerateDefaultMaps();
        }

        try
        {
            string mapJsonContent = FileAccess.GetFileAsString(emulatorMapFilePath);
            systemToEmulatorMap = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(mapJsonContent, RommJsonContext.Default.Options);
        }

        catch (Exception exception)
        {
            GD.PrintErr($"Failed to load emulator map (likely old format): {exception.Message}. Regenerating...");
            GenerateDefaultMaps();

            try
            {
                string mapJsonContent = FileAccess.GetFileAsString(emulatorMapFilePath);
                systemToEmulatorMap = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(mapJsonContent, RommJsonContext.Default.Options);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Failed to load emulator map after regeneration: {ex.Message}");
            }
        }

        MergeMissingDefaultMappings();
    }

    private void MergeMissingDefaultMappings()
    {
        if (systemToEmulatorMap == null)
        {
            systemToEmulatorMap = new Dictionary<string, List<string>>();
        }

        var addedSystemSlugs = new List<string>();

        foreach (var defaultMapping in BuildDefaultEmulatorMap())
        {
            if (!systemToEmulatorMap.ContainsKey(defaultMapping.Key))
            {
                systemToEmulatorMap[defaultMapping.Key] = defaultMapping.Value;
                addedSystemSlugs.Add(defaultMapping.Key);
                continue;
            }

            var mappedEmulators = systemToEmulatorMap[defaultMapping.Key];

            foreach (string defaultEmulatorName in defaultMapping.Value)
            {
                if (!mappedEmulators.Contains(defaultEmulatorName))
                {
                    mappedEmulators.Add(defaultEmulatorName);
                    addedSystemSlugs.Add($"{defaultMapping.Key}:{defaultEmulatorName}");
                }
            }
        }

        if (addedSystemSlugs.Count == 0)
        {
            return;
        }

        try
        {
            string serializedMapJson = JsonSerializer.Serialize(systemToEmulatorMap, RommJsonContext.Default.Options);
            using var emulatorMapFile = FileAccess.Open(emulatorMapFilePath, FileAccess.ModeFlags.Write);
            emulatorMapFile.StoreString(serializedMapJson);
            GD.Print($"Added missing default emulator mappings: {string.Join(", ", addedSystemSlugs)}");
        }

        catch (Exception exception)
        {
            GD.PrintErr($"Failed to persist merged emulator map: {exception.Message}");
        }
    }

    public string GetMappedEmulator(string systemSlug)
    {
        if (string.IsNullOrEmpty(systemSlug))
        {
            return null;
        }

        if (appInstance.configManager.PreferredEmulators.ContainsKey(systemSlug))
        {
            string preferred = appInstance.configManager.PreferredEmulators[systemSlug];
            if (systemToEmulatorMap.ContainsKey(systemSlug) && systemToEmulatorMap[systemSlug].Contains(preferred))
            {
                return preferred;
            }
        }

        if (systemToEmulatorMap.ContainsKey(systemSlug) && systemToEmulatorMap[systemSlug].Count > 0)
        {
            return systemToEmulatorMap[systemSlug][0];
        }

        return null;
    }

    public List<string> GetSupportedEmulators(string systemSlug)
    {
        if (string.IsNullOrEmpty(systemSlug))
        {
            return new List<string>();
        }

        if (systemToEmulatorMap.ContainsKey(systemSlug))
        {
            return systemToEmulatorMap[systemSlug];
        }

        return new List<string>();
    }

    public Dictionary<string, EmulatorMeta> GetAllAvailableEmulators()
    {
        Dictionary<string, EmulatorMeta> availableEmulators = new Dictionary<string, EmulatorMeta>();
        string installScriptsDirectoryPath = appInstance.configManager.InstallScriptsPath;

        if (!DirAccess.DirExistsAbsolute(installScriptsDirectoryPath))
        {
            return availableEmulators;
        }

        using var installScriptsDirectory = DirAccess.Open(installScriptsDirectoryPath);

        if (installScriptsDirectory != null)
        {
            installScriptsDirectory.ListDirBegin();
            string directoryEntryName = installScriptsDirectory.GetNext();

            while (directoryEntryName != "")
            {
                if (installScriptsDirectory.CurrentIsDir() && directoryEntryName != "." && directoryEntryName != "..")
                {
                    string metadataFilePath = installScriptsDirectoryPath.PathJoin(directoryEntryName).PathJoin("meta.json");

                    if (FileAccess.FileExists(metadataFilePath))
                    {
                        try
                        {
                            var metadataJsonContent = FileAccess.GetFileAsString(metadataFilePath);
                            var emulatorMetadata = JsonSerializer.Deserialize<EmulatorMeta>(metadataJsonContent, RommJsonContext.Default.Options);

                            if (emulatorMetadata != null)
                            {
                                availableEmulators[directoryEntryName] = emulatorMetadata;
                            }
                        }

                        catch (Exception exception)
                        {
                            GD.PrintErr($"Failed to parse meta.json for {directoryEntryName}: {exception.Message}");
                        }
                    }
                }

                directoryEntryName = installScriptsDirectory.GetNext();
            }
        }

        return availableEmulators;
    }

    public void SaveEmulatorSetting(string emulatorSlug, string settingId, object settingValue)
    {
        string emulatorDirectoryPath = Path.Combine(appInstance.configManager.EmulatorsPath, emulatorSlug);

        if (!System.IO.Directory.Exists(emulatorDirectoryPath))
        {
            System.IO.Directory.CreateDirectory(emulatorDirectoryPath);
        }

        string userSettingsFilePath = Path.Combine(emulatorDirectoryPath, "user_settings.json");

        Dictionary<string, object> userSettings = new Dictionary<string, object>();

        if (System.IO.File.Exists(userSettingsFilePath))
        {
            try
            {
                string existingSettingsJson = System.IO.File.ReadAllText(userSettingsFilePath);
                userSettings = JsonSerializer.Deserialize<Dictionary<string, object>>(existingSettingsJson) ?? new Dictionary<string, object>();
            }

            catch {}
        }

        userSettings[settingId] = settingValue;
        System.IO.File.WriteAllText(userSettingsFilePath, JsonSerializer.Serialize(userSettings, new JsonSerializerOptions { WriteIndented = true }));

        EmulatorMeta emulatorMetadata = LoadEmulatorMetadataFromDisk(emulatorSlug);

        if (emulatorMetadata != null && emulatorMetadata.SettingsFields != null)
        {
            EmulatorSettingField targetSettingField = emulatorMetadata.SettingsFields.Find(field => field.Id == settingId);
            string currentOperatingSystem = OS.GetName().ToLower();
            string configFileRelativePath = targetSettingField?.ResolveConfigFileRelativePath(currentOperatingSystem);
            string configSection = targetSettingField?.ResolveConfigSection(currentOperatingSystem);
            string configKey = targetSettingField?.ResolveConfigKey(currentOperatingSystem);

            if (targetSettingField != null && !string.IsNullOrEmpty(configFileRelativePath) && !string.IsNullOrEmpty(configSection) && !string.IsNullOrEmpty(configKey))
            {
                if (emulatorMetadata.EmulatorDirName != null && emulatorMetadata.EmulatorDirName.ContainsKey(currentOperatingSystem))
                {
                    string targetEmulatorInstallDirectory = Path.Combine(appInstance.configManager.EmulatorsPath, emulatorMetadata.EmulatorDirName[currentOperatingSystem]);
                    string configurationFilePath = Path.Combine(targetEmulatorInstallDirectory, configFileRelativePath);

                    string stringValue = "";

                    if (settingValue is bool booleanValue)
                    {
                        stringValue = booleanValue ? "true" : "false";
                    }

                    else if (settingValue is string rawStringValue)
                    {
                        stringValue = rawStringValue;
                    }

                    else if (settingValue is JsonElement jsonElement)
                    {
                        if (jsonElement.ValueKind == JsonValueKind.True)
                        {
                            stringValue = "true";
                        }

                        else if (jsonElement.ValueKind == JsonValueKind.False)
                        {
                            stringValue = "false";
                        }

                        else if (jsonElement.ValueKind == JsonValueKind.String)
                        {
                            stringValue = jsonElement.GetString();
                        }
                    }

                    var updaters = new IConfigurationUpdater[]
                    {
                        new JsonConfigurationUpdater(),

                        new BmlConfigurationUpdater(),

                        new QtConfigurationUpdater(),

                        new IniConfigurationUpdater()
                    };

                    foreach (var updater in updaters)
                    {
                        if (updater.CanHandle(configurationFilePath))
                        {
                            updater.UpdateValue(configurationFilePath, configSection, configKey, stringValue, settingValue);
                            break;
                        }
                    }
                }
            }
        }
    }

    public Dictionary<string, JsonElement> LoadEmulatorSettings(string emulatorSlug)
    {
        string userSettingsFilePath = Path.Combine(appInstance.configManager.EmulatorsPath, emulatorSlug, "user_settings.json");

        if (System.IO.File.Exists(userSettingsFilePath))
        {
            try
            {
                string settingsJsonContent = System.IO.File.ReadAllText(userSettingsFilePath);
                return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(settingsJsonContent, RommJsonContext.Default.Options) ?? new Dictionary<string, JsonElement>();
            }

            catch {}
        }

        return new Dictionary<string, JsonElement>();
    }

    public EmulatorMeta LoadEmulatorMetadataFromDisk(string emulatorName)
    {
        string metadataFilePath = appInstance.configManager.InstallScriptsPath.PathJoin(emulatorName).PathJoin("meta.json");

        if (!FileAccess.FileExists(metadataFilePath))
        {
            return null;
        }

        try
        {
            var metadataJsonContent = FileAccess.GetFileAsString(metadataFilePath);
            return JsonSerializer.Deserialize<EmulatorMeta>(metadataJsonContent, RommJsonContext.Default.Options);
        }

        catch (Exception exception)
        {
            GD.PrintErr($"Failed to load emulator metadata for {emulatorName}: {exception.Message}");
            return null;
        }
    }

    public string ResolveExecutablePath(EmulatorMeta emulatorMetadata, string currentOperatingSystem, string emulatorInstallDirectory)
    {
        string literalExecutableName = emulatorMetadata.ExecutableName != null && emulatorMetadata.ExecutableName.ContainsKey(currentOperatingSystem)
            ? emulatorMetadata.ExecutableName[currentOperatingSystem]
            : null;

        if (!string.IsNullOrEmpty(literalExecutableName))
        {
            string literalExecutablePath = Path.GetFullPath(Path.Combine(emulatorInstallDirectory, literalExecutableName));

            if (System.IO.File.Exists(literalExecutablePath))
            {
                return literalExecutablePath;
            }
        }

        if (emulatorMetadata.ExecutableRegex != null && emulatorMetadata.ExecutableRegex.ContainsKey(currentOperatingSystem) && System.IO.Directory.Exists(emulatorInstallDirectory))
        {
            var executablePattern = new System.Text.RegularExpressions.Regex(emulatorMetadata.ExecutableRegex[currentOperatingSystem], System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            string matchingFilePath = System.IO.Directory.GetFiles(emulatorInstallDirectory).FirstOrDefault(filePath => executablePattern.IsMatch(Path.GetFileName(filePath)));

            if (matchingFilePath != null)
            {
                return Path.GetFullPath(matchingFilePath);
            }
        }

        return string.IsNullOrEmpty(literalExecutableName) ? null : Path.GetFullPath(Path.Combine(emulatorInstallDirectory, literalExecutableName));
    }

    public bool IsEmulatorInstalled(string emulatorName)
    {
        if (string.IsNullOrEmpty(emulatorName))
        {
            return false;
        }

        var emulatorMetadata = LoadEmulatorMetadataFromDisk(emulatorName);

        if (emulatorMetadata == null)
        {
            return false;
        }

        string currentOperatingSystem = OS.GetName().ToLower();

        bool hasExecutableEntry = (emulatorMetadata.ExecutableName != null && emulatorMetadata.ExecutableName.ContainsKey(currentOperatingSystem)) ||
                                  (emulatorMetadata.ExecutableRegex != null && emulatorMetadata.ExecutableRegex.ContainsKey(currentOperatingSystem));

        if (emulatorMetadata.EmulatorDirName == null || !emulatorMetadata.EmulatorDirName.ContainsKey(currentOperatingSystem) || !hasExecutableEntry)
        {
            return false;
        }

        string emulatorInstallDirectory = appInstance.configManager.EmulatorsPath.PathJoin(emulatorName);
        string fullExecutablePath = ResolveExecutablePath(emulatorMetadata, currentOperatingSystem, emulatorInstallDirectory);

        return fullExecutablePath != null && System.IO.File.Exists(fullExecutablePath);
    }

    public List<string> GetEmulatorsHoldingSavesForSystem(string systemSlug, string excludedEmulatorName)
    {
        var emulatorsHoldingSaves = new List<string>();

        foreach (string candidateEmulatorName in GetSupportedEmulators(systemSlug))
        {
            if (candidateEmulatorName == excludedEmulatorName)
            {
                continue;
            }

            string relativeSaveDirectory = LoadEmulatorMetadataFromDisk(candidateEmulatorName)?.ResolveSaveDirectoryForSystem(systemSlug);

            if (string.IsNullOrEmpty(relativeSaveDirectory))
            {
                continue;
            }

            string saveDirectory = Path.Combine(appInstance.configManager.SavesPath, candidateEmulatorName, relativeSaveDirectory.Replace('/', Path.DirectorySeparatorChar));

            if (Directory.Exists(saveDirectory) && Directory.EnumerateFiles(saveDirectory, "*", SearchOption.AllDirectories).Any())
            {
                emulatorsHoldingSaves.Add(GetEmulatorDisplayName(candidateEmulatorName));
            }
        }

        return emulatorsHoldingSaves;
    }

    public string GetEmulatorDisplayName(string emulatorName)
    {
        return LoadEmulatorMetadataFromDisk(emulatorName)?.Name ?? emulatorName;
    }

    public bool IsSelectedCoreInstalled(string emulatorName, string systemSlug)
    {
        var emulatorMetadata = LoadEmulatorMetadataFromDisk(emulatorName);
        string selectedCore = ResolveSelectedCore(emulatorMetadata, systemSlug);

        if (selectedCore == null)
        {
            return true;
        }

        string currentOperatingSystem = OS.GetName().ToLower();
        string coreRelativePath = emulatorMetadata.ResolveCoreRelativePath(selectedCore, currentOperatingSystem);

        if (coreRelativePath == null || emulatorMetadata.EmulatorDirName == null || !emulatorMetadata.EmulatorDirName.ContainsKey(currentOperatingSystem))
        {
            return true;
        }

        string emulatorInstallDirectory = Path.Combine(appInstance.configManager.EmulatorsPath, emulatorMetadata.EmulatorDirName[currentOperatingSystem]);

        return System.IO.File.Exists(Path.Combine(emulatorInstallDirectory, coreRelativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    public async Task<List<ReleaseOption>> GetAvailableReleases(string emulatorName)
    {
        var emulatorMetadata = LoadEmulatorMetadataFromDisk(emulatorName);
        string currentOperatingSystem = OS.GetName().ToLower();

        if (emulatorMetadata?.InstallRecipe == null || !emulatorMetadata.InstallRecipe.ContainsKey(currentOperatingSystem))
        {
            GD.PrintErr($"No install recipe found for {emulatorName} on {currentOperatingSystem}.");
            return new List<ReleaseOption>();
        }

        return await UniversalInstaller.ListReleases(emulatorMetadata.InstallRecipe[currentOperatingSystem]);
    }

    public bool IsEmulatorInstalling(string emulatorName)
    {
        return !string.IsNullOrEmpty(emulatorName) && installingEmulators.Contains(emulatorName);
    }

    public bool UninstallEmulator(string emulatorName)
    {
        if (string.IsNullOrEmpty(emulatorName))
        {
            return false;
        }

        if (IsEmulatorInstalling(emulatorName))
        {
            GD.PrintErr($"Cannot uninstall {emulatorName} while it is being installed.");
            return false;
        }

        if (IsEmulatorRunning)
        {
            GD.PrintErr($"Cannot uninstall {emulatorName} while an emulator is running.");
            return false;
        }

        var emulatorMetadata = LoadEmulatorMetadataFromDisk(emulatorName);

        if (emulatorMetadata == null)
        {
            GD.PrintErr($"Emulator recipe not found for: {emulatorName}");
            return false;
        }

        string emulatorInstallDirectory = appInstance.configManager.EmulatorsPath.PathJoin(emulatorName);

        if (!System.IO.Directory.Exists(emulatorInstallDirectory))
        {
            GD.Print($"{emulatorName} is not installed.");
            return false;
        }

        try
        {
            UniversalInstaller.ClearDirectoryPreservingPaths(emulatorInstallDirectory, emulatorMetadata.GetPreservePaths());
            GD.Print($"Uninstalled {emulatorName} (save data preserved).");
            return true;
        }

        catch (Exception exception)
        {
            GD.PrintErr($"Failed to uninstall {emulatorName}: {exception.Message}");
            return false;
        }
    }

    public async Task InstallEmulator(string emulatorName, ReleaseOption selectedRelease = null)
    {
        var emulatorMetadata = LoadEmulatorMetadataFromDisk(emulatorName);

        if (emulatorMetadata == null)
        {
            GD.PrintErr($"Emulator recipe not found for: {emulatorName}");
            EmitSignal(SignalName.EmulatorInstallationCompleted, emulatorName, false);
            return;
        }

        if (IsEmulatorRunning)
        {
            GD.PrintErr($"Cannot install {emulatorName} while an emulator is running. Close it and try again.");
            EmitSignal(SignalName.EmulatorInstallationCompleted, emulatorName, false);
            return;
        }

        if (!installingEmulators.Add(emulatorName))
        {
            GD.Print($"{emulatorName} is already being installed.");
            return;
        }

        string currentOperatingSystem = OS.GetName().ToLower();

        try
        {
            bool installationSucceeded = await UniversalInstaller.Install(appInstance, emulatorName, emulatorMetadata, currentOperatingSystem, selectedRelease);

            if (installationSucceeded)
            {
                GD.Print($"Successfully installed {emulatorName}.");
            }

            else
            {
                GD.PrintErr($"Failed to install {emulatorName}.");
            }

            installingEmulators.Remove(emulatorName);
            EmitSignal(SignalName.EmulatorInstallationCompleted, emulatorName, installationSucceeded);
        }

        catch (Exception exception)
        {
            GD.PrintErr($"Exception during install: {exception.Message}");
            installingEmulators.Remove(emulatorName);
            EmitSignal(SignalName.EmulatorInstallationCompleted, emulatorName, false);
        }
    }

    public static bool IsUsableFirmwarePath(string firmwarePath)
    {
        return !string.IsNullOrEmpty(firmwarePath)
            && Path.IsPathRooted(firmwarePath)
            && System.IO.File.Exists(firmwarePath);
    }

    private string ResolveFirmwarePath(GameSystem gameSystem)
    {
        if (gameSystem == null)
        {
            return null;
        }

        if (IsUsableFirmwarePath(gameSystem.PrefferedFirmware))
        {
            return Path.GetFullPath(gameSystem.PrefferedFirmware);
        }

        string biosDirectoryPath = appInstance.configManager.BiosPath.PathJoin(gameSystem.Slug);

        if (DirAccess.DirExistsAbsolute(biosDirectoryPath))
        {
            var biosFiles = DirAccess.GetFilesAt(biosDirectoryPath);

            if (biosFiles.Length > 0)
            {
                return Path.GetFullPath(biosDirectoryPath.PathJoin(biosFiles[0]));
            }
        }

        return null;
    }

    private void CopyBiosFilesToEmulatorDirectory(string biosSourceDirectoryPath, string emulatorBiosDirectoryPath)
    {
        if (!DirAccess.DirExistsAbsolute(biosSourceDirectoryPath))
        {
            return;
        }

        if (!DirAccess.DirExistsAbsolute(emulatorBiosDirectoryPath))
        {
            DirAccess.MakeDirRecursiveAbsolute(emulatorBiosDirectoryPath);
        }

        var biosFileNames = DirAccess.GetFilesAt(biosSourceDirectoryPath);

        foreach (var biosFileName in biosFileNames)
        {
            string sourceFilePath = Path.Combine(biosSourceDirectoryPath, biosFileName);
            string destinationFilePath = Path.Combine(emulatorBiosDirectoryPath, biosFileName);

            if (!Godot.FileAccess.FileExists(destinationFilePath))
            {
                System.IO.File.Copy(sourceFilePath, destinationFilePath, true);
            }
        }
    }

    private string StripBiosPathPlaceholderFromArguments(string launchArguments)
    {
        launchArguments = System.Text.RegularExpressions.Regex.Replace(launchArguments, @"\s*-+[-a-zA-Z0-9_]+\s+""?\{bios_path\}""?", "");
        launchArguments = launchArguments.Replace("\"{bios_path}\"", "").Replace("{bios_path}", "");
        return launchArguments;
    }

    private string AppendDynamicSettingsToArguments(string launchArguments, string emulatorName, EmulatorMeta emulatorMetadata)
    {
        string settingsArguments = BuildDynamicSettingsArguments(emulatorName, emulatorMetadata);

        if (launchArguments != null && launchArguments.Contains("{settings}"))
        {
            if (string.IsNullOrEmpty(settingsArguments))
            {
                return launchArguments.Replace("{settings} ", "").Replace(" {settings}", "").Replace("{settings}", "");
            }

            return launchArguments.Replace("{settings}", settingsArguments);
        }

        if (string.IsNullOrEmpty(settingsArguments))
        {
            return launchArguments;
        }

        return launchArguments + " " + settingsArguments;
    }

    public string ResolveSelectedCore(EmulatorMeta emulatorMetadata, string systemSlug)
    {
        var selectableCores = emulatorMetadata?.GetSelectableCores(systemSlug);

        if (selectableCores == null || selectableCores.Count == 0)
        {
            return null;
        }

        if (appInstance.configManager.PreferredCores.TryGetValue(systemSlug, out string preferredCore) && selectableCores.Contains(preferredCore))
        {
            return preferredCore;
        }

        return selectableCores[0];
    }

    private string ResolveSystemPlaceholder(string launchArguments, EmulatorMeta emulatorMetadata, string systemSlug)
    {
        if (launchArguments == null || !launchArguments.Contains("{system}"))
        {
            return launchArguments;
        }

        string currentOperatingSystem = OS.GetName().ToLower();
        string systemFragment = emulatorMetadata.ResolveCoreLaunchArgument(ResolveSelectedCore(emulatorMetadata, systemSlug), currentOperatingSystem) ?? "";

        if (string.IsNullOrEmpty(systemFragment) && emulatorMetadata.SystemFlags != null && !string.IsNullOrEmpty(systemSlug)
            && emulatorMetadata.SystemFlags.TryGetValue(systemSlug, out JsonElement mappedFragment))
        {
            systemFragment = EmulatorSettingField.ResolveOsScopedValue(mappedFragment, currentOperatingSystem) ?? "";
        }

        if (string.IsNullOrEmpty(systemFragment))
        {
            return launchArguments.Replace("{system} ", "").Replace(" {system}", "").Replace("{system}", "");
        }

        return launchArguments.Replace("{system}", systemFragment);
    }

    private string BuildDynamicSettingsArguments(string emulatorName, EmulatorMeta emulatorMetadata)
    {
        if (emulatorMetadata.SettingsFields == null)
        {
            return "";
        }

        var savedUserSettings = LoadEmulatorSettings(emulatorName);
        var settingsArgumentParts = new List<string>();

        foreach (var settingField in emulatorMetadata.SettingsFields)
        {
            if (string.IsNullOrEmpty(settingField.Id))
            {
                continue;
            }

            bool hasUserOverride = savedUserSettings.TryGetValue(settingField.Id, out JsonElement settingElement);

            if (settingField.Type == "boolean")
            {
                bool booleanSettingValue = settingField.DefaultValueBool;

                if (hasUserOverride && settingElement.ValueKind == JsonValueKind.True)
                {
                    booleanSettingValue = true;
                }

                if (hasUserOverride && settingElement.ValueKind == JsonValueKind.False)
                {
                    booleanSettingValue = false;
                }

                if (booleanSettingValue && !string.IsNullOrEmpty(settingField.LaunchArgTrue))
                {
                    settingsArgumentParts.Add(settingField.LaunchArgTrue);
                }

                else if (!booleanSettingValue && !string.IsNullOrEmpty(settingField.LaunchArgFalse))
                {
                    settingsArgumentParts.Add(settingField.LaunchArgFalse);
                }
            }

            else if (settingField.Type == "dropdown" || settingField.Type == "string")
            {
                string stringSettingValue = settingField.DefaultValueString;

                if (hasUserOverride && settingElement.ValueKind == JsonValueKind.String)
                {
                    stringSettingValue = settingElement.GetString();
                }

                if (!string.IsNullOrEmpty(stringSettingValue) && !string.IsNullOrEmpty(settingField.LaunchArgFormat))
                {
                    settingsArgumentParts.Add(settingField.LaunchArgFormat.Replace("{value}", stringSettingValue));
                }
            }
        }

        return string.Join(" ", settingsArgumentParts);
    }

    private string ApplyBiosArgumentsAndCopyFiles(string launchArguments, string firmwarePath, string emulatorInstallDirectory, EmulatorMeta emulatorMetadata, string currentOperatingSystem)
    {
        if (!string.IsNullOrEmpty(firmwarePath))
        {
            if (launchArguments.Contains("{bios_path}"))
            {
                launchArguments = launchArguments.Replace("{bios_path}", firmwarePath);
            }

            if (emulatorMetadata.EmulatorBiosPath != null && emulatorMetadata.EmulatorBiosPath.ContainsKey(currentOperatingSystem))
            {
                string biosSourceDirectoryPath = Path.GetDirectoryName(firmwarePath);
                string emulatorBiosDirectoryPath = Path.GetFullPath(Path.Combine(emulatorInstallDirectory, emulatorMetadata.EmulatorBiosPath[currentOperatingSystem]));
                CopyBiosFilesToEmulatorDirectory(biosSourceDirectoryPath, emulatorBiosDirectoryPath);
            }
        }

        else
        {
            launchArguments = StripBiosPathPlaceholderFromArguments(launchArguments);
        }

        return launchArguments;
    }

    private Process BuildAndStartEmulatorProcess(string executablePath, string launchArguments, string workingDirectory, EmulatorMeta emulatorMetadata = null)
    {
        ProcessStartInfo processStartInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = launchArguments,
            WorkingDirectory = workingDirectory,
            CreateNoWindow = true,
            UseShellExecute = false
        };

        ApplyLaunchEnvironment(processStartInfo, emulatorMetadata, workingDirectory);

        return Process.Start(processStartInfo);
    }

    private void ApplyLaunchEnvironment(ProcessStartInfo processStartInfo, EmulatorMeta emulatorMetadata, string emulatorInstallDirectory)
    {
        if (emulatorMetadata?.LaunchEnv == null)
        {
            return;
        }

        string currentOperatingSystem = OS.GetName().ToLower();

        if (!emulatorMetadata.LaunchEnv.TryGetValue(currentOperatingSystem, out var environmentVariables) || environmentVariables == null)
        {
            return;
        }

        foreach (var environmentVariable in environmentVariables)
        {
            string resolvedValue = environmentVariable.Value.Replace("{emulator_dir}", emulatorInstallDirectory);
            processStartInfo.Environment[environmentVariable.Key] = resolvedValue;
        }
    }

    public async void LaunchEmulatorWithGame(Game game)
    {
        if (game == null)
        {
            GD.PrintErr("Game object is null.");
            return;
        }

        if (game.System == null)
        {
            GD.PrintErr("Game.System is null.");
            return;
        }

        string mappedEmulatorName = GetMappedEmulator(game.System.Slug);

        if (string.IsNullOrEmpty(mappedEmulatorName))
        {
            GD.PrintErr($"No emulator mapped for system: {game.System.Name} ({game.System.Slug})");
            return;
        }

        var emulatorMetadata = LoadEmulatorMetadataFromDisk(mappedEmulatorName);

        if (emulatorMetadata == null)
        {
            GD.PrintErr($"Meta file not found for emulator: {mappedEmulatorName}");
            return;
        }

        try
        {
            string currentOperatingSystem = OS.GetName().ToLower();

            if (emulatorMetadata.EmulatorDirName == null || !emulatorMetadata.EmulatorDirName.ContainsKey(currentOperatingSystem))
            {
                GD.PrintErr("EmulatorDirName is missing or does not contain key for the current OS.");
                return;
            }

            string emulatorInstallDirectory = Path.Combine(appInstance.configManager.EmulatorsPath, emulatorMetadata.EmulatorDirName[currentOperatingSystem]);
            string fullExecutablePath = ResolveExecutablePath(emulatorMetadata, currentOperatingSystem, emulatorInstallDirectory);

            if (string.IsNullOrEmpty(fullExecutablePath))
            {
                GD.PrintErr("No executable_name or executable_regex entry for the current OS.");
                return;
            }

            SaveStore.LinkEmulatorSaveDirectories(appInstance, mappedEmulatorName, emulatorMetadata, currentOperatingSystem, game.System.Slug);

            string selectedCore = ResolveSelectedCore(emulatorMetadata, game.System.Slug);

            if (selectedCore != null && !await UniversalInstaller.EnsureCoreInstalled(appInstance, mappedEmulatorName, emulatorMetadata, selectedCore, currentOperatingSystem))
            {
                GD.PrintErr($"Cannot launch {game.Name}: the {selectedCore} core is not installed and could not be downloaded.");
                return;
            }

            if (game.Files == null || game.Files.Count == 0)
            {
                GD.PrintErr("Game has no files.");
                return;
            }

            string romFileName = game.Files[0].FileName;
            string fullRomPath = Path.GetFullPath(Path.Combine(appInstance.configManager.RomsPath, game.System.Slug, romFileName));

            string launchArguments = emulatorMetadata.LaunchArgsWithGame;

            if (string.IsNullOrEmpty(launchArguments))
            {
                GD.PrintErr("LaunchArgsWithGame is not defined in meta.json.");
                return;
            }

            launchArguments = launchArguments.Replace("{rom_path}", fullRomPath);
            launchArguments = ResolveSystemPlaceholder(launchArguments, emulatorMetadata, game.System.Slug);

            string firmwarePath = ResolveFirmwarePath(game.System);
            launchArguments = ApplyBiosArgumentsAndCopyFiles(launchArguments, firmwarePath, emulatorInstallDirectory, emulatorMetadata, currentOperatingSystem);
            launchArguments = AppendDynamicSettingsToArguments(launchArguments, mappedEmulatorName, emulatorMetadata);

            if (emulatorMetadata.SettingsFields != null)
            {
                foreach (var settingField in emulatorMetadata.SettingsFields)
                {
                    string hiddenConfigRelativePath = settingField.ResolveConfigFileRelativePath(currentOperatingSystem);
                    string hiddenConfigSection = settingField.ResolveConfigSection(currentOperatingSystem);
                    string hiddenConfigKey = settingField.ResolveConfigKey(currentOperatingSystem);

                    if (settingField.Type == "hidden" && !string.IsNullOrEmpty(hiddenConfigRelativePath) && !string.IsNullOrEmpty(hiddenConfigSection) && !string.IsNullOrEmpty(hiddenConfigKey))
                    {
                        string stringValue = settingField.DefaultValueString;

                        if (stringValue != null && stringValue.Contains("{game_id}"))
                        {
                            stringValue = stringValue.Replace("{game_id}", game.Id.ToString());
                        }

                        string configFilePath = Path.Combine(emulatorInstallDirectory, hiddenConfigRelativePath);
                        var updaters = new IConfigurationUpdater[] { new JsonConfigurationUpdater(), new BmlConfigurationUpdater(), new QtConfigurationUpdater(), new IniConfigurationUpdater() };

                        foreach (var updater in updaters)
                        {
                            if (updater.CanHandle(configFilePath))
                            {
                                updater.UpdateValue(configFilePath, hiddenConfigSection, hiddenConfigKey, stringValue, stringValue);
                                break;
                            }
                        }
                    }
                }
            }

            GameSystem currentGameSystem = appInstance.dataBus.systems.FirstOrDefault(s => s.Id == game.PlatformId);
            ApplyControllerMappings(emulatorMetadata, emulatorInstallDirectory, currentGameSystem);

            DateTime sessionStart = DateTime.UtcNow;

            GD.Print($"Launching {mappedEmulatorName} for {game.System.Slug}:\n  exe:  {fullExecutablePath}\n  args: {launchArguments}");

            if (appInstance.saveSyncManager != null)
            {
                await appInstance.saveSyncManager.SyncBeforeLaunch(game);
            }

            Process emulatorProcess = BuildAndStartEmulatorProcess(fullExecutablePath, launchArguments, emulatorInstallDirectory, emulatorMetadata);

            if (emulatorProcess != null)
            {
                activeEmulatorProcess = emulatorProcess;
                activeGame = game;
                activeSessionStart = sessionStart;
            }

            else
            {
                GD.PrintErr("Failed to start emulator process. Process.Start returned null.");
            }
        }

        catch (Exception exception)
        {
            GD.PrintErr($"An exception occurred while launching the emulator: {exception.Message}");
            GD.PrintErr($"Stack Trace: {exception.StackTrace}");
        }
    }

    public string GetEmulatorLaunchArgs(string emulatorName)
    {
        var availableEmulators = GetAllAvailableEmulators();

        if (availableEmulators.TryGetValue(emulatorName, out EmulatorMeta emulatorMetadata))
        {
            return emulatorMetadata.LaunchArgsWithoutGame;
        }

        return "";
    }

    public bool IsEmulatorRunning
    {
        get
        {
            if (activeEmulatorProcess != null && !activeEmulatorProcess.HasExited)
            {
                return true;
            }

            return false;
        }
    }

    public void CloseEmulator()
    {
        if (activeEmulatorProcess != null && !activeEmulatorProcess.HasExited)
        {
            activeEmulatorProcess.CloseMainWindow();

            if (!activeEmulatorProcess.WaitForExit(5000))
            {
                activeEmulatorProcess.Kill();
            }

        }
    }

    public void LaunchEmulatorWithoutGame(string emulatorName, GameSystem currentGameSystem)
    {
        if (string.IsNullOrEmpty(emulatorName))
        {
            GD.PrintErr("No emulator name provided.");
            return;
        }

        var emulatorMetadata = LoadEmulatorMetadataFromDisk(emulatorName);

        if (emulatorMetadata == null)
        {
            GD.PrintErr($"Meta file not found for emulator: {emulatorName}");
            return;
        }

        try
        {
            string currentOperatingSystem = OS.GetName().ToLower();

            if (emulatorMetadata.EmulatorDirName == null || !emulatorMetadata.EmulatorDirName.ContainsKey(currentOperatingSystem))
            {
                GD.PrintErr($"Incomplete meta.json for {emulatorName} on OS: {currentOperatingSystem}");
                return;
            }

            string emulatorInstallDirectory = appInstance.configManager.EmulatorsPath + emulatorMetadata.EmulatorDirName[currentOperatingSystem];
            string fullExecutablePath = ResolveExecutablePath(emulatorMetadata, currentOperatingSystem, emulatorInstallDirectory);

            if (string.IsNullOrEmpty(fullExecutablePath))
            {
                GD.PrintErr($"Incomplete meta.json for {emulatorName} on OS: {currentOperatingSystem}");
                return;
            }

            SaveStore.LinkEmulatorSaveDirectories(appInstance, emulatorName, emulatorMetadata, currentOperatingSystem, currentGameSystem?.Slug);

            string launchArguments = emulatorMetadata.LaunchArgsWithoutGame;
            launchArguments = ResolveSystemPlaceholder(launchArguments, emulatorMetadata, currentGameSystem?.Slug);

            if (currentGameSystem != null)
            {
                string firmwarePath = ResolveFirmwarePath(currentGameSystem);
                launchArguments = ApplyBiosArgumentsAndCopyFiles(launchArguments, firmwarePath, emulatorInstallDirectory, emulatorMetadata, currentOperatingSystem);
            }

            launchArguments = AppendDynamicSettingsToArguments(launchArguments, emulatorName, emulatorMetadata);

            Process emulatorProcess = BuildAndStartEmulatorProcess(fullExecutablePath, launchArguments, emulatorInstallDirectory, emulatorMetadata);

            if (emulatorProcess != null)
            {
                activeEmulatorProcess = emulatorProcess;

                emulatorProcess.EnableRaisingEvents = true;
                emulatorProcess.Exited += (sender, exitEventArgs) =>
                {
                    GD.Print("Emulator was closed.");
                };
            }
        }

        catch (Exception exception)
        {
            GD.PrintErr($"Failed to launch emulator: {exception.Message}");
        }
    }

    private static Dictionary<string, List<string>> BuildDefaultEmulatorMap()
    {
        return new Dictionary<string, List<string>>
        {
            {"ngc", new List<string>{"dolphin"}},
            {"wii", new List<string>{"dolphin"}},
            {"snes", new List<string>{"retroarch", "snes9x"}},
            {"n64", new List<string>{"retroarch", "gopher64"}},
            {"nes", new List<string>{"retroarch", "ares"}},
            {"gb", new List<string>{"retroarch", "mGBA"}},
            {"gbc", new List<string>{"retroarch", "mGBA"}},
            {"gba", new List<string>{"retroarch", "mGBA"}},
            {"nds", new List<string>{"retroarch", "melonDS"}},
            {"new-nintendo-3ds", new List<string>{"azahar"}},
            {"psx", new List<string>{"retroarch", "duckstation"}},
            {"ps2", new List<string>{"pcsx2"}},
            {"ps3", new List<string>{"rpcs3"}},
            {"ps4", new List<string>{"shadPS4"}},
            {"psp", new List<string>{"ppsspp", "retroarch"}},
            {"sega32", new List<string>{"retroarch", "ares"}},
            {"segacd", new List<string>{"retroarch", "ares"}},
            {"sms", new List<string>{"retroarch", "ares"}},
            {"genesis", new List<string>{"retroarch", "ares"}},
            {"dc", new List<string>{"flycast", "retroarch"}},
            {"saturn", new List<string>{"retroarch"}},
            {"game-gear", new List<string>{"retroarch"}},
            {"sg-1000", new List<string>{"retroarch"}},
            {"pce", new List<string>{"retroarch"}},
            {"pcfx", new List<string>{"retroarch"}},
            {"neogeoaes", new List<string>{"retroarch"}},
            {"neogeomvs", new List<string>{"retroarch"}},
            {"arcade", new List<string>{"retroarch"}},
            {"neo-geo-pocket", new List<string>{"retroarch"}},
            {"neo-geo-pocket-color", new List<string>{"retroarch"}},
            {"atari2600", new List<string>{"retroarch"}},
            {"atari5200", new List<string>{"retroarch"}},
            {"atari7800", new List<string>{"retroarch"}},
            {"lynx", new List<string>{"retroarch"}},
            {"jaguar", new List<string>{"retroarch"}},
            {"wonderswan", new List<string>{"retroarch"}},
            {"wonderswan-color", new List<string>{"retroarch"}},
            {"virtual-boy", new List<string>{"retroarch"}},
            {"3do", new List<string>{"retroarch"}},
            {"colecovision", new List<string>{"retroarch"}},
            {"intellivision", new List<string>{"retroarch"}},
            {"msx", new List<string>{"retroarch"}},
            {"msx2", new List<string>{"retroarch"}},
            {"c64", new List<string>{"retroarch"}},
            {"amiga", new List<string>{"retroarch"}},
            {"vectrex", new List<string>{"retroarch"}},
            {"nintendo-dsi", new List<string>{"retroarch"}}
        };
    }

    private void GenerateDefaultMaps()
    {
        var defaultPlatformToEmulatorMap = BuildDefaultEmulatorMap();

        try
        {
            string serializedMapJson = JsonSerializer.Serialize(defaultPlatformToEmulatorMap, RommJsonContext.Default.Options);
            using var emulatorMapFile = FileAccess.Open(emulatorMapFilePath, FileAccess.ModeFlags.Write);
            emulatorMapFile.StoreString(serializedMapJson);
        }

        catch (Exception exception)
        {
            GD.PrintErr($"Failed to generate default emulator map: {exception.Message}");
        }

        try
        {
            string serializedExecutableJson = JsonSerializer.Serialize(executableMapFilePath, RommJsonContext.Default.Options);
            using var executableMapFile = FileAccess.Open(executableMapFilePath, FileAccess.ModeFlags.Write);
            executableMapFile.StoreString(serializedExecutableJson);
        }

        catch (Exception exception)
        {
            GD.PrintErr($"Failed to generate default executable map: {exception.Message}");
        }
    }

    private static readonly bool SuspendControllerMapping = true;

    private void ApplyControllerMappings(EmulatorMeta emulatorMetadata, string emulatorInstallDirectory, GameSystem currentGameSystem)
    {
        if (SuspendControllerMapping)
        {
            GD.Print("Controller mapping is suspended; leaving emulator config untouched.");
            return;
        }

        if (emulatorMetadata.ControllerConfig == null)
        {
            return;
        }

        var controllerConfig = emulatorMetadata.ControllerConfig;
        var connectedControllers = controllerManager.GetConnectedControllers();
        int availableControllerCount = Math.Min(connectedControllers.Count, controllerConfig.MaxControllers);
        string configFilePath = Path.Combine(emulatorInstallDirectory, controllerConfig.ConfigFileRelativePath);

        GD.Print($"Applying controller mappings: {availableControllerCount} of {controllerConfig.MaxControllers} max controllers");

        if (availableControllerCount == 0)
        {
            GD.Print("No controllers detected; leaving shipped controller config untouched.");
            return;
        }

        if (controllerConfig.Format == "ini" && controllerConfig.ControllerSections != null)
        {
            ApplyIniControllerMappings(controllerConfig, connectedControllers, availableControllerCount, configFilePath, currentGameSystem);
        }

        else if (controllerConfig.Format == "json")
        {
            ApplyJsonControllerMappings(controllerConfig, connectedControllers, availableControllerCount, configFilePath);
        }
    }

    private void ApplyIniControllerMappings(ControllerConfig controllerConfig, List<ConnectedController> connectedControllers, int availableControllerCount, string configFilePath, GameSystem currentGameSystem)
    {
        var iniUpdater = new IniConfigurationUpdater();

        foreach (var sectionDef in controllerConfig.ControllerSections)
        {
            for (int portOffset = 0; portOffset < controllerConfig.MaxControllers; portOffset++)
            {
                int portNumber = sectionDef.PortStart + portOffset;
                string sectionName = sectionDef.SectionTemplate.Replace("{port}", portNumber.ToString());
                bool isControllerConnected = portOffset < availableControllerCount;

                if (!string.IsNullOrEmpty(sectionDef.TypeKey))
                {
                    string typeValue = isControllerConnected ? sectionDef.TypeConnected : sectionDef.TypeDisconnected;
                    iniUpdater.UpdateValue(configFilePath, sectionName, sectionDef.TypeKey, typeValue, typeValue);
                }

                if (!string.IsNullOrEmpty(sectionDef.DeviceKey))
                {
                    if (isControllerConnected)
                    {
                        string existingDevice = iniUpdater.ReadValue(configFilePath, sectionName, sectionDef.DeviceKey);

                        if (string.IsNullOrEmpty(existingDevice))
                        {
                            string deviceValue = sectionDef.DeviceTemplate
                                .Replace("{sdl_index}", connectedControllers[portOffset].ConnectionOrder.ToString())
                                .Replace("{controller_name}", connectedControllers[portOffset].ControllerName);
                            iniUpdater.UpdateValue(configFilePath, sectionName, sectionDef.DeviceKey, deviceValue, deviceValue);
                        }

                        else
                        {
                            GD.Print($"Keeping existing device for {sectionName}: {existingDevice}");
                        }
                    }

                    else if (!string.IsNullOrEmpty(sectionDef.DeviceDisconnected))
                    {
                        iniUpdater.UpdateValue(configFilePath, sectionName, sectionDef.DeviceKey, sectionDef.DeviceDisconnected, sectionDef.DeviceDisconnected);
                    }
                }

                if (isControllerConnected && sectionDef.Mappings != null)
                {
                    int sdlIndex = connectedControllers[portOffset].ConnectionOrder;
                    string controllerName = connectedControllers[portOffset].ControllerName;

                    foreach (var mapping in sectionDef.Mappings)
                    {
                        string resolvedValue = ResolvePlatformMacros(mapping.Value, currentGameSystem, controllerConfig, portOffset)
                            .Replace("{sdl_index}", sdlIndex.ToString())
                            .Replace("{controller_name}", controllerName);
                        iniUpdater.UpdateValue(configFilePath, sectionName, mapping.Key, resolvedValue, resolvedValue);
                    }
                }

                if (isControllerConnected && sectionDef.StaticValues != null)
                {
                    foreach (var staticEntry in sectionDef.StaticValues)
                    {
                        iniUpdater.UpdateValue(configFilePath, sectionName, staticEntry.Key, staticEntry.Value, staticEntry.Value);
                    }
                }
            }
        }
    }

    private string ResolvePlatformMacros(string value, GameSystem system, ControllerConfig config, int playerIndex)
    {
        if (string.IsNullOrEmpty(value) || system == null || config.PlatformLayout == null) return value;

        string result = value;

        foreach (var kvp in config.PlatformLayout)
        {
            string platformButton = kvp.Key;
            string defaultSdlInput = kvp.Value;
            string macro = $"{{Platform_{platformButton}}}";
            if (result.Contains(macro))
            {
                string mappedSdlInput = defaultSdlInput;
                if (appInstance.configManager.PlatformInputMappings.ContainsKey(system.Slug) &&
                    appInstance.configManager.PlatformInputMappings[system.Slug].ContainsKey(playerIndex) &&
                    appInstance.configManager.PlatformInputMappings[system.Slug][playerIndex].ContainsKey(platformButton))
                {
                    mappedSdlInput = appInstance.configManager.PlatformInputMappings[system.Slug][playerIndex][platformButton];
                }

                string emulatorSpecificString = "";
                if (!string.IsNullOrEmpty(mappedSdlInput) && config.SdlStringMap != null && config.SdlStringMap.ContainsKey(mappedSdlInput))
                {
                    emulatorSpecificString = config.SdlStringMap[mappedSdlInput];
                }

                else if (config.SdlStringMap != null && config.SdlStringMap.ContainsKey(defaultSdlInput))
                {
                    GD.Print($"Controller mapping '{platformButton}' -> '{mappedSdlInput}' is not valid for this emulator; using default '{defaultSdlInput}'.");
                    emulatorSpecificString = config.SdlStringMap[defaultSdlInput];
                }

                result = result.Replace(macro, emulatorSpecificString);
            }
        }

        return result;
    }

    private void ApplyJsonControllerMappings(ControllerConfig controllerConfig, List<ConnectedController> connectedControllers, int availableControllerCount, string configFilePath)
    {
        if (string.IsNullOrEmpty(controllerConfig.AssignmentKeyPath) && string.IsNullOrEmpty(controllerConfig.EnabledKeyPath))
        {
            return;
        }

        JsonNode rootNode = null;

        if (System.IO.File.Exists(configFilePath))
        {
            try
            {
                string existingJson = System.IO.File.ReadAllText(configFilePath);
                rootNode = JsonNode.Parse(existingJson);
            }

            catch { }
        }

        if (rootNode == null)
        {
            rootNode = new JsonObject();
        }

        if (!string.IsNullOrEmpty(controllerConfig.AssignmentKeyPath))
        {
            var assignmentArray = new JsonArray();

            for (int i = 0; i < controllerConfig.MaxControllers; i++)
            {
                if (i < availableControllerCount && !string.IsNullOrEmpty(controllerConfig.AssignmentTemplate))
                {
                    string assignmentValue = controllerConfig.AssignmentTemplate
                        .Replace("{sdl_index}", connectedControllers[i].ConnectionOrder.ToString())
                        .Replace("{controller_name}", connectedControllers[i].ControllerName);
                    assignmentArray.Add(assignmentValue);
                }

                else
                {
                    assignmentArray.Add("");
                }
            }

            SetNestedJsonValue(rootNode, controllerConfig.AssignmentKeyPath, assignmentArray);
        }

        if (!string.IsNullOrEmpty(controllerConfig.EnabledKeyPath))
        {
            var enabledArray = new JsonArray();

            for (int i = 0; i < controllerConfig.MaxControllers; i++)
            {
                enabledArray.Add(i < availableControllerCount);
            }

            SetNestedJsonValue(rootNode, controllerConfig.EnabledKeyPath, enabledArray);
        }

        System.IO.File.WriteAllText(configFilePath, rootNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private void SetNestedJsonValue(JsonNode rootNode, string dotSeparatedKeyPath, JsonNode valueToSet)
    {
        string[] pathSegments = dotSeparatedKeyPath.Split('.');
        JsonNode currentNode = rootNode;

        for (int i = 0; i < pathSegments.Length - 1; i++)
        {
            if (currentNode[pathSegments[i]] == null)
            {
                currentNode[pathSegments[i]] = new JsonObject();
            }

            currentNode = currentNode[pathSegments[i]];
        }

        currentNode[pathSegments.Last()] = valueToSet;
    }
}
