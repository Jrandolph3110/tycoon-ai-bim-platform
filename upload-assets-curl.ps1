#!/usr/bin/env powershell
<#
.SYNOPSIS
Upload installer assets to GitHub release v0.17.0 using curl

.DESCRIPTION
This script uploads the TycoonAI-BIM-Platform installer files to the GitHub release using curl.
Uses the existing GitHub authentication from the system.

.EXAMPLE
.\upload-assets-curl.ps1
#>

param(
    [string]$ReleaseTag = "v0.17.0",
    [string]$RepoOwner = "Jrandolph3110", 
    [string]$RepoName = "tycoon-ai-bim-platform",
    [string]$ReleaseId = "232255608"
)

Write-Host "=== UPLOADING RELEASE ASSETS WITH CURL ===" -ForegroundColor Green

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

# Try to get GitHub token from environment or git config
$token = $env:GITHUB_TOKEN
if (-not $token) {
    try {
        $token = git config --global github.token
    } catch {
        # Token not found in git config
    }
}

if (-not $token) {
    Write-Host "❌ GitHub token not found. Please set GITHUB_TOKEN environment variable or run:" -ForegroundColor Red
    Write-Host "   git config --global github.token YOUR_TOKEN" -ForegroundColor Yellow
    Write-Host "   Or set environment variable: `$env:GITHUB_TOKEN = 'YOUR_TOKEN'" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ GitHub token found" -ForegroundColor Green

# Upload URL for the release
$uploadUrl = "https://uploads.github.com/repos/$RepoOwner/$RepoName/releases/$ReleaseId/assets"

Write-Host "🚀 Uploading assets to release $ReleaseTag..." -ForegroundColor Yellow

try {
    # Upload Setup.exe
    Write-Host "   Uploading TycoonAI-BIM-Platform-Setup.exe..." -ForegroundColor Yellow
    $setupUploadUrl = "$uploadUrl" + "?name=TycoonAI-BIM-Platform-Setup.exe"
    
    $result1 = curl -X POST `
        -H "Authorization: token $token" `
        -H "Content-Type: application/octet-stream" `
        --data-binary "@$setupExe" `
        "$setupUploadUrl"
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✅ Setup.exe uploaded successfully" -ForegroundColor Green
    } else {
        Write-Host "   ❌ Failed to upload Setup.exe" -ForegroundColor Red
        Write-Host "   Response: $result1" -ForegroundColor Red
        exit 1
    }
    
    # Upload MSI
    Write-Host "   Uploading TycoonAI-BIM-Platform.msi..." -ForegroundColor Yellow
    $msiUploadUrl = "$uploadUrl" + "?name=TycoonAI-BIM-Platform.msi"
    
    $result2 = curl -X POST `
        -H "Authorization: token $token" `
        -H "Content-Type: application/octet-stream" `
        --data-binary "@$msiFile" `
        "$msiUploadUrl"
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✅ MSI uploaded successfully" -ForegroundColor Green
    } else {
        Write-Host "   ❌ Failed to upload MSI" -ForegroundColor Red
        Write-Host "   Response: $result2" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "🎉 All assets uploaded successfully!" -ForegroundColor Green
    Write-Host "🔗 Release URL: https://github.com/$RepoOwner/$RepoName/releases/tag/$ReleaseTag" -ForegroundColor Cyan
    
} catch {
    Write-Host "❌ Upload failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Release v0.17.0 is now complete with installer assets!" -ForegroundColor Green
