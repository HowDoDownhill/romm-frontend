using Godot;
using System;
using System.Collections.Generic;

public class MainSceneInputHandler
{
    private MainScene mainScene;
    private AppInstance appInstance;

    public bool isListeningForInput = false;
    public Action<string> inputListenCallback;

    public bool isListeningForEmulatorCloseHotkeys = false;
    public int expectedEmulatorCloseHotkeysCount = 0;
    public Godot.Collections.Array collectedEmulatorCloseHotkeys = new Godot.Collections.Array();
    private static readonly PackedScene settingsListEntryScene = GD.Load<PackedScene>("res://scenes/settings_list/settings_list_entry.tscn");

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

    private double secondsEmulatorCloseHotkeysHeld;
    private bool emulatorCloseRequestedThisHold;

    public MainSceneInputHandler(MainScene mainScene, AppInstance appInstance)
    {
        this.mainScene = mainScene;
        this.appInstance = appInstance;
    }

    public double EmulatorCloseHoldProgress
    {
        get
        {
            float requiredSeconds = appInstance.configManager.EmulatorCloseHoldSeconds;
            return requiredSeconds <= 0.0f ? 0.0 : Mathf.Clamp(secondsEmulatorCloseHotkeysHeld / requiredSeconds, 0.0, 1.0);
        }
    }

    public void UpdateEmulatorCloseHold(double delta)
    {
        if (appInstance.emulatorManager == null || !appInstance.emulatorManager.IsEmulatorRunning || !AreEmulatorCloseHotkeysHeld())
        {
            secondsEmulatorCloseHotkeysHeld = 0.0;
            emulatorCloseRequestedThisHold = false;
            return;
        }

        if (emulatorCloseRequestedThisHold)
        {
            return;
        }

        secondsEmulatorCloseHotkeysHeld += delta;

        if (secondsEmulatorCloseHotkeysHeld < appInstance.configManager.EmulatorCloseHoldSeconds)
        {
            return;
        }

        emulatorCloseRequestedThisHold = true;
        GD.Print($"[Input] emulator close hotkeys held for {appInstance.configManager.EmulatorCloseHoldSeconds}s; closing the emulator.");
        appInstance.emulatorManager.CloseEmulator();
    }

    private bool AreEmulatorCloseHotkeysHeld()
    {
        int hotkeyCount = appInstance.configManager.EmulatorCloseHotkeyCount;
        var hotkeyButtons = appInstance.configManager.EmulatorCloseHotkeys;

        if (hotkeyCount <= 0 || hotkeyButtons == null || hotkeyButtons.Count == 0)
        {
            return false;
        }

        int physicalDeviceId = ResolveCloseHotkeyDeviceId();

        if (physicalDeviceId < 0)
        {
            return false;
        }

        for (int hotkeyIndex = 0; hotkeyIndex < hotkeyCount && hotkeyIndex < hotkeyButtons.Count; hotkeyIndex++)
        {
            if (!Input.IsJoyButtonPressed(physicalDeviceId, (JoyButton)hotkeyButtons[hotkeyIndex].AsInt32()))
            {
                return false;
            }
        }

        return true;
    }

    private int ResolveCloseHotkeyDeviceId()
    {
        int layerDeviceId = appInstance.inputLayer?.ResolvePlayerOneDeviceId() ?? -1;

        if (layerDeviceId >= 0)
        {
            return layerDeviceId;
        }

        var connectedControllers = appInstance.controllerManager?.GetConnectedControllers();
        return connectedControllers != null && connectedControllers.Count > 0 ? connectedControllers[0].GodotDeviceId : -1;
    }

    private const string ControllerLayerOfferBody =
        "[b]Set up your controllers automatically?[/b]\n\n" +
        "RomM can present every controller to your emulators as an Xbox 360 pad, and configure each emulator's controls for you.\n\n" +
        "• Any controller works the same. PlayStation, Switch Pro, 8BitDo and others are translated to the Xbox 360 layout emulators expect.\n" +
        "• Player 1 stays Player 1, in the order you plug controllers in.\n" +
        "• Emulator controls are set up for you, instead of configuring each one by hand.\n\n" +
        "This writes controller settings into your emulator configuration files. You can turn it off at any time in Settings.";

    public void OfferControllerLayerIfNotYetAsked()
    {
        if (appInstance.configManager.ControllerMappingConsent != ConfigManager.ControllerMappingConsentUnasked)
        {
            return;
        }

        if (mainScene.changelogPanel == null || mainScene.panelStack.HasOpenPanel)
        {
            return;
        }

        mainScene.changelogPanel.ShowControllerLayerOffer(ControllerLayerOfferBody, "Set Up Controllers", "No Thanks");
    }

    public void OnControllerLayerOfferAccepted()
    {
        appInstance.configManager.SaveControllerMappingConsent(ConfigManager.ControllerMappingConsentAccepted);
        GD.Print("[InputLayer] the user accepted automatic controller mapping.");
    }

    public void OnControllerLayerOfferDeclined()
    {
        appInstance.configManager.SaveControllerMappingConsent(ConfigManager.ControllerMappingConsentDeclined);
        GD.Print("[InputLayer] the user declined automatic controller mapping; emulators keep their own controller settings.");
    }

    public void UpdateEmulatorCloseHotkeysBtnText()
    {
        if (mainScene.emulatorCloseHotkeysBtn != null)
        {
            var currentKeys = appInstance.configManager.EmulatorCloseHotkeys;
            var keyNames = new List<string>();
            for (int i = 0; i < appInstance.configManager.EmulatorCloseHotkeyCount; i++)
            {
                if (i < currentKeys.Count)
                {
                    keyNames.Add(((JoyButton)currentKeys[i].AsInt32()).ToString());
                }
            }
            mainScene.emulatorCloseHotkeysBtn.Text = $"Record Hotkeys [hold {string.Join(" + ", keyNames)} for {appInstance.configManager.EmulatorCloseHoldSeconds:0.#}s]";
        }
    }

    public string ConvertInputEventToStandardString(InputEvent @event)
    {
        if (@event is InputEventJoypadButton joyBtn)
        {
            switch (joyBtn.ButtonIndex)
            {
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

        var allMappings = appInstance.configManager.PlatformInputMappings.ContainsKey(systemSlug)
            ? appInstance.configManager.PlatformInputMappings[systemSlug]
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
                        appInstance.configManager.SavePlatformInputMapping(systemSlug, currentPlayerIndex, buttonName, detectedInput);
                        mappingBtn.GrabFocus();
                    };
                };

                row.AddChild(mappingBtn);

                var entry = settingsListEntryScene.Instantiate<MarginContainer>();
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
