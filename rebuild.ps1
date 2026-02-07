# Whisper Voice - Rebuild and Restart Script
# This script kills the running app, rebuilds it, and optionally restarts it
# Run with: powershell -ExecutionPolicy Bypass -File rebuild.ps1

param(
    [switch]$NoRestart
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Whisper Voice - Rebuild & Restart" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Kill running processes
Write-Host "[1/3] Checking for running WhisperVoice processes..." -ForegroundColor Yellow
$processes = Get-Process -Name "WhisperVoice" -ErrorAction SilentlyContinue

if ($processes) {
    Write-Host "Found $($processes.Count) running process(es). Stopping..." -ForegroundColor Yellow
    $processes | Stop-Process -Force
    Start-Sleep -Seconds 1
    Write-Host "All processes stopped." -ForegroundColor Green
} else {
    Write-Host "No running processes found." -ForegroundColor Gray
}

# Step 2: Build the application
Write-Host ""
Write-Host "[2/3] Building application..." -ForegroundColor Yellow
Write-Host ""

Push-Location "src\WhisperVoice"
try {
    & dotnet publish -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "Build successful!" -ForegroundColor Green

# Step 3: Launch the new version (unless -NoRestart)
$exePath = "src\WhisperVoice\bin\Release\net8.0-windows\win-x64\publish\WhisperVoice.exe"

if (-not $NoRestart) {
    Write-Host ""
    Write-Host "[3/3] Launching new version..." -ForegroundColor Yellow

    if (Test-Path $exePath) {
        Start-Process -FilePath $exePath
        Write-Host "Application started!" -ForegroundColor Green
    } else {
        Write-Host "Error: Could not find $exePath" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host ""
    Write-Host "[3/3] Skipped launch (--NoRestart flag)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "   Done!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Executable location:" -ForegroundColor Cyan
Write-Host "  $exePath" -ForegroundColor White
Write-Host ""
