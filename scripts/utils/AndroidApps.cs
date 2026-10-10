using Godot;

public static class AndroidApps
{
    private const string ApkMimeType = "application/vnd.android.package-archive";
    private const int GrantReadUriPermission = 0x00000001;
    private const int NewTask = 0x10000000;

    public static bool IsAvailable => OS.HasFeature("android");

    private static GodotObject Runtime => Engine.GetSingleton("AndroidRuntime");

    private static GodotObject Activity => Runtime.Call("getActivity").AsGodotObject();

    private static GodotObject PackageManager => Activity.Call("getPackageManager").AsGodotObject();

    public static bool IsInstalled(string packageName)
    {
        if (!IsAvailable || string.IsNullOrEmpty(packageName))
        {
            return false;
        }

        Variant launchIntent = PackageManager.Call("getLaunchIntentForPackage", packageName);
        ClearJavaException();
        return launchIntent.VariantType != Variant.Type.Nil;
    }

    public static bool CanRequestInstalls()
    {
        return IsAvailable && PackageManager.Call("canRequestPackageInstalls").AsBool();
    }

    public static bool OpenInstaller(string apkPath)
    {
        if (!IsAvailable)
        {
            return false;
        }

        GodotObject file = JavaClassWrapper.Wrap("java.io.File").Call("File", apkPath).AsGodotObject();
        string authority = Activity.Call("getPackageName").AsString() + ".fileprovider";
        GodotObject contentUri = JavaClassWrapper.Wrap("androidx.core.content.FileProvider")
            .Call("getUriForFile", Activity, authority, file).AsGodotObject();

        if (ReportJavaException("resolving the APK's content URI"))
        {
            return false;
        }

        GodotObject intent = JavaClassWrapper.Wrap("android.content.Intent").Call("Intent", "android.intent.action.VIEW").AsGodotObject();
        intent.Call("setDataAndType", contentUri, ApkMimeType);
        intent.Call("addFlags", GrantReadUriPermission | NewTask);
        Activity.Call("startActivity", intent);
        return !ReportJavaException("opening the installer");
    }

    public static bool OpenUninstaller(string packageName)
    {
        if (!IsAvailable)
        {
            return false;
        }

        GodotObject packageUri = JavaClassWrapper.Wrap("android.net.Uri").Call("parse", "package:" + packageName).AsGodotObject();
        GodotObject intent = JavaClassWrapper.Wrap("android.content.Intent").Call("Intent", "android.intent.action.DELETE", packageUri).AsGodotObject();
        intent.Call("addFlags", NewTask);
        Activity.Call("startActivity", intent);
        return !ReportJavaException("opening the uninstaller");
    }

    public static bool OpenInstallPermissionSettings()
    {
        if (!IsAvailable)
        {
            return false;
        }

        string packageName = Activity.Call("getPackageName").AsString();
        GodotObject packageUri = JavaClassWrapper.Wrap("android.net.Uri").Call("parse", "package:" + packageName).AsGodotObject();
        GodotObject intent = JavaClassWrapper.Wrap("android.content.Intent")
            .Call("Intent", "android.settings.MANAGE_UNKNOWN_APP_SOURCES", packageUri).AsGodotObject();
        intent.Call("addFlags", NewTask);
        Activity.Call("startActivity", intent);
        return !ReportJavaException("opening the install permission settings");
    }

    private static void ClearJavaException()
    {
        JavaClassWrapper.GetException();
    }

    private static bool ReportJavaException(string action)
    {
        Variant exception = JavaClassWrapper.GetException();

        if (exception.VariantType == Variant.Type.Nil)
        {
            return false;
        }

        GD.PrintErr($"[Android] {action} failed: {exception}");
        return true;
    }
}
