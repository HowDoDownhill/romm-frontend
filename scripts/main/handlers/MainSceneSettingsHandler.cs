using Godot;
using System;
using System.Collections.Generic;

public class MainSceneSettingsHandler
{
    private MainScene mainScene;
    private AppInstance appInstance;
    private static readonly char[] GodotReservedNodeNameCharacters = new[] { '/', ':', '@', '"', '%', '.', '$' };
    private static readonly PackedScene settingsListEntryScene = GD.Load<PackedScene>("res://scenes/settings_list/settings_list_entry.tscn");
    private static readonly PackedScene settingsSectionNavEntryScene = GD.Load<PackedScene>("res://scenes/settings_list/settings_section_nav_entry.tscn");

    public MainSceneSettingsHandler(MainScene mainScene, AppInstance appInstance)
    {
        this.mainScene = mainScene;
        this.appInstance = appInstance;
    }

    public Control SectionsTree => mainScene.settingsSectionsTree;

    public Control OptionsContainer => mainScene.sectionOptionsContainer;

    public void ToggleSettingsMenu()
    {
        mainScene.SectionHandler.ToggleSettings();
    }

    public void FocusFirstSettingsEntry()
    {
        CycleFocusInContainer(mainScene.settingsSectionsTree, 0);
    }

    public void SetupSettingsTree()
    {
        if (mainScene.settingsSectionsTree == null)
        {
            return;
        }

        foreach (Node child in mainScene.settingsSectionsTree.GetChildren())
        {
            mainScene.settingsSectionsTree.RemoveChild(child);
            child.QueueFree();
        }

        if (mainScene.sectionOptionsContainer != null)
        {
            foreach (Node child in mainScene.sectionOptionsContainer.GetChildren())
            {
                mainScene.sectionOptionsContainer.RemoveChild(child);
                child.QueueFree();
            }
        }

        AddNavEntry("General Settings", GenerateGeneralSettingsForm);
        AddNavEntry("Game List Settings", GenerateGameListSettingsForm);
        AddNavEntry("Input Settings", GenerateInputSettingsForm);

        AddNavHeader("Platform Settings");

        if (mainScene.GameListHandler.gameSystems != null)
        {
            foreach (var system in mainScene.GameListHandler.gameSystems)
            {
                AddNavEntry(system.Name, () => GeneratePlatformSettingsForm(system));
            }
        }

        var visibleForm = GetVisibleSettingsForm();
        if (visibleForm == null && mainScene.sectionOptionsContainer?.GetChildCount() > 0)
        {
            if (mainScene.sectionOptionsContainer.GetChild(0) is Control c)
            {
                c.Visible = true;
            }
        }
    }

    private void AddNavHeader(string headerName)
    {
        var label = new Label();
        label.Text = headerName;
        label.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f, 1f));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_bottom", 5);
        margin.AddThemeConstantOverride("margin_left", 5);
        margin.AddChild(label);

        mainScene.settingsSectionsTree.AddChild(margin);
    }

    private void AddNavEntry(string sectionName, Action generateFormAction)
    {
        if (generateFormAction != null) generateFormAction();

        var entry = settingsSectionNavEntryScene.Instantiate<SettingsSectionNavEntry>();
        entry.Setup(sectionName);
        entry.EntrySelected += OnSettingsTreeItemSelected;
        mainScene.settingsSectionsTree.AddChild(entry);
    }

    public Control GetVisibleSettingsForm()
    {
        if (mainScene.sectionOptionsContainer == null)
        {
            return null;
        }

        foreach (Node child in mainScene.sectionOptionsContainer.GetChildren())
        {
            if (child is Control c && c.Visible)
            {
                return c;
            }
        }

        return null;
    }

    public Control FindFirstFocusable(Node node)
    {
        return FocusCycler.FindFirstFocusable(node);
    }

    public void CycleFocusedOption(int direction)
    {
        var focusOwner = mainScene.GetViewport().GuiGetFocusOwner();

        if (focusOwner == null)
        {
            return;
        }

        if (focusOwner is OptionButton optBtn)
        {
            if (optBtn.ItemCount == 0)
            {
                return;
            }

            int newIdx = optBtn.Selected + direction;

            if (newIdx < 0)
            {
                newIdx = optBtn.ItemCount - 1;
            }

            if (newIdx >= optBtn.ItemCount)
            {
                newIdx = 0;
            }

            optBtn.Select(newIdx);
            optBtn.EmitSignal(OptionButton.SignalName.ItemSelected, newIdx);
        }
        else if (focusOwner is BaseButton btn && btn.ToggleMode)
        {
            bool toggled = !btn.ButtonPressed;

            if (direction == 1 && !btn.ButtonPressed)
            {
                toggled = true;
            }
            else if (direction == -1 && btn.ButtonPressed)
            {
                toggled = false;
            }
            else
            {
                return;
            }

            btn.ButtonPressed = toggled;
            btn.EmitSignal(BaseButton.SignalName.Toggled, btn.ButtonPressed);
        }
        else
        {
            SpinBox spinBox = focusOwner as SpinBox ?? focusOwner.GetParent() as SpinBox;
            if (spinBox != null)
            {
                double step = spinBox.Step > 0 ? spinBox.Step : 1;
                double newValue = spinBox.Value + (direction * step);

                if (newValue < spinBox.MinValue)
                {
                    newValue = spinBox.MaxValue;
                }

                if (newValue > spinBox.MaxValue)
                {
                    newValue = spinBox.MinValue;
                }

                spinBox.Value = newValue;
            }
        }
    }

    public void CycleFocusInContainer(Control container, int direction)
    {
        FocusCycler.Cycle(container, direction);
    }

    private void GenerateGeneralSettingsForm()
    {
        if (mainScene.sectionOptionsContainer == null)
        {
            return;
        }

        string nodeName = "GeneralSettings";

        if (mainScene.sectionOptionsContainer.HasNode(nodeName))
        {
            return;
        }

        MarginContainer formContainer = new MarginContainer();
        formContainer.Name = nodeName;
        formContainer.Visible = false;
        formContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        ScrollContainer scrollContainer = new ScrollContainer();
        scrollContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scrollContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        scrollContainer.FollowFocus = true;
        formContainer.AddChild(scrollContainer);

        VBoxContainer vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scrollContainer.AddChild(vbox);
        mainScene.sectionOptionsContainer.AddChild(formContainer);

        HBoxContainer fieldBox = new HBoxContainer();
        Label label = new Label();
        label.Text = "App Theme";
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        fieldBox.AddChild(label);

        CarouselButton themeOptionButton = new CarouselButton();
        int idx = 0;
        int selectedIdx = 0;
        string currentTheme = appInstance.configManager.AppTheme;

        foreach (var theme in System.Linq.Enumerable.Prepend(ConfigManager.Themes.Keys, ConfigManager.SystemThemeName))
        {
            themeOptionButton.AddItem(theme, idx);
            if (theme == currentTheme)
            {
                selectedIdx = idx;
            }
            idx++;
        }

        themeOptionButton.Select(selectedIdx);
        themeOptionButton.ItemSelected += (long index) =>
        {
            string selectedTheme = themeOptionButton.GetItemText((int)index);
            appInstance.configManager.SaveAppTheme(selectedTheme);
            mainScene.ApplyTheme();
        };

        fieldBox.AddChild(themeOptionButton);
        var entry = settingsListEntryScene.Instantiate<SettingsListEntry>();
        entry.GetNode<MarginContainer>("PanelContainer/ContentMargin").AddChild(fieldBox);
        vbox.AddChild(entry);

        HBoxContainer backgroundFieldBox = new HBoxContainer();
        Label backgroundLabel = new Label();
        backgroundLabel.Text = "Background Style";
        backgroundLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        backgroundFieldBox.AddChild(backgroundLabel);

        CarouselButton backgroundOptionButton = new CarouselButton();
        int selectedBackgroundIdx = 0;
        string currentBackground = appInstance.configManager.AppBackground;

        for (int i = 0; i < ConfigManager.BackgroundStyles.Length; i++)
        {
            backgroundOptionButton.AddItem(ConfigManager.BackgroundStyles[i].Name, i);
            if (ConfigManager.BackgroundStyles[i].Name == currentBackground)
            {
                selectedBackgroundIdx = i;
            }
        }

        backgroundOptionButton.Select(selectedBackgroundIdx);
        backgroundOptionButton.ItemSelected += (long index) =>
        {
            appInstance.configManager.SaveAppBackground(backgroundOptionButton.GetItemText((int)index));
            mainScene.ApplyTheme();
        };

        backgroundFieldBox.AddChild(backgroundOptionButton);
        var backgroundEntry = settingsListEntryScene.Instantiate<SettingsListEntry>();
        backgroundEntry.GetNode<MarginContainer>("PanelContainer/ContentMargin").AddChild(backgroundFieldBox);
        vbox.AddChild(backgroundEntry);

        HBoxContainer discreteGpuFieldBox = new HBoxContainer();
        Label discreteGpuLabel = new Label();
        discreteGpuLabel.Text = "Prefer discrete GPU";
        discreteGpuLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        discreteGpuFieldBox.AddChild(discreteGpuLabel);

        CheckButton discreteGpuCheckbox = new CheckButton();
        discreteGpuCheckbox.ButtonPressed = appInstance.configManager.PreferDiscreteGpu;

        discreteGpuCheckbox.Toggled += (bool toggledOn) =>
        {
            appInstance.configManager.SavePreferDiscreteGpu(toggledOn);
            DiscreteGpuPreference.RegisterWindowsGpuPreference(OS.GetExecutablePath(), toggledOn);
        };

        discreteGpuFieldBox.AddChild(discreteGpuCheckbox);
        var discreteGpuEntry = settingsListEntryScene.Instantiate<SettingsListEntry>();
        discreteGpuEntry.GetNode<MarginContainer>("PanelContainer/ContentMargin").AddChild(discreteGpuFieldBox);
        vbox.AddChild(discreteGpuEntry);
    }

    private void GenerateGameListSettingsForm()
    {
        if (mainScene.sectionOptionsContainer == null)
        {
            return;
        }

        string nodeName = "GameListSettings";

        if (mainScene.sectionOptionsContainer.HasNode(nodeName))
        {
            return;
        }

        MarginContainer formContainer = new MarginContainer();
        formContainer.Name = nodeName;
        formContainer.Visible = false;
        formContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        ScrollContainer scrollContainer = new ScrollContainer();
        scrollContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scrollContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        scrollContainer.FollowFocus = true;
        formContainer.AddChild(scrollContainer);

        VBoxContainer vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scrollContainer.AddChild(vbox);
        mainScene.sectionOptionsContainer.AddChild(formContainer);

        HBoxContainer fieldBox = new HBoxContainer();

        Label label = new Label();
        label.Text = "Hide games without box art";
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        fieldBox.AddChild(label);

        CheckButton checkbox = new CheckButton();
        checkbox.ButtonPressed = appInstance.configManager.HideGamesWithoutBoxArt;

        CheckButton showAllCheckbox = new CheckButton();
        showAllCheckbox.ButtonPressed = appInstance.configManager.ShowAllSystems;

        checkbox.Toggled += (bool toggledOn) =>
        {
            appInstance.configManager.SaveGameListSettings(toggledOn, showAllCheckbox.ButtonPressed);

            if (mainScene.GameListHandler.gameSystems != null && mainScene.GameListHandler.currentGameSystemIndex >= 0 && mainScene.GameListHandler.currentGameSystemIndex < mainScene.GameListHandler.gameSystems.Count)
            {
                mainScene.GameListHandler.SelectSystemByIndex(mainScene.GameListHandler.currentGameSystemIndex);
                mainScene.GameListHandler.OnSystemSelected(mainScene.GameListHandler.gameSystems[mainScene.GameListHandler.currentGameSystemIndex]);
            }
        };
        fieldBox.AddChild(checkbox);

        var entry1 = settingsListEntryScene.Instantiate<SettingsListEntry>();
        entry1.GetNode<MarginContainer>("PanelContainer/ContentMargin").AddChild(fieldBox);
        vbox.AddChild(entry1);

        HBoxContainer fieldBox2 = new HBoxContainer();
        Label label2 = new Label();
        label2.Text = "Show all systems";
        label2.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        fieldBox2.AddChild(label2);

        showAllCheckbox.Toggled += (bool toggledOn) =>
        {
            appInstance.configManager.SaveGameListSettings(checkbox.ButtonPressed, toggledOn);
            mainScene.GetCache();
            mainScene.GameListHandler.SelectSystemByIndex(0);

            Callable.From(() => {
                SetupSettingsTree();
                if (mainScene.settingsSectionsTree != null && mainScene.settingsSectionsTree.GetChildCount() > 1)
                {
                    if (mainScene.settingsSectionsTree.GetChild(1) is SettingsSectionNavEntry navEntry)
                    {
                        navEntry.GrabFocus();
                        OnSettingsTreeItemSelected(navEntry.SectionName);
                    }
                }
            }).CallDeferred();
        };
        fieldBox2.AddChild(showAllCheckbox);

        var entry2 = settingsListEntryScene.Instantiate<SettingsListEntry>();
        entry2.GetNode<MarginContainer>("PanelContainer/ContentMargin").AddChild(fieldBox2);
        vbox.AddChild(entry2);
    }

    private void GenerateInputSettingsForm()
    {
        if (mainScene.sectionOptionsContainer == null)
        {
            return;
        }

        string nodeName = "InputSettings";

        if (mainScene.sectionOptionsContainer.HasNode(nodeName))
        {
            return;
        }

        MarginContainer formContainer = new MarginContainer();
        formContainer.Name = nodeName;
        formContainer.Visible = false;
        formContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        ScrollContainer scrollContainer = new ScrollContainer();
        scrollContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scrollContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        scrollContainer.FollowFocus = true;
        formContainer.AddChild(scrollContainer);

        VBoxContainer vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scrollContainer.AddChild(vbox);
        mainScene.sectionOptionsContainer.AddChild(formContainer);

        HBoxContainer automaticMappingBox = new HBoxContainer();
        Label automaticMappingLabel = new Label();
        automaticMappingLabel.Text = "Automatic Controller Mapping";
        automaticMappingLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        automaticMappingBox.AddChild(automaticMappingLabel);

        Label automaticMappingStatus = new Label();
        automaticMappingStatus.Text = DescribeInputLayerAvailability();
        automaticMappingStatus.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        automaticMappingBox.AddChild(automaticMappingStatus);

        CheckButton automaticMappingCheckbox = new CheckButton();
        automaticMappingCheckbox.ButtonPressed = appInstance.configManager.ControllerMappingConsent == ConfigManager.ControllerMappingConsentAccepted;

        automaticMappingCheckbox.Toggled += (bool toggledOn) =>
        {
            appInstance.configManager.SaveControllerMappingConsent(toggledOn
                ? ConfigManager.ControllerMappingConsentAccepted
                : ConfigManager.ControllerMappingConsentDeclined);
            automaticMappingStatus.Text = DescribeInputLayerAvailability();
        };

        automaticMappingBox.AddChild(automaticMappingCheckbox);
        var automaticMappingEntry = settingsListEntryScene.Instantiate<SettingsListEntry>();
        automaticMappingEntry.GetNode<MarginContainer>("PanelContainer/ContentMargin").AddChild(automaticMappingBox);
        vbox.AddChild(automaticMappingEntry);

        HBoxContainer countBox = new HBoxContainer();
        Label countLabel = new Label();
        countLabel.Text = "Number of Emulator Close Hotkeys";
        countLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        countBox.AddChild(countLabel);

        SpinBox countSpin = new SpinBox();
        countSpin.MinValue = 1;
        countSpin.MaxValue = 10;
        countSpin.Value = appInstance.configManager.EmulatorCloseHotkeyCount;
        countBox.AddChild(countSpin);

        var entry1 = settingsListEntryScene.Instantiate<SettingsListEntry>();
        entry1.GetNode<MarginContainer>("PanelContainer/ContentMargin").AddChild(countBox);
        vbox.AddChild(entry1);

        HBoxContainer holdBox = new HBoxContainer();
        Label holdLabel = new Label();
        holdLabel.Text = "Seconds to Hold Before Closing";
        holdLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        holdBox.AddChild(holdLabel);

        SpinBox holdSpin = new SpinBox();
        holdSpin.MinValue = 0.5;
        holdSpin.MaxValue = 10.0;
        holdSpin.Step = 0.5;
        holdSpin.Value = appInstance.configManager.EmulatorCloseHoldSeconds;
        holdBox.AddChild(holdSpin);

        holdSpin.ValueChanged += (double holdSeconds) =>
        {
            appInstance.configManager.SaveEmulatorCloseHoldSeconds((float)holdSeconds);
            mainScene.InputHandler.UpdateEmulatorCloseHotkeysBtnText();
        };

        var holdEntry = settingsListEntryScene.Instantiate<SettingsListEntry>();
        holdEntry.GetNode<MarginContainer>("PanelContainer/ContentMargin").AddChild(holdBox);
        vbox.AddChild(holdEntry);

        mainScene.emulatorCloseHotkeysBtn = new Button();
        mainScene.InputHandler.UpdateEmulatorCloseHotkeysBtnText();

        mainScene.emulatorCloseHotkeysBtn.Pressed += () =>
        {
            mainScene.InputHandler.expectedEmulatorCloseHotkeysCount = (int)countSpin.Value;
            mainScene.InputHandler.collectedEmulatorCloseHotkeys.Clear();
            mainScene.InputHandler.isListeningForEmulatorCloseHotkeys = true;
            mainScene.emulatorCloseHotkeysBtn.Text = $"Listening... (0/{mainScene.InputHandler.expectedEmulatorCloseHotkeysCount})";
        };

        var entry2 = settingsListEntryScene.Instantiate<SettingsListEntry>();
        entry2.GetNode<MarginContainer>("PanelContainer/ContentMargin").AddChild(mainScene.emulatorCloseHotkeysBtn);
        vbox.AddChild(entry2);

        countSpin.ValueChanged += (double val) =>
        {
            int newCount = (int)val;
            appInstance.configManager.SaveInputSettings(newCount, appInstance.configManager.EmulatorCloseHotkeys);
            mainScene.InputHandler.UpdateEmulatorCloseHotkeysBtnText();
        };
    }

    private void GeneratePlatformSettingsForm(GameSystem system)
    {
        if (mainScene.sectionOptionsContainer == null) return;
        string nodeName = BuildSectionNodeName(system.Name);
        if (mainScene.sectionOptionsContainer.HasNode(nodeName)) return;

        MarginContainer formContainer = new MarginContainer();
        formContainer.Name = nodeName;
        formContainer.Visible = false;
        formContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        ScrollContainer scrollContainer = new ScrollContainer();
        scrollContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scrollContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        scrollContainer.FollowFocus = true;
        formContainer.AddChild(scrollContainer);

        VBoxContainer vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scrollContainer.AddChild(vbox);
        mainScene.sectionOptionsContainer.AddChild(formContainer);

        HBoxContainer prefEmulatorBox = new HBoxContainer();
        Label prefLabel = new Label();
        prefLabel.Text = "Preferred Emulator";
        prefLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        prefEmulatorBox.AddChild(prefLabel);

        CarouselButton emulatorOptionButton = new CarouselButton();
        prefEmulatorBox.AddChild(emulatorOptionButton);

        var entry = settingsListEntryScene.Instantiate<SettingsListEntry>();
        entry.GetNode<MarginContainer>("PanelContainer/ContentMargin").AddChild(prefEmulatorBox);
        vbox.AddChild(entry);

        MarginContainer emulatorSettingsContainer = new MarginContainer();
        vbox.AddChild(emulatorSettingsContainer);

        MarginContainer controllerMappingsContainer = new MarginContainer();
        vbox.AddChild(controllerMappingsContainer);

        List<string> supportedEmulators = appInstance.emulatorManager.GetSupportedEmulators(system.Slug);
        var allEmulators = appInstance.emulatorManager.GetAllAvailableEmulators();

        int idx = 0;
        int selectedIdx = 0;
        string currentPref = appInstance.configManager.PreferredEmulators.ContainsKey(system.Slug) ? appInstance.configManager.PreferredEmulators[system.Slug] : null;

        foreach (string emuSlug in supportedEmulators)
        {
            if (allEmulators.ContainsKey(emuSlug))
            {
                emulatorOptionButton.AddItem(allEmulators[emuSlug].Name, idx);
                emulatorOptionButton.SetItemMetadata(idx, emuSlug);

                if (emuSlug == currentPref || (currentPref == null && idx == 0))
                {
                    selectedIdx = idx;
                    currentPref = emuSlug;
                }
                idx++;
            }
        }

        if (idx == 0)
        {
            emulatorOptionButton.AddItem("No mapped emulator", 0);
            emulatorOptionButton.Disabled = true;
        }
        else
        {
            emulatorOptionButton.Select(selectedIdx);
            if (allEmulators.ContainsKey(currentPref))
            {
                GenerateEmulatorSettingsUI(currentPref, allEmulators[currentPref], emulatorSettingsContainer, system.Slug);
                mainScene.InputHandler.GeneratePlatformControllerMappingsUI(system.Slug, allEmulators[currentPref], controllerMappingsContainer);
            }

            emulatorOptionButton.ItemSelected += (long index) =>
            {
                string selectedSlug = emulatorOptionButton.GetItemMetadata((int)index).AsString();
                appInstance.configManager.SavePreferredEmulator(system.Slug, selectedSlug);
                if (allEmulators.ContainsKey(selectedSlug))
                {
                    GenerateEmulatorSettingsUI(selectedSlug, allEmulators[selectedSlug], emulatorSettingsContainer, system.Slug);
                    mainScene.InputHandler.GeneratePlatformControllerMappingsUI(system.Slug, allEmulators[selectedSlug], controllerMappingsContainer);
                }
            };
        }
    }

    private void GenerateEmulatorSettingsUI(string slug, EmulatorMeta meta, Control parentContainer, string systemSlug = null)
    {
        foreach (Node child in parentContainer.GetChildren())
        {
            child.QueueFree();
        }

        var selectableCores = meta.GetSelectableCores(systemSlug);
        var emulatorsHoldingSaves = systemSlug == null
            ? new List<string>()
            : appInstance.emulatorManager.GetEmulatorsHoldingSavesForSystem(systemSlug, slug);

        if (meta.SettingsFields == null && selectableCores.Count == 0 && emulatorsHoldingSaves.Count == 0) return;

        VBoxContainer vbox = new VBoxContainer();
        parentContainer.AddChild(vbox);

        if (emulatorsHoldingSaves.Count > 0)
        {
            AddSavesStayWithOtherEmulatorWarning(vbox, meta.Name, emulatorsHoldingSaves);
        }

        if (selectableCores.Count > 0)
        {
            AddCoreSelector(vbox, meta, systemSlug, selectableCores);
        }

        if (meta.SettingsFields == null) return;

        var userSettings = appInstance.emulatorManager.LoadEmulatorSettings(slug);

        foreach (var field in meta.SettingsFields)
        {
            if (string.IsNullOrEmpty(field.Id)) continue;

            bool hasValue = userSettings.TryGetValue(field.Id, out System.Text.Json.JsonElement element);

            HBoxContainer fieldBox = new HBoxContainer();
            Label label = new Label();
            label.Text = field.Label;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            fieldBox.AddChild(label);

            if (field.Type == "boolean")
            {
                CheckButton checkbox = new CheckButton();
                bool val = field.DefaultValueBool;
                if (hasValue && element.ValueKind == System.Text.Json.JsonValueKind.True) val = true;
                if (hasValue && element.ValueKind == System.Text.Json.JsonValueKind.False) val = false;
                checkbox.ButtonPressed = val;
                checkbox.Toggled += (bool toggledOn) => { appInstance.emulatorManager.SaveEmulatorSetting(slug, field.Id, toggledOn); };
                fieldBox.AddChild(checkbox);
            }
            else if (field.Type == "string")
            {
                LineEdit lineEdit = new LineEdit();
                lineEdit.CustomMinimumSize = new Vector2(200, 0);
                string val = field.DefaultValueString;
                if (hasValue && element.ValueKind == System.Text.Json.JsonValueKind.String) val = element.GetString();
                lineEdit.Text = val;
                lineEdit.TextChanged += (string newText) => { appInstance.emulatorManager.SaveEmulatorSetting(slug, field.Id, newText); };
                fieldBox.AddChild(lineEdit);
            }
            else if (field.Type == "dropdown")
            {
                CarouselButton optionButton = new CarouselButton();
                string val = field.DefaultValueString;
                if (hasValue && element.ValueKind == System.Text.Json.JsonValueKind.String) val = element.GetString();
                int idx = 0; int selectedIdx = 0;
                if (field.Options != null)
                {
                    foreach (var option in field.Options)
                    {
                        optionButton.AddItem(option.Key, idx);
                        optionButton.SetItemMetadata(idx, option.Value);
                        if (option.Value == val) selectedIdx = idx;
                        idx++;
                    }
                }
                optionButton.Select(selectedIdx);
                optionButton.ItemSelected += (long index) => {
                    string selectedValue = optionButton.GetItemMetadata((int)index).AsString();
                    appInstance.emulatorManager.SaveEmulatorSetting(slug, field.Id, selectedValue);
                };
                fieldBox.AddChild(optionButton);
            }

            var entry = settingsListEntryScene.Instantiate<SettingsListEntry>();
            entry.GetNode<MarginContainer>("PanelContainer/ContentMargin").AddChild(fieldBox);
            vbox.AddChild(entry);
        }
    }

    private void AddSavesStayWithOtherEmulatorWarning(VBoxContainer vbox, string selectedEmulatorDisplayName, List<string> emulatorsHoldingSaves)
    {
        Label warningLabel = new Label();
        warningLabel.Text = $"{string.Join(" and ", emulatorsHoldingSaves)} already has saves for this system. "
            + $"{selectedEmulatorDisplayName} keeps its own saves, so those will not carry over.";
        warningLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        warningLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        warningLabel.AddThemeColorOverride("font_color", new Color(1f, 0.78f, 0.35f, 1f));

        var warningEntry = settingsListEntryScene.Instantiate<SettingsListEntry>();
        warningEntry.GetNode<MarginContainer>("PanelContainer/ContentMargin").AddChild(warningLabel);
        vbox.AddChild(warningEntry);
    }

    private void AddCoreSelector(VBoxContainer vbox, EmulatorMeta meta, string systemSlug, List<string> selectableCores)
    {
        HBoxContainer coreBox = new HBoxContainer();
        Label coreLabel = new Label();
        coreLabel.Text = "Core";
        coreLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        coreBox.AddChild(coreLabel);

        CarouselButton coreOptionButton = new CarouselButton();
        string selectedCore = appInstance.emulatorManager.ResolveSelectedCore(meta, systemSlug);
        int selectedCoreIndex = 0;

        for (int coreIndex = 0; coreIndex < selectableCores.Count; coreIndex++)
        {
            coreOptionButton.AddItem(selectableCores[coreIndex], coreIndex);
            coreOptionButton.SetItemMetadata(coreIndex, selectableCores[coreIndex]);

            if (selectableCores[coreIndex] == selectedCore)
            {
                selectedCoreIndex = coreIndex;
            }
        }

        coreOptionButton.Select(selectedCoreIndex);

        coreOptionButton.ItemSelected += (long index) =>
        {
            appInstance.configManager.SavePreferredCore(systemSlug, coreOptionButton.GetItemMetadata((int)index).AsString());
        };

        coreBox.AddChild(coreOptionButton);

        var coreEntry = settingsListEntryScene.Instantiate<SettingsListEntry>();
        coreEntry.GetNode<MarginContainer>("PanelContainer/ContentMargin").AddChild(coreBox);
        vbox.AddChild(coreEntry);
    }

    private static string BuildSectionNodeName(string sectionName)
    {
        var nodeNameBuilder = new System.Text.StringBuilder();

        foreach (char sectionNameCharacter in sectionName ?? "")
        {
            if (!char.IsWhiteSpace(sectionNameCharacter) && Array.IndexOf(GodotReservedNodeNameCharacters, sectionNameCharacter) < 0)
            {
                nodeNameBuilder.Append(sectionNameCharacter);
            }
        }

        return nodeNameBuilder.Length == 0 ? "Section" : nodeNameBuilder.ToString();
    }

    private void OnSettingsTreeItemSelected(string sectionName)
    {
        if (mainScene.settingsSectionsTree == null || mainScene.sectionOptionsContainer == null)
        {
            return;
        }

        foreach (Node child in mainScene.sectionOptionsContainer.GetChildren())
        {
            if (child is Control control)
            {
                control.Visible = false;
            }
        }

        string nodeName = BuildSectionNodeName(sectionName);
        var activePanel = mainScene.sectionOptionsContainer.GetNodeOrNull<Control>(nodeName);

        if (activePanel != null)
        {
            activePanel.Visible = true;
        }
    }

    private string DescribeInputLayerAvailability()
    {
        if (appInstance.inputLayer == null)
        {
            return "unavailable";
        }

        if (appInstance.configManager.ControllerMappingConsent != ConfigManager.ControllerMappingConsentAccepted)
        {
            return "emulators use their own controller settings";
        }

        return appInstance.inputLayer.IsVirtualPadBackendAvailable
            ? "emulators will see one Xbox 360 pad per player"
            : appInstance.inputLayer.VirtualPadBackendUnavailableReason;
    }
}
