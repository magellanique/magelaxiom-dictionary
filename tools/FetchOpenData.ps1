$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$raw = Join-Path $root "data\raw"

New-Item -ItemType Directory -Force -Path $raw | Out-Null

function Download-IfMissing {
    param(
        [string]$Url,
        [string]$OutFile
    )

    if (Test-Path -LiteralPath $OutFile) {
        Write-Host "Already exists: $OutFile"
        return
    }

    Write-Host "Downloading $Url"
    curl.exe -L -o $OutFile $Url
}

function Expand-ZipIfMissing {
    param(
        [string]$Archive,
        [string]$Target,
        [string]$ExpectedPath
    )

    if (Test-Path -LiteralPath $ExpectedPath) {
        Write-Host "Already extracted: $ExpectedPath"
        return
    }

    New-Item -ItemType Directory -Force -Path $Target | Out-Null
    Expand-Archive -Path $Archive -DestinationPath $Target -Force
}

$oewnZip = Join-Path $raw "english-wordnet-2025.zip"

Download-IfMissing "https://github.com/globalwordnet/english-wordnet/releases/download/2025-edition/english-wordnet-2025.zip" $oewnZip

Expand-ZipIfMissing $oewnZip (Join-Path $raw "oewn") (Join-Path $raw "oewn\oewn2025\data.noun")

Write-Host "Open data is ready under $raw"
