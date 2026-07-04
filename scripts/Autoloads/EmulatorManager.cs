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
        return filePath.EndsWith(".ini", StringComparison.OrdinalIgnoreCase) ||
               filePath.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase) ||
               !filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
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

    [JsonPropertyName("emulator_dir_name")]
    public Dictionary<string, string> EmulatorDirName { get; set; }

    [JsonPropertyName("emulator_bios_path")]
    public Dictionary<string, string> EmulatorBiosPath { get; set; }

    [JsonPropertyName("relative_save_path")]
    public Dictionary<string, JsonElement> RelativeSavePath { get; set; }

    [JsonPropertyName("launch_args_with_game")]
    public string LaunchArgsWithGame { get; set; }

    [JsonPropertyName("launch_args_without_game")]
    public string LaunchArgsWithoutGame { get; set; }

    [JsonPropertyName("install_recipe")]
    public Dictionary<string, InstallRecipe> InstallRecipe { get; set; }

    [JsonPropertyName("settings_fields")]
    public List<EmulatorSettingField> SettingsFields { get; set; }

    [JsonPropertyName("controller_config")]
    public ControllerConfig ControllerConfig { get; set; }
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
    public string ConfigFileRelativePath { get; set; }

    [JsonPropertyName("config_section")]
    public string ConfigSection { get; set; }

    [JsonPropertyName("config_key")]
    public string ConfigKey { get; set; }

    [JsonPropertyName("options")]
    public Dictionary<string, string> Options { get; set; }

    [JsonPropertyName("default_value_bool")]
    public bool DefaultValueBool { get; set; }

    [JsonPropertyName("default_value_string")]
    public string DefaultValueString { get; set; }
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
}

public class ControllerConfig
{
    [JsonPropertyName("max_controllers")]
    public int MaxControllers { get; set; }

    [JsonPropertyName("config_file_relative_path")]
    public string ConfigFileRelativePath { get; set; }

    [JsonPropertyName("format")]
    public string Format { get; set; }

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

            if (targetSettingField != null && !string.IsNullOrEmpty(targetSettingField.ConfigFileRelativePath) && !string.IsNullOrEmpty(targetSettingField.ConfigSection) && !string.IsNullOrEmpty(targetSettingField.ConfigKey))
            {
                string currentOperatingSystem = OS.GetName().ToLower();

                if (emulatorMetadata.EmulatorDirName != null && emulatorMetadata.EmulatorDirName.ContainsKey(currentOperatingSystem))
                {
                    string targetEmulatorInstallDirectory = Path.Combine(appInstance.configManager.EmulatorsPath, emulatorMetadata.EmulatorDirName[currentOperatingSystem]);
                    string configurationFilePath = Path.Combine(targetEmulatorInstallDirectory, targetSettingField.ConfigFileRelativePath);

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

                        new IniConfigurationUpdater(),

                        new BmlConfigurationUpdater()
                    };

                    foreach (var updater in updaters)
                    {
                        if (updater.CanHandle(configurationFilePath))
                        {
                            updater.UpdateValue(configurationFilePath, targetSettingField.ConfigSection, targetSettingField.ConfigKey, stringValue, settingValue);
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

        if (emulatorMetadata.EmulatorDirName == null || emulatorMetadata.ExecutableName == null ||
            !emulatorMetadata.EmulatorDirName.ContainsKey(currentOperatingSystem) || !emulatorMetadata.ExecutableName.ContainsKey(currentOperatingSystem))
        {
            return false;
        }

        string emulatorInstallDirectory = appInstance.configManager.EmulatorsPath.PathJoin(emulatorName);
        string executableRelativePath = emulatorMetadata.ExecutableName[currentOperatingSystem];
        string fullExecutablePath = Path.GetFullPath(Path.Combine(emulatorInstallDirectory, executableRelativePath));

        return FileAccess.FileExists(fullExecutablePath);
    }

    public async Task InstallEmulator(string emulatorName)
    {
        var emulatorMetadata = LoadEmulatorMetadataFromDisk(emulatorName);

        if (emulatorMetadata == null)
        {
            GD.PrintErr($"Emulator recipe not found for: {emulatorName}");
            EmitSignal(SignalName.EmulatorInstallationCompleted, emulatorName, false);
            return;
        }

        string currentOperatingSystem = OS.GetName().ToLower();

        try
        {
            bool installationSucceeded = await UniversalInstaller.Install(appInstance, emulatorName, emulatorMetadata, currentOperatingSystem);

            if (installationSucceeded)
            {
                GD.Print($"Successfully installed {emulatorName}.");
            }

            else
            {
                GD.PrintErr($"Failed to install {emulatorName}.");
            }

            EmitSignal(SignalName.EmulatorInstallationCompleted, emulatorName, installationSucceeded);
        }

        catch (Exception exception)
        {
            GD.PrintErr($"Exception during install: {exception.Message}");
            EmitSignal(SignalName.EmulatorInstallationCompleted, emulatorName, false);
        }
    }

    private string ResolveFirmwarePath(GameSystem gameSystem)
    {
        if (gameSystem == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(gameSystem.PrefferedFirmware))
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
        if (emulatorMetadata.SettingsFields == null)
        {
            return launchArguments;
        }

        var savedUserSettings = LoadEmulatorSettings(emulatorName);

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
                    launchArguments += " " + settingField.LaunchArgTrue;
                }

                else if (!booleanSettingValue && !string.IsNullOrEmpty(settingField.LaunchArgFalse))
                {
                    launchArguments += " " + settingField.LaunchArgFalse;
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
                    launchArguments += " " + settingField.LaunchArgFormat.Replace("{value}", stringSettingValue);
                }
            }
        }

        return launchArguments;
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

    private Process BuildAndStartEmulatorProcess(string executablePath, string launchArguments, string workingDirectory)
    {
        ProcessStartInfo processStartInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = launchArguments,
            WorkingDirectory = workingDirectory,
            CreateNoWindow = true,
            UseShellExecute = false
        };

        return Process.Start(processStartInfo);
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


            if (emulatorMetadata.ExecutableName == null || !emulatorMetadata.ExecutableName.ContainsKey(currentOperatingSystem))
            {
                GD.PrintErr("ExecutableName is missing or does not contain key for the current OS.");
                return;
            }

            string emulatorInstallDirectory = Path.Combine(appInstance.configManager.EmulatorsPath, emulatorMetadata.EmulatorDirName[currentOperatingSystem]);
            string executableRelativePath = emulatorMetadata.ExecutableName[currentOperatingSystem];
            string fullExecutablePath = Path.Combine(emulatorInstallDirectory, executableRelativePath);

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

            string firmwarePath = ResolveFirmwarePath(game.System);
            launchArguments = ApplyBiosArgumentsAndCopyFiles(launchArguments, firmwarePath, emulatorInstallDirectory, emulatorMetadata, currentOperatingSystem);
            launchArguments = AppendDynamicSettingsToArguments(launchArguments, mappedEmulatorName, emulatorMetadata);

            if (emulatorMetadata.SettingsFields != null)
            {
                foreach (var settingField in emulatorMetadata.SettingsFields)
                {
                    if (settingField.Type == "hidden" && !string.IsNullOrEmpty(settingField.ConfigFileRelativePath) && !string.IsNullOrEmpty(settingField.ConfigSection) && !string.IsNullOrEmpty(settingField.ConfigKey))
                    {
                        string stringValue = settingField.DefaultValueString;

                        if (stringValue != null && stringValue.Contains("{game_id}"))
                        {
                            stringValue = stringValue.Replace("{game_id}", game.Id.ToString());
                        }

                        string configFilePath = Path.Combine(emulatorInstallDirectory, settingField.ConfigFileRelativePath);
                        var updaters = new IConfigurationUpdater[] { new JsonConfigurationUpdater(), new IniConfigurationUpdater(), new BmlConfigurationUpdater() };

                        foreach (var updater in updaters)
                        {
                            if (updater.CanHandle(configFilePath))
                            {
                                updater.UpdateValue(configFilePath, settingField.ConfigSection, settingField.ConfigKey, stringValue, stringValue);
                                break;
                            }
                        }
                    }
                }
            }

            ApplyControllerMappings(emulatorMetadata, emulatorInstallDirectory);

            DateTime sessionStart = DateTime.UtcNow;

            if (appInstance.saveSyncManager != null)
            {
                await appInstance.saveSyncManager.SyncBeforeLaunch(game);
            }

            Process emulatorProcess = BuildAndStartEmulatorProcess(fullExecutablePath, launchArguments, emulatorInstallDirectory);

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

            if (emulatorMetadata.EmulatorDirName == null || emulatorMetadata.ExecutableName == null ||
                !emulatorMetadata.EmulatorDirName.ContainsKey(currentOperatingSystem) || !emulatorMetadata.ExecutableName.ContainsKey(currentOperatingSystem))
            {
                GD.PrintErr($"Incomplete meta.json for {emulatorName} on OS: {currentOperatingSystem}");
                return;
            }

            string emulatorInstallDirectory = appInstance.configManager.EmulatorsPath + emulatorMetadata.EmulatorDirName[currentOperatingSystem];
            string executableRelativePath = emulatorMetadata.ExecutableName[currentOperatingSystem];
            string fullExecutablePath = Path.Join(emulatorInstallDirectory, executableRelativePath);

            string launchArguments = emulatorMetadata.LaunchArgsWithoutGame;

            if (currentGameSystem != null)
            {
                string firmwarePath = ResolveFirmwarePath(currentGameSystem);
                launchArguments = ApplyBiosArgumentsAndCopyFiles(launchArguments, firmwarePath, emulatorInstallDirectory, emulatorMetadata, currentOperatingSystem);
            }

            launchArguments = AppendDynamicSettingsToArguments(launchArguments, emulatorName, emulatorMetadata);

            Process emulatorProcess = BuildAndStartEmulatorProcess(fullExecutablePath, launchArguments, emulatorInstallDirectory);

            if (emulatorProcess != null)
            {
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

    private void GenerateDefaultMaps()
    {
        var defaultPlatformToEmulatorMap = new Dictionary<string, List<string>>
        {
            {"ngc", new List<string>{"dolphin"}},
            {"wii", new List<string>{"dolphin"}},
            {"snes", new List<string>{"snes9x"}},
            {"n64", new List<string>{"gopher64"}},
            {"nes", new List<string>{"nestopia"}},
            {"gb", new List<string>{"mGBA"}},
            {"gba", new List<string>{"mGBA"}},
            {"nds", new List<string>{"melonDS"}},
            {"psx", new List<string>{"duckstation"}},
            {"ps2", new List<string>{"pcsx2"}},
            {"ps3", new List<string>{"rpcs3"}},
            {"ps4", new List<string>{"shadPS4"}},
            {"psp", new List<string>{"ppsspp"}},
            {"sega32", new List<string>{"ares"}},
            {"segacd", new List<string>{"ares"}},
            {"sms", new List<string>{"ares"}},
            {"genesis", new List<string>{"ares"}},
            {"dc", new List<string>{"flycast"}}
        };

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

    private void ApplyControllerMappings(EmulatorMeta emulatorMetadata, string emulatorInstallDirectory)
    {
        if (emulatorMetadata.ControllerConfig == null)
        {
            return;
        }

        var controllerConfig = emulatorMetadata.ControllerConfig;
        var connectedControllers = controllerManager.GetConnectedControllers();
        int availableControllerCount = Math.Min(connectedControllers.Count, controllerConfig.MaxControllers);
        string configFilePath = Path.Combine(emulatorInstallDirectory, controllerConfig.ConfigFileRelativePath);

        GD.Print($"Applying controller mappings: {availableControllerCount} of {controllerConfig.MaxControllers} max controllers");

        if (controllerConfig.Format == "ini" && controllerConfig.ControllerSections != null)
        {
            ApplyIniControllerMappings(controllerConfig, connectedControllers, availableControllerCount, configFilePath);
        }

        else if (controllerConfig.Format == "json")
        {
            ApplyJsonControllerMappings(controllerConfig, connectedControllers, availableControllerCount, configFilePath);
        }
    }

    private void ApplyIniControllerMappings(ControllerConfig controllerConfig, List<ConnectedController> connectedControllers, int availableControllerCount, string configFilePath)
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
                        string deviceValue = sectionDef.DeviceTemplate
                            .Replace("{sdl_index}", connectedControllers[portOffset].ConnectionOrder.ToString())
                            .Replace("{controller_name}", connectedControllers[portOffset].ControllerName);
                        iniUpdater.UpdateValue(configFilePath, sectionName, sectionDef.DeviceKey, deviceValue, deviceValue);
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
                        string resolvedValue = mapping.Value
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

