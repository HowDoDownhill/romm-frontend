using Godot;
using System.Collections.Generic;
using System.Linq;

public static class StartupTimeline
{
    private static readonly List<(string Name, ulong Milliseconds)> marks = new List<(string, ulong)>();

    public static void Mark(string name)
    {
        marks.Add((name, Time.GetTicksMsec()));
    }

    public static string Describe()
    {
        ulong previous = 0;
        var parts = marks.Select(mark =>
        {
            string part = $"{mark.Name} {mark.Milliseconds} (+{mark.Milliseconds - previous})";
            previous = mark.Milliseconds;
            return part;
        });

        return string.Join(", ", parts);
    }
}
