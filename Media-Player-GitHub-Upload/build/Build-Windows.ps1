param(
    [string]$Version = "0.6.0"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$projectRoot = Split-Path -Parent $PSScriptRoot
$releaseDir = Join-Path $projectRoot "Release"
$workDir = Join-Path $projectRoot "build-work"
$srcDir = Join-Path $projectRoot "src"
$assetDir = Join-Path $projectRoot "assets"

Remove-Item -LiteralPath $releaseDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $workDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $releaseDir, $workDir | Out-Null

Write-Host "Downloading the current x64 libmpv development package..."
$headers = @{ "User-Agent" = "Luma-Player-GitHub-Build" }
$release = Invoke-RestMethod -Headers $headers -Uri "https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases/latest"
$asset = $release.assets | Where-Object {
    $_.name -match '^mpv-dev-x86_64-[0-9]{8}.*\.7z$' -and $_.name -notmatch 'x86_64-v3'
} | Select-Object -First 1

if ($null -eq $asset) {
    throw "A compatible mpv x64 development package was not found."
}

$archive = Join-Path $workDir $asset.name
Invoke-WebRequest -Headers $headers -Uri $asset.browser_download_url -OutFile $archive

$sevenZip = (Get-Command 7z.exe -ErrorAction SilentlyContinue).Source
if (-not $sevenZip) { $sevenZip = "C:\Program Files\7-Zip\7z.exe" }
if (-not (Test-Path $sevenZip)) { throw "7-Zip is unavailable on the Windows runner." }

$engineDir = Join-Path $workDir "mpv"
New-Item -ItemType Directory -Force -Path $engineDir | Out-Null
& $sevenZip x $archive "-o$engineDir" -y | Out-Null
if ($LASTEXITCODE -ne 0) { throw "The mpv archive could not be extracted." }

$mpvDll = Get-ChildItem -LiteralPath $engineDir -Filter "mpv-2.dll" -File -Recurse | Select-Object -First 1
if ($null -eq $mpvDll) {
    $mpvDll = Get-ChildItem -LiteralPath $engineDir -Filter "libmpv-2.dll" -File -Recurse | Select-Object -First 1
}
if ($null -eq $mpvDll) { throw "libmpv DLL was not found in the downloaded package." }

$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { throw "The .NET Framework C# compiler is unavailable." }

Write-Host "Compiling the native Windows application..."
& $csc /nologo /target:winexe /optimize+ /platform:x64 /langversion:5 /codepage:65001 `
    "/out:$releaseDir\LumaPlayer.exe" `
    "/win32manifest:$projectRoot\app.manifest" `
    "/win32icon:$assetDir\LumaPlayer.ico" `
    /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    "$srcDir\Program.cs" "$srcDir\MpvNative.cs" "$srcDir\TimelineBar.cs" "$srcDir\PlayerForm.cs"
if ($LASTEXITCODE -ne 0) { throw "C# compilation failed." }

Copy-Item -LiteralPath $mpvDll.FullName -Destination (Join-Path $releaseDir "mpv-2.dll") -Force
Copy-Item -LiteralPath (Join-Path $assetDir "LumaPlayer.ico") -Destination (Join-Path $releaseDir "LumaPlayer.ico") -Force

$license = Get-ChildItem -LiteralPath $engineDir -File -Recurse | Where-Object {
    $_.Name -match '^(COPYING|LICENSE)'
} | Select-Object -First 1
if ($null -ne $license) {
    Copy-Item -LiteralPath $license.FullName -Destination (Join-Path $releaseDir "mpv-COPYING.txt") -Force
} else {
    @(
        "Luma Player bundles mpv/libmpv from:"
        $asset.browser_download_url
        "mpv source and license information: https://github.com/mpv-player/mpv"
    ) | Set-Content -LiteralPath (Join-Path $releaseDir "mpv-COPYING.txt") -Encoding UTF8
}

$expectedFileVersion = "$Version.0"
$actualFileVersion = (Get-Item -LiteralPath (Join-Path $releaseDir "LumaPlayer.exe")).VersionInfo.FileVersion
if ($actualFileVersion -ne $expectedFileVersion) {
    throw "Compiled EXE version mismatch. Expected $expectedFileVersion, found $actualFileVersion."
}

$minimumEngineBytes = 5MB
if ((Get-Item -LiteralPath (Join-Path $releaseDir "mpv-2.dll")).Length -lt $minimumEngineBytes) {
    throw "The bundled playback engine is unexpectedly small."
}

Write-Host "Windows application and bundled playback engine verified."
