$ErrorActionPreference = 'Stop'
$source = $PSScriptRoot
$targetDirectory = Join-Path $env:LOCALAPPDATA 'Programs\AudioOutputSwitcher'
$oldTargetDirectory = Join-Path $env:LOCALAPPDATA 'Programs\LineInTrayWinUI'
$targetExe = Join-Path $targetDirectory 'AudioOutputSwitcher.exe'

Get-Process LineInTray,LineInTrayWinUI,AudioOutputSwitcher -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 400
New-Item -ItemType Directory -Force -Path $targetDirectory | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $targetDirectory -Recurse -Force
& icacls.exe $targetExe /setintegritylevel Medium | Out-Null
if (Test-Path $oldTargetDirectory) { Remove-Item -LiteralPath $oldTargetDirectory -Recurse -Force -ErrorAction SilentlyContinue }

$desktop = [Environment]::GetFolderPath('Desktop')
$shortcutPath = Join-Path $desktop 'Audio Output Switcher.lnk'
foreach ($oldName in @('音频输出快捷切换.lnk','音频输入快捷切换.lnk')) {
    $oldShortcut = Join-Path $desktop $oldName
    if (Test-Path $oldShortcut) { Remove-Item -LiteralPath $oldShortcut -Force }
}
if (Test-Path $shortcutPath) { Remove-Item -LiteralPath $shortcutPath -Force }
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $targetExe
$shortcut.WorkingDirectory = $targetDirectory
$shortcut.IconLocation = "$targetExe,0"
$shortcut.Description = 'Audio Output Switcher'
$shortcut.Save()
Write-Host "已安装到：$targetDirectory" -ForegroundColor Green
Write-Host "桌面快捷方式：$shortcutPath" -ForegroundColor Green
Start-Process -FilePath $targetExe





