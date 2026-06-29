using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public class ClientSaveState
{
    [JsonPropertyName("rom_id")]
    public int RomId { get; set; }

    [JsonPropertyName("file_name")]
    public string FileName { get; set; }

    [JsonPropertyName("slot")]
    public string Slot { get; set; }

    [JsonPropertyName("emulator")]
    public string Emulator { get; set; }

    [JsonPropertyName("content_hash")]
    public string ContentHash { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("file_size_bytes")]
    public long FileSizeBytes { get; set; }
}

public class DevicePayload
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("client")]
    public string Client { get; set; }

    [JsonPropertyName("platform")]
    public string Platform { get; set; }

    [JsonPropertyName("sync_mode")]
    public string SyncMode { get; set; }

    [JsonPropertyName("allow_existing")]
    public bool AllowExisting { get; set; }
}

public class SyncNegotiatePayload
{
    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; }

    [JsonPropertyName("saves")]
    public List<ClientSaveState> Saves { get; set; }
}

public class SyncOperationSchema
{
    [JsonPropertyName("rom_id")]
    public int RomId { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; } // "upload", "download", "conflict", "no_op"

    [JsonPropertyName("file_name")]
    public string FileName { get; set; }

    [JsonPropertyName("server_save_id")]
    public int? ServerSaveId { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; }
}

public class SyncNegotiateResponse
{
    [JsonPropertyName("session_id")]
    public int SessionId { get; set; }

    [JsonPropertyName("operations")]
    public List<SyncOperationSchema> Operations { get; set; }
}

public class SyncPlaySessionEntry
{
    [JsonPropertyName("rom_id")]
    public int? RomId { get; set; }

    [JsonPropertyName("save_slot")]
    public string SaveSlot { get; set; }

    [JsonPropertyName("start_time")]
    public DateTime StartTime { get; set; }

    [JsonPropertyName("end_time")]
    public DateTime EndTime { get; set; }

    [JsonPropertyName("duration_ms")]
    public long DurationMs { get; set; }
}

public class SyncCompletePayload
{
    [JsonPropertyName("operations_completed")]
    public int OperationsCompleted { get; set; }

    [JsonPropertyName("operations_failed")]
    public int OperationsFailed { get; set; }

    [JsonPropertyName("play_sessions")]
    public List<SyncPlaySessionEntry> PlaySessions { get; set; }
}
