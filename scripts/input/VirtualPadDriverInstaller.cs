using Godot;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

public static class VirtualPadDriverInstaller
{
    public const string DriverDisplayName = "Virtual Controller Driver";

    private const string DriverDownloadUrl = "https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/ViGEmBus_1.22.0_x64_x86_arm64.exe";
    private const string DriverInstallerFileName = "ViGEmBus_1.22.0_x64_x86_arm64.exe";
    private const long ExpectedInstallerSizeBytes = 6278576;
    private const string ExpectedPublisherName = "Nefarius Software Solutions";
    private const string SilentInstallArguments = "/exenoui /qn";
    private const int ElevationCancelledErrorCode = 1223;
    private const int DriverAppearanceTimeoutSeconds = 90;
    private const int DriverAppearancePollMilliseconds = 1000;

    public static bool IsSupportedPlatform => OperatingSystem.IsWindows();

    public static async Task<bool> DownloadAndInstall(AppInstance appInstance, Func<bool> isDriverPresent, Action<string> reportStatus)
    {
        if (!IsSupportedPlatform)
        {
            reportStatus("The controller driver is only needed on Windows.");
            return false;
        }

        if (isDriverPresent())
        {
            return true;
        }

        string installerPath = Path.Combine(Path.GetTempPath(), DriverInstallerFileName);

        if (!await DownloadInstaller(appInstance, installerPath, reportStatus))
        {
            return false;
        }

        if (!VerifyInstaller(installerPath, reportStatus))
        {
            DeleteInstaller(installerPath);
            return false;
        }

        if (!RunInstallerElevated(installerPath, reportStatus))
        {
            DeleteInstaller(installerPath);
            return false;
        }

        bool driverAppeared = await WaitForDriverToAppear(isDriverPresent, reportStatus);
        DeleteInstaller(installerPath);

        reportStatus(driverAppeared
            ? "Controller driver installed."
            : "The controller driver did not finish installing. A restart may be required.");

        return driverAppeared;
    }

    private static async Task<bool> DownloadInstaller(AppInstance appInstance, string installerPath, Action<string> reportStatus)
    {
        reportStatus("Downloading the controller driver...");

        if (await UniversalInstaller.DownloadFileAsync(appInstance, DriverDownloadUrl, installerPath, DriverDisplayName))
        {
            return true;
        }

        reportStatus("Could not download the controller driver.");
        return false;
    }

    private static bool VerifyInstaller(string installerPath, Action<string> reportStatus)
    {
        var installerFile = new FileInfo(installerPath);

        if (!installerFile.Exists || installerFile.Length != ExpectedInstallerSizeBytes)
        {
            reportStatus("The downloaded driver did not match the expected file. Installation cancelled.");
            GD.PrintErr($"[InputLayer] driver installer size {installerFile.Length} did not match the expected {ExpectedInstallerSizeBytes}.");
            return false;
        }

        if (!DescribesExpectedPublisher(installerPath, out string publisherSubject))
        {
            reportStatus("The downloaded driver was not signed by the expected publisher. Installation cancelled.");
            GD.PrintErr($"[InputLayer] driver installer publisher was '{publisherSubject}'.");
            return false;
        }

        return true;
    }

    private static bool DescribesExpectedPublisher(string installerPath, out string publisherSubject)
    {
        publisherSubject = "";

        try
        {
            using var signingCertificate = new X509Certificate2(System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(installerPath));
            publisherSubject = signingCertificate.Subject;
            return publisherSubject.Contains(ExpectedPublisherName, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception signatureFailure)
        {
            publisherSubject = $"unreadable ({signatureFailure.GetType().Name})";
            return false;
        }
    }

    private static bool RunInstallerElevated(string installerPath, Action<string> reportStatus)
    {
        reportStatus("Installing the controller driver. Please accept the Windows prompt...");

        var processStartInfo = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = SilentInstallArguments,
            UseShellExecute = true,
            Verb = "runas"
        };

        try
        {
            using Process installerProcess = Process.Start(processStartInfo);
            installerProcess.WaitForExit();
            GD.Print($"[InputLayer] driver installer exited with code {installerProcess.ExitCode}; verifying by driver presence instead.");
            return true;
        }
        catch (System.ComponentModel.Win32Exception elevationFailure) when (elevationFailure.NativeErrorCode == ElevationCancelledErrorCode)
        {
            reportStatus("Driver installation was cancelled.");
            return false;
        }
        catch (Exception installationFailure)
        {
            reportStatus("The controller driver installer could not be started.");
            GD.PrintErr($"[InputLayer] could not start the driver installer: {installationFailure.Message}");
            return false;
        }
    }

    private static async Task<bool> WaitForDriverToAppear(Func<bool> isDriverPresent, Action<string> reportStatus)
    {
        reportStatus("Waiting for the controller driver to start...");

        for (int elapsedSeconds = 0; elapsedSeconds < DriverAppearanceTimeoutSeconds; elapsedSeconds++)
        {
            if (isDriverPresent())
            {
                return true;
            }

            await Task.Delay(DriverAppearancePollMilliseconds);
        }

        return isDriverPresent();
    }

    private static void DeleteInstaller(string installerPath)
    {
        try
        {
            File.Delete(installerPath);
        }
        catch (Exception deletionFailure)
        {
            GD.Print($"[InputLayer] could not remove the downloaded driver installer: {deletionFailure.Message}");
        }
    }
}
