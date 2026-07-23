using Godot;
using System.Collections.Generic;

public partial class ReleasePickerPopup : Control
{
    private Label titleLabel;
    private Label statusLabel;
    private VBoxContainer releaseListContainer;
    private ScrollContainer scrollContainer;

    private Color focusColor = new Color(1, 1, 1, 0.6f);

    public string EmulatorName { get; private set; }
    public List<ReleaseOption> Releases { get; private set; } = new List<ReleaseOption>();

    [Signal]
    public delegate void ReleaseChosenEventHandler(int index);

    public override void _Ready()
    {
        Visible = false;
        ZIndex = 200;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.7f) };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        var centerContainer = new CenterContainer();
        centerContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(centerContainer);

        var panel = new PanelContainer();
        panel.Material = GD.Load<ShaderMaterial>("res://assets/materials/mica_panel.tres");
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 1f),
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12
        };
        panel.AddThemeStyleboxOverride("panel", style);
        centerContainer.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 30);
        margin.AddThemeConstantOverride("margin_top", 25);
        margin.AddThemeConstantOverride("margin_right", 30);
        margin.AddThemeConstantOverride("margin_bottom", 25);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        margin.AddChild(vbox);

        titleLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        titleLabel.AddThemeFontSizeOverride("font_size", 22);
        vbox.AddChild(titleLabel);

        statusLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        statusLabel.AddThemeFontSizeOverride("font_size", 16);
        vbox.AddChild(statusLabel);

        scrollContainer = new ScrollContainer
        {
            FollowFocus = true,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(420, 0)
        };
        scrollContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
        vbox.AddChild(scrollContainer);

        releaseListContainer = new VBoxContainer();
        releaseListContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scrollContainer.AddChild(releaseListContainer);
    }

    public void ShowLoading(string emulatorDisplayName)
    {
        EmulatorName = emulatorDisplayName;
        Releases = new List<ReleaseOption>();
        titleLabel.Text = $"Install {emulatorDisplayName}";
        statusLabel.Text = "Fetching available versions...";
        statusLabel.Visible = true;
        ClearReleaseList();
        UpdateScrollHeight(0);
    }

    public void ShowError(string message)
    {
        statusLabel.Text = message;
        statusLabel.Visible = true;
        ClearReleaseList();
        UpdateScrollHeight(0);
    }

    public void Populate(string emulatorDisplayName, List<ReleaseOption> releases)
    {
        EmulatorName = emulatorDisplayName;
        Releases = releases;
        titleLabel.Text = $"Install {emulatorDisplayName}";
        statusLabel.Text = "Select a version";
        ClearReleaseList();

        for (int i = 0; i < releases.Count; i++)
        {
            var release = releases[i];
            int index = i;

            var entryBtn = new Button
            {
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(400, 44)
            };
            entryBtn.Text = string.IsNullOrEmpty(release.PublishedDate)
                ? release.VersionLabel
                : $"{release.VersionLabel}    ({release.PublishedDate})";
            StyleEntryButton(entryBtn);

            entryBtn.Pressed += () =>
            {
                EmitSignal(SignalName.ReleaseChosen, index);
            };

            releaseListContainer.AddChild(entryBtn);
        }

        UpdateScrollHeight(releases.Count);

        if (releaseListContainer.GetChildCount() > 0)
        {
            releaseListContainer.GetChild<Button>(0).GrabFocus();
        }
    }

    private void ClearReleaseList()
    {
        foreach (Node child in releaseListContainer.GetChildren())
        {
            releaseListContainer.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void UpdateScrollHeight(int entryCount)
    {
        float height = Mathf.Clamp(entryCount * 48, 0, 480);
        scrollContainer.CustomMinimumSize = new Vector2(420, height);
    }

    private void StyleEntryButton(Button entryBtn)
    {
        var btnStyle = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0), CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8 };
        var focusStyle = new StyleBoxFlat { BgColor = focusColor, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8 };

        entryBtn.AddThemeStyleboxOverride("normal", btnStyle);
        entryBtn.AddThemeStyleboxOverride("hover", focusStyle);
        entryBtn.AddThemeStyleboxOverride("focus", focusStyle);
        entryBtn.AddThemeStyleboxOverride("pressed", focusStyle);
    }

    public void ApplyTheme(Color accentColor)
    {
        accentColor.A = 0.65f;
        focusColor = accentColor;

        if (releaseListContainer != null)
        {
            foreach (Node child in releaseListContainer.GetChildren())
            {
                if (child is Button btn) StyleEntryButton(btn);
            }
        }
    }

    public void HandleInput(InputEvent @event)
    {
        if (@event.IsActionPressed("Back") || @event.IsActionPressed("ui_cancel"))
        {
            Visible = false;
            return;
        }

        if (@event.IsActionPressed("Select") || @event.IsActionPressed("ui_accept"))
        {
            int idx = GetFocusedIndex();

            if (idx >= 0)
            {
                EmitSignal(SignalName.ReleaseChosen, idx);
            }
            else if (releaseListContainer.GetChildCount() > 0)
            {
                releaseListContainer.GetChild<Button>(0).GrabFocus();
            }

            return;
        }

        int current = GetFocusedIndex();
        int count = releaseListContainer.GetChildCount();

        if (count == 0) return;

        if (current < 0)
        {
            releaseListContainer.GetChild<Button>(0).GrabFocus();
            return;
        }

        int target = current;

        if (@event.IsActionPressed("MoveUp", true) || @event.IsActionPressed("ui_up", true))
        {
            if (current > 0) target = current - 1;
        }
        else if (@event.IsActionPressed("MoveDown", true) || @event.IsActionPressed("ui_down", true))
        {
            if (current + 1 < count) target = current + 1;
        }

        if (target != current)
        {
            releaseListContainer.GetChild<Button>(target).GrabFocus();
        }
    }

    private int GetFocusedIndex()
    {
        var focused = GetViewport().GuiGetFocusOwner();
        if (focused == null) return -1;
        for (int i = 0; i < releaseListContainer.GetChildCount(); i++)
        {
            if (releaseListContainer.GetChild(i) == focused) return i;
        }
        return -1;
    }
}
