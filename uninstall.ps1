# Whisper Voice Windows Uninstaller
# Run with: powershell -ExecutionPolicy Bypass -File uninstall.ps1

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Whisper Voice Uninstaller" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$response = Read-Host "Are you sure you want to uninstall Whisper Voice? (y/N)"
if ($response -ne "y" -and $response -ne "Y") {
    Write-Host "Uninstall cancelled." -ForegroundColor Yellow
    exit 0
}

# Remove auto-start registry entry
$regPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
if (Get-ItemProperty -Path $regPath -Name "WhisperVoice" -ErrorAction SilentlyContinue) {
    Remove-ItemProperty -Path $regPath -Name "WhisperVoice"
    Write-Host "Removed auto-start entry." -ForegroundColor Green
}

# Remove configuration directory
$configDir = Join-Path $env:LOCALAPPDATA "WhisperVoice"
if (Test-Path $configDir) {
    Remove-Item -Path $configDir -Recurse -Force
    Write-Host "Removed configuration directory." -ForegroundColor Green
}

# Clean up temp files
$tempDir = Join-Path $env:TEMP "WhisperVoice"
if (Test-Path $tempDir) {
    Remove-Item -Path $tempDir -Recurse -Force
    Write-Host "Removed temporary files." -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "   Uninstall Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Note: The application files were not removed." -ForegroundColor Yellow
Write-Host "You can manually delete the application folder." -ForegroundColor Yellow
Write-Host ""
