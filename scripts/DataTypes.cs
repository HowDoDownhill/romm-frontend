using Godot;

public class GameSystem
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string LogoUrl { get; set; }
}

public class Game
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Path { get; set; }
    public string Description { get; set; }
    public string CoverArtUrl { get; set; }
    public GameSystem System { get; set; }
    
    public string LocalFilename { get; set; }
}

public class User
{
    public string Username { get; set; }
    public string Token { get; set; }
}