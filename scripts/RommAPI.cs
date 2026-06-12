using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public partial class RomMAPI : Node, IBackend
{
    private readonly System.Net.Http.HttpClient _httpClient = new System.Net.Http.HttpClient();
    public string ApiHost => _apiHost;
    private string _apiHost;
    private string _authToken;
    private bool _useBasicAuth = false;

    public async Task<(bool isSuccess, string errorMessage)> AuthenticateAsync(string username, string password, string host, string apiKey)
    {
        _apiHost = host.EndsWith("/") ? host.TrimEnd('/') : host;

        if (!_apiHost.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
            !_apiHost.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Host must start with http:// or https://");
        }

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _useBasicAuth = false;

        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            
            try
            {
                HttpResponseMessage testResponse = await _httpClient.GetAsync($"{_apiHost}/api/platforms");
                if (testResponse.IsSuccessStatusCode)
                {
                    _authToken = apiKey;
                    return (true, null);
                }
                return (false, $"Server rejected API key: {testResponse.StatusCode}");
            }
            catch (Exception e)
            {
                return (false, $"Connection failed: {e.Message}");
            }
        }

        var requestBody = new Dictionary<string, string>
        {
            { "grant_type", "password" },
            { "username", username },
            { "password", password },
            { "scope", "platforms.read roms.read" }
        };
        var content = new FormUrlEncodedContent(requestBody);

        try
        {
            HttpResponseMessage response = await _httpClient.PostAsync($"{_apiHost}/api/token", content);

            if (response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                var responseData = JsonSerializer.Deserialize<Dictionary<string, string>>(responseBody);
                
                if (responseData != null && responseData.ContainsKey("access_token"))
                {
                    _authToken = responseData["access_token"];
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
                    return (true, null);
                }
            }
            
            var authHeaderValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeaderValue);
            
            HttpResponseMessage basicAuthTest = await _httpClient.GetAsync($"{_apiHost}/api/platforms");
            if (basicAuthTest.IsSuccessStatusCode)
            {
                _useBasicAuth = true;
                return (true, null);
            }

            string errorContent = await response.Content.ReadAsStringAsync();
            return (false, $"Login failed: {response.StatusCode} / {basicAuthTest.StatusCode}");
        }
        catch (HttpRequestException e)
        {
            GD.PrintErr($"Authentication request failed: {e.Message}");
            return (false, "Could not connect to server.");
        }
        catch (Exception e)
        {
            GD.PrintErr($"An unexpected error occurred: {e.Message}");
            return (false, "An unexpected error occurred.");
        }
    }

    public async Task<List<GameSystem>> GetSystemsAsync()
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"{_apiHost}/api/platforms");

            if (response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                List<GameSystem> systems = JsonSerializer.Deserialize<List<GameSystem>>(responseBody, options);
                
                if (systems != null)
                {
                    return systems.Where(s => s.RomCount > 0).ToList();
                }
            }
            else
            {
                GD.PrintErr($"Failed to fetch systems. Status code: {response.StatusCode}");
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"GetSystems request failed: {e.Message}");
        }

        return new List<GameSystem>();
    }

    public async Task<GameResponse> GetGamesAsync(GameSystem system, int page = 1, int size = 100)
    {
        try
        {
            int offset = (page - 1) * size;
            string requestUrl = $"{_apiHost}/api/roms?platform_ids={system.Id}&limit={size}&offset={offset}";
            GD.Print($"Requesting games from URL: {requestUrl}");
            HttpResponseMessage response = await _httpClient.GetAsync(requestUrl);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<GameResponse>(responseBody, options);
            }
            else
            {
                GD.PrintErr($"GetGamesAsync failed for system {system.Name}. Status: {response.StatusCode}. Body: {responseBody}");
            }
        }
        catch (HttpRequestException e)
        {
            GD.PrintErr($"GetGames request failed: {e.Message}");
        }
        catch (JsonException e)
        {
            GD.PrintErr($"GetGames JSON deserialization failed: {e.Message}");
        }

        return null;
    }

    public string GetRomDownloadUrl(Game game)
    {
        if (game == null) return null;
        return $"{_apiHost}/api/roms/download?rom_ids={game.Id}";
    }

    public string[] GetAuthHeaders()
    {
        if (_httpClient.DefaultRequestHeaders.Authorization == null)
        {
            return new string[0];
        }
        return new string[] { $"Authorization: {_httpClient.DefaultRequestHeaders.Authorization}" };
    }
}
