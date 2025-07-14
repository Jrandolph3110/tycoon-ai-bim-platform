#!/usr/bin/env powershell
<#
.SYNOPSIS
Upload installer assets to GitHub release v0.17.0

.DESCRIPTION
This script uploads the TycoonAI-BIM-Platform installer files to the GitHub release.
Requires GitHub CLI (gh) to be installed and authenticated.

.EXAMPLE
.\upload-release-assets.ps1
#>

param(
    [string]$ReleaseTag = "v0.17.0",
    [string]$RepoOwner = "Jrandolph3110",
    [string]$RepoName = "tycoon-ai-bim-platform"
)

Write-Host "=== UPLOADING RELEASE ASSETS ===" -ForegroundColor Green

# Check if GitHub CLI is installed
try {
    $ghVersion = gh --version
    Write-Host "GitHub CLI found: $($ghVersion[0])" -ForegroundColor Green
} catch {
    Write-Host "❌ GitHub CLI (gh) not found. Please install it first:" -ForegroundColor Red
    Write-Host "   winget install GitHub.cli" -ForegroundColor Yellow
    Write-Host "   Then run: gh auth login" -ForegroundColor Yellow
    exit 1
}

# Check authentication
try {
    $authStatus = gh auth status 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Not authenticated with GitHub. Please run:" -ForegroundColor Red
        Write-Host "   gh auth login" -ForegroundColor Yellow
        exit 1
    }
    Write-Host "✅ GitHub authentication verified" -ForegroundColor Green
} catch {
    Write-Host "❌ GitHub authentication check failed" -ForegroundColor Red
    exit 1
}

# Define file paths
$installerDir = "src\installer\bin\Release"
$setupExe = Join-Path $installerDir "TycoonAI-BIM-Platform-Setup.exe"
$msiFile = Join-Path $installerDir "TycoonAI-BIM-Platform.msi"

# Check if files exist
if (-not (Test-Path $setupExe)) {
    Write-Host "❌ Setup.exe not found: $setupExe" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $msiFile)) {
    Write-Host "❌ MSI file not found: $msiFile" -ForegroundColor Red
    exit 1
}

Write-Host "📁 Found installer files:" -ForegroundColor Cyan
Write-Host "   Setup: $setupExe" -ForegroundColor White
Write-Host "   MSI:   $msiFile" -ForegroundColor White

# Get file sizes
$setupSize = [math]::Round((Get-Item $setupExe).Length / 1MB, 2)
$msiSize = [math]::Round((Get-Item $msiFile).Length / 1MB, 2)

Write-Host "📊 File sizes:" -ForegroundColor Cyan
Write-Host "   Setup: $setupSize MB" -ForegroundColor White
Write-Host "   MSI:   $msiSize MB" -ForegroundColor White

# Upload files to release
Write-Host "🚀 Uploading assets to release $ReleaseTag..." -ForegroundColor Yellow

try {
    # Upload Setup.exe
    Write-Host "   Uploading TycoonAI-BIM-Platform-Setup.exe..." -ForegroundColor Yellow
    gh release upload $ReleaseTag $setupExe --repo "$RepoOwner/$RepoName" --clobber
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✅ Setup.exe uploaded successfully" -ForegroundColor Green
    } else {
        Write-Host "   ❌ Failed to upload Setup.exe" -ForegroundColor Red
        exit 1
    }
    
    # Upload MSI
    Write-Host "   Uploading TycoonAI-BIM-Platform.msi..." -ForegroundColor Yellow
    gh release upload $ReleaseTag $msiFile --repo "$RepoOwner/$RepoName" --clobber
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✅ MSI uploaded successfully" -ForegroundColor Green
    } else {
        Write-Host "   ❌ Failed to upload MSI" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "🎉 All assets uploaded successfully!" -ForegroundColor Green
    Write-Host "🔗 Release URL: https://github.com/$RepoOwner/$RepoName/releases/tag/$ReleaseTag" -ForegroundColor Cyan

} catch {
    Write-Host "❌ Upload failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Release v0.17.0 is now complete with installer assets!" -ForegroundColor Green
