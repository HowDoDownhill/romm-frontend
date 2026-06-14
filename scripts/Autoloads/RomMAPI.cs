using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public partial class RomMAPI : Node
{
    private readonly System.Net.Http.HttpClient _httpClient = new System.Net.Http.HttpClient();
    public string ApiHost => apiHost;
    private string apiHost;
    private string _authToken;
    private bool _useBasicAuth = false;

    private AppInstance appInstance;

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.rommApi = this; 
    }

    public async Task<(bool isSuccess, string errorMessage)> AuthenticateAsync(string username, string password, string host, string apiKey)
    {
        apiHost = host.EndsWith("/") ? host.TrimEnd('/') : host;

        if (!apiHost.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
            !apiHost.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
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
                HttpResponseMessage testResponse = await _httpClient.GetAsync($"{apiHost}/api/platforms");
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
            HttpResponseMessage response = await _httpClient.PostAsync($"{apiHost}/api/token", content);

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
            
            HttpResponseMessage basicAuthTest = await _httpClient.GetAsync($"{apiHost}/api/platforms");
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
            HttpResponseMessage response = await _httpClient.GetAsync($"{apiHost}/api/platforms");

            if (response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                List<GameSystem> systems = JsonSerializer.Deserialize<List<GameSystem>>(responseBody, options);
                
                if (systems != null)
                {
                    foreach (var system in systems)
                    {
                        if (EmulatorDefaults.IgdbSlugToEmulator.TryGetValue(system.IgdbSlug ?? "", out string emulator))
                        {
                            system.MappedEmulator = emulator;
                        }
                        else if (EmulatorDefaults.IgdbSlugToEmulator.TryGetValue(system.Slug ?? "", out string emulatorBySlug))
                        {
                            system.MappedEmulator = emulatorBySlug;
                        }
                    }
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
            string requestUrl = $"{apiHost}/api/roms?platform_ids={system.Id}&limit={size}&offset={offset}";
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
        return $"{apiHost}/api/roms/download?rom_ids={game.Id}";
    }

    public string[] GetAuthHeaders()
    {
        if (_httpClient.DefaultRequestHeaders.Authorization == null)
        {
            return new string[0];
        }
        return new string[] { $"Authorization: {_httpClient.DefaultRequestHeaders.Authorization}" };
    }

    // --- Firmware Methods ---

    public async Task<List<Firmware>> GetFirmwareAsync(int? platformId = null)
    {
        try
        {
            string requestUrl = $"{apiHost}/api/firmware";
            if (platformId.HasValue)
            {
                requestUrl += $"?platform_id={platformId.Value}";
            }

            GD.Print($"Requesting firmware from URL: {requestUrl}");
            HttpResponseMessage response = await _httpClient.GetAsync(requestUrl);

            if (response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                List<Firmware> firmwareList = JsonSerializer.Deserialize<List<Firmware>>(responseBody, options);
                
                return firmwareList ?? new List<Firmware>();
            }
            else
            {
                GD.PrintErr($"Failed to fetch firmware. Status code: {response.StatusCode}");
            }
        }
        catch (HttpRequestException e)
        {
            GD.PrintErr($"GetFirmware request failed: {e.Message}");
        }
        catch (JsonException e)
        {
            GD.PrintErr($"GetFirmware JSON deserialization failed: {e.Message}");
        }
        catch (Exception e)
        {
            GD.PrintErr($"An unexpected error occurred: {e.Message}");
        }

        return new List<Firmware>();
    }

    public async Task<Firmware> GetFirmwareByIdAsync(int id)
    {
        try
        {
            string requestUrl = $"{apiHost}/api/firmware/{id}";
            GD.Print($"Requesting firmware from URL: {requestUrl}");
            
            HttpResponseMessage response = await _httpClient.GetAsync(requestUrl);

            if (response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                
                return JsonSerializer.Deserialize<Firmware>(responseBody, options);
            }
            else
            {
                GD.PrintErr($"Failed to fetch firmware ID {id}. Status code: {response.StatusCode}");
            }
        }
        catch (HttpRequestException e)
        {
            GD.PrintErr($"GetFirmwareById request failed: {e.Message}");
        }
        catch (JsonException e)
        {
            GD.PrintErr($"GetFirmwareById JSON deserialization failed: {e.Message}");
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"An unexpected error occurred: {e.Message}");
        }

        return null;
    }

    public string GetFirmwareDownloadUrl(Firmware firmware)
    {
        if (firmware == null) return null;
        
        return $"{apiHost}/api/firmware/download?firmware_ids={firmware.Id}";
    }
}