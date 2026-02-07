# Windows Code Signing Guide

This guide explains how to sign WhisperVoice.exe to remove the "Unknown Publisher" warning on Windows.

## Why Sign?

When users download an unsigned EXE:
- Windows SmartScreen shows "Windows protected your PC"
- Users must click "More info" → "Run anyway"
- Some corporate environments block unsigned executables

With a signed EXE:
- SmartScreen shows your publisher name
- Users can run immediately (after building reputation)
- Professional appearance

## Certificate Options

### Certum Open Source Code Signing ($29/year)

Best option for open source projects. [Certum Store](https://certum.store/open-source-code-signing-code.html)

**Requirements:**
- Valid ID
- Proof of open source project (GitHub repo)
- Cryptographic card + reader (included or bring your own)

**Process:**
1. Purchase certificate at Certum Store
2. Complete identity verification (1-2 business days)
3. Receive cryptographic card with certificate
4. Install proCertum CardManager software
5. Use certificate with SignTool

### Azure Trusted Signing (Free, Limited Availability)

Microsoft's free signing service. [Learn more](https://learn.microsoft.com/en-us/windows/apps/develop/smart-app-control/code-signing-for-smart-app-control)

**Requirements:**
- US or Canada based organization
- 3+ years of verifiable business history
- Azure subscription

Not yet available for individual developers outside US/Canada.

## Signing with SignTool

### Prerequisites

1. Install [Windows SDK](https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/)
2. SignTool is at: `C:\Program Files (x86)\Windows Kits\10\bin\10.0.xxxxx.0\x64\signtool.exe`

### Sign with Certum Card

```powershell
# Sign the executable
signtool sign /n "Your Name" /t http://time.certum.pl /fd sha256 /v WhisperVoice.exe

# Verify signature
signtool verify /pa /v WhisperVoice.exe
```

Parameters:
- `/n "Your Name"` - Certificate subject name
- `/t http://time.certum.pl` - Timestamp server (important!)
- `/fd sha256` - SHA-256 digest algorithm
- `/v` - Verbose output

### Sign with PFX File

If you have a PFX certificate file:

```powershell
signtool sign /f certificate.pfx /p "password" /t http://time.certum.pl /fd sha256 /v WhisperVoice.exe
```

## GitHub Actions Integration

Automate signing in your release workflow:

```yaml
# .github/workflows/release.yml
name: Build and Sign Release

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    runs-on: windows-latest

    steps:
    - uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'

    - name: Build
      run: |
        cd src\WhisperVoice
        dotnet publish -c Release

    - name: Sign executable
      env:
        CERTIFICATE_BASE64: ${{ secrets.CODE_SIGNING_CERT }}
        CERTIFICATE_PASSWORD: ${{ secrets.CODE_SIGNING_PASSWORD }}
      run: |
        # Decode certificate from base64
        $cert = [System.Convert]::FromBase64String($env:CERTIFICATE_BASE64)
        [System.IO.File]::WriteAllBytes("$env:TEMP\cert.pfx", $cert)

        # Sign
        & "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe" sign `
          /f "$env:TEMP\cert.pfx" `
          /p $env:CERTIFICATE_PASSWORD `
          /t http://time.certum.pl `
          /fd sha256 `
          /v src\WhisperVoice\bin\Release\net8.0-windows\win-x64\publish\WhisperVoice.exe

        # Cleanup
        Remove-Item "$env:TEMP\cert.pfx"

    - name: Create Release
      uses: softprops/action-gh-release@v1
      with:
        files: src\WhisperVoice\bin\Release\net8.0-windows\win-x64\publish\WhisperVoice.exe
```

### Setting up Secrets

1. Export your certificate as PFX with password
2. Convert to base64: `[Convert]::ToBase64String([IO.File]::ReadAllBytes("cert.pfx"))`
3. In GitHub repo → Settings → Secrets:
   - `CODE_SIGNING_CERT`: Base64-encoded PFX
   - `CODE_SIGNING_PASSWORD`: PFX password

## SmartScreen Reputation

Even with a valid signature, new certificates start with no reputation:
- First downloads may still show SmartScreen warning
- Reputation builds over time as users download and run
- EV (Extended Validation) certificates have instant reputation but cost $300+/year

### Building Reputation Faster

1. Submit to Microsoft for analysis (optional)
2. Consistent releases with same certificate
3. No malware reports from users
4. Time (typically 2-4 weeks of active use)

## Troubleshooting

### "Certificate not found"

- Ensure the cryptographic card is inserted
- Install proCertum CardManager
- Restart after driver installation

### "Timestamp failed"

- Check internet connection
- Try alternative timestamp servers:
  - `http://timestamp.digicert.com`
  - `http://timestamp.sectigo.com`
  - `http://timestamp.globalsign.com`

### "SignTool not found"

Add to PATH or use full path:
```powershell
$env:PATH += ";C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64"
```

## Cost Comparison

| Certificate Type | Cost/Year | Reputation | Best For |
|-----------------|-----------|------------|----------|
| Certum Open Source | $29 | Builds over time | Open source projects |
| Standard Code Signing | $200-400 | Builds over time | Commercial software |
| EV Code Signing | $300-600 | Instant | Enterprise/critical software |
| Azure Trusted Signing | Free | Microsoft trusted | Qualifying organizations |

## Next Steps

1. Purchase Certum certificate ($29)
2. Complete identity verification
3. Set up GitHub Actions workflow
4. Create first signed release
5. Monitor SmartScreen reputation
