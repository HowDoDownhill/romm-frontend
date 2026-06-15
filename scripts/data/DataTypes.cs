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
}

public static class EmulatorDefaults
{
    public static readonly Dictionary<string, string> IgdbSlugToEmulator = new Dictionary<string, string>
    {
        // Nintendo
        { "nes", "fceumm" }, // Nintendo Entertainment System
        { "snes", "snes9x" }, // Super Nintendo Entertainment System
        { "n64", "mupen64plus_next" }, // Nintendo 64
        { "ngc", "dolphin" }, // Nintendo GameCube
        { "gc", "dolphin" }, // Nintendo GameCube
        { "gamecube", "dolphin" }, // Nintendo GameCube
        { "wii", "dolphin" }, // Wii
        { "wiiu", "cemu" }, // Wii U
        { "switch", "yuzu" }, // Nintendo Switch
        { "gb", "gambatte" }, // Game Boy
        { "gbc", "gambatte" }, // Game Boy Color
        { "gba", "mgba" }, // Game Boy Advance
        { "nds", "melonds" }, // Nintendo DS
        { "new-nintendo-3ds", "citra" }, // Nintendo 3DS
        { "vb", "mednafen_vb" }, // Virtual Boy
        { "pokemini", "poke_mini" }, // Pokémon Mini

        // Sony
        { "psx", "duckstation" }, // PlayStation
        { "ps2", "pcsx2" }, // PlayStation 2
        { "ps3", "rpcs3" }, // PlayStation 3
        { "psp", "ppsspp" }, // PlayStation Portable
        { "psvita", "vita3k" }, // PlayStation Vita

        // Sega
        { "sg-1000", "gearsystem" }, // Sega SG-1000
        { "ms", "genesis_plus_gx" }, // Sega Master System
        { "md", "genesis_plus_gx" }, // Sega Mega Drive / Genesis
        { "gg", "genesis_plus_gx" }, // Sega Game Gear
        { "mega-cd", "genesis_plus_gx" }, // Sega CD
        { "32x", "picodrive" }, // Sega 32X
        { "saturn", "yabause" }, // Sega Saturn
        { "dc", "flycast" }, // Sega Dreamcast

        // Microsoft
        { "xbox", "xemu" }, // Xbox
        { "xbox360", "xenia" }, // Xbox 360

        // NEC / SNK
        { "pce", "beetle_pce_fast" }, // PC Engine / TurboGrafx-16
        { "pcecd", "beetle_pce_fast" }, // PC Engine CD-ROM² / TurboGrafx-CD
        { "ng", "fbneo" }, // Neo Geo MVS / AES
        { "ngcd", "neocd" }, // Neo Geo CD
        { "ngp", "mednafen_ngp" }, // Neo Geo Pocket
        { "ngpc", "mednafen_ngp" }, // Neo Geo Pocket Color

        // Atari
        { "2600", "stella" }, // Atari 2600
        { "5200", "a5200" }, // Atari 5200
        { "7800", "prosystem" }, // Atari 7800
        { "lynx", "handy" }, // Atari Lynx
        { "jaguar", "virtualjaguar" }, // Atari Jaguar

        // Arcade & Home Computers
        { "arcade", "fbneo" }, // Arcade
        { "mame", "mame" }, // MAME
        { "3do", "opera" }, // 3DO Interactive Multiplayer
        { "amiga", "puae" }, // Amiga
        { "amiga-cd32", "puae" }, // Amiga CD32
        { "c64", "vice_x64" }, // Commodore 64
        { "msx", "bluemsx" }, // MSX
        { "msx2", "bluemsx" }, // MSX2
        { "dos", "dosbox_pure" }, // PC (DOS)
        { "scummvm", "scummvm" }, // ScummVM
        { "x68000", "px68k" }, // Sharp X68000
        
        // Other / Fantasy Consoles
        { "pico-8", "retro8" }, // Pico-8
        { "tic-80", "tic80" }, // TIC-80
        { "wswan", "mednafen_wswan" }, // WonderSwan
        { "wswanc", "mednafen_wswan" } // WonderSwan Color
    };
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
