using Godot;
using System;
using System.Collections.Generic;

public partial class UiDemo : Node
{
    private const string NextSystemAction = "CylceSystemUp";
    private const double MaximumHoldSeconds = 3.0;
    private const double MinimumHoldSeconds = 0.5;

    private readonly Queue<(double At, Action Step)> timeline = new Queue<(double, Action)>();

    private MainScene mainScene;
    private double elapsedSeconds;
    private double scheduleCursor;
    private bool holdingNextSystem;
    private double holdStartedAt;

    public void Begin(MainScene owner)
    {
        mainScene = owner;
        mainScene.IsAutomatedInputRunning = true;
        GD.Print($"[Demo] started at frame {Engine.GetFramesDrawn()}");

        At(0.0, () =>
        {
            RegisterControllerInput();
            mainScene.PreviewAppearance("Midnight", "Horizon");
            mainScene.SetGameListView(0);
            mainScene.ActiveGameList?.GrabFocus();
        });

        Wait(2.0);
        Repeat(6, 0.55, () => PressAndRelease("ui_down"));
        Wait(0.8);
        Repeat(3, 1.3, () => PressAndRelease(NextSystemAction));
        Wait(0.6);
        Repeat(4, 0.5, () => PressAndRelease("ui_down"));

        Wait(0.8);
        At(0.0, BeginNextSystemHold);
        Wait(1.6);
        Tap("ui_down");
        Wait(0.5);
        Tap("ui_right");
        Wait(0.5);
        Tap("ui_right");
        Wait(0.7);
        Tap("ui_accept");

        Wait(1.4);
        Repeat(5, 0.5, () => PressAndRelease("ui_down"));

        Wait(0.8);
        Tap("ToggleSettings");
        Wait(0.9);
        Repeat(5, 0.45, () => PressAndRelease("ui_down"));
        Wait(0.8);
        Tap("Back");

        Wait(1.2);
        At(0.0, () =>
        {
            mainScene.SetGameListView(1);
            mainScene.ActiveGameList?.GrabFocus();
        });
        Wait(1.2);
        Repeat(3, 0.45, () => PressAndRelease("ui_right"));
        Repeat(2, 0.5, () => PressAndRelease("ui_down"));
        Tap("ui_left");
        Wait(0.9);
        At(0.0, () => mainScene.PreviewAppearance("Amethyst", "Silk"));
        Wait(1.8);
        Repeat(3, 0.5, () => PressAndRelease("ui_down"));
        Wait(0.6);
        Tap(NextSystemAction);
        Wait(1.4);
        Repeat(2, 0.45, () => PressAndRelease("ui_right"));

        Wait(1.0);
        At(0.0, () =>
        {
            mainScene.SetGameListView(2);
            mainScene.ActiveGameList?.GrabFocus();
        });
        Wait(1.0);
        Repeat(8, 0.3, () => PressAndRelease("ui_down"));
        Wait(0.6);
        Repeat(2, 0.8, () => PressAndRelease("ui_right"));
        Wait(0.6);
        At(0.0, () => mainScene.PreviewAppearance("Obsidian", "Mesh"));
        Wait(1.8);
        Repeat(4, 0.4, () => PressAndRelease("ui_down"));

        Wait(1.0);
        At(0.0, () =>
        {
            mainScene.PreviewAppearance("Graphite", "Silk");
            mainScene.SetGameListView(0);
            mainScene.ActiveGameList?.GrabFocus();
        });
        Wait(1.2);
        Repeat(4, 0.5, () => PressAndRelease("ui_down"));

        Wait(1.0);
        At(0.0, () => mainScene.SectionHandler.ShowSection(MainSceneSectionHandler.Section.Settings));
        Wait(1.8);
        Tap("ui_down");
        Wait(2.4);
        Tap("Back");

        Wait(1.4);
        At(0.0, () => mainScene.PreviewAppearance("Midnight", "Horizon"));
        Wait(1.2);
        Repeat(3, 0.6, () => PressAndRelease("ui_up"));
        Wait(2.5);
        At(0.0, Finish);
    }

    private void At(double delay, Action step)
    {
        scheduleCursor += delay;
        timeline.Enqueue((scheduleCursor, step));
    }

    private void Wait(double seconds)
    {
        scheduleCursor += seconds;
    }

    private void Tap(string action)
    {
        At(0.0, () => PressAndRelease(action));
    }

    private void Repeat(int count, double secondsBetween, Action step)
    {
        for (int i = 0; i < count; i++)
        {
            At(i == 0 ? 0.0 : secondsBetween, step);
        }

        Wait(secondsBetween);
    }

    private static void PressAndRelease(string action)
    {
        RegisterControllerInput();
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
    }

    private static void RegisterControllerInput()
    {
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = JoyButton.Misc1, Pressed = true, Device = 0 });
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = JoyButton.Misc1, Pressed = false, Device = 0 });
    }

    private void BeginNextSystemHold()
    {
        RegisterControllerInput();
        Input.ParseInputEvent(new InputEventAction { Action = NextSystemAction, Pressed = true });
        holdingNextSystem = true;
        holdStartedAt = elapsedSeconds;
    }

    private void Finish()
    {
        mainScene.IsAutomatedInputRunning = false;
        GD.Print($"[Demo] finished at frame {Engine.GetFramesDrawn()}");
        GetTree().Quit();
    }

    public override void _Process(double delta)
    {
        elapsedSeconds += delta;

        if (holdingNextSystem)
        {
            double heldFor = elapsedSeconds - holdStartedAt;

            if ((heldFor >= MinimumHoldSeconds && mainScene.HasOpenPanel) || heldFor >= MaximumHoldSeconds)
            {
                Input.ParseInputEvent(new InputEventAction { Action = NextSystemAction, Pressed = false });
                holdingNextSystem = false;
            }
        }

        while (timeline.Count > 0 && timeline.Peek().At <= elapsedSeconds)
        {
            timeline.Dequeue().Step();
        }
    }
}
