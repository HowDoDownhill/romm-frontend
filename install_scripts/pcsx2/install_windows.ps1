param (
    [Parameter(Mandatory=$true)]
    [string]$InstallDirectory
)

# Variable for the final directory name you want after extraction
$DesiredDirectoryName = "pcsx2"

# Force PowerShell to use TLS 1.2
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# FIX: Set the target directory to be the InstallDirectory + pcsx2
$EmulatorDir = Join-Path -Path $InstallDirectory -ChildPath $DesiredDirectoryName

Write-Host "STATUS: Starting pcsx2 Emulator installation..."
Write-Host "STATUS: Target directory: $EmulatorDir"

if (!(Test-Path -Path $EmulatorDir)) {
    Write-Host "STATUS: Creating installation directory..."
    New-Item -ItemType Directory -Force -Path $EmulatorDir | Out-Null
}

$DownloadUrl = "https://github.com/PCSX2/pcsx2/releases/download/v2.6.3/pcsx2-v2.6.3-windows-x64-Qt.7z"
$FileName = "pcsx2-v2.6.3-windows-x64-Qt.7z"
# Download to the root install directory temporarily
$ArchiveFilePath = Join-Path -Path $InstallDirectory -ChildPath $FileName

Write-Host "STATUS: Downloading pcsx2 archive from $DownloadUrl..."
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

Write-Host "STATUS: Extracting archive directly to $EmulatorDir..."
$7zPath = ($PSScriptRoot | Split-Path -Parent | Split-Path -Parent) + "/tools/7zip/windows/7za.exe"

if (Test-Path $7zPath) {
    try {
        # FIX: -o"$EmulatorDir" now points directly to the pcsx2 subfolder
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

Write-Host "STATUS: Installation complete!"
exit 0