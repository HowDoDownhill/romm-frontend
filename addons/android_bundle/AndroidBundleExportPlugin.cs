#if TOOLS
using Godot;
using System.IO;

[Tool]
public partial class AndroidBundleExportPlugin : EditorExportPlugin
{
    public const string BundledDirectory = "install_scripts";

    public override string _GetName()
    {
        return "AndroidBundle";
    }

    public override void _ExportBegin(string[] features, bool isDebug, string path, uint flags)
    {
        if (System.Array.IndexOf(features, "android") < 0)
        {
            return;
        }

        string sourceDirectory = ProjectSettings.GlobalizePath("res://" + BundledDirectory);
        int bundledFileCount = 0;

        foreach (string filePath in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, filePath).Replace(Path.DirectorySeparatorChar, '/');

            if (relativePath == ".gdignore")
            {
                continue;
            }

            AddFile($"res://{BundledDirectory}/{relativePath}", File.ReadAllBytes(filePath), false);
            bundledFileCount++;
        }

        GD.Print($"[AndroidBundle] packed {bundledFileCount} files from {BundledDirectory}");
    }
}
#endif
