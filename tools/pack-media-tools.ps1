param([Parameter(Mandatory=$true)][string]$FFmpegDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot
$target = Join-Path $projectRoot 'CheckInReminder/Assets/MediaTools'
New-Item -ItemType Directory -Force -Path $target | Out-Null
$source = (Resolve-Path -LiteralPath $FFmpegDirectory).Path
foreach ($name in @('ffmpeg.exe', 'ffprobe.exe')) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $name))) { throw "Missing $name" }
}
$files = @((Join-Path $source 'ffmpeg.exe'), (Join-Path $source 'ffprobe.exe')) + @(Get-ChildItem -LiteralPath $source -Filter '*.dll' | Select-Object -ExpandProperty FullName)
$license = Join-Path (Split-Path $source) 'LICENSE.txt'
if (-not (Test-Path -LiteralPath $license)) { throw 'The build license must be present in the parent directory.' }
$files += $license
Copy-Item -LiteralPath $license -Destination (Join-Path $target 'LICENSE-FFmpeg.txt') -Force
Compress-Archive -LiteralPath $files -DestinationPath (Join-Path $target 'tools.zip') -Force
& (Join-Path $source 'ffmpeg.exe') -version | Out-File -LiteralPath (Join-Path $target 'build-info.txt') -Encoding utf8
Get-FileHash -LiteralPath (Join-Path $target 'tools.zip') -Algorithm SHA256
