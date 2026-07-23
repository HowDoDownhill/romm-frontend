using Godot;
using System;
using System.Collections.Generic;

public class MainSceneInputHandler
{
    private MainScene _mainScene;
    private AppInstance _appInstance;

    public bool isListeningForInput = false;
    public Action<string> inputListenCallback;

    public bool isListeningForEmulatorCloseHotkeys = false;
    public int expectedEmulatorCloseHotkeysCount = 0;
    public Godot.Collections.Array collectedEmulatorCloseHotkeys = new Godot.Collections.Array();
    private static readonly PackedScene _settingsListEntryScene = GD.Load<PackedScene>("res://scenes/settings_list/settings_list_entry.tscn");

    public static readonly string[] StandardSdlInputs = new string[]
    {
        "FaceSouth", "FaceEast", "FaceWest", "FaceNorth",
        "LeftShoulder", "RightShoulder",
        "LeftTrigger", "RightTrigger",
        "DpadUp", "DpadDown", "DpadLeft", "DpadRight",
        "Back", "Start", "Guide",
        "LeftStick", "RightStick",
        "LeftStick_Up", "LeftStick_Down", "LeftStick_Left", "LeftStick_Right",
        "RightStick_Up", "RightStick_Down", "RightStick_Left", "RightStick_Right"
    };

    public MainSceneInputHandler(MainScene mainScene, AppInstance appInstance)
    {
        _mainScene = mainScene;
        _appInstance = appInstance;
    }

    public void UpdateEmulatorCloseHotkeysBtnText()
    {
        if (_mainScene.emulatorCloseHotkeysBtn != null)
        {
            var currentKeys = _appInstance.configManager.EmulatorCloseHotkeys;
            var keyNames = new List<string>();
            for (int i = 0; i < _appInstance.configManager.EmulatorCloseHotkeyCount; i++)
            {
                if (i < currentKeys.Count)
                {
                    keyNames.Add(((JoyButton)currentKeys[i].AsInt32()).ToString());
                }
            }
            _mainScene.emulatorCloseHotkeysBtn.Text = $"Record Hotkeys [{string.Join(", ", keyNames)}]";
        }
    }

    public string ConvertInputEventToStandardString(InputEvent @event)
    {
        if (@event is InputEventJoypadButton joyBtn)
        {
            switch (joyBtn.ButtonIndex)
            {
                // Godot names the face buttons Xbox-style, but the canonical vocabulary
                // here (StandardSdlInputs, and every emulator's sdl_string_map) is
                // positional. JoyButton.A/B/X/Y are South/East/West/North respectively;
                // returning "A".."Y" instead produced values no sdl_string_map contains,
                // which resolved to empty bindings and left face buttons dead.
                case JoyButton.A: return "FaceSouth";
                case JoyButton.B: return "FaceEast";
                case JoyButton.X: return "FaceWest";
                case JoyButton.Y: return "FaceNorth";
                case JoyButton.LeftShoulder: return "LeftShoulder";
                case JoyButton.RightShoulder: return "RightShoulder";
                case JoyButton.Back: return "Back";
                case JoyButton.Start: return "Start";
                case JoyButton.Guide: return "Guide";
                case JoyButton.LeftStick: return "LeftStick";
                case JoyButton.RightStick: return "RightStick";
                case JoyButton.DpadUp: return "DpadUp";
                case JoyButton.DpadDown: return "DpadDown";
                case JoyButton.DpadLeft: return "DpadLeft";
                case JoyButton.DpadRight: return "DpadRight";
            }
        }
        else if (@event is InputEventJoypadMotion joyMotion && Mathf.Abs(joyMotion.AxisValue) > 0.5f)
        {
            bool isPositive = joyMotion.AxisValue > 0;
            switch (joyMotion.Axis)
            {
                case JoyAxis.TriggerLeft: return "LeftTrigger";
                case JoyAxis.TriggerRight: return "RightTrigger";
                case JoyAxis.LeftX: return isPositive ? "LeftStick_Right" : "LeftStick_Left";
                case JoyAxis.LeftY: return isPositive ? "LeftStick_Down" : "LeftStick_Up";
                case JoyAxis.RightX: return isPositive ? "RightStick_Right" : "RightStick_Left";
                case JoyAxis.RightY: return isPositive ? "RightStick_Down" : "RightStick_Up";
            }
        }
        else if (@event is InputEventKey keyEvent)
        {
            return "Key_" + keyEvent.Keycode.ToString();
        }
        return null;
    }

    public void GeneratePlatformControllerMappingsUI(string systemSlug, EmulatorMeta meta, Control parentContainer)
    {
        foreach (Node child in parentContainer.GetChildren())
        {
            child.QueueFree();
        }

        if (meta.ControllerConfig == null || meta.ControllerConfig.PlatformLayout == null || meta.ControllerConfig.PlatformLayout.Count == 0)
        {
            return;
        }

        VBoxContainer vbox = new VBoxContainer();
        parentContainer.AddChild(vbox);

        vbox.AddChild(new HSeparator());

        Label title = new Label();
        title.Text = "Controller Mappings";
        title.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
        vbox.AddChild(title);

        int maxControllers = meta.ControllerConfig.MaxControllers > 0 ? meta.ControllerConfig.MaxControllers : 1;

        var allMappings = _appInstance.configManager.PlatformInputMappings.ContainsKey(systemSlug) 
            ? _appInstance.configManager.PlatformInputMappings[systemSlug] 
            : new Dictionary<int, Dictionary<string, string>>();

        for (int playerIndex = 0; playerIndex < maxControllers; playerIndex++)
        {
            int currentPlayerIndex = playerIndex;
            var currentMappings = allMappings.ContainsKey(currentPlayerIndex) ? allMappings[currentPlayerIndex] : new Dictionary<string, string>();

            VBoxContainer playerBox = new VBoxContainer();
            vbox.AddChild(playerBox);

            Label playerLabel = new Label();
            playerLabel.Text = $"Player {currentPlayerIndex + 1}";
            playerLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.8f, 1.0f));
            playerBox.AddChild(playerLabel);

            foreach (var kvp in meta.ControllerConfig.PlatformLayout)
            {
                string buttonName = kvp.Key;
                string defaultSdl = kvp.Value;

                HBoxContainer row = new HBoxContainer();
                Label lbl = new Label();
                lbl.Text = buttonName;
                lbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                row.AddChild(lbl);

                Button mappingBtn = new Button();
                string mappedInput = currentMappings.ContainsKey(buttonName) ? currentMappings[buttonName] : defaultSdl;
                mappingBtn.Text = string.IsNullOrEmpty(mappedInput) ? "Unmapped" : mappedInput;
                
                mappingBtn.Pressed += () =>
                {
                    mappingBtn.Text = "Listening...";
                    isListeningForInput = true;
                    inputListenCallback = (string detectedInput) =>
                    {
                        mappingBtn.Text = detectedInput;
                        _appInstance.configManager.SavePlatformInputMapping(systemSlug, currentPlayerIndex, buttonName, detectedInput);
                        mappingBtn.GrabFocus();
                    };
                };

                row.AddChild(mappingBtn);
                
                var entry = _settingsListEntryScene.Instantiate<MarginContainer>();
                entry.AddChild(row);
                playerBox.AddChild(entry);
            }

            if (playerIndex < maxControllers - 1)
            {
                playerBox.AddChild(new HSeparator());
            }
        }
    }
}
