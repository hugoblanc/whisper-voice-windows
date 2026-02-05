# Whisper Voice Windows Installer
# Run with: powershell -ExecutionPolicy Bypass -File install.ps1

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Whisper Voice for Windows Installer" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$configDir = Join-Path $env:LOCALAPPDATA "WhisperVoice"
$configFile = Join-Path $configDir "config.json"

# Check if already configured
if (Test-Path $configFile) {
    Write-Host "Existing configuration found." -ForegroundColor Yellow
    $response = Read-Host "Do you want to reconfigure? (y/N)"
    if ($response -ne "y" -and $response -ne "Y") {
        Write-Host "Keeping existing configuration." -ForegroundColor Green
        exit 0
    }
}

# Create config directory
if (-not (Test-Path $configDir)) {
    New-Item -ItemType Directory -Path $configDir -Force | Out-Null
}

# Get API Key
Write-Host ""
Write-Host "Step 1: OpenAI API Key" -ForegroundColor Yellow
Write-Host "Get your API key from: https://platform.openai.com/api-keys"
Write-Host ""
$apiKey = Read-Host "Enter your OpenAI API key"

if ([string]::IsNullOrWhiteSpace($apiKey)) {
    Write-Host "Error: API key is required." -ForegroundColor Red
    exit 1
}

# Choose toggle shortcut
Write-Host ""
Write-Host "Step 2: Toggle Shortcut (press to start/stop recording)" -ForegroundColor Yellow
Write-Host ""
Write-Host "  1) Alt+Space (recommended)"
Write-Host "  2) Ctrl+Space"
Write-Host "  3) Win+Shift+Space"
Write-Host ""
$shortcutChoice = Read-Host "Choose option (1-3)"

$shortcutModifiers = 0x0001  # MOD_ALT
$shortcutKeyCode = 0x20      # VK_SPACE

switch ($shortcutChoice) {
    "2" {
        $shortcutModifiers = 0x0002  # MOD_CONTROL
    }
    "3" {
        $shortcutModifiers = 0x000C  # MOD_WIN | MOD_SHIFT
    }
}

# Choose PTT key
Write-Host ""
Write-Host "Step 3: Push-to-Talk Key (hold to record)" -ForegroundColor Yellow
Write-Host ""
Write-Host "  1) F1"
Write-Host "  2) F2"
Write-Host "  3) F3 (recommended)"
Write-Host "  4) F4"
Write-Host "  5) F5"
Write-Host "  6) F6"
Write-Host ""
$pttChoice = Read-Host "Choose option (1-6)"

$pttKeyCode = 0x72  # VK_F3

switch ($pttChoice) {
    "1" { $pttKeyCode = 0x70 }  # VK_F1
    "2" { $pttKeyCode = 0x71 }  # VK_F2
    "3" { $pttKeyCode = 0x72 }  # VK_F3
    "4" { $pttKeyCode = 0x73 }  # VK_F4
    "5" { $pttKeyCode = 0x74 }  # VK_F5
    "6" { $pttKeyCode = 0x75 }  # VK_F6
}

# Create configuration
$config = @{
    apiKey = $apiKey
    shortcutModifiers = $shortcutModifiers
    shortcutKeyCode = $shortcutKeyCode
    pushToTalkKeyCode = $pttKeyCode
}

$config | ConvertTo-Json | Out-File -FilePath $configFile -Encoding UTF8

Write-Host ""
Write-Host "Configuration saved to: $configFile" -ForegroundColor Green

# Auto-start option
Write-Host ""
Write-Host "Step 4: Auto-start" -ForegroundColor Yellow
$autoStart = Read-Host "Start Whisper Voice automatically when Windows starts? (y/N)"

if ($autoStart -eq "y" -or $autoStart -eq "Y") {
    $exePath = Join-Path $PSScriptRoot "src\WhisperVoice\bin\Release\net8.0-windows\win-x64\publish\WhisperVoice.exe"

    if (-not (Test-Path $exePath)) {
        # Try alternative paths
        $exePath = Join-Path $PSScriptRoot "WhisperVoice.exe"
    }

    if (Test-Path $exePath) {
        $regPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
        Set-ItemProperty -Path $regPath -Name "WhisperVoice" -Value "`"$exePath`""
        Write-Host "Auto-start enabled." -ForegroundColor Green
    } else {
        Write-Host "Warning: Could not find WhisperVoice.exe for auto-start." -ForegroundColor Yellow
        Write-Host "Please build the project first, then run this script again." -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "   Installation Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "To build the application, run:" -ForegroundColor Cyan
Write-Host "  cd src\WhisperVoice" -ForegroundColor White
Write-Host "  dotnet publish -c Release" -ForegroundColor White
Write-Host ""
Write-Host "Then run WhisperVoice.exe from:" -ForegroundColor Cyan
Write-Host "  src\WhisperVoice\bin\Release\net8.0-windows\win-x64\publish\" -ForegroundColor White
Write-Host ""
