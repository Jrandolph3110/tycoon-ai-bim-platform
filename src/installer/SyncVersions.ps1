# Tycoon AI-BIM Platform Version Synchronization Script
# Fixes version inconsistencies by synchronizing all files to version.txt

param(
    [Parameter(Mandatory=$false)]
    [switch]$DryRun = $false
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$VersionFile = Join-Path $ScriptDir "version.txt"

Write-Host "=== TYCOON VERSION SYNCHRONIZATION ===" -ForegroundColor Green

# Read master version
if (Test-Path $VersionFile) {
    $MasterVersion = Get-Content $VersionFile -Raw
    $MasterVersion = $MasterVersion.Trim()
    Write-Host "Master Version (version.txt): $MasterVersion" -ForegroundColor Cyan
} else {
    Write-Host "ERROR: version.txt not found!" -ForegroundColor Red
    exit 1
}

# Define all version locations
$VersionLocations = @(
    @{
        File = "Product.wxs"
        Path = Join-Path $ScriptDir "Product.wxs"
        Pattern = 'Version="([\d\.]+)"'
        Replacement = 'Version="' + $MasterVersion + '"'
        Description = "WiX installer product"
    },
    @{
        File = "Bundle.wxs"
        Path = Join-Path $ScriptDir "Bundle.wxs"
        Pattern = 'Version="([\d\.]+)"'
        Replacement = 'Version="' + $MasterVersion + '"'
        Description = "WiX bootstrapper bundle"
    },
    @{
        File = "AssemblyInfo.cs"
        Path = Join-Path $ScriptDir "..\revit-addin\Properties\AssemblyInfo.cs"
        Pattern = 'AssemblyVersion\("([\d\.]+)"\)'
        Replacement = 'AssemblyVersion("' + $MasterVersion + '")'
        Description = "Revit add-in assembly version"
    },
    @{
        File = "AssemblyInfo.cs (FileVersion)"
        Path = Join-Path $ScriptDir "..\revit-addin\Properties\AssemblyInfo.cs"
        Pattern = 'AssemblyFileVersion\("([\d\.]+)"\)'
        Replacement = 'AssemblyFileVersion("' + $MasterVersion + '")'
        Description = "Revit add-in file version"
    },
    @{
        File = "package.json"
        Path = Join-Path $ScriptDir "..\mcp-server\package.json"
        Pattern = '"version":\s*"([\d\.]+)"'
        Replacement = '"version": "' + $MasterVersion + '"'
        Description = "MCP server package"
    },
    @{
        File = "package-lock.json"
        Path = Join-Path $ScriptDir "..\mcp-server\package-lock.json"
        Pattern = '"version":\s*"([\d\.]+)"'
        Replacement = '"version": "' + $MasterVersion + '"'
        Description = "MCP server lock file"
    }
)

Write-Host ""
Write-Host "Checking current versions..." -ForegroundColor Yellow

$Inconsistencies = @()
foreach ($Location in $VersionLocations) {
    if (Test-Path $Location.Path) {
        $Content = Get-Content $Location.Path -Raw
        if ($Content -match $Location.Pattern) {
            # Use first capture group for version
            $CurrentVersion = $matches[1]

            if ($CurrentVersion -ne $MasterVersion) {
                $Inconsistencies += $Location
                Write-Host "❌ $($Location.File): $CurrentVersion → $MasterVersion" -ForegroundColor Red
            } else {
                Write-Host "✅ $($Location.File): $CurrentVersion" -ForegroundColor Green
            }
        } else {
            Write-Host "⚠️  $($Location.File): Pattern not found" -ForegroundColor Yellow
        }
    } else {
        Write-Host "⚠️  $($Location.File): File not found" -ForegroundColor Yellow
    }
}

if ($Inconsistencies.Count -eq 0) {
    Write-Host ""
    Write-Host "✅ All versions are already synchronized!" -ForegroundColor Green
    exit 0
}

Write-Host ""
Write-Host "Found $($Inconsistencies.Count) version inconsistencies" -ForegroundColor Yellow

if ($DryRun) {
    Write-Host ""
    Write-Host "DRY RUN MODE - No files will be modified" -ForegroundColor Cyan
    Write-Host "Run without -DryRun to apply changes" -ForegroundColor Gray
    exit 0
}

Write-Host ""
Write-Host "Synchronizing versions..." -ForegroundColor Blue

foreach ($Location in $Inconsistencies) {
    try {
        $Content = Get-Content $Location.Path -Raw -Encoding UTF8
        $UpdatedContent = $Content -replace $Location.Pattern, $Location.Replacement
        
        if ($Content -ne $UpdatedContent) {
            Set-Content $Location.Path -Value $UpdatedContent -NoNewline -Encoding UTF8
            Write-Host "UPDATED: $($Location.File)" -ForegroundColor Green
            Write-Host "   $($Location.Description)" -ForegroundColor Gray
        }
    } catch {
        Write-Host "ERROR updating $($Location.File): $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "✅ Version synchronization completed!" -ForegroundColor Green
Write-Host "All files now use version: $MasterVersion" -ForegroundColor Cyan
