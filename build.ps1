param(
    [switch]$CreatePortablePackage
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$solution = Join-Path $root 'DestinyChatDesktop.sln'
$wpfProject = Join-Path $root 'src\DestinyChatDesktop.Wpf\DestinyChatDesktop.Wpf.csproj'
$distDir = Join-Path $root 'dist'
$artifactDir = Join-Path $root 'artifacts'
$portableStageDir = Join-Path $artifactDir 'portable'
$portablePackageName = 'DestinyChatDesktop-portable.zip'
$portablePackagePath = Join-Path $artifactDir $portablePackageName
$portableChecksumPath = Join-Path $artifactDir 'DestinyChatDesktop-portable.sha256'

function Ensure-DotNet {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw '.NET SDK not found. Install .NET 8 SDK from https://aka.ms/dotnet/download'
    }
}

Ensure-DotNet

if (-not (Test-Path $solution)) {
    throw "Solution not found: $solution"
}

if (-not (Test-Path $wpfProject)) {
    throw "WPF project not found: $wpfProject"
}

New-Item -ItemType Directory -Force -Path $distDir | Out-Null

Write-Host "Restoring and publishing WPF app to $distDir ..."
dotnet restore $solution
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE."
}

dotnet publish $wpfProject -c Release -o $distDir --self-contained false
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$publishedExe = Join-Path $distDir 'DestinyChatDesktop.exe'
if (-not (Test-Path $publishedExe)) {
    throw "Expected output not found: $publishedExe"
}

Write-Host "Built $publishedExe"

if ($CreatePortablePackage) {
    New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

    if (Test-Path $portableStageDir) {
        Remove-Item -LiteralPath $portableStageDir -Recurse -Force
    }

    if (Test-Path $portablePackagePath) {
        Remove-Item -LiteralPath $portablePackagePath -Force
    }

    if (Test-Path $portableChecksumPath) {
        Remove-Item -LiteralPath $portableChecksumPath -Force
    }

    New-Item -ItemType Directory -Force -Path $portableStageDir | Out-Null
    $excludeFromPackage = @('state.json', 'cookies.json', 'split-self-test.json')
    Get-ChildItem -LiteralPath $distDir | Where-Object { $excludeFromPackage -notcontains $_.Name } | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $portableStageDir -Recurse -Force
    }
    Compress-Archive -Path (Join-Path $portableStageDir '*') -DestinationPath $portablePackagePath -Force

    $hash = (Get-FileHash -Algorithm SHA256 $portablePackagePath).Hash.ToLowerInvariant()
    Set-Content -LiteralPath $portableChecksumPath -Value "$hash *$portablePackageName"

    Remove-Item -LiteralPath $portableStageDir -Recurse -Force
    Write-Host "Packaged portable release to $portablePackagePath"
    Write-Host "Wrote SHA256 to $portableChecksumPath"
}
