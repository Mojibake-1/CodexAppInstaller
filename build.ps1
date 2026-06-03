<#
    Build the modern WPF Codex App Installer with the .NET Framework C# compiler (no SDK required).

    Output: CodexAppInstaller.exe  (WPF, XAML-free, single self-contained exe).
    The PowerShell backend (Install-CodexApp.ps1) is embedded as a resource AND preferred from disk.
#>
[CmdletBinding()]
param(
    [switch]$SelfTest,   # after building, launch with --self-test (exits immediately, no window) to validate startup
    [switch]$Run         # after building, launch the GUI
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

$fw   = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$csc  = Join-Path $fw "csc.exe"
$wpf  = Join-Path $fw "WPF"
$xaml = "C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll"

if (-not (Test-Path $csc))  { throw "csc.exe not found at $csc" }
if (-not (Test-Path $wpf))  { throw "WPF assemblies not found at $wpf" }
if (-not (Test-Path $xaml)) {
    $found = Get-ChildItem "C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Xaml" -Recurse -Filter "System.Xaml.dll" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) { $xaml = $found.FullName } else { throw "System.Xaml.dll not found in GAC" }
}

$refs = @(
    (Join-Path $wpf "PresentationFramework.dll"),
    (Join-Path $wpf "PresentationCore.dll"),
    (Join-Path $wpf "WindowsBase.dll"),
    $xaml
)

$sources = Get-ChildItem (Join-Path $root "src\*.cs") | Select-Object -ExpandProperty FullName
if (-not $sources) { throw "No .cs sources found under src\" }

$out      = Join-Path $root "CodexAppInstaller.exe"
$manifest = Join-Path $root "src\app.manifest"
$icon     = Join-Path $root "src\icon.ico"
$script   = Join-Path $root "Install-CodexApp.ps1"

$cscArgs = @(
    "/nologo",
    "/target:winexe",
    "/platform:x64",
    "/optimize+",
    "/codepage:65001",   # read sources as UTF-8 so Segoe Fluent Icons glyphs survive
    "/out:$out"
)
if (Test-Path $manifest) { $cscArgs += "/win32manifest:$manifest" }
if (Test-Path $icon)     { $cscArgs += "/win32icon:$icon" }
if (Test-Path $script)   { $cscArgs += "/resource:$script,Install-CodexApp.ps1" }
foreach ($r in $refs)    { $cscArgs += "/r:$r" }
$cscArgs += $sources

Write-Host "Compiling $($sources.Count) source file(s) -> CodexAppInstaller.exe" -ForegroundColor Cyan
& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "csc.exe failed with exit code $LASTEXITCODE" }
Write-Host ("Build OK: {0} ({1:N0} bytes)" -f $out, (Get-Item $out).Length) -ForegroundColor Green

if ($SelfTest) {
    Write-Host "Self-test..." -ForegroundColor Cyan
    $p = Start-Process -FilePath $out -ArgumentList "--self-test" -PassThru -Wait
    Write-Host ("Self-test exit code: {0}" -f $p.ExitCode) -ForegroundColor ($(if ($p.ExitCode -eq 0) { "Green" } else { "Red" }))
}
if ($Run) { Start-Process -FilePath $out }
