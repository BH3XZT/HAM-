# Builds HamStartup.exe with the .NET Framework 4.x C# compiler.
# No SDK / NuGet / runtime install is required on the target machine (Windows 10/11 ships .NET 4.8).
param([string]$OutFile = 'HamStartup.exe')
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$src = Join-Path $root 'src'
$dist = Join-Path $root 'dist'
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) { throw 'csc.exe not found' }

if (-not (Test-Path $dist)) { New-Item -ItemType Directory -Path $dist | Out-Null }

$sources = Get-ChildItem -Path $src -Filter *.cs | Sort-Object Name | ForEach-Object { $_.FullName }
$args = @(
    '/nologo',
    '/target:winexe',
    '/optimize+',
    '/codepage:65001',
    ('/out:' + (Join-Path $dist $OutFile)),
    ('/win32icon:' + (Join-Path $root 'assets\app.ico')),
    ('/win32manifest:' + (Join-Path $root 'app.manifest')),
    ('/resource:' + (Join-Path $root 'assets\app-256.png') + ',HamStartup.app.png'),
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll'
) + $sources

& $csc $args
if ($LASTEXITCODE -ne 0) { throw "compile failed (exit $LASTEXITCODE)" }

Write-Host ('built: ' + (Join-Path $dist $OutFile))
Get-Item (Join-Path $dist $OutFile) | Select-Object FullName, Length, LastWriteTime
