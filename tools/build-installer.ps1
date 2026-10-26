param(
    [string]$Version = "1.0.0",
    [string]$Iscc
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$publishDir = Join-Path $root "dist\publish"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

dotnet publish src/LuminanceCapture.App/LuminanceCapture.App.csproj -c Release -r win-x64 --self-contained true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

$candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)
if (-not $Iscc) { $Iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1 }
if (-not $Iscc) {
    Write-Host "Inno Setup not found. Installing via winget (per-user)..."
    winget install --id JRSoftware.InnoSetup -e --scope user --silent --accept-package-agreements --accept-source-agreements
    $Iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $Iscc) { throw "Inno Setup (ISCC.exe) could not be found or installed. Install Inno Setup 6 and run again." }

& $Iscc "/DAppVersion=$Version" (Join-Path $root "installer\LuminanceCapture.iss")
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)" }
Write-Host "Installer written to $(Join-Path $root 'dist')"
