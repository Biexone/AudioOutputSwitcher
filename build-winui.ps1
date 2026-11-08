param(
    [switch]$SelfContained = $true
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'winui\LineInTray.WinUI\LineInTray.WinUI.csproj'
$dotnet = Join-Path $root '.tools\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { throw '缺少本地 .NET 10 SDK：.tools\dotnet\dotnet.exe' }

$env:DOTNET_ROOT = Join-Path $root '.tools\dotnet'
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
$env:TMP = Join-Path $root '.tmp'
$env:TEMP = $env:TMP
$env:NUGET_PACKAGES = Join-Path $root '.nuget\packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $root '.nuget\http-cache'
$env:NUGET_PLUGINS_CACHE_PATH = Join-Path $root '.nuget\plugins-cache'
$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet-home'
$env:APPDATA = Join-Path $root '.appdata'
$env:LOCALAPPDATA = Join-Path $root '.localappdata'
New-Item -ItemType Directory -Force -Path $env:TMP,$env:NUGET_PACKAGES,$env:NUGET_HTTP_CACHE_PATH,$env:NUGET_PLUGINS_CACHE_PATH,$env:DOTNET_CLI_HOME,$env:APPDATA,$env:LOCALAPPDATA | Out-Null

Get-Process AudioOutputSwitcher,LineInTrayWinUI -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300
$builtExe = Join-Path $root 'winui\LineInTray.WinUI\bin\Release\net10.0-windows10.0.26100.0\win-x64\AudioOutputSwitcher.exe'
if (Test-Path $builtExe) {
    & icacls.exe $builtExe /setintegritylevel Low | Out-Null
    Remove-Item -LiteralPath $builtExe -Force -ErrorAction SilentlyContinue
}

$out = Join-Path $root 'dist-winui'
if (Test-Path $out) { Remove-Item -LiteralPath $out -Recurse -Force }
$publishArgs = @('publish', $project, '-c', 'Release', '-r', 'win-x64', '-o', $out,
    '-p:WindowsAppSDKSelfContained=false')
if ($SelfContained) { $publishArgs += '-p:SelfContained=true' } else { $publishArgs += '-p:SelfContained=false' }
& $dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败: $LASTEXITCODE" }

$exe = Join-Path $out 'AudioOutputSwitcher.exe'
if (-not (Test-Path $exe)) { throw "发布输出缺少 exe: $exe" }
& icacls.exe $exe /setintegritylevel Medium | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'install-winui.ps1') -Destination $out -Force
Copy-Item -LiteralPath (Join-Path $root 'install-winui.cmd') -Destination $out -Force
Copy-Item -LiteralPath (Join-Path $root 'README-WinUI.md') -Destination $out -Force -ErrorAction SilentlyContinue

$size = (Get-Item $exe).Length
$hash = (Get-FileHash $exe -Algorithm SHA256).Hash
Write-Host ''
Write-Host 'Build OK (WinUI 3)'
Write-Host "  Output: $out"
Write-Host "  Exe   : $exe"
Write-Host "  Size  : $size bytes"
Write-Host "  SHA256: $hash"


