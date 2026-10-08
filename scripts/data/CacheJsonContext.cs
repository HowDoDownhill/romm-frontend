using System.Collections.Generic;
using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(List<GameSystem>))]
[JsonSerializable(typeof(Dictionary<int, List<Game>>))]
public partial class CacheJsonContext : JsonSerializerContext
{
}
