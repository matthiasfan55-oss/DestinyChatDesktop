param(
    [switch]$CreatePortablePackage
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceFile = Join-Path $root 'DestinyChatDesktop.cs'
$configFile = Join-Path $root 'appsettings.json'
$manifestFile = Join-Path $root 'app.manifest'
$outDir = Join-Path $root 'dist'
$artifactDir = Join-Path $root 'artifacts'
$portableStageDir = Join-Path $artifactDir 'portable'
$portablePackageName = 'DestinyChatDesktop-portable.zip'
$portablePackagePath = Join-Path $artifactDir $portablePackageName
$portableChecksumPath = Join-Path $artifactDir 'DestinyChatDesktop-portable.sha256'
$sdkVersion = '1.0.3856.49'
$sdkRoot = Join-Path $root 'sdk\microsoft.web.webview2'
$packageDir = Join-Path $sdkRoot $sdkVersion
$packageFile = Join-Path $packageDir "microsoft.web.webview2.$sdkVersion.nupkg"
$extractDir = Join-Path $packageDir 'package'
$coreDll = Join-Path $extractDir 'lib\net462\Microsoft.Web.WebView2.Core.dll'
$formsDll = Join-Path $extractDir 'lib\net462\Microsoft.Web.WebView2.WinForms.dll'
$loaderDll = Join-Path $extractDir 'runtimes\win-x64\native\WebView2Loader.dll'
$outExe = Join-Path $outDir 'DestinyChatDesktop.exe'
$rootExe = Join-Path $root 'DestinyChatDesktop.exe'
$rootConfigFile = Join-Path $root 'appsettings.json'
$rootCoreDll = Join-Path $root 'Microsoft.Web.WebView2.Core.dll'
$rootFormsDll = Join-Path $root 'Microsoft.Web.WebView2.WinForms.dll'
$rootLoaderDll = Join-Path $root 'WebView2Loader.dll'

function Get-CSharpCompiler {
    $candidates = @(
        'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe',
        'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw 'Unable to find csc.exe.'
}

function Ensure-WebView2Sdk {
    if ((Test-Path $coreDll) -and (Test-Path $formsDll) -and (Test-Path $loaderDll)) {
        return
    }

    New-Item -ItemType Directory -Force -Path $packageDir | Out-Null

    if (-not (Test-Path $packageFile)) {
        $packageUrl = "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$sdkVersion/microsoft.web.webview2.$sdkVersion.nupkg"
        Invoke-WebRequest -Uri $packageUrl -OutFile $packageFile
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (Test-Path $extractDir) {
        Remove-Item -LiteralPath $extractDir -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $extractDir | Out-Null
    [System.IO.Compression.ZipFile]::ExtractToDirectory($packageFile, $extractDir)
}

function Copy-BestEffort {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Source,
        [Parameter(Mandatory = $true)]
        [string]$Destination
    )

    try {
        Copy-Item $Source $Destination -Force
        return $true
    }
    catch {
        Write-Warning "Could not copy $Source to $Destination. Close the app and build again if you want the root-level files refreshed."
        return $false
    }
}

Ensure-WebView2Sdk

if (-not (Test-Path $sourceFile)) {
    throw "Source file not found: $sourceFile"
}

if (-not (Test-Path $configFile)) {
    throw "Config file not found: $configFile"
}

if (-not (Test-Path $manifestFile)) {
    throw "Manifest file not found: $manifestFile"
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$compiler = Get-CSharpCompiler

& $compiler `
    /nologo `
    /target:winexe `
    /platform:x64 `
    /optimize+ `
    /win32manifest:$manifestFile `
    /out:$outExe `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    /reference:System.Web.Extensions.dll `
    /reference:System.IO.Compression.FileSystem.dll `
    /reference:$coreDll `
    /reference:$formsDll `
    $sourceFile

if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE."
}

Copy-Item $coreDll $outDir -Force
Copy-Item $formsDll $outDir -Force
Copy-Item $loaderDll $outDir -Force
Copy-Item $configFile $outDir -Force
Copy-BestEffort -Source $outExe -Destination $rootExe | Out-Null
Copy-BestEffort -Source $coreDll -Destination $rootCoreDll | Out-Null
Copy-BestEffort -Source $formsDll -Destination $rootFormsDll | Out-Null
Copy-BestEffort -Source $loaderDll -Destination $rootLoaderDll | Out-Null
if ([System.IO.Path]::GetFullPath($configFile) -ne [System.IO.Path]::GetFullPath($rootConfigFile)) {
    Copy-BestEffort -Source $configFile -Destination $rootConfigFile | Out-Null
}

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
    Copy-Item (Join-Path $outDir '*') $portableStageDir -Force
    Compress-Archive -Path (Join-Path $portableStageDir '*') -DestinationPath $portablePackagePath -Force

    $hash = (Get-FileHash -Algorithm SHA256 $portablePackagePath).Hash.ToLowerInvariant()
    Set-Content -LiteralPath $portableChecksumPath -Value "$hash *$portablePackageName"

    Remove-Item -LiteralPath $portableStageDir -Recurse -Force
    Write-Host "Packaged portable release to $portablePackagePath"
    Write-Host "Wrote SHA256 to $portableChecksumPath"
}

Write-Host "Built $outExe"
Write-Host "Copied runnable app to $rootExe"
