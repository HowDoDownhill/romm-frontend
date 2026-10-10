using Godot;

public static class AndroidApps
{
    private const string ApkMimeType = "application/vnd.android.package-archive";
    private const int GrantReadUriPermission = 0x00000001;
    private const int NewTask = 0x10000000;
    private const int ClearTask = 0x00008000;
    private const int ClearTop = 0x04000000;

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

        GodotObject contentUri = ContentUriForFile(apkPath);

        if (contentUri == null)
        {
            return false;
        }

        GodotObject intent = JavaClassWrapper.Wrap("android.content.Intent").Call("Intent", "android.intent.action.VIEW").AsGodotObject();
        intent.Call("setDataAndType", contentUri, ApkMimeType);
        intent.Call("addFlags", GrantReadUriPermission | NewTask);
        Activity.Call("startActivity", intent);
        return !ReportJavaException("opening the installer");
    }

    public static bool LaunchApp(string packageName)
    {
        if (!IsAvailable)
        {
            return false;
        }

        Variant launchIntent = PackageManager.Call("getLaunchIntentForPackage", packageName);

        if (launchIntent.VariantType == Variant.Type.Nil)
        {
            ClearJavaException();
            return false;
        }

        GodotObject intent = launchIntent.AsGodotObject();
        intent.Call("addFlags", NewTask);
        Activity.Call("startActivity", intent);
        return !ReportJavaException($"opening {packageName}");
    }

    private const string PlayStorePackage = "com.android.vending";

    public static bool OpenStoreListing(string packageName)
    {
        if (!IsAvailable || string.IsNullOrEmpty(packageName) || !IsInstalled(PlayStorePackage))
        {
            return false;
        }

        GodotObject listingUri = JavaClassWrapper.Wrap("android.net.Uri").Call("parse", "market://details?id=" + packageName).AsGodotObject();
        GodotObject intent = JavaClassWrapper.Wrap("android.content.Intent").Call("Intent", "android.intent.action.VIEW", listingUri).AsGodotObject();
        intent.Call("setPackage", PlayStorePackage);
        intent.Call("addFlags", NewTask);
        Activity.Call("startActivity", intent);
        return !ReportJavaException($"opening the Play Store listing for {packageName}");
    }

    public static bool OpenGameInApp(string packageName, AndroidLaunchSpec launch, string gamePath, System.Collections.Generic.Dictionary<string, string> extras)
    {
        if (!IsAvailable)
        {
            return false;
        }

        GodotObject intent = JavaClassWrapper.Wrap("android.content.Intent").Call("Intent", launch.Action ?? "android.intent.action.VIEW").AsGodotObject();
        intent.Call("setClassName", packageName, launch.Activity);
        int flags = NewTask;

        if (launch.PassesGameAsDocument)
        {
            GodotObject documentUri = AndroidStorage.DocumentUriForPath(gamePath);

            if (documentUri == null)
            {
                GD.PrintErr($"[Android] no folder access covers {gamePath}.");
                return false;
            }

            intent.Call("setDataAndType", documentUri, "*/*");
            flags |= GrantReadUriPermission;

            if (launch.Game.StartsWith(AndroidLaunchSpec.DocumentExtraGamePrefix))
            {
                intent.Call("putExtra", launch.Game.Substring(AndroidLaunchSpec.DocumentExtraGamePrefix.Length), documentUri.Call("toString").AsString());
            }
        }

        else if (launch.Game != null && launch.Game.StartsWith(AndroidLaunchSpec.ExtraGamePrefix))
        {
            intent.Call("putExtra", launch.Game.Substring(AndroidLaunchSpec.ExtraGamePrefix.Length), gamePath);
        }

        foreach (var extra in extras)
        {
            intent.Call("putExtra", extra.Key, extra.Value);
        }

        if (launch.RestartTask)
        {
            flags |= ClearTask | ClearTop;
        }

        intent.Call("addFlags", flags);
        Activity.Call("startActivity", intent);
        return !ReportJavaException($"opening {gamePath} in {packageName}");
    }

    private static GodotObject ContentUriForFile(string filePath)
    {
        GodotObject file = JavaClassWrapper.Wrap("java.io.File").Call("File", filePath).AsGodotObject();
        string authority = Activity.Call("getPackageName").AsString() + ".fileprovider";
        GodotObject contentUri = JavaClassWrapper.Wrap("androidx.core.content.FileProvider")
            .Call("getUriForFile", Activity, authority, file).AsGodotObject();

        return ReportJavaException($"resolving a content URI for {filePath}") ? null : contentUri;
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
