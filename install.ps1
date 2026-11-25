<#
.SYNOPSIS
    Installs or removes vibepollo-updater on Windows (Start Menu shortcut, Desktop shortcut, CLI shim).
.DESCRIPTION
    Copies vibepollo-updater into %LOCALAPPDATA%\vibepollo-updater\bin, creates a vibepollo-updater.cmd
    CLI launcher (added to your user PATH), and creates Start Menu and Desktop shortcuts.
    Use -NoDesktop to skip the Desktop shortcut, or -Uninstall to remove the updater (cached
    packages and config backups are kept).
#>
[CmdletBinding()]
param(
    [switch]$NoDesktop,
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$AppRoot   = Join-Path $env:LOCALAPPDATA 'vibepollo-updater'
$BinDir    = Join-Path $AppRoot 'bin'
$TargetPy  = Join-Path $BinDir 'vibepollo-updater.py'
$TargetCmd = Join-Path $BinDir 'vibepollo-updater.cmd'

$StartMenuDir = Join-Path ([Environment]::GetFolderPath('Programs')) 'Vibepollo Updater'
$StartMenuLnk = Join-Path $StartMenuDir 'Vibepollo Updater.lnk'
$DesktopLnk   = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Vibepollo Updater.lnk'

function Remove-FromUserPath([string]$Dir) {
    $cur = [Environment]::GetEnvironmentVariable('Path', 'User')
    if (-not $cur) { return }
    $norm = $Dir.TrimEnd('\')
    $parts = @($cur -split ';' | Where-Object { $_ -and ($_.TrimEnd('\') -ine $norm) })
    [Environment]::SetEnvironmentVariable('Path', ($parts -join ';'), 'User')
}

function Add-ToUserPath([string]$Dir) {
    $cur = [Environment]::GetEnvironmentVariable('Path', 'User')
    $norm = $Dir.TrimEnd('\')
    $parts = @()
    if ($cur) {
        $parts = @($cur -split ';' | Where-Object { $_ })
        if ($parts | Where-Object { $_.TrimEnd('\') -ieq $norm }) { return }
    }
    $parts += $Dir
    [Environment]::SetEnvironmentVariable('Path', ($parts -join ';'), 'User')
}

if ($Uninstall) {
    foreach ($p in @($TargetPy, $TargetCmd, $StartMenuLnk, $DesktopLnk)) {
        if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Force }
    }
    if ((Test-Path -LiteralPath $StartMenuDir) -and -not (Get-ChildItem -LiteralPath $StartMenuDir -Force)) {
        Remove-Item -LiteralPath $StartMenuDir -Force
    }
    Remove-FromUserPath $BinDir
    Write-Host "Removed vibepollo-updater launchers (cached installers and config backups in $AppRoot kept)."
    exit 0
}

# Find Python 3.10+ for CLI and GUI.
$PyCli = $null
foreach ($cand in @('python.exe', 'py.exe')) {
    $cmd = Get-Command $cand -ErrorAction SilentlyContinue
    if ($cmd) { $PyCli = $cmd.Source; break }
}
if (-not $PyCli) {
    throw "Python 3.10+ is required. Install Python from python.org or winget (Python.Python.3.13) and re-run."
}

$PyGui = $null
foreach ($cand in @('pythonw.exe', 'pyw.exe', 'python.exe', 'py.exe')) {
    $cmd = Get-Command $cand -ErrorAction SilentlyContinue
    if ($cmd) { $PyGui = $cmd.Source; break }
}

$Src = Join-Path $ScriptDir 'vibepollo-updater'
if (-not (Test-Path -LiteralPath $Src -PathType Leaf)) {
    throw "Could not find vibepollo-updater next to install.ps1 ($ScriptDir)."
}

New-Item -ItemType Directory -Force -Path $BinDir | Out-Null
Copy-Item -LiteralPath $Src -Destination $TargetPy -Force

# CLI wrapper so `vibepollo-updater --check` works from cmd.exe and PowerShell.
$cmdBody = "@echo off`r`n`"$PyCli`" -I `"$TargetPy`" %*`r`n"
Set-Content -LiteralPath $TargetCmd -Value $cmdBody -Encoding ASCII -NoNewline

Add-ToUserPath $BinDir

# Prefer Vibepollo's own icon when installed; fall back to a standard Windows shell icon.
$IconLoc = "$env:SystemRoot\System32\shell32.dll,46"
foreach ($iconCand in @(
    (Join-Path $env:ProgramFiles 'Apollo\apollo.ico'),
    (Join-Path $env:ProgramFiles 'Apollo\sunshine.ico'),
    (Join-Path $env:ProgramFiles 'Apollo\apollo.exe'),
    (Join-Path $env:ProgramFiles 'Apollo\sunshine.exe')
)) {
    if (Test-Path -LiteralPath $iconCand -PathType Leaf) {
        $IconLoc = "$iconCand,0"
        break
    }
}

$Wsh = New-Object -ComObject WScript.Shell
New-Item -ItemType Directory -Force -Path $StartMenuDir | Out-Null

function New-UpdaterShortcut([string]$Path) {
    $lnk = $Wsh.CreateShortcut($Path)
    $lnk.TargetPath = $PyGui
    $lnk.Arguments = "-I `"$TargetPy`""
    $lnk.WorkingDirectory = $BinDir
    $lnk.IconLocation = $IconLoc
    $lnk.Description = 'Install, update, or roll back Vibepollo (Stable / Latest / specific version).'
    $lnk.Save()
}

New-UpdaterShortcut $StartMenuLnk
if (-not $NoDesktop) {
    $deskDir = Split-Path -Parent $DesktopLnk
    if (Test-Path -LiteralPath $deskDir -PathType Container) {
        New-UpdaterShortcut $DesktopLnk
    }
}

& $PyCli -c "import PySide6" 2>$null
$hasQt = ($LASTEXITCODE -eq 0)

Write-Host "Installed vibepollo-updater to $TargetPy"
Write-Host "CLI launcher : $TargetCmd (added $BinDir to your user PATH)"
Write-Host "Start Menu   : $StartMenuLnk"
if (-not $NoDesktop) { Write-Host "Desktop      : $DesktopLnk" }
if (-not $hasQt) {
    Write-Host ""
    Write-Host "Note: PySide6 is not installed yet. CLI mode works right away (`vibepollo-updater --check`)."
    Write-Host "For the desktop window, run:  `"$PyCli`" -m pip install PySide6"
} else {
    Write-Host "Run 'vibepollo-updater --check' (in a new terminal) or open 'Vibepollo Updater' from Start / Desktop."
}
