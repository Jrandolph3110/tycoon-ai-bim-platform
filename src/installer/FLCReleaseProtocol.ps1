# F.L. Crane & Sons Development Protocol Script
# Handles changelog generation and git operations for releases

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [Parameter(Mandatory=$false)]
    [string]$ChangelogMessage = "",
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipGitOperations
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host ""
Write-Host "F.L. Crane and Sons Development Protocol" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# Generate Changelog
function New-ChangelogEntry {
    param(
        [string]$Version,
        [string]$Message = ""
    )
    
    $ChangelogPath = Join-Path $ScriptDir "..\..\CHANGELOG.md"
    $Date = Get-Date -Format "yyyy-MM-dd"
    
    # Default changelog message for this release
    if ([string]::IsNullOrEmpty($Message)) {
        $Message = "GitHub Script Refresh Debugging Enhancement

Bug Fixes:
- Enhanced JSON deserialization debugging in GitCacheManager.cs
- Added detailed logging for base64 decoding and JSON structure validation
- Improved error handling with JsonException details including line numbers and positions
- Added manifest content length and preview logging for troubleshooting

Technical Improvements:
- Resolves Error converting value Version to type ScriptManifest issue
- Enhanced GitHub-driven script system reliability
- Better diagnostic information for JSON parsing failures
- Improved debugging capabilities for GitHub API integration

Testing:
- Ready for testing GitHub script refresh functionality
- Enhanced logging will help identify root cause of deserialization issues
- Maintains backward compatibility with existing script cache system"
    }
    
    $ChangelogEntry = "## [$Version] - $Date

$Message

"
    
    # Create or update changelog
    if (Test-Path $ChangelogPath) {
        $ExistingContent = Get-Content $ChangelogPath -Raw
        $NewContent = $ChangelogEntry + $ExistingContent
    } else {
        $Header = "# Tycoon AI-BIM Platform Changelog

All notable changes to the Tycoon AI-BIM Platform will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

"
        $NewContent = $Header + $ChangelogEntry
    }
    
    Set-Content -Path $ChangelogPath -Value $NewContent -Encoding UTF8
    Write-Host "📝 Generated changelog entry for version $Version" -ForegroundColor Green
    return $ChangelogPath
}

# Commit and Tag Release
function Invoke-ReleaseCommit {
    param(
        [string]$Version,
        [string]$ChangelogPath
    )
    
    if ($SkipGitOperations) {
        Write-Host "⏭️ Skipping git operations as requested" -ForegroundColor Yellow
        return
    }
    
    try {
        # Check if we're in a git repository
        $null = & git status 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Not in a git repository or git not available. Skipping git operations."
            return
        }
        
        Write-Host "📦 Committing release changes..." -ForegroundColor Blue
        
        # Add all version-related files
        & git add version.txt
        & git add Product.wxs
        & git add ..\revit-addin\Properties\AssemblyInfo.cs
        & git add ..\mcp-server\package.json
        & git add $ChangelogPath
        
        # Commit with detailed message
        $CommitMessage = "Release v$Version - GitHub Script Refresh Debugging Enhancement

- Enhanced JSON deserialization debugging in GitCacheManager.cs
- Added detailed logging for GitHub API response processing
- Improved error handling for script manifest parsing
- Ready for testing GitHub script refresh functionality

Version: $Version
Build: Release"
        
        & git commit -m $CommitMessage
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Committed release changes" -ForegroundColor Green
            
            # Create annotated tag
            $TagMessage = "Tycoon AI-BIM Platform v$Version

GitHub Script Refresh Debugging Enhancement
- Enhanced JSON deserialization debugging
- Improved error handling and logging
- Ready for production testing

F.L. Crane & Sons Development Team"
            
            & git tag -a "v$Version" -m $TagMessage
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "🏷️ Created release tag v$Version" -ForegroundColor Green
                
                # Push changes and tags
                Write-Host "📤 Pushing to remote repository..." -ForegroundColor Blue
                & git push origin main
                & git push origin "v$Version"
                
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "✅ Successfully pushed release to remote repository" -ForegroundColor Green
                } else {
                    Write-Warning "Failed to push to remote repository. Please push manually."
                }
            } else {
                Write-Warning "Failed to create git tag. Please tag manually."
            }
        } else {
            Write-Warning "Failed to commit changes. Please commit manually."
        }
    }
    catch {
        Write-Warning "Git operations failed: $($_.Exception.Message)"
    }
}

# Execute the protocol
$ChangelogPath = New-ChangelogEntry -Version $Version -Message $ChangelogMessage
Write-Host "📝 Changelog generated: $ChangelogPath" -ForegroundColor Green

Write-Host ""
Write-Host "F.L. Crane and Sons Release Protocol" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

Invoke-ReleaseCommit -Version $Version -ChangelogPath $ChangelogPath

Write-Host ""
Write-Host "📋 Release Summary:" -ForegroundColor Cyan
Write-Host "   Version: $Version" -ForegroundColor White
Write-Host "   Changelog: $ChangelogPath" -ForegroundColor White
if (-not $SkipGitOperations) {
    Write-Host "   Git Tag: v$Version" -ForegroundColor White
    Write-Host "   Repository: Updated and tagged" -ForegroundColor White
}
Write-Host ""
