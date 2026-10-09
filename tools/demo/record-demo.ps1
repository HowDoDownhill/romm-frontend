param(
    [string]$GodotBin = $env:GODOT_BIN,
    [string]$FFmpeg = 'ffmpeg',
    [string]$OutDir = (Join-Path $PSScriptRoot '..\..\releases')
)

# Records the scripted UI tour (--ui-demo) at a fixed 60 fps and encodes two MP4s: a full-quality
# one and one under 10 MB for embedding on GitHub. The tour drives the app with simulated input,
# so it needs a logged-in config.cfg and a library with cover art. Your saved theme, background
# and game view are not changed.

$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

if (-not $GodotBin -or -not (Test-Path $GodotBin)) {
    throw 'Set GODOT_BIN or pass -GodotBin with the path to the Godot mono console executable.'
}

New-Item -ItemType Directory -Force $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path
$work = Join-Path ([IO.Path]::GetTempPath()) "romm-frontend-demo-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force $work | Out-Null

# Godot reads movie quality from project settings; override.cfg raises it for this run only.
$override = Join-Path $project 'override.cfg'
$overrideBackup = $null
if (Test-Path $override) {
    $overrideBackup = Get-Content $override -Raw
}
Set-Content -Path $override -Value "[editor]`n`nmovie_writer/mjpeg_quality=0.95`n" -Encoding ascii

try {
    $avi = Join-Path $work 'demo.avi'
    $log = Join-Path $work 'log.txt'
    Start-Process -FilePath $GodotBin -NoNewWindow -Wait `
        -ArgumentList @('--path', "`"$project`"", '--windowed', '--resolution', '1920x1080',
                        '--write-movie', "`"$avi`"", '--fixed-fps', '60',
                        '--', '--ui-demo', '--ui-capture-size=1920x1080') `
        -RedirectStandardOutput $log -RedirectStandardError (Join-Path $work 'err.txt')
}
finally {
    if ($null -ne $overrideBackup) { Set-Content -Path $override -Value $overrideBackup -NoNewline }
    else { Remove-Item $override -Force }
}

$started = Select-String -Path $log -Pattern '\[Demo\] started at frame (\d+)' | Select-Object -First 1
if (-not $started) {
    throw "The tour did not start. See $log (is the app logged in?)."
}

# Everything before the tour starts is the loading screen; skip it.
$startSeconds = [int]$started.Matches[0].Groups[1].Value / 60.0

$full = Join-Path $OutDir 'romm-frontend-demo.mp4'
$small = Join-Path $OutDir 'romm-frontend-demo-small.mp4'
& $FFmpeg -y -loglevel error -ss $startSeconds -i $avi -c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p -movflags +faststart -an $full
& $FFmpeg -y -loglevel error -ss $startSeconds -i $avi -c:v libx264 -preset slow -crf 30 -pix_fmt yuv420p -movflags +faststart -an $small

Remove-Item $work -Recurse -Force
Get-Item $full, $small | ForEach-Object { '{0}  {1:N1} MB' -f $_.FullName, ($_.Length / 1MB) }
