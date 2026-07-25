using Godot;
using System;
using System.Collections.Generic;

public class MainSceneSettingsHandler
{
    private MainScene mainScene;
    private AppInstance appInstance;
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
        string nodeName = system.Name.Replace(" ", "");
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
                GenerateEmulatorSettingsUI(currentPref, allEmulators[currentPref], emulatorSettingsContainer);
                mainScene.InputHandler.GeneratePlatformControllerMappingsUI(system.Slug, allEmulators[currentPref], controllerMappingsContainer);
            }

            emulatorOptionButton.ItemSelected += (long index) =>
            {
                string selectedSlug = emulatorOptionButton.GetItemMetadata((int)index).AsString();
                appInstance.configManager.SavePreferredEmulator(system.Slug, selectedSlug);
                if (allEmulators.ContainsKey(selectedSlug))
                {
                    GenerateEmulatorSettingsUI(selectedSlug, allEmulators[selectedSlug], emulatorSettingsContainer);
                    mainScene.InputHandler.GeneratePlatformControllerMappingsUI(system.Slug, allEmulators[selectedSlug], controllerMappingsContainer);
                }
            };
        }
    }

    private void GenerateEmulatorSettingsUI(string slug, EmulatorMeta meta, Control parentContainer)
    {
        foreach (Node child in parentContainer.GetChildren())
        {
            child.QueueFree();
        }

        if (meta.SettingsFields == null) return;

        VBoxContainer vbox = new VBoxContainer();
        parentContainer.AddChild(vbox);

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

        string nodeName = sectionName.Replace(" ", "");
        var activePanel = mainScene.sectionOptionsContainer.GetNodeOrNull<Control>(nodeName);

        if (activePanel != null)
        {
            activePanel.Visible = true;
        }
    }
}
