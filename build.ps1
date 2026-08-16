param(
    [switch]$RefreshData
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root "src\Magelaxiom\Program.cs"
$builderSrc = Join-Path $root "tools\OfflineDataBuilder.cs"
$build = Join-Path $root "build"
$dist = Join-Path $root "dist"
$builderOut = Join-Path $build "OfflineDataBuilder.exe"
$out = Join-Path $dist "Magelaxiom.exe"
$dictionaryResource = Join-Path $root "data\generated\dictionary.tsv"
$dictionaryBinaryResource = Join-Path $root "data\generated\dictionary.bin"
$logoResource = Join-Path $root "assets\magelaxiom-logo.png"
$iconResource = Join-Path $build "magelaxiom-logo.ico"
$iconBuilder = Join-Path $root "tools\MakeIcon.ps1"
$compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "C# compiler not found: $compiler"
}

New-Item -ItemType Directory -Force -Path $build | Out-Null
New-Item -ItemType Directory -Force -Path $dist | Out-Null

if (-not (Test-Path -LiteralPath $logoResource)) {
    throw "Logo asset not found: $logoResource"
}

if ($RefreshData -or
    -not (Test-Path -LiteralPath $dictionaryResource) -or
    -not (Test-Path -LiteralPath $dictionaryBinaryResource)) {
    & $compiler @(
        "/nologo",
        "/target:exe",
        "/optimize+",
        "/out:$builderOut",
        "/reference:System.dll",
        "/reference:System.Core.dll",
        "/reference:System.Xml.dll",
        $builderSrc
    )

    if ($LASTEXITCODE -ne 0) {
        throw "OfflineDataBuilder compile failed with exit code $LASTEXITCODE"
    }

    & $builderOut $root

    if ($LASTEXITCODE -ne 0) {
        throw "OfflineDataBuilder failed with exit code $LASTEXITCODE"
    }
}

if (-not (Test-Path -LiteralPath $iconResource) -or
    (Get-Item -LiteralPath $iconResource).LastWriteTime -lt (Get-Item -LiteralPath $logoResource).LastWriteTime) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $iconBuilder -SourcePng $logoResource -OutIco $iconResource

    if ($LASTEXITCODE -ne 0) {
        throw "Icon generation failed with exit code $LASTEXITCODE"
    }
}

& $compiler @(
    "/nologo",
    "/target:winexe",
    "/optimize+",
    "/out:$out",
    "/win32icon:$iconResource",
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Windows.Forms.dll",
    "/resource:$dictionaryBinaryResource,Magelaxiom.dictionary.bin",
    "/resource:$logoResource,Magelaxiom.logo.png",
    $src
)

if ($LASTEXITCODE -ne 0) {
    throw "csc.exe failed with exit code $LASTEXITCODE"
}

Write-Host "Built $out"
