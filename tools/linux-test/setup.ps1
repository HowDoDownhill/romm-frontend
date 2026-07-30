<#
.SYNOPSIS
    One-time setup for pushing builds to the Arch test machine.
    Generates a dedicated SSH key, installs it on the target, and checks the tools each side needs.
#>
[CmdletBinding()]
param([string]$ConfigPath)

$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $ConfigPath) { $ConfigPath = Join-Path $ScriptDir 'config.json' }

if (-not (Test-Path $ConfigPath)) {
    throw "No config at $ConfigPath. Copy config.example.json to config.json and fill in host/user first."
}

$Config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
if (-not $Config.port) { $Config | Add-Member port 22 -Force }
if (-not $Config.wslDistro) { $Config | Add-Member wslDistro 'archlinux' -Force }
if (-not $Config.identityFile) {
    $Config | Add-Member identityFile (Join-Path $env:USERPROFILE '.ssh\romm-linux-test') -Force
}
$Target = "$($Config.user)@$($Config.host)"

Write-Host "=== 1. SSH key ===" -ForegroundColor Cyan
$sshDir = Split-Path -Parent $Config.identityFile
if (-not (Test-Path $sshDir)) { New-Item -ItemType Directory -Force $sshDir | Out-Null }

if (Test-Path $Config.identityFile) {
    Write-Host "Key already exists: $($Config.identityFile)"
} else {
    & ssh-keygen -t ed25519 -f $Config.identityFile -N '""' -C 'romm-frontend-linux-test'
    if ($LASTEXITCODE -ne 0) { throw "ssh-keygen failed." }
    Write-Host "Created $($Config.identityFile)"
}

Write-Host ""
Write-Host "=== 2. Install the key on $Target ===" -ForegroundColor Cyan
Write-Host "You will be prompted for the Arch machine's password once."
$publicKey = (Get-Content "$($Config.identityFile).pub" -Raw).Trim()
$installCommand = "set -e; mkdir -p ~/.ssh; chmod 700 ~/.ssh; touch ~/.ssh/authorized_keys; " +
                  "if ! grep -qxF '$publicKey' ~/.ssh/authorized_keys; then echo '$publicKey' >> ~/.ssh/authorized_keys; fi; " +
                  "chmod 600 ~/.ssh/authorized_keys; echo installed"
& ssh -p $Config.port -o StrictHostKeyChecking=accept-new $Target $installCommand
if ($LASTEXITCODE -ne 0) { throw "Could not install the key. Is sshd running on the Arch machine (systemctl status sshd)?" }

Write-Host ""
Write-Host "=== 3. Verify key-only login ===" -ForegroundColor Cyan
& ssh -p $Config.port -i $Config.identityFile -o BatchMode=yes -o StrictHostKeyChecking=accept-new $Target 'echo key login OK'
if ($LASTEXITCODE -ne 0) { throw "Key login failed." }

Write-Host ""
Write-Host "=== 4. Tools on the Arch machine ===" -ForegroundColor Cyan
$remoteCheck = @'
for tool in rsync grim; do
    if command -v "$tool" >/dev/null 2>&1; then echo "  ok      $tool"; else echo "  MISSING $tool"; fi
done
echo "  session $(loginctl show-session "$(loginctl list-sessions --no-legend | awk 'NR==1{print $1}')" -p Type --value 2>/dev/null)"
'@
& ssh -p $Config.port -i $Config.identityFile -o BatchMode=yes $Target $remoteCheck

Write-Host ""
Write-Host "=== 5. rsync in WSL (delta sync from Windows) ===" -ForegroundColor Cyan
& wsl -d $Config.wslDistro -- sh -c 'command -v rsync >/dev/null 2>&1'
if ($LASTEXITCODE -eq 0) {
    Write-Host "  ok      rsync in WSL/$($Config.wslDistro)"
} else {
    Write-Host "  MISSING rsync in WSL/$($Config.wslDistro)" -ForegroundColor Yellow
    Write-Host "          wsl -d $($Config.wslDistro) -u root -- pacman -Sy --noconfirm rsync openssh"
    Write-Host "          (without it, deploy.ps1 falls back to a slower full tarball copy)"
}

$remoteKeyName = Split-Path $Config.identityFile -Leaf
$wslKeyCheck = & wsl -d $Config.wslDistro -- sh -c "test -f ~/.ssh/$remoteKeyName && echo yes || echo no"
if ("$wslKeyCheck".Trim() -eq 'yes') {
    Write-Host "  ok      key present in WSL"
} else {
    Write-Host "  Copying key into WSL so rsync can use it..."
    # The key cannot be referenced in place at /mnt/c/... - drvfs reports 777 and ssh
    # refuses a world-readable private key. Copy it inside WSL rather than piping it
    # through PowerShell, which re-encodes the stream and corrupts the PEM.
    $wslKeyPath = "$(& wsl -d $Config.wslDistro -- wslpath -a ($Config.identityFile -replace '\\', '/'))".Trim()
    & wsl -d $Config.wslDistro -- sh -c "mkdir -p ~/.ssh && chmod 700 ~/.ssh && tr -d '\r' < '$wslKeyPath' > ~/.ssh/$remoteKeyName && chmod 600 ~/.ssh/$remoteKeyName && echo copied"
    if ($LASTEXITCODE -ne 0) { throw "Failed to copy the key into WSL." }
}

Write-Host ""
Write-Host "Setup complete. Deploy with:" -ForegroundColor Green
Write-Host "  tools\linux-test\deploy.ps1"
