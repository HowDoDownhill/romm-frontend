using Godot;
using System.Collections.Generic;

public partial class DataBus : Node
{
    public List<GameSystem> systems { get; set; } = new List<GameSystem>();
    public Dictionary<int, List<Game>> gameCache { get; set; } = new Dictionary<int, List<Game>>();

    private AppInstance appInstance;

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.dataBus = this; 
    }
}
