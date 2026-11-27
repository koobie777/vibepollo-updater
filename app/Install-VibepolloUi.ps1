# Installs the published Vibepollo window and points the existing shortcuts at it.
# The Python CLI in %LOCALAPPDATA%\vibepollo-updater\bin stays.
$ErrorActionPreference = 'Stop'

$src = Join-Path $PSScriptRoot 'dist\win-x64'
$exeName = 'vibepollo-ui.exe'
if (-not (Test-Path -LiteralPath (Join-Path $src $exeName))) {
    throw "Publish the Windows build first. Missing $src\$exeName"
}

$dest = Join-Path $env:LOCALAPPDATA 'vibepollo-updater\ui'
if (Test-Path -LiteralPath $dest) {
    $bak = $dest + '.bak-' + (Get-Date -Format 'yyyy-MM-dd-HHmm')
    if (Test-Path -LiteralPath $bak) { Remove-Item -LiteralPath $bak -Recurse -Force }
    Rename-Item -LiteralPath $dest -NewName (Split-Path $bak -Leaf)
}
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item -Path (Join-Path $src '*') -Destination $dest -Recurse -Force
$exe = Join-Path $dest $exeName

$engine = Join-Path (Split-Path $PSScriptRoot -Parent) 'vibepollo-updater'
$bin = Join-Path $env:LOCALAPPDATA 'vibepollo-updater\bin'
if (Test-Path -LiteralPath $engine) {
    New-Item -ItemType Directory -Force -Path $bin | Out-Null
    Copy-Item -LiteralPath $engine -Destination (Join-Path $bin 'vibepollo-updater.py') -Force
    Write-Output "Updated CLI engine in $bin"
}

$icon = "$env:SystemRoot\System32\shell32.dll,46"
foreach ($cand in @(
    (Join-Path $env:ProgramFiles 'Apollo\apollo.ico'),
    (Join-Path $env:ProgramFiles 'Apollo\sunshine.ico')
)) {
    if (Test-Path -LiteralPath $cand) { $icon = "$cand,0"; break }
}

$shell = New-Object -ComObject WScript.Shell
function Set-Link([string]$Path) {
    $dir = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $lnk = $shell.CreateShortcut($Path)
    $lnk.TargetPath = $exe
    $lnk.Arguments = ''
    $lnk.WorkingDirectory = $dest
    $lnk.WindowStyle = 1
    $lnk.Description = 'Install, update, or roll back Vibepollo'
    $lnk.IconLocation = $icon
    $lnk.Save()
}

$start = Join-Path ([Environment]::GetFolderPath('Programs')) 'Vibepollo Updater\Vibepollo Updater.lnk'
Set-Link $start
$desk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Vibepollo Updater.lnk'
if (Test-Path -LiteralPath (Split-Path $desk -Parent)) { Set-Link $desk }
Write-Output "Installed $exe"
