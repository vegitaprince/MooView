param([string]$InnoCompiler = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe")
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$app = Join-Path $root 'artifacts\app'
$release = Join-Path $root 'artifacts\release'
dotnet restore "$root\Source\MooView.csproj" --source https://api.nuget.org/v3/index.json -r win-x64 -p:SelfContained=true -p:RuntimeFrameworkVersion=10.0.14
if ($LASTEXITCODE) { throw 'Restore failed' }
dotnet publish "$root\Source\MooView.csproj" --no-restore -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.14 -o $app
if ($LASTEXITCODE) { throw 'Publish failed' }
New-Item -ItemType Directory -Force $release | Out-Null
& $InnoCompiler "/DPublishDir=$app" "/DReleaseDir=$release" "$root\packaging\MooView.iss"
if ($LASTEXITCODE) { throw 'Installer compilation failed' }
Compress-Archive -Path "$app\*" -DestinationPath "$release\MooView-Portable-1.2.0-x64.zip" -Force
Get-ChildItem $release -File | Where-Object Extension -in '.exe','.zip' | Get-FileHash -Algorithm SHA256 | ForEach-Object { "$($_.Hash.ToLower())  $([IO.Path]::GetFileName($_.Path))" } | Set-Content "$release\SHA256SUMS.txt" -Encoding ascii
