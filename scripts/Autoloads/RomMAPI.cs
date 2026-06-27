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
    private readonly System.Net.Http.HttpClient httpClient = new System.Net.Http.HttpClient();
    public string ApiHost => apiHostUrl;
    private string apiHostUrl;
    private string authenticationToken;
    private bool isUsingBasicAuthentication = false;

    private AppInstance appInstance;

    public override void _Ready()
    {
        appInstance = GetNode<AppInstance>("/root/AppInstance");
        appInstance.rommApi = this;
    }

    public async Task<(bool isSuccess, string errorMessage)> AuthenticateAsync(string username, string password, string host, string apiKey)
    {
        apiHostUrl = host.EndsWith("/") ? host.TrimEnd('/') : host;

        if (!apiHostUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !apiHostUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Host must start with http:// or https://");
        }

        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        isUsingBasicAuthentication = false;

        if (!string.IsNullOrEmpty(apiKey))
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            try
            {
                HttpResponseMessage apiKeyTestResponse = await httpClient.GetAsync($"{apiHostUrl}/api/platforms");
                if (apiKeyTestResponse.IsSuccessStatusCode)
                {
                    authenticationToken = apiKey;
                    return (true, null);
                }
                return (false, $"Server rejected API key: {apiKeyTestResponse.StatusCode}");
            }
            catch (Exception exception)
            {
                return (false, $"Connection failed: {exception.Message}");
            }
        }

        var tokenRequestBody = new Dictionary<string, string>
        {
            { "grant_type", "password" },
            { "username", username },
            { "password", password },
            { "scope", "platforms.read roms.read" }
        };
        var encodedFormContent = new FormUrlEncodedContent(tokenRequestBody);

        try
        {
            HttpResponseMessage tokenResponse = await httpClient.PostAsync($"{apiHostUrl}/api/token", encodedFormContent);

            if (tokenResponse.IsSuccessStatusCode)
            {
                string tokenResponseBody = await tokenResponse.Content.ReadAsStringAsync();
                var deserializedTokenResponse = JsonSerializer.Deserialize<Dictionary<string, string>>(tokenResponseBody);

                if (deserializedTokenResponse != null && deserializedTokenResponse.ContainsKey("access_token"))
                {
                    authenticationToken = deserializedTokenResponse["access_token"];
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authenticationToken);
                    return (true, null);
                }
            }

            var base64EncodedCredentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64EncodedCredentials);

            HttpResponseMessage basicAuthTestResponse = await httpClient.GetAsync($"{apiHostUrl}/api/platforms");
            if (basicAuthTestResponse.IsSuccessStatusCode)
            {
                isUsingBasicAuthentication = true;
                return (true, null);
            }

            return (false, $"Login failed: {tokenResponse.StatusCode} / {basicAuthTestResponse.StatusCode}");
        }
        catch (HttpRequestException exception)
        {
            GD.PrintErr($"Authentication request failed: {exception.Message}");
            return (false, "Could not connect to server.");
        }
        catch (Exception exception)
        {
            GD.PrintErr($"An unexpected error occurred: {exception.Message}");
            return (false, "An unexpected error occurred.");
        }
    }

    public async Task<List<GameSystem>> GetSystemsAsync()
    {
        try
        {
            HttpResponseMessage platformsResponse = await httpClient.GetAsync($"{apiHostUrl}/api/platforms");

            if (platformsResponse.IsSuccessStatusCode)
            {
                string platformsResponseBody = await platformsResponse.Content.ReadAsStringAsync();
                var deserializationOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                List<GameSystem> allGameSystems = JsonSerializer.Deserialize<List<GameSystem>>(platformsResponseBody, deserializationOptions);

                if (allGameSystems != null)
                {
                    foreach (var gameSystem in allGameSystems)
                    {
                        if (appInstance.emulatorManager.GetMappedEmulator(gameSystem.Slug) != "")
                        {
                            gameSystem.MappedEmulator = appInstance.emulatorManager.GetMappedEmulator(gameSystem.Slug);
                        }
                    }
                    return allGameSystems.Where(system => system.RomCount > 0).ToList();
                }
            }
            else
            {
                GD.PrintErr($"Failed to fetch systems. Status code: {platformsResponse.StatusCode}");
            }
        }
        catch (Exception exception)
        {
            GD.PrintErr($"GetSystems request failed: {exception.Message}");
        }

        return new List<GameSystem>();
    }

    public async Task<GameResponse> GetGamesAsync(GameSystem gameSystem, int pageNumber = 1, int pageSize = 100)
    {
        try
        {
            int queryOffset = (pageNumber - 1) * pageSize;
            string gamesRequestUrl = $"{apiHostUrl}/api/roms?platform_ids={gameSystem.Id}&limit={pageSize}&offset={queryOffset}&include=path_cover_3d,path_cover_large";
            HttpResponseMessage gamesResponse = await httpClient.GetAsync(gamesRequestUrl);
            string gamesResponseBody = await gamesResponse.Content.ReadAsStringAsync();

            if (gamesResponse.IsSuccessStatusCode)
            {
                var deserializationOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<GameResponse>(gamesResponseBody, deserializationOptions);
            }
            else
            {
                GD.PrintErr($"GetGamesAsync failed for system {gameSystem.Name}. Status: {gamesResponse.StatusCode}. Body: {gamesResponseBody}");
            }
        }
        catch (HttpRequestException exception)
        {
            GD.PrintErr($"GetGames request failed: {exception.Message}");
        }
        catch (JsonException exception)
        {
            GD.PrintErr($"GetGames JSON deserialization failed: {exception.Message}");
        }
        catch (UriFormatException exception)
        {
            GD.PrintErr($"Invalid URL format: {exception.Message}");
        }

        return null;
    }

    public string GetRomDownloadUrl(Game game)
    {
        if (game == null) return null;
        return $"{apiHostUrl}/api/roms/download?rom_ids={game.Id}";
    }

    public string[] GetAuthHeaders()
    {
        if (httpClient.DefaultRequestHeaders.Authorization == null)
        {
            return new string[0];
        }
        return new string[] { $"Authorization: {httpClient.DefaultRequestHeaders.Authorization}" };
    }

    public async Task<List<Firmware>> GetFirmwareAsync(int? platformId = null)
    {
        try
        {
            string firmwareRequestUrl = $"{apiHostUrl}/api/firmware";
            if (platformId.HasValue)
            {
                firmwareRequestUrl += $"?platform_id={platformId.Value}";
            }

            HttpResponseMessage firmwareResponse = await httpClient.GetAsync(firmwareRequestUrl);

            if (firmwareResponse.IsSuccessStatusCode)
            {
                string firmwareResponseBody = await firmwareResponse.Content.ReadAsStringAsync();
                var deserializationOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                List<Firmware> firmwareList = JsonSerializer.Deserialize<List<Firmware>>(firmwareResponseBody, deserializationOptions);

                return firmwareList ?? new List<Firmware>();
            }
            else
            {
                GD.PrintErr($"Failed to fetch firmware. Status code: {firmwareResponse.StatusCode}");
            }
        }
        catch (HttpRequestException exception)
        {
            GD.PrintErr($"GetFirmware request failed: {exception.Message}");
        }
        catch (JsonException exception)
        {
            GD.PrintErr($"GetFirmware JSON deserialization failed: {exception.Message}");
        }
        catch (Exception exception)
        {
            GD.PrintErr($"An unexpected error occurred: {exception.Message}");
        }

        return new List<Firmware>();
    }

    public async Task<Firmware> GetFirmwareByIdAsync(int firmwareId)
    {
        try
        {
            string firmwareByIdRequestUrl = $"{apiHostUrl}/api/firmware/{firmwareId}";

            HttpResponseMessage firmwareResponse = await httpClient.GetAsync(firmwareByIdRequestUrl);

            if (firmwareResponse.IsSuccessStatusCode)
            {
                string firmwareResponseBody = await firmwareResponse.Content.ReadAsStringAsync();
                var deserializationOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                return JsonSerializer.Deserialize<Firmware>(firmwareResponseBody, deserializationOptions);
            }
            else
            {
                GD.PrintErr($"Failed to fetch firmware ID {firmwareId}. Status code: {firmwareResponse.StatusCode}");
            }
        }
        catch (HttpRequestException exception)
        {
            GD.PrintErr($"GetFirmwareById request failed: {exception.Message}");
        }
        catch (JsonException exception)
        {
            GD.PrintErr($"GetFirmwareById JSON deserialization failed: {exception.Message}");
        }
        catch (Exception exception)
        {
            GD.PrintErr($"An unexpected error occurred: {exception.Message}");
        }

        return null;
    }

    public string GetFirmwareDownloadUrl(Firmware firmware)
    {
        if (firmware == null) return null;

        string urlEncodedFileName = Uri.EscapeDataString(firmware.FileName);

        return $"{apiHostUrl}/api/firmware/{firmware.Id}/content/{urlEncodedFileName}";
    }

    public async Task<bool> DownloadAssetAsync(string assetUrl, string destinationFilePath)
    {
        try
        {
            var assetResponse = await httpClient.GetAsync(assetUrl, HttpCompletionOption.ResponseHeadersRead);
            if (assetResponse.IsSuccessStatusCode)
            {
                using var destinationFileStream = new System.IO.FileStream(destinationFilePath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None);
                await assetResponse.Content.CopyToAsync(destinationFileStream);
                return true;
            }
            else
            {
                GD.PrintErr($"Failed to download asset {assetUrl}. Status: {assetResponse.StatusCode} {assetResponse.ReasonPhrase}");
            }
        }
        catch (Exception exception)
        {
            GD.PrintErr($"Failed to download asset {assetUrl}: {exception.Message}");
        }
        return false;
    }
}
