using System.Collections.Generic;
using System.Linq;

public static class CollectionProjection
{
    public const int FirstSyntheticSystemId = -1;

    public static List<GameSystem> Project(List<Collection> collections, Dictionary<int, List<Game>> gameCache)
    {
        var collectionSystems = new List<GameSystem>();

        if (gameCache == null)
        {
            return collectionSystems;
        }

        RemoveProjectedCollections(gameCache);

        var gamesById = BuildGameLookup(gameCache);
        int nextSyntheticSystemId = FirstSyntheticSystemId;

        foreach (var collection in BuildOrderedCollections(collections))
        {
            var collectionGames = ResolveCollectionGames(collection.RomIds, gamesById);

            if (collectionGames.Count == 0)
            {
                continue;
            }

            var collectionSystem = new GameSystem
            {
                Id = nextSyntheticSystemId,
                Name = collection.Name,
                Slug = $"collection-{collection.Id}",
                RomCount = collectionGames.Count,
                IsCollection = true,
                IsFavoriteCollection = collection.IsFavorite
            };

            gameCache[nextSyntheticSystemId] = collectionGames;
            collectionSystems.Add(collectionSystem);
            nextSyntheticSystemId--;
        }

        return collectionSystems;
    }

    public static void RemoveProjectedCollections(Dictionary<int, List<Game>> gameCache)
    {
        if (gameCache == null)
        {
            return;
        }

        foreach (int syntheticSystemId in gameCache.Keys.Where(systemId => systemId < 0).ToList())
        {
            gameCache.Remove(syntheticSystemId);
        }
    }

    private static IEnumerable<Collection> BuildOrderedCollections(List<Collection> collections)
    {
        if (collections == null)
        {
            return new List<Collection>();
        }

        var seenCollectionIds = new HashSet<int>();
        var uniqueCollections = new List<Collection>();

        foreach (var collection in collections)
        {
            if (collection == null || collection.IsVirtual || string.IsNullOrWhiteSpace(collection.Name))
            {
                continue;
            }

            if (!seenCollectionIds.Add(collection.Id))
            {
                continue;
            }

            uniqueCollections.Add(collection);
        }

        return uniqueCollections
            .OrderByDescending(collection => collection.IsFavorite)
            .ThenBy(collection => collection.Name);
    }

    private static Dictionary<int, Game> BuildGameLookup(Dictionary<int, List<Game>> gameCache)
    {
        var gamesById = new Dictionary<int, Game>();

        foreach (var cacheEntry in gameCache)
        {
            if (cacheEntry.Key < 0 || cacheEntry.Value == null)
            {
                continue;
            }

            foreach (var game in cacheEntry.Value)
            {
                gamesById[game.Id] = game;
            }
        }

        return gamesById;
    }

    private static List<Game> ResolveCollectionGames(List<int> romIds, Dictionary<int, Game> gamesById)
    {
        var collectionGames = new List<Game>();

        if (romIds == null)
        {
            return collectionGames;
        }

        foreach (int romId in romIds)
        {
            if (gamesById.TryGetValue(romId, out Game game))
            {
                collectionGames.Add(game);
            }
        }

        return collectionGames;
    }
}
