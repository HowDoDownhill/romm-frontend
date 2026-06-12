using Godot;
using System.Collections.Generic;

public partial class DataBus : Node
{
    public List<GameSystem> Systems { get; set; } = new List<GameSystem>();
    public Dictionary<int, List<Game>> GameCache { get; set; } = new Dictionary<int, List<Game>>();
}
