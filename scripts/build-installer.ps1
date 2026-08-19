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
$packageVersion = if ($Version.StartsWith("v", [System.StringComparison]::OrdinalIgnoreCase)) { $Version.Substring(1) } else { $Version }
$publishScript = Join-Path $repoRoot "scripts\publish.ps1"
$signScript = Join-Path $repoRoot "scripts\sign-files.ps1"
$installerScript = Join-Path $repoRoot "installer\AJDock.iss"
$installerOut = Join-Path $repoRoot "dist\installer"

$isccCommand = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
$candidatePaths = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)

$iscc = if ($isccCommand) {
    $isccCommand.Source
} else {
    $candidatePaths | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (-not $iscc) {
    throw "Inno Setup 6 was not found. Install it from https://jrsoftware.org/isinfo.php, then rerun scripts\build-installer.ps1."
}

& $publishScript `
    -Version $packageVersion `
    -Runtime $Runtime `
    -Sign:$Sign `
    -CertificateThumbprint $CertificateThumbprint `
    -CertificatePath $CertificatePath `
    -CertificatePassword $CertificatePassword `
    -TimestampUrl $TimestampUrl `
    -SignToolPath $SignToolPath
if ($LASTEXITCODE -ne 0) {
    throw "publish.ps1 failed with exit code $LASTEXITCODE"
}

New-Item -ItemType Directory -Path $installerOut -Force | Out-Null

& $iscc `
    "/DAppVersion=$packageVersion" `
    "/DRuntime=$Runtime" `
    $installerScript

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed with exit code $LASTEXITCODE"
}

$setupPath = Join-Path $installerOut "AJDockSetup-v$packageVersion-$Runtime.exe"
if ($Sign) {
    & $signScript `
        -Path $setupPath `
        -CertificateThumbprint $CertificateThumbprint `
        -CertificatePath $CertificatePath `
        -CertificatePassword $CertificatePassword `
        -TimestampUrl $TimestampUrl `
        -SignToolPath $SignToolPath
}

Write-Host "Created $setupPath"
