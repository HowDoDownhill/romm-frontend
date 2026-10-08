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
    public ControllerManager controllerManager;
    public NetplayManager netplayManager;
    public NetplayLobby netplayLobby;
    public NetplayDiscovery netplayDiscovery;
    public NetplayPortMapper netplayPortMapper;
    public InputLayer inputLayer;
    
    public override void _Ready()
    {
        StartupTimeline.Mark("first autoload");
        configManager = GetNode<ConfigManager>("/root/ConfigManager");
        rommApi = GetNode<RomMAPI>("/root/RomMAPI");
        downloadManager = GetNode<DownloadManager>("/root/DownloadManager");
        cacheManager = GetNode<CacheManager>("/root/CacheManager");
        emulatorManager = GetNode<EmulatorManager>("/root/EmulatorManager");
        dataBus = GetNode<DataBus>("/root/DataBus");
        controllerManager = GetNode<ControllerManager>("/root/ControllerManager");
        Callable.From(ApplyDiscreteGpuPreference).CallDeferred();
    }

    private void ApplyDiscreteGpuPreference()
    {
        DiscreteGpuPreference.RegisterWindowsGpuPreference(OS.GetExecutablePath(), configManager.PreferDiscreteGpu);

        if (DiscreteGpuPreference.ShouldRelaunchOnDiscreteGpu(configManager.PreferDiscreteGpu)
            && DiscreteGpuPreference.RelaunchOnDiscreteGpu())
        {
            GetTree().Quit();
        }
    }
}
