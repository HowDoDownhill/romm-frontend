using Godot;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public partial class RomMAPI : Node, IBackend
{
    private readonly System.Net.Http.HttpClient _httpClient = new System.Net.Http.HttpClient();
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

        // If an API Key (Bearer token) is provided, try that first.
        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            
            try
            {
                // Test the key using the platforms endpoint
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

        // Try getting an OAuth2 token from /api/token
        var requestBody = new Dictionary<string, string>
        {
            { "grant_type", "password" },
            { "username", username },
            { "password", password }
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
            
            // If /api/token fails, let's fallback to testing Basic HTTP Authentication
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
                return JsonSerializer.Deserialize<List<GameSystem>>(responseBody, options) ?? new List<GameSystem>();
            }
        }
        catch (HttpRequestException e)
        {
            GD.PrintErr($"GetSystems request failed: {e.Message}");
        }

        return new List<GameSystem>();
    }

    public async Task<List<Game>> GetGamesAsync(string systemId)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"{_apiHost}/api/roms?platform_id={systemId}");

            if (response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<List<Game>>(responseBody, options) ?? new List<Game>();
            }
        }
        catch (HttpRequestException e)
        {
            GD.PrintErr($"GetGames request failed: {e.Message}");
        }

        return new List<Game>();
    }
}
