# HeadlessCoder installer for Windows (PowerShell).
#
#   irm https://raw.githubusercontent.com/SideswipeN7/HeadlessCoder/main/install.ps1 | iex
#
# Installs headlesscoder.exe and an `hc` shortcut into
# %LOCALAPPDATA%\Programs\HeadlessCoder (override with $env:HC_INSTALL_DIR) and
# adds it to your user PATH. Pin a version with $env:HC_VERSION = 'vX.Y.Z'.

$ErrorActionPreference = 'Stop'
$repo = 'SideswipeN7/HeadlessCoder'
$binName = 'headlesscoder'

$installDir = if ($env:HC_INSTALL_DIR) { $env:HC_INSTALL_DIR }
              else { Join-Path $env:LOCALAPPDATA 'Programs\HeadlessCoder' }

function Write-Step($msg) { Write-Host "* $msg" -ForegroundColor DarkYellow }

# --- detect architecture ----------------------------------------------------
$osArch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
$arch = if ($osArch -eq 'Arm64') { 'arm64' } else { 'x64' }
$asset = "$binName-win-$arch.exe"

# --- resolve version --------------------------------------------------------
if ($env:HC_VERSION) {
  $tag = $env:HC_VERSION
} else {
  Write-Step "Resolving latest release of $repo ..."
  $tag = (Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/latest" `
            -Headers @{ 'User-Agent' = 'headlesscoder-installer' }).tag_name
}
if (-not $tag) { throw "Could not determine the latest release. Set `$env:HC_VERSION." }

$url = "https://github.com/$repo/releases/download/$tag/$asset"

# --- install ----------------------------------------------------------------
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
$target = Join-Path $installDir 'headlesscoder.exe'

Write-Step "Downloading $asset ($tag) ..."
Invoke-WebRequest -Uri $url -OutFile $target -Headers @{ 'User-Agent' = 'headlesscoder-installer' }

# `hc` shortcut via a small .cmd shim.
$shim = Join-Path $installDir 'hc.cmd'
Set-Content -Path $shim -Encoding ASCII -Value "@echo off`r`n`"%~dp0headlesscoder.exe`" %*"

Write-Step "Installed headlesscoder.exe -> $target"
Write-Step "Shortcut  hc -> headlesscoder.exe"

# --- add to user PATH -------------------------------------------------------
$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
if (($userPath -split ';') -notcontains $installDir) {
  [Environment]::SetEnvironmentVariable('Path', "$userPath;$installDir", 'User')
  Write-Step "Added $installDir to your user PATH (open a new terminal to pick it up)."
}

Write-Host ""
Write-Step "Done. Run:  headlesscoder --no-sleep    (or just:  hc)"
