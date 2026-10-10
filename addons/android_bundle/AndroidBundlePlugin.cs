#if TOOLS
using Godot;

[Tool]
public partial class AndroidBundlePlugin : EditorPlugin
{
    private AndroidBundleExportPlugin exportPlugin;

    public override void _EnterTree()
    {
        exportPlugin = new AndroidBundleExportPlugin();
        AddExportPlugin(exportPlugin);
    }

    public override void _ExitTree()
    {
        RemoveExportPlugin(exportPlugin);
        exportPlugin = null;
    }
}
#endif
