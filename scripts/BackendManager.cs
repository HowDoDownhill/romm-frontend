using Godot;

public partial class BackendManager : Node
{
    public IBackend ActiveBackend { get; set; }
}