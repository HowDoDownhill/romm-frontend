using Godot;

public interface IGameListItem
{
    string Title { set; }
    bool Selected { get; set; }
    bool ShowsCover { get; }
    void SetCover(Texture2D texture, bool isPlaceholder);
    void SetInstalledIcon(Texture2D icon);
    void Reveal();
    void ResetReveal();
}
