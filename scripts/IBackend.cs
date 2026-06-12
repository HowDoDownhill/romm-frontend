using System.Collections.Generic;
using System.Threading.Tasks;

public interface IBackend
{
    Task<(bool isSuccess, string errorMessage)> AuthenticateAsync(string username, string password, string host, string apiKey);
    Task<List<GameSystem>> GetSystemsAsync();
    Task<GameResponse> GetGamesAsync(GameSystem system, int page = 1, int size = 100);
    string GetRomDownloadUrl(Game game);
    string[] GetAuthHeaders();
}
