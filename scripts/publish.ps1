param(
    [string]$Version = "0.1.0",
    [string]$Runtime = "win-x64",
    [switch]$Sign,
    [string]$CertificateThumbprint = "",
    [string]$CertificatePath = "",
    [string]$CertificatePassword = $env:AJDOCK_CERT_PASSWORD,
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [string]$SignToolPath = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repoRoot "src\AJDock.App\AJDock.App.csproj"
$publishRoot = Join-Path $repoRoot "dist\publish"
$publishDir = Join-Path $publishRoot $Runtime
$packageVersion = if ($Version.StartsWith("v", [System.StringComparison]::OrdinalIgnoreCase)) { $Version.Substring(1) } else { $Version }
$zipPath = Join-Path $repoRoot "dist\AJDock-v$packageVersion-$Runtime.zip"
$localDotnet = Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { "dotnet" }
$signScript = Join-Path $repoRoot "scripts\sign-files.ps1"

if (Test-Path $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

& $dotnet publish $project `
    -c Release `
    -r $Runtime `
    --source "https://api.nuget.org/v3/index.json" `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

if ($Sign) {
    $signTargets = Get-ChildItem -Path $publishDir -Include "*.exe", "*.dll" -File -Recurse
    if ($signTargets.Count -eq 0) {
        throw "No executable files were found to sign in '$publishDir'."
    }

    & $signScript `
        -Path $signTargets.FullName `
        -CertificateThumbprint $CertificateThumbprint `
        -CertificatePath $CertificatePath `
        -CertificatePassword $CertificatePassword `
        -TimestampUrl $TimestampUrl `
        -SignToolPath $SignToolPath
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

Write-Host "Created $zipPath"
