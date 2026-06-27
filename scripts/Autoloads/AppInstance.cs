using Godot;
using System;

public partial class AppInstance : Node
{
    public ConfigManager configManager;
    public RomMAPI rommApi; 
    public DownloadManager downloadManager;
    public CacheManager cacheManager;
    public EmulatorManager emulatorManager;
    public DataBus dataBus;
    public AssetManager assetManager;
    public SaveSyncManager saveSyncManager;
    
    public override void _Ready()
    {
        configManager = GetNode<ConfigManager>("/root/ConfigManager");
        rommApi = GetNode<RomMAPI>("/root/RomMAPI");
        downloadManager = GetNode<DownloadManager>("/root/DownloadManager");
        cacheManager = GetNode<CacheManager>("/root/CacheManager");
        emulatorManager = GetNode<EmulatorManager>("/root/EmulatorManager");
        dataBus = GetNode<DataBus>("/root/DataBus");
    }
}
