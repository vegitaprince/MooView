param([string]$BuildDirectory = (Join-Path $PSScriptRoot 'artifacts/native'))
$ErrorActionPreference = 'Stop'
$cmakeCommand = Get-Command cmake -ErrorAction SilentlyContinue
$cmake = if ($cmakeCommand) { $cmakeCommand.Source } else { $null }
if (!$cmake) {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path -LiteralPath $vswhere) {
        $visualStudio = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
        $candidate = Join-Path $visualStudio 'Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe'
        if (Test-Path -LiteralPath $candidate) { $cmake = $candidate }
    }
}
if (!$cmake) { throw 'Install Visual Studio C++ desktop build tools and CMake 3.24 or newer.' }
& $cmake -S "$PSScriptRoot/native" -B $BuildDirectory -A x64
if ($LASTEXITCODE) { throw 'EXR configuration failed' }
& $cmake --build $BuildDirectory --config Release --parallel 4 --target MooViewExr ExrFixtures
if ($LASTEXITCODE) { throw 'EXR native build failed' }
$destination = Join-Path $PSScriptRoot 'native/bin'
New-Item -ItemType Directory -Force $destination | Out-Null
Copy-Item -LiteralPath (Join-Path $BuildDirectory 'Release/MooViewExr.dll') -Destination $destination -Force
