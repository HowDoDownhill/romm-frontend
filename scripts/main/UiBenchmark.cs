using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public partial class UiBenchmark : Node
{
    private class Phase
    {
        public string Name;
        public int Steps;
        public double SecondsBetweenSteps;
        public Action Step;
        public double TrailingSeconds;
        public List<double> FrameMilliseconds = new List<double>();
        public string EndState = "";
    }

    private readonly List<Phase> phases = new List<Phase>();

    private MainScene mainScene;
    private string reportPath;
    private ulong startupToLibraryMilliseconds;
    private int phaseIndex = -1;
    private int stepsTaken;
    private ulong nextStepAtMicroseconds;
    private ulong phaseEndsAtMicroseconds;
    private ulong previousFrameMicroseconds;
    private string pendingReleaseAction;

    public void Begin(MainScene owner, string outputPath, ulong libraryReadyMilliseconds)
    {
        mainScene = owner;
        reportPath = outputPath;
        startupToLibraryMilliseconds = libraryReadyMilliseconds;
        mainScene.IsAutomatedInputRunning = true;

        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        Engine.MaxFps = 0;

        phases.Add(new Phase { Name = "idle", Steps = 0, TrailingSeconds = 2.0 });
        phases.Add(new Phase { Name = "scroll-held", Steps = 40, SecondsBetweenSteps = 0.1, Step = () => PressAndRelease("ui_down"), TrailingSeconds = 0.5 });
        phases.Add(new Phase { Name = "scroll-back", Steps = 40, SecondsBetweenSteps = 0.1, Step = () => PressAndRelease("ui_up"), TrailingSeconds = 0.5 });
        phases.Add(new Phase { Name = "scroll-flood", Steps = 40, SecondsBetweenSteps = 0.0, Step = () => PressAndRelease("ui_down"), TrailingSeconds = 1.0 });
        phases.Add(new Phase { Name = "system-switch", Steps = 10, SecondsBetweenSteps = 0.8, Step = () => PressSystemCycle("CycleSystemDown"), TrailingSeconds = 1.0 });
        phases.Add(new Phase { Name = "system-switch-back", Steps = 10, SecondsBetweenSteps = 0.8, Step = () => PressSystemCycle("CylceSystemUp"), TrailingSeconds = 1.0 });

        previousFrameMicroseconds = Time.GetTicksUsec();
        AdvancePhase();
    }

    private static void PressAndRelease(string action)
    {
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
    }

    private void PressSystemCycle(string action)
    {
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });
        pendingReleaseAction = action;
    }

    private string DescribeState()
    {
        int gameIndex = (mainScene.gameList as VerticalCarousel)?.SelectedIndex ?? -1;
        int gameCount = mainScene.gameList?.GetChildCount() ?? 0;
        return $"game {gameIndex}/{gameCount}, system {mainScene.systemCarousel?.SelectedIndex}";
    }

    private void AdvancePhase()
    {
        if (phaseIndex >= 0)
        {
            phases[phaseIndex].EndState = DescribeState();
        }

        phaseIndex++;

        if (phaseIndex >= phases.Count)
        {
            mainScene.IsAutomatedInputRunning = false;
            WriteReport();
            GetTree().Quit();
            return;
        }

        stepsTaken = 0;
        nextStepAtMicroseconds = Time.GetTicksUsec();
        phaseEndsAtMicroseconds = 0;
        mainScene.gameList?.GrabFocus();
    }

    public override void _Process(double delta)
    {
        if (phaseIndex < 0 || phaseIndex >= phases.Count)
        {
            return;
        }

        ulong now = Time.GetTicksUsec();
        Phase phase = phases[phaseIndex];
        phase.FrameMilliseconds.Add((now - previousFrameMicroseconds) / 1000.0);
        previousFrameMicroseconds = now;

        if (pendingReleaseAction != null)
        {
            Input.ParseInputEvent(new InputEventAction { Action = pendingReleaseAction, Pressed = false });
            pendingReleaseAction = null;
        }

        if (stepsTaken < phase.Steps)
        {
            if (now >= nextStepAtMicroseconds)
            {
                phase.Step();
                stepsTaken++;
                nextStepAtMicroseconds = now + (ulong)(phase.SecondsBetweenSteps * 1_000_000);
            }

            return;
        }

        if (phaseEndsAtMicroseconds == 0)
        {
            phaseEndsAtMicroseconds = now + (ulong)(phase.TrailingSeconds * 1_000_000);
        }

        if (now >= phaseEndsAtMicroseconds)
        {
            AdvancePhase();
        }
    }

    private void WriteReport()
    {
        var report = new StringBuilder();
        report.AppendLine($"startup_to_library_ms {startupToLibraryMilliseconds}");
        report.AppendLine($"renderer {RenderingServer.GetVideoAdapterName()} | window {DisplayServer.WindowGetSize()} | canvas {mainScene.GetViewportRect().Size} | vsync off");
        report.AppendLine("phase                frames   mean    p50    p95    p99    max  >8.3ms >16.7ms >33ms  end state");

        foreach (Phase phase in phases)
        {
            List<double> frames = phase.FrameMilliseconds.Skip(1).OrderBy(value => value).ToList();

            if (frames.Count == 0)
            {
                continue;
            }

            report.AppendLine(string.Format("{0,-20} {1,6} {2,6:0.00} {3,6:0.00} {4,6:0.00} {5,6:0.00} {6,6:0.0} {7,6} {8,7} {9,5}  {10}",
                phase.Name, frames.Count, frames.Average(), Percentile(frames, 0.50), Percentile(frames, 0.95), Percentile(frames, 0.99), frames[^1],
                frames.Count(value => value > 8.3), frames.Count(value => value > 16.7), frames.Count(value => value > 33.3), phase.EndState));
        }

        string text = report.ToString();
        GD.Print("[Bench]\n" + text);

        using var file = FileAccess.Open(reportPath, FileAccess.ModeFlags.Write);
        file?.StoreString(text);
    }

    private static double Percentile(List<double> sortedValues, double fraction)
    {
        int index = (int)Math.Ceiling(fraction * sortedValues.Count) - 1;
        return sortedValues[Math.Clamp(index, 0, sortedValues.Count - 1)];
    }
}
