param(
    [Parameter(Mandatory = $true)]
    [string[]]$Path,
    [string]$CertificateThumbprint = "",
    [string]$CertificatePath = "",
    [string]$CertificatePassword = $env:AJDOCK_CERT_PASSWORD,
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [string]$SignToolPath = ""
)

$ErrorActionPreference = "Stop"

function Find-SignTool {
    param([string]$ExplicitPath)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path $ExplicitPath)) {
            throw "SignTool was not found at '$ExplicitPath'."
        }

        return (Resolve-Path $ExplicitPath).Path
    }

    $command = Get-Command "signtool.exe" -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $kitsRoot = "C:\Program Files (x86)\Windows Kits\10\bin"
    if (Test-Path $kitsRoot) {
        $candidate = Get-ChildItem -Path $kitsRoot -Filter "signtool.exe" -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "\\x64\\signtool\.exe$" } |
            Sort-Object FullName -Descending |
            Select-Object -First 1

        if ($candidate) {
            return $candidate.FullName
        }
    }

    throw "signtool.exe was not found. Install the Windows SDK or pass -SignToolPath."
}

$signTool = Find-SignTool -ExplicitPath $SignToolPath
$resolvedFiles = foreach ($file in $Path) {
    if (-not (Test-Path $file)) {
        throw "Cannot sign missing file '$file'."
    }

    (Resolve-Path $file).Path
}

foreach ($file in $resolvedFiles) {
    Write-Host "Signing $file"

    $args = @("sign", "/fd", "SHA256", "/tr", $TimestampUrl, "/td", "SHA256")
    if (-not [string]::IsNullOrWhiteSpace($CertificatePath)) {
        $args += @("/f", $CertificatePath)
        if (-not [string]::IsNullOrWhiteSpace($CertificatePassword)) {
            $args += @("/p", $CertificatePassword)
        }
    } elseif (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
        $args += @("/sha1", $CertificateThumbprint)
    } else {
        $args += "/a"
    }

    $args += $file
    & $signTool @args
    if ($LASTEXITCODE -ne 0) {
        throw "signtool sign failed with exit code $LASTEXITCODE for '$file'."
    }

    & $signTool verify /pa /v $file
    if ($LASTEXITCODE -ne 0) {
        throw "signtool verify failed with exit code $LASTEXITCODE for '$file'."
    }
}
