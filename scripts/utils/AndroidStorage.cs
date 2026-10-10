using Godot;
using System;

public static class AndroidStorage
{
    private const string ExternalStorageAuthority = "com.android.externalstorage.documents";
    private const string PrimaryVolumePrefix = "primary:";
    private const string GamesFolderName = "RomM";
    private const int GrantReadUriPermission = 0x00000001;
    private const int GrantWriteUriPermission = 0x00000002;

    public static string SharedStorageRoot => "/storage/emulated/0";

    public static string GamesRoot => OS.GetSystemDir(OS.SystemDir.Documents).TrimEnd('/') + "/" + GamesFolderName;

    private static GodotObject Activity => Engine.GetSingleton("AndroidRuntime").Call("getActivity").AsGodotObject();

    private static GodotObject ContentResolver => Activity.Call("getContentResolver").AsGodotObject();

    private static GodotObject DocumentsContract => JavaClassWrapper.Wrap("android.provider.DocumentsContract");

    public static string DocumentIdForPath(string path)
    {
        string normalizedPath = path.Replace('\\', '/').TrimEnd('/');

        if (!normalizedPath.StartsWith(SharedStorageRoot + "/", StringComparison.Ordinal))
        {
            return null;
        }

        return PrimaryVolumePrefix + normalizedPath.Substring(SharedStorageRoot.Length + 1);
    }

    public static bool HasAccessTo(string path)
    {
        return FindGrantedTreeCovering(path) != null;
    }

    public static string FindGrantedTreeCovering(string path)
    {
        if (!AndroidApps.IsAvailable)
        {
            return null;
        }

        string documentId = DocumentIdForPath(path);

        if (documentId == null)
        {
            return null;
        }

        GodotObject grants = ContentResolver.Call("getPersistedUriPermissions").AsGodotObject();
        int grantCount = grants.Call("size").AsInt32();

        for (int index = 0; index < grantCount; index++)
        {
            GodotObject grant = grants.Call("get", index).AsGodotObject();

            if (!grant.Call("isReadPermission").AsBool())
            {
                continue;
            }

            GodotObject grantedUri = grant.Call("getUri").AsGodotObject();
            string treeDocumentId = DocumentsContract.Call("getTreeDocumentId", grantedUri).AsString();

            if (Covers(treeDocumentId, documentId))
            {
                return treeDocumentId;
            }
        }

        return null;
    }

    public static void RequestAccessTo(string path, Action<bool> onFinished)
    {
        DirAccess.MakeDirRecursiveAbsolute(path);

        DisplayServer.FileDialogShow("Choose the RomM folder", path, "", false, DisplayServer.FileDialogMode.OpenDir, Array.Empty<string>(),
            Callable.From<bool, string[], long>((chosen, selectedPaths, filterIndex) =>
            {
                bool granted = chosen && selectedPaths.Length > 0 && KeepTreeGrant(selectedPaths[0], path);
                GD.Print($"[Android] folder access for {path}: {(granted ? "granted" : "not granted")}");
                onFinished?.Invoke(granted);
            }));
    }

    private static bool KeepTreeGrant(string treeUriText, string requiredPath)
    {
        GodotObject treeUri = JavaClassWrapper.Wrap("android.net.Uri").Call("parse", treeUriText).AsGodotObject();
        string treeDocumentId = DocumentsContract.Call("getTreeDocumentId", treeUri).AsString();

        if (!Covers(treeDocumentId, DocumentIdForPath(requiredPath)))
        {
            GD.PrintErr($"[Android] the chosen folder {treeDocumentId} does not contain {requiredPath}.");
            return false;
        }

        ContentResolver.Call("takePersistableUriPermission", treeUri, GrantReadUriPermission | GrantWriteUriPermission);
        return JavaClassWrapper.GetException() == null;
    }

    public static GodotObject DocumentUriForPath(string path)
    {
        string treeDocumentId = FindGrantedTreeCovering(path);

        if (treeDocumentId == null)
        {
            return null;
        }

        GodotObject treeUri = DocumentsContract.Call("buildTreeDocumentUri", ExternalStorageAuthority, treeDocumentId).AsGodotObject();
        return DocumentsContract.Call("buildDocumentUriUsingTree", treeUri, DocumentIdForPath(path)).AsGodotObject();
    }

    private static bool Covers(string treeDocumentId, string documentId)
    {
        if (string.IsNullOrEmpty(treeDocumentId) || string.IsNullOrEmpty(documentId))
        {
            return false;
        }

        return documentId == treeDocumentId
            || documentId.StartsWith(treeDocumentId.TrimEnd('/') + "/", StringComparison.Ordinal)
            || treeDocumentId == PrimaryVolumePrefix;
    }
}
