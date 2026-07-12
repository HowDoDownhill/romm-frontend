using Godot;
using System;
using System.Collections.Generic;

public class MainSceneSettingsHandler
{
    private MainScene _mainScene;
    private AppInstance _appInstance;
    private static readonly PackedScene _settingsListEntryScene = GD.Load<PackedScene>("res://scenes/settings_list/settings_list_entry.tscn");
    private static readonly PackedScene _settingsSectionNavEntryScene = GD.Load<PackedScene>("res://scenes/settings_list/settings_section_nav_entry.tscn");

    public MainSceneSettingsHandler(MainScene mainScene, AppInstance appInstance)
    {
        _mainScene = mainScene;
        _appInstance = appInstance;
    }

    public void ToggleSettingsMenu()
    {
        if (_mainScene.settingsMenuContainer != null)
        {
            var gamesListContainer = _mainScene.gameList?.GetParent()?.GetParent<Control>();

            if (_mainScene.settingsMenuContainer.Visible)
            {
                _mainScene.settingsMenuContainer.Visible = false;

                if (_mainScene.settingsFooter != null)
                {
                    _mainScene.settingsFooter.Visible = false;
                }

                if (gamesListContainer != null)
                {
                    gamesListContainer.Visible = true;
                }

                if (_mainScene.gameListFooter != null)
                {
                    _mainScene.gameListFooter.Visible = (_mainScene.downloadsListContainer == null || !_mainScene.downloadsListContainer.Visible);
                }

                if (_mainScene.downloadsFooter != null)
                {
                    _mainScene.downloadsFooter.Visible = (_mainScene.downloadsListContainer != null && _mainScene.downloadsListContainer.Visible);
                }

                if (_mainScene.gameList != null)
                {
                    _mainScene.gameList.GrabFocus();
                    _mainScene.GameListHandler.OnGameSelected((long)_mainScene.gameList.Get("SelectedIndex"));
                }
            }
            else
            {
                _mainScene.settingsMenuContainer.Visible = true;

                if (_mainScene.settingsFooter != null)
                {
                    _mainScene.settingsFooter.Visible = true;
                }

                if (gamesListContainer != null)
                {
                    gamesListContainer.Visible = false;
                }

                if (_mainScene.gameListFooter != null)
                {
                    _mainScene.gameListFooter.Visible = false;
                }

                if (_mainScene.downloadsFooter != null)
                {
                    _mainScene.downloadsFooter.Visible = false;
                }

                CycleFocusInContainer(_mainScene.settingsSectionsTree, 0);
            }

            _mainScene.UpdateHeaderLabel();
        }
    }

    public void SetupSettingsTree()
    {
        if (_mainScene.settingsSectionsTree == null)
        {
            return;
        }

        foreach (Node child in _mainScene.settingsSectionsTree.GetChildren())
        {
            child.QueueFree();
        }
        
        if (_mainScene.sectionOptionsContainer != null)
        {
            foreach (Node child in _mainScene.sectionOptionsContainer.GetChildren())
            {
                child.QueueFree();
            }
        }

        AddNavEntry("General Settings", GenerateGeneralSettingsForm);
        AddNavEntry("Game List Settings", GenerateGameListSettingsForm);
        AddNavEntry("Input Settings", GenerateInputSettingsForm);
        
        AddNavHeader("Platform Settings");

        if (_mainScene.GameListHandler.gameSystems != null)
        {
            foreach (var system in _mainScene.GameListHandler.gameSystems)
            {
                AddNavEntry(system.Name, () => GeneratePlatformSettingsForm(system));
            }
        }
        
        // Hide all forms initially and show the first one if available
        var visibleForm = GetVisibleSettingsForm();
        if (visibleForm == null && _mainScene.sectionOptionsContainer?.GetChildCount() > 0)
        {
            if (_mainScene.sectionOptionsContainer.GetChild(0) is Control c)
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
        
        _mainScene.settingsSectionsTree.AddChild(margin);
    }

    private void AddNavEntry(string sectionName, Action generateFormAction)
    {
        if (generateFormAction != null) generateFormAction();

        var entry = _settingsSectionNavEntryScene.Instantiate<SettingsSectionNavEntry>();
        entry.Setup(sectionName);
        entry.EntrySelected += OnSettingsTreeItemSelected;
        _mainScene.settingsSectionsTree.AddChild(entry);
    }

    public Control GetVisibleSettingsForm()
    {
        if (_mainScene.sectionOptionsContainer == null)
        {
            return null;
        }

        foreach (Node child in _mainScene.sectionOptionsContainer.GetChildren())
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
        if (node is Control c && c.FocusMode != Control.FocusModeEnum.None && c.Visible)
        {
            return c;
        }

        foreach (Node child in node.GetChildren())
        {
            Control found = FindFirstFocusable(child);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    public void CycleFocusedOption(int direction)
    {
        var focusOwner = _mainScene.GetViewport().GuiGetFocusOwner();

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
        if (container == null)
        {
            return;
        }

        List<Control> focusableChildren = new List<Control>();
        GatherFocusableControls(container, focusableChildren);
        
        if (focusableChildren.Count == 0)
        {
            return;
        }

        var focusOwner = _mainScene.GetViewport().GuiGetFocusOwner();
        int currentIndex = focusOwner != null ? focusableChildren.IndexOf(focusOwner) : -1;
        
        if (currentIndex == -1)
        {
            focusableChildren[0].GrabFocus();
            return;
        }
        
        int nextIndex = currentIndex + direction;

        if (nextIndex < 0)
        {
            nextIndex = focusableChildren.Count - 1;
        }
        else if (nextIndex >= focusableChildren.Count)
        {
            nextIndex = 0;
        }

        focusableChildren[nextIndex].GrabFocus();
    }
    
    private void GatherFocusableControls(Node parent, List<Control> list)
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is Control c)
            {
                if (!c.Visible)
                {
                    continue;
                }

                if (c.FocusMode != Control.FocusModeEnum.None)
                {
                    list.Add(c);
                }
            }

            GatherFocusableControls(child, list);
        }
    }

    private void GenerateGeneralSettingsForm()
    {
        if (_mainScene.sectionOptionsContainer == null)
        {
            return;
        }

        string nodeName = "GeneralSettings";

        if (_mainScene.sectionOptionsContainer.HasNode(nodeName))
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
        _mainScene.sectionOptionsContainer.AddChild(formContainer);

        HBoxContainer fieldBox = new HBoxContainer();
        Label label = new Label();
        label.Text = "App Theme";
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        fieldBox.AddChild(label);

        OptionButton themeOptionButton = new OptionButton();
        int idx = 0;
        int selectedIdx = 0;
        string currentTheme = _appInstance.configManager.AppTheme;

        foreach (var theme in ConfigManager.Themes.Keys)
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
            _appInstance.configManager.SaveAppTheme(selectedTheme);
            _mainScene.ApplyTheme();
        };

        fieldBox.AddChild(themeOptionButton);
        var entry = _settingsListEntryScene.Instantiate<SettingsListEntry>();
        entry.GetNode<MarginContainer>("PanelContainer/ContentMargin").AddChild(fieldBox);
        vbox.AddChild(entry);
    }

    private void GenerateGameListSettingsForm()
    {
        if (_mainScene.sectionOptionsContainer == null)
        {
            return;
        }

        string nodeName = "GameListSettings";

        if (_mainScene.sectionOptionsContainer.HasNode(nodeName))
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
        _mainScene.sectionOptionsContainer.AddChild(formContainer);

        HBoxContainer fieldBox = new HBoxContainer();
        
        Label label = new Label();
        label.Text = "Hide games without box art";
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        fieldBox.AddChild(label);

        CheckButton checkbox = new CheckButton();
        checkbox.ButtonPressed = _appInstance.configManager.HideGamesWithoutBoxArt;
        
        CheckButton showAllCheckbox = new CheckButton();
        showAllCheckbox.ButtonPressed = _appInstance.configManager.ShowAllSystems;

        checkbox.Toggled += (bool toggledOn) => 
        {
            _appInstance.configManager.SaveGameListSettings(toggledOn, showAllCheckbox.ButtonPressed);

            if (_mainScene.GameListHandler.gameSystems != null && _mainScene.GameListHandler.currentGameSystemIndex >= 0 && _mainScene.GameListHandler.currentGameSystemIndex < _mainScene.GameListHandler.gameSystems.Count)
            {
                _mainScene.GameListHandler.SelectSystemByIndex(_mainScene.GameListHandler.currentGameSystemIndex); 
                _mainScene.GameListHandler.OnSystemSelected(_mainScene.GameListHandler.gameSystems[_mainScene.GameListHandler.currentGameSystemIndex]);
            }
        };
        fieldBox.AddChild(checkbox);
        
        var entry1 = _settingsListEntryScene.Instantiate<SettingsListEntry>();
        entry1.GetNode<MarginContainer>("PanelContainer/ContentMargin").AddChild(fieldBox);
        vbox.AddChild(entry1);

        HBoxContainer fieldBox2 = new HBoxContainer();
        Label label2 = new Label();
        label2.Text = "Show all systems";
        label2.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        fieldBox2.AddChild(label2);
        
        showAllCheckbox.Toggled += (bool toggledOn) => 
        {
            _appInstance.configManager.SaveGameListSettings(checkbox.ButtonPressed, toggledOn);
            _mainScene.GetCache();
            _mainScene.GameListHandler.SelectSystemByIndex(0);
            
            Callable.From(() => {
                SetupSettingsTree();
                if (_mainScene.settingsSectionsTree != null && _mainScene.settingsSectionsTree.GetChildCount() > 1)
                {
                    if (_mainScene.settingsSectionsTree.GetChild(1) is SettingsSectionNavEntry navEntry)
                    {
                        navEntry.GrabFocus();
                        OnSettingsTreeItemSelected(navEntry.SectionName);
                    }
                }
            }).CallDeferred();
        };
        fieldBox2.AddChild(showAllCheckbox);
        
        var entry2 = _settingsListEntryScene.Instantiate<SettingsListEntry>();
        entry2.GetNode<MarginContainer>("PanelContainer/ContentMargin").AddChild(fieldBox2);
        vbox.AddChild(entry2);
    }

    private void GenerateInputSettingsForm()
    {
        if (_mainScene.sectionOptionsContainer == null)
        {
            return;
        }

        string nodeName = "InputSettings";

        if (_mainScene.sectionOptionsContainer.HasNode(nodeName))
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
        _mainScene.sectionOptionsContainer.AddChild(formContainer);

        HBoxContainer countBox = new HBoxContainer();
        Label countLabel = new Label();
        countLabel.Text = "Number of Emulator Close Hotkeys";
        countLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        countBox.AddChild(countLabel);

        SpinBox countSpin = new SpinBox();
        countSpin.MinValue = 1;
        countSpin.MaxValue = 10;
        countSpin.Value = _appInstance.configManager.EmulatorCloseHotkeyCount;
        countBox.AddChild(countSpin);
        
        var entry1 = _settingsListEntryScene.Instantiate<SettingsListEntry>();
        entry1.GetNode<MarginContainer>("PanelContainer/ContentMargin").AddChild(countBox);
        vbox.AddChild(entry1);

        _mainScene.emulatorCloseHotkeysBtn = new Button();
        _mainScene.InputHandler.UpdateEmulatorCloseHotkeysBtnText(); // Assuming InputHandler is public on MainScene
        
        _mainScene.emulatorCloseHotkeysBtn.Pressed += () =>
        {
            _mainScene.InputHandler.expectedEmulatorCloseHotkeysCount = (int)countSpin.Value;
            _mainScene.InputHandler.collectedEmulatorCloseHotkeys.Clear();
            _mainScene.InputHandler.isListeningForEmulatorCloseHotkeys = true;
            _mainScene.emulatorCloseHotkeysBtn.Text = $"Listening... (0/{_mainScene.InputHandler.expectedEmulatorCloseHotkeysCount})";
        };
        
        var entry2 = _settingsListEntryScene.Instantiate<SettingsListEntry>();
        entry2.GetNode<MarginContainer>("PanelContainer/ContentMargin").AddChild(_mainScene.emulatorCloseHotkeysBtn);
        vbox.AddChild(entry2);

        countSpin.ValueChanged += (double val) =>
        {
            int newCount = (int)val;
            _appInstance.configManager.SaveInputSettings(newCount, _appInstance.configManager.EmulatorCloseHotkeys);
            _mainScene.InputHandler.UpdateEmulatorCloseHotkeysBtnText();
        };
    }

    private void GeneratePlatformSettingsForm(GameSystem system)
    {
        if (_mainScene.sectionOptionsContainer == null) return;
        string nodeName = system.Name.Replace(" ", "");
        if (_mainScene.sectionOptionsContainer.HasNode(nodeName)) return;

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
        _mainScene.sectionOptionsContainer.AddChild(formContainer);

        HBoxContainer prefEmulatorBox = new HBoxContainer();
        Label prefLabel = new Label();
        prefLabel.Text = "Preferred Emulator";
        prefLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        prefEmulatorBox.AddChild(prefLabel);

        OptionButton emulatorOptionButton = new OptionButton();
        prefEmulatorBox.AddChild(emulatorOptionButton);
        
        var entry = _settingsListEntryScene.Instantiate<SettingsListEntry>();
        entry.GetNode<MarginContainer>("PanelContainer/ContentMargin").AddChild(prefEmulatorBox);
        vbox.AddChild(entry);

        MarginContainer emulatorSettingsContainer = new MarginContainer();
        vbox.AddChild(emulatorSettingsContainer);

        MarginContainer controllerMappingsContainer = new MarginContainer();
        vbox.AddChild(controllerMappingsContainer);

        List<string> supportedEmulators = _appInstance.emulatorManager.GetSupportedEmulators(system.Slug);
        var allEmulators = _appInstance.emulatorManager.GetAllAvailableEmulators();

        int idx = 0;
        int selectedIdx = 0;
        string currentPref = _appInstance.configManager.PreferredEmulators.ContainsKey(system.Slug) ? _appInstance.configManager.PreferredEmulators[system.Slug] : null;

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
                _mainScene.InputHandler.GeneratePlatformControllerMappingsUI(system.Slug, allEmulators[currentPref], controllerMappingsContainer);
            }

            emulatorOptionButton.ItemSelected += (long index) =>
            {
                string selectedSlug = emulatorOptionButton.GetItemMetadata((int)index).AsString();
                _appInstance.configManager.SavePreferredEmulator(system.Slug, selectedSlug);
                if (allEmulators.ContainsKey(selectedSlug))
                {
                    GenerateEmulatorSettingsUI(selectedSlug, allEmulators[selectedSlug], emulatorSettingsContainer);
                    _mainScene.InputHandler.GeneratePlatformControllerMappingsUI(system.Slug, allEmulators[selectedSlug], controllerMappingsContainer);
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

        var userSettings = _appInstance.emulatorManager.LoadEmulatorSettings(slug);
        
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
                checkbox.Toggled += (bool toggledOn) => { _appInstance.emulatorManager.SaveEmulatorSetting(slug, field.Id, toggledOn); };
                fieldBox.AddChild(checkbox);
            }
            else if (field.Type == "string")
            {
                LineEdit lineEdit = new LineEdit();
                lineEdit.CustomMinimumSize = new Vector2(200, 0);
                string val = field.DefaultValueString;
                if (hasValue && element.ValueKind == System.Text.Json.JsonValueKind.String) val = element.GetString();
                lineEdit.Text = val;
                lineEdit.TextChanged += (string newText) => { _appInstance.emulatorManager.SaveEmulatorSetting(slug, field.Id, newText); };
                fieldBox.AddChild(lineEdit);
            }
            else if (field.Type == "dropdown")
            {
                OptionButton optionButton = new OptionButton();
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
                    _appInstance.emulatorManager.SaveEmulatorSetting(slug, field.Id, selectedValue);
                };
                fieldBox.AddChild(optionButton);
            }
            
            var entry = _settingsListEntryScene.Instantiate<SettingsListEntry>();
            entry.GetNode<MarginContainer>("PanelContainer/ContentMargin").AddChild(fieldBox);
            vbox.AddChild(entry);
        }
    }

    private void OnSettingsTreeItemSelected(string sectionName)
    {
        if (_mainScene.settingsSectionsTree == null || _mainScene.sectionOptionsContainer == null)
        {
            return;
        }

        foreach (Node child in _mainScene.sectionOptionsContainer.GetChildren())
        {
            if (child is Control control)
            {
                control.Visible = false;
            }
        }

        string nodeName = sectionName.Replace(" ", "");
        var activePanel = _mainScene.sectionOptionsContainer.GetNodeOrNull<Control>(nodeName);

        if (activePanel != null)
        {
            activePanel.Visible = true;
        }
    }
}
