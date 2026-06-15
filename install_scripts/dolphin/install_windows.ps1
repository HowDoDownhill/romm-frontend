param (
    [Parameter(Mandatory=$true)]
    [string]$InstallDirectory
)

# Variable for the final directory name you want after extraction
$DesiredDirectoryName = "dolphin"

# Force PowerShell to use TLS 1.2
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$EmulatorDir = $InstallDirectory

Write-Host "STATUS: Starting dolphin Emulator installation..."
Write-Host "STATUS: Target directory: $EmulatorDir"

if (!(Test-Path -Path $EmulatorDir)) {
    Write-Host "STATUS: Creating installation directory..."
    New-Item -ItemType Directory -Force -Path $EmulatorDir | Out-Null
}

$DownloadUrl = "https://dl.dolphin-emu.org/releases/2603a/dolphin-2603a-x64.7z"
$FileName = "dolphin-2603a-x64.7z"
$ArchiveFilePath = Join-Path -Path $InstallDirectory -ChildPath $FileName

Write-Host "STATUS: Downloading dolphin archive from $DownloadUrl..."
try {
    $webClient = New-Object System.Net.WebClient
    $webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36")
    $webClient.DownloadFile($DownloadUrl, $ArchiveFilePath)
    $webClient.Dispose()
} catch {
    Write-Error "ERROR: Download failed."
    if ($_.Exception.Response) {
        $response = $_.Exception.Response
        $statusCode = [int]$response.StatusCode
        $statusDescription = $response.StatusDescription

        Write-Error "StatusCode: $statusCode"
        Write-Error "StatusDescription: $statusDescription"

        $responseStream = $response.GetResponseStream()
        $streamReader = New-Object System.IO.StreamReader($responseStream)
        $responseBody = $streamReader.ReadToEnd()
        $streamReader.Close()
        $responseStream.Close()

        Write-Error "--- SERVER RESPONSE HEADERS ---"
        $response.Headers.AllKeys | ForEach-Object { Write-Error "$_ : $($response.Headers[$_])" }

        Write-Error "--- SERVER RESPONSE BODY ---"
        Write-Error $responseBody
    } else {
        Write-Error "An exception occurred, but no HTTP response was received."
        Write-Error $_.Exception.Message
    }
    exit 1
}

Write-Host "STATUS: Extracting archive..."
$7zPath = ($PSScriptRoot | Split-Path -Parent | Split-Path -Parent) + "/tools/7zip/windows/7za.exe"

if (Test-Path $7zPath) {
    try {
        $process = Start-Process -FilePath $7zPath -ArgumentList "x `"$ArchiveFilePath`" -o`"$EmulatorDir`" -y" -Wait -NoNewWindow -PassThru
        if ($process.ExitCode -ne 0) {
            Write-Error "ERROR: 7-Zip extraction failed with code $($process.ExitCode)."
            exit 1
        }
    } catch {
        Write-Error "ERROR: Failed to execute 7-Zip: $_"
        exit 1
    }
} else {
    Write-Error "ERROR: Bundled 7-Zip not found at $7zPath"
    exit 1
}

Write-Host "STATUS: Cleaning up temporary files..."
Remove-Item -Path $ArchiveFilePath -Force

# --- NEW RENAME LOGIC ---
Write-Host "STATUS: Locating and renaming extracted directory..."
# Locate the folder created by 7-zip (usually named something like 'dolphin-0.10.5-win64')
$ExtractedDir = Get-ChildItem -Path $EmulatorDir -Directory | Where-Object { $_.Name -like "dolphin-*" } | Select-Object -First 1

if ($ExtractedDir) {
    $NewDirPath = Join-Path -Path $EmulatorDir -ChildPath $DesiredDirectoryName

    # If a folder with the desired name already exists, remove it to prevent rename errors
    if (Test-Path -Path $NewDirPath) {
        Write-Host "STATUS: Removing existing '$DesiredDirectoryName' directory to make room for fresh install..."
        Remove-Item -Path $NewDirPath -Recurse -Force
    }

    Rename-Item -Path $ExtractedDir.FullName -NewName $DesiredDirectoryName
    Write-Host "STATUS: Successfully renamed '$($ExtractedDir.Name)' to '$DesiredDirectoryName'."
} else {
    Write-Warning "WARNING: Could not automatically locate the extracted dolphin folder to rename."
}

Write-Host "STATUS: Installation complete!"
exit 0