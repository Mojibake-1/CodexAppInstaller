<#
    Build the self-contained installer (CodexAppInstaller-Setup.exe) and a portable zip.

    The setup exe embeds the app payload (CodexAppInstaller.exe + Install-CodexApp.ps1 + icon)
    and installs per-user to %LOCALAPPDATA%\Programs\CodexAppInstaller with Start Menu /
    Desktop shortcuts and an Add/Remove Programs entry. No third-party packager required.
#>
[CmdletBinding()]
param([switch]$TestInstall)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

# 1) Make sure the app itself is freshly built.
& "$root\build.ps1" | Write-Host
if (-not (Test-Path "$root\CodexAppInstaller.exe")) { throw "App build did not produce CodexAppInstaller.exe" }

$fw   = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$csc  = Join-Path $fw "csc.exe"
$wpf  = Join-Path $fw "WPF"
$xaml = "C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll"
if (-not (Test-Path $xaml)) {
    $xaml = (Get-ChildItem "C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Xaml" -Recurse -Filter "System.Xaml.dll" | Select-Object -First 1).FullName
}

$dist = Join-Path $root "dist"
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$setupOut = Join-Path $dist "CodexAppInstaller-Setup.exe"

$cscArgs = @(
    "/nologo", "/target:winexe", "/platform:x64", "/optimize+", "/codepage:65001",
    "/out:$setupOut",
    "/win32manifest:$root\src\app.manifest",
    "/win32icon:$root\src\icon.ico",
    "/resource:$root\CodexAppInstaller.exe,CodexApp.exe",
    "/resource:$root\Install-CodexApp.ps1,Install.ps1",
    "/resource:$root\src\icon.ico,CodexApp.ico",
    "/r:$wpf\PresentationFramework.dll", "/r:$wpf\PresentationCore.dll", "/r:$wpf\WindowsBase.dll", "/r:$xaml",
    "$root\installer\Setup.cs"
)

Write-Host "Compiling installer -> CodexAppInstaller-Setup.exe" -ForegroundColor Cyan
& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "csc.exe failed with exit code $LASTEXITCODE" }
Write-Host ("Setup built: {0} ({1:N0} bytes)" -f $setupOut, (Get-Item $setupOut).Length) -ForegroundColor Green

# 2) Portable zip (no install — just unzip and run).
$portDir = Join-Path $dist "portable"
Remove-Item $portDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $portDir | Out-Null
Copy-Item "$root\CodexAppInstaller.exe" $portDir
Copy-Item "$root\Install-CodexApp.ps1" $portDir
Copy-Item "$root\README.md" $portDir
$zip = Join-Path $dist "CodexAppInstaller-portable.zip"
Remove-Item $zip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path "$portDir\*" -DestinationPath $zip -Force
Remove-Item $portDir -Recurse -Force -ErrorAction SilentlyContinue
Write-Host ("Portable zip: {0} ({1:N0} bytes)" -f $zip, (Get-Item $zip).Length) -ForegroundColor Green

if ($TestInstall) {
    Write-Host "Silent install test..." -ForegroundColor Cyan
    $p = Start-Process -FilePath $setupOut -ArgumentList "/S" -PassThru -Wait
    Write-Host ("install exit: {0}" -f $p.ExitCode)
}

Get-ChildItem $dist | Select-Object Name, Length | Format-Table -AutoSize
