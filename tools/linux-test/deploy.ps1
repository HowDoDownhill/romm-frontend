<#
.SYNOPSIS
    Exports the Linux build, pushes it to the Arch test machine over SSH and drives it there.

.EXAMPLE
    tools\linux-test\deploy.ps1                       # export, sync, launch
    tools\linux-test\deploy.ps1 -SkipBuild            # sync and launch the existing build
    tools\linux-test\deploy.ps1 -Action logs          # tail the remote log
    tools\linux-test\deploy.ps1 -Action shot          # screenshot the remote screen back to here
    tools\linux-test\deploy.ps1 -Action stop
#>
[CmdletBinding()]
param(
    [ValidateSet('deploy', 'sync', 'run', 'stop', 'status', 'logs', 'shot', 'env', 'push-login')]
    [string]$Action = 'deploy',

    [switch]$SkipBuild,
    [switch]$PushLogin,
    [ValidateSet('default', 'discrete', 'integrated')]
    [string]$Gpu = 'discrete',
    [string[]]$AppArgs = @(),
    [int]$LogLines = 200,
    [string]$ConfigPath
)

$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = (Resolve-Path (Join-Path $ScriptDir '..\..')).Path
$LinuxBuildDir = Join-Path $ProjectRoot 'build\linux'

if (-not $ConfigPath) { $ConfigPath = Join-Path $ScriptDir 'config.json' }

# build/ is gitignored, so this marker does not survive a fresh checkout and has to be
# recreated. Without it Godot scans the build output as project resources - screenshots
# pulled back from the test machine get imported and packed into romm-frontend.pck.
function Set-BuildDirectoryIgnored {
    New-Item -ItemType Directory -Force (Join-Path $ProjectRoot 'build') | Out-Null
    $ignoreMarker = Join-Path $ProjectRoot 'build\.gdignore'
    if (-not (Test-Path $ignoreMarker)) { New-Item -ItemType File $ignoreMarker | Out-Null }
}

function Read-DeployConfig {
    if (-not (Test-Path $ConfigPath)) {
        throw "No config at $ConfigPath. Copy config.example.json to config.json and fill in your host, then run setup.ps1."
    }
    $config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
    foreach ($required in 'host', 'user') {
        if (-not $config.$required) { throw "config.json is missing '$required'." }
    }
    if (-not $config.port) { $config | Add-Member port 22 -Force }
    if (-not $config.remoteDir) { $config | Add-Member remoteDir 'romm-frontend-test' -Force }
    if (-not $config.identityFile) {
        $config | Add-Member identityFile (Join-Path $env:USERPROFILE '.ssh\romm-linux-test') -Force
    }
    if (-not $config.wslDistro) { $config | Add-Member wslDistro 'archlinux' -Force }
    return $config
}

$Config = Read-DeployConfig
$Target = "$($Config.user)@$($Config.host)"
$RemoteScript = "$($Config.remoteDir)/remote-run.sh"

$RemoteKeyName = Split-Path $Config.identityFile -Leaf

# Redirecting a native command's stderr wraps each line in an ErrorRecord under
# Windows PowerShell 5.1, which a Stop preference turns into a thrown terminating
# error even when ssh exits 0. Exit code is the only trustworthy signal here.
function Invoke-RemoteCommand {
    param([string]$CommandLine)

    $sshArgs = @(
        '-p', $Config.port,
        '-i', $Config.identityFile,
        '-o', 'StrictHostKeyChecking=accept-new',
        '-o', 'BatchMode=yes',
        $Target,
        $CommandLine
    )

    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & ssh @sshArgs 2>&1 | ForEach-Object { "$_" }
        $exit = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousPreference
    }

    $output | ForEach-Object { Write-Host $_ }
    if ($exit -ne 0) { throw "Remote command failed (exit $exit): $CommandLine" }
}

function Invoke-Scp {
    param([string]$Source, [string]$Destination)

    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & scp -P $Config.port -i $Config.identityFile -o StrictHostKeyChecking=accept-new $Source $Destination 2>&1 |
            ForEach-Object { Write-Verbose "$_" }
        $exit = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousPreference
    }
    if ($exit -ne 0) { throw "scp failed (exit $exit): $Source -> $Destination" }
}

function Export-LinuxBuild {
    $godot = $env:GODOT_BIN
    if (-not $godot) { $godot = 'E:\Godot\Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64.exe' }
    if (-not (Test-Path $godot)) { throw "Godot not found at '$godot'. Set GODOT_BIN to override." }

    Write-Host "Exporting Linux build..." -ForegroundColor Cyan
    Set-BuildDirectoryIgnored
    New-Item -ItemType Directory -Force $LinuxBuildDir | Out-Null

    # Godot's GUI executable detaches from the console and returns immediately, leaving
    # $LASTEXITCODE unset - an export that is still running looks like one that failed.
    # Start-Process -Wait is the only reliable way to wait for it and read a real exit code.
    $godotArguments = @(
        '--headless',
        '--path', "`"$ProjectRoot`"",
        '--export-release', '"Linux Desktop"',
        "`"$(Join-Path $LinuxBuildDir 'romm-frontend.x86_64')`""
    )
    $godotProcess = Start-Process -FilePath $godot -ArgumentList $godotArguments -NoNewWindow -Wait -PassThru

    if ($godotProcess.ExitCode -ne 0) { throw "Godot export failed (exit $($godotProcess.ExitCode))." }

}

# Kept out of Export-LinuxBuild so -SkipBuild still refreshes these. They are plain
# data the app reads at runtime, so a stale copy silently ships old emulator metadata
# and default configs while the binary looks up to date.
function Copy-RuntimeDataIntoBuild {
    # Copy-Item of a directory into a destination that already contains a directory of
    # that name nests it (build/linux/tools/tools). Copy the contents instead.
    foreach ($dir in 'install_scripts', 'tools') {
        $destination = Join-Path $LinuxBuildDir $dir
        New-Item -ItemType Directory -Force $destination | Out-Null
        Copy-Item (Join-Path $ProjectRoot "$dir\*") $destination -Recurse -Force
    }
}

# Only these paths ship. The build folder also accumulates runtime data (config.cfg
# with live credentials, roms, saves, caches) whenever the app is run in place.
function Get-ShippedPaths {
    $shipped = @()
    foreach ($name in 'romm-frontend.x86_64', 'romm-frontend.sh', 'romm-frontend.pck', 'install_scripts', 'tools') {
        $path = Join-Path $LinuxBuildDir $name
        if (Test-Path $path) { $shipped += $path }
    }
    $shipped += (Get-ChildItem $LinuxBuildDir -Filter 'data_romm-frontend_*' | ForEach-Object { $_.FullName })
    return $shipped
}

function ConvertTo-WslPath {
    param([string]$WindowsPath)
    $converted = & wsl -d $Config.wslDistro -- wslpath -a ($WindowsPath -replace '\\', '/')
    if ($LASTEXITCODE -ne 0) { throw "wslpath failed for $WindowsPath" }
    return $converted.Trim()
}

# rsync must exist on both ends. WSL only supplies the sending half - Windows has
# no rsync of its own - so a missing binary on either side means the tarball path.
function Test-RsyncAvailable {
    & wsl -d $Config.wslDistro -- sh -c 'command -v rsync >/dev/null 2>&1' 2>$null
    if ($LASTEXITCODE -ne 0) { return $false }

    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & ssh -p $Config.port -i $Config.identityFile -o BatchMode=yes $Target 'command -v rsync >/dev/null 2>&1' 2>&1 | Out-Null
        $exit = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousPreference
    }
    return ($exit -eq 0)
}

function Sync-WithRsync {
    param([string[]]$Sources)

    $wslSources = $Sources | ForEach-Object { ConvertTo-WslPath $_ }
    $sshCommand = "ssh -p $($Config.port) -i ~/.ssh/$RemoteKeyName -o StrictHostKeyChecking=accept-new"

    # Protect filters keep the test machine's own runtime state across pushes, so a
    # deploy does not wipe its logs, login config or downloaded roms.
    $rsyncArgs = @(
        '-rlt', '--delete', '--chmod=D755,F755', '--info=stats1',
        '-f', 'P .test-state', '-f', 'P config.cfg', '-f', 'P themes.json',
        '-f', 'P saves', '-f', 'P states', '-f', 'P roms', '-f', 'P bios',
        '-f', 'P downloads', '-f', 'P emulators', '-f', 'P *.cache',
        '-e', $sshCommand
    ) + $wslSources + @("${Target}:$($Config.remoteDir)/")

    & wsl -d $Config.wslDistro -- rsync @rsyncArgs
    if ($LASTEXITCODE -ne 0) { throw "rsync failed (exit $LASTEXITCODE)." }
}

function Sync-WithScp {
    param([string[]]$Sources)

    Write-Host "rsync missing on one end - falling back to a full tarball copy." -ForegroundColor Yellow
    $tarball = Join-Path $env:TEMP 'romm-frontend-linux.tar'
    if (Test-Path $tarball) { Remove-Item $tarball -Force }

    $names = $Sources | ForEach-Object { Split-Path $_ -Leaf }
    & tar -cf $tarball -C $LinuxBuildDir @names
    if ($LASTEXITCODE -ne 0) { throw "tar failed (exit $LASTEXITCODE)." }

    Invoke-Scp $tarball "${Target}:$($Config.remoteDir)/build.tar"
    Invoke-RemoteCommand "cd '$($Config.remoteDir)' && tar -xf build.tar && rm -f build.tar"
    Remove-Item $tarball -Force
}

function Push-Build {
    Copy-RuntimeDataIntoBuild

    $sources = Get-ShippedPaths
    if (-not ($sources | Where-Object { $_ -like '*romm-frontend.x86_64' })) {
        throw "No Linux build found in $LinuxBuildDir. Run without -SkipBuild first."
    }

    Write-Host "Syncing $($sources.Count) paths to ${Target}:$($Config.remoteDir)/" -ForegroundColor Cyan
    Invoke-RemoteCommand "mkdir -p '$($Config.remoteDir)'"

    if (Test-RsyncAvailable) { Sync-WithRsync $sources } else { Sync-WithScp $sources }

    Invoke-Scp (Join-Path $ScriptDir 'remote-run.sh') "${Target}:$RemoteScript"
    Invoke-RemoteCommand "sed -i 's/\r`$//' '$RemoteScript' && chmod +x '$RemoteScript' '$($Config.remoteDir)/romm-frontend.x86_64'"
}

# Only the RomM credentials travel. DeviceId is deliberately left behind: RomMAPI
# registers it per machine and SaveSyncManager stamps it on every save, so sharing
# one between Windows and the test box would merge two devices into one.
$LoginKeys = @('Host', 'Username', 'Password', 'ApiKey', 'ValidLoginLastUsed')

function Read-LocalRomMSection {
    $localConfig = Join-Path $ProjectRoot 'config.cfg'
    if (-not (Test-Path $localConfig)) { throw "No config.cfg at $localConfig to take login details from." }

    $values = [ordered]@{}
    $section = ''
    foreach ($rawLine in Get-Content $localConfig) {
        $line = $rawLine -replace "^\xEF\xBB\xBF", '' -replace '^﻿', ''
        if ($line -match '^\s*\[(.+?)\]\s*$') { $section = $Matches[1]; continue }
        if ($section -ne 'RomM') { continue }
        if ($line -match '^\s*([A-Za-z0-9_]+)\s*=\s*(.+?)\s*$') {
            if ($LoginKeys -contains $Matches[1]) { $values[$Matches[1]] = $Matches[2] }
        }
    }

    if ($values.Count -eq 0) { throw "No [RomM] login keys found in $localConfig." }
    return $values
}

function Push-LoginConfig {
    $values = Read-LocalRomMSection

    $lines = @()
    foreach ($key in $values.Keys) { $lines += "$key=$($values[$key])" }

    # Godot's ConfigFile parser treats a leading BOM as part of the first key name,
    # which turns the [RomM] header into a key and silently loses the section.
    $staging = Join-Path $env:TEMP 'romm-login.values'
    [System.IO.File]::WriteAllText($staging, ($lines -join "`n") + "`n", (New-Object System.Text.UTF8Encoding($false)))

    $remoteValues = "$($Config.remoteDir)/.test-state/login.values"
    Invoke-RemoteCommand "mkdir -p '$($Config.remoteDir)/.test-state'"
    Invoke-Scp $staging "${Target}:$remoteValues"
    Remove-Item $staging -Force

    Invoke-RemoteCommand "'$RemoteScript' merge-login '$remoteValues'"

    Write-Host "Pushed login for $($values['Username']) to $($Config.host)" -ForegroundColor Green
    Write-Host "Sent: $($values.Keys -join ', ')"
    Write-Host "Withheld: DeviceId (the test box registers its own with RomM)"
}

function Start-RemoteApp {
    $quoted = ($AppArgs | ForEach-Object { "'$_'" }) -join ' '
    Write-Host "Launching on $($Config.host) (gpu: $Gpu)..." -ForegroundColor Cyan
    Invoke-RemoteCommand "'$RemoteScript' run --gpu $Gpu -- $quoted"
}

function Receive-Screenshot {
    $remotePath = "$($Config.remoteDir)/.test-state/screenshot.png"
    Invoke-RemoteCommand "'$RemoteScript' shot '$remotePath'"

    Set-BuildDirectoryIgnored
    $localDir = Join-Path $ProjectRoot 'build\linux-test-shots'
    New-Item -ItemType Directory -Force $localDir | Out-Null
    $localPath = Join-Path $localDir ("screenshot-{0:yyyyMMdd-HHmmss}.png" -f (Get-Date))

    Invoke-Scp "${Target}:$remotePath" $localPath
    Write-Host $localPath -ForegroundColor Green
}

switch ($Action) {
    'deploy' {
        if (-not $SkipBuild) { Export-LinuxBuild }
        Push-Build
        if ($PushLogin) { Push-LoginConfig }
        Start-RemoteApp
    }
    'sync' {
        if (-not $SkipBuild) { Export-LinuxBuild }
        Push-Build
        if ($PushLogin) { Push-LoginConfig }
    }
    'push-login' { Push-LoginConfig }
    'run'    { Start-RemoteApp }
    'stop'   { Invoke-RemoteCommand "'$RemoteScript' stop" }
    'status' { Invoke-RemoteCommand "'$RemoteScript' status" }
    'logs'   { Invoke-RemoteCommand "'$RemoteScript' logs $LogLines" }
    'shot'   { Receive-Screenshot }
    'env'    { Invoke-RemoteCommand "'$RemoteScript' env" }
}
