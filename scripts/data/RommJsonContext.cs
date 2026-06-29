using System.Collections.Generic;
using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GameResponse))]
[JsonSerializable(typeof(List<GameResponse>))]
[JsonSerializable(typeof(Game))]
[JsonSerializable(typeof(List<Game>))]
[JsonSerializable(typeof(Dictionary<int, List<Game>>))]
[JsonSerializable(typeof(RomFile))]
[JsonSerializable(typeof(List<RomFile>))]
[JsonSerializable(typeof(GameSystem))]
[JsonSerializable(typeof(List<GameSystem>))]
[JsonSerializable(typeof(Firmware))]
[JsonSerializable(typeof(List<Firmware>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(SyncNegotiateResponse))]
[JsonSerializable(typeof(EmulatorMeta))]
[JsonSerializable(typeof(DevicePayload))]
[JsonSerializable(typeof(Dictionary<string, System.Text.Json.JsonElement>))]
[JsonSerializable(typeof(SyncNegotiatePayload))]
[JsonSerializable(typeof(SyncCompletePayload))]
[JsonSerializable(typeof(GithubReleaseInfo))]
public partial class RommJsonContext : JsonSerializerContext
{
}
