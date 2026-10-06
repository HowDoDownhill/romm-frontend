using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class DataBus : Node
{
    public List<GameSystem> systems { get; set; } = new List<GameSystem>();
    public Dictionary<int, List<Game>> gameCache { get; set; } = new Dictionary<int, List<Game>>();
    public List<GameSystem> collectionSystems { get; set; } = new List<GameSystem>();
    public int favoriteCollectionId { get; set; }
    public HashSet<int> favoriteRomIds { get; set; } = new HashSet<int>();

    public bool HasFavoriteCollection => favoriteCollectionId > 0;

    public bool IsFavorite(int romId) => favoriteRomIds.Contains(romId);

    public GameSystem FavoriteCollectionSystem => collectionSystems?.FirstOrDefault(system => system.IsFavoriteCollection);

    public void ApplyFavoriteChange(Game game, bool isNowFavorite)
    {
        if (game == null)
        {
            return;
        }

        if (isNowFavorite)
        {
            favoriteRomIds.Add(game.Id);
        }

        else
        {
            favoriteRomIds.Remove(game.Id);
        }

        var favoriteSystem = FavoriteCollectionSystem;

        if (favoriteSystem == null || !gameCache.TryGetValue(favoriteSystem.Id, out var favoriteGames))
        {
            return;
        }

        if (isNowFavorite && !favoriteGames.Any(existing => existing.Id == game.Id))
        {
            favoriteGames.Add(game);
        }

        else if (!isNowFavorite)
        {
            favoriteGames.RemoveAll(existing => existing.Id == game.Id);
        }

        favoriteSystem.RomCount = favoriteGames.Count;
    }

    private AppInstance appInstance;

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.dataBus = this; 
    }
}
