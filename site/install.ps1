# Installs the native repolore executable on Windows from the GitHub release, after checking its SHA-256 against the
# release's checksum file, and adds its folder to the user's PATH.
#   irm https://repolore.org/install.ps1 | iex
# Environment: REPOLORE_VERSION (default: the version this script was published with),
#              REPOLORE_INSTALL_DIR (default: %LOCALAPPDATA%\Programs\RepoLore),
#              REPOLORE_DOWNLOAD_BASE (default: the GitHub release; for example an internal mirror of its files).
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$version = if ($env:REPOLORE_VERSION) { $env:REPOLORE_VERSION } else { '__VERSION__' }
$installDir = if ($env:REPOLORE_INSTALL_DIR) { $env:REPOLORE_INSTALL_DIR } else { Join-Path $env:LOCALAPPDATA 'Programs\RepoLore' }
$base = if ($env:REPOLORE_DOWNLOAD_BASE) { $env:REPOLORE_DOWNLOAD_BASE } else { "https://github.com/R-Elias/repolore/releases/download/v$version" }

$arch = switch ($env:PROCESSOR_ARCHITECTURE) {
  'AMD64' { 'x64' }
  'ARM64' { 'arm64' }
  default { throw "repolore install: unsupported processor $($env:PROCESSOR_ARCHITECTURE)" }
}
$archive = "repolore-$version-win-$arch.zip"
$work = Join-Path ([System.IO.Path]::GetTempPath()) ("repolore-" + [System.Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
  Invoke-WebRequest -UseBasicParsing -Uri "$base/$archive" -OutFile (Join-Path $work $archive)
  Invoke-WebRequest -UseBasicParsing -Uri "$base/repolore-$version.sha256" -OutFile (Join-Path $work 'sums')

  $line = Get-Content (Join-Path $work 'sums') | Where-Object { $_ -match "  $([regex]::Escape($archive))$" } | Select-Object -First 1
  if (-not $line) { throw "repolore install: the checksum file does not list $archive" }
  $expected = $line.Split(' ')[0]
  $actual = (Get-FileHash -Algorithm SHA256 (Join-Path $work $archive)).Hash.ToLowerInvariant()
  if ($actual -ne $expected) { throw "repolore install: checksum mismatch for ${archive}: expected $expected, got $actual" }

  Expand-Archive -Path (Join-Path $work $archive) -DestinationPath (Join-Path $work 'x')
  New-Item -ItemType Directory -Force -Path $installDir | Out-Null
  Copy-Item -Force (Join-Path $work 'x\repolore.exe') (Join-Path $installDir 'repolore.exe')
} finally {
  Remove-Item -Recurse -Force $work
}

Write-Host "repolore $version installed to $installDir\repolore.exe"
$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
if (-not (($userPath -split ';') -contains $installDir)) {
  [Environment]::SetEnvironmentVariable('Path', (@($userPath, $installDir) | Where-Object { $_ }) -join ';', 'User')
  Write-Host "Added $installDir to your user PATH; open a new terminal to run 'repolore'."
}
