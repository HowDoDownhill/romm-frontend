using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Godot;

public class RomFile
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("file_name")]
    public string FileName { get; set; }

    [JsonPropertyName("full_path")]
    public string FullPath { get; set; }
}

public class GameSystem
{
    public int Id { get; set; }
    public string Name { get; set; }
    
    [JsonPropertyName("url_logo")]
    public string LogoUrl { get; set; }
    
    [JsonPropertyName("rom_count")]
    public int RomCount { get; set; }
    
    [JsonPropertyName("slug")]
    public string Slug { get; set; }
    
    [JsonPropertyName("igdb_slug")]
    public string IgdbSlug { get; set; }
    
    public string MappedEmulator { get; set; }

    public string PrefferedFirmware { get; set; }

    public List<Firmware> AvailableFirmwares { get; set; }
}



public class Game
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Path { get; set; }
    
    [JsonPropertyName("summary")]
    public string Description { get; set; }
    
    [JsonPropertyName("url_cover")]
    public string CoverArtUrl { get; set; }

    [JsonPropertyName("path_cover_large")]
    public string PathCoverLarge { get; set; }

    [JsonPropertyName("path_cover_small")]
    public string PathCoverSmall { get; set; }
    
    [JsonPropertyName("platform_id")]
    public int PlatformId { get; set; }

    [JsonPropertyName("platform_slug")]
    public string PlatformSlug { get; set; }

    [JsonPropertyName("platform_display_name")]
    public string PlatformDisplayName { get; set; }

    [JsonPropertyName("files")]
    public List<RomFile> Files { get; set; }
    
    [JsonPropertyName("fs_name")]
    public string LocalFilename { get; set; }

    [JsonIgnore]
    public GameSystem System { get; set; }
}

public class Firmware
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("file_name")]
    public string FileName { get; set; }

    [JsonPropertyName("file_name_no_tags")]
    public string FileNameNoTags { get; set; }

    [JsonPropertyName("file_name_no_ext")]
    public string FileNameNoExt { get; set; }

    [JsonPropertyName("file_extension")]
    public string FileExtension { get; set; }

    [JsonPropertyName("file_path")]
    public string FilePath { get; set; }

    [JsonPropertyName("file_size_bytes")]
    public long FileSizeBytes { get; set; }

    [JsonPropertyName("full_path")]
    public string FullPath { get; set; }

    [JsonPropertyName("is_verified")]
    public bool IsVerified { get; set; }

    [JsonPropertyName("crc_hash")]
    public string CrcHash { get; set; }

    [JsonPropertyName("md5_hash")]
    public string Md5Hash { get; set; }

    [JsonPropertyName("sha1_hash")]
    public string Sha1Hash { get; set; }

    [JsonPropertyName("missing_from_fs")]
    public bool MissingFromFs { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

public class GameResponse
{
    [JsonPropertyName("items")]
    public List<Game> Games { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }
}

public class User
{
    public string Username { get; set; }
    public string Token { get; set; }
}
