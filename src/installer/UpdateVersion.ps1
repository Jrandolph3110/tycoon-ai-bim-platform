# Tycoon AI-BIM Platform Version Update Script
# Updates version numbers across all project files

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("Major", "Minor", "Build", "Revision")]
    [string]$Increment,

    [Parameter(Mandatory=$false)]
    [string]$SetVersion = $null
)

# Script configuration
$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$VersionFile = Join-Path $ScriptDir "version.txt"

# Read current version
if (Test-Path $VersionFile) {
    $CurrentVersion = Get-Content $VersionFile -Raw
    $CurrentVersion = $CurrentVersion.Trim()
} else {
    $CurrentVersion = "0.12.0.2"
    Write-Host "WARNING: Version file not found, using default: $CurrentVersion" -ForegroundColor Yellow
}

Write-Host "Current Version: $CurrentVersion" -ForegroundColor Cyan

# Parse version components
$VersionParts = $CurrentVersion.Split('.')
if ($VersionParts.Length -ne 4) {
    Write-Host "ERROR: Invalid version format. Expected Major.Minor.Build.Revision" -ForegroundColor Red
    exit 1
}

$Major = [int]$VersionParts[0]
$Minor = [int]$VersionParts[1]
$Build = [int]$VersionParts[2]
$Revision = [int]$VersionParts[3]

# Calculate new version
if ($SetVersion) {
    $Version = $SetVersion
    Write-Host "Setting version to: $Version" -ForegroundColor Yellow
} else {
    switch ($Increment) {
        "Major" {
            $Major++
            $Minor = 0
            $Build = 0
            $Revision = 0
        }
        "Minor" {
            $Minor++
            $Build = 0
            $Revision = 0
        }
        "Build" {
            $Build++
            $Revision = 0
        }
        "Revision" {
            $Revision++
        }
    }
    $Version = "$Major.$Minor.$Build.$Revision"
    Write-Host "Incrementing $Increment version to: $Version" -ForegroundColor Green
}

# Function to update version in files
function Update-VersionInFile {
    param(
        [string]$FilePath,
        [string]$Pattern,
        [string]$Replacement,
        [string]$Description = ""
    )

    if (Test-Path $FilePath) {
        try {
            $Content = Get-Content $FilePath -Raw -Encoding UTF8
            $UpdatedContent = $Content -replace $Pattern, $Replacement

            if ($Content -ne $UpdatedContent) {
                Set-Content $FilePath -Value $UpdatedContent -NoNewline -Encoding UTF8
                Write-Host "UPDATED: $FilePath" -ForegroundColor Green
                if ($Description) {
                    Write-Host "   $Description" -ForegroundColor Gray
                }
            } else {
                Write-Host "NO CHANGE: $FilePath" -ForegroundColor Gray
            }
        } catch {
            Write-Host "ERROR updating $FilePath : $($_.Exception.Message)" -ForegroundColor Red
        }
    } else {
        Write-Host "NOT FOUND: $FilePath" -ForegroundColor Yellow
    }
}

# Function to validate current version consistency
function Test-VersionConsistency {
    Write-Host ""
    Write-Host "Validating current version consistency..." -ForegroundColor Blue

    $Files = @(
        @{ Path = Join-Path $ScriptDir "Product.wxs"; Pattern = 'Version="([\d\.]+)"'; Name = "Product.wxs" }
        @{ Path = Join-Path $ScriptDir "..\revit-addin\Properties\AssemblyInfo.cs"; Pattern = 'AssemblyVersion\("([\d\.]+)"\)'; Name = "AssemblyInfo.cs" }
        @{ Path = Join-Path $ScriptDir "..\mcp-server\package.json"; Pattern = '"version":\s*"([\d\.]+)"'; Name = "package.json" }
        @{ Path = Join-Path $ScriptDir "Bundle.wxs"; Pattern = 'Version="([\d\.]+)"'; Name = "Bundle.wxs" }
    )

    $Inconsistencies = @()
    foreach ($File in $Files) {
        if (Test-Path $File.Path) {
            $Content = Get-Content $File.Path -Raw
            if ($Content -match $File.Pattern) {
                $FileVersion = $matches[1]
                if ($FileVersion -ne $CurrentVersion) {
                    $Inconsistencies += @{ File = $File.Name; Expected = $CurrentVersion; Found = $FileVersion }
                    Write-Host "⚠️  $($File.Name): $FileVersion (expected: $CurrentVersion)" -ForegroundColor Yellow
                } else {
                    Write-Host "✅ $($File.Name): $FileVersion" -ForegroundColor Green
                }
            } else {
                Write-Host "❌ $($File.Name): Version pattern not found" -ForegroundColor Red
            }
        } else {
            Write-Host "❌ $($File.Name): File not found" -ForegroundColor Red
        }
    }

    if ($Inconsistencies.Count -gt 0) {
        Write-Host ""
        Write-Host "WARNING: Version inconsistencies detected!" -ForegroundColor Yellow
        Write-Host "The script will fix these inconsistencies." -ForegroundColor Gray
    } else {
        Write-Host ""
        Write-Host "✅ All versions are consistent!" -ForegroundColor Green
    }
}

# Validate current version consistency
Test-VersionConsistency

Write-Host ""
Write-Host "Updating version references..." -ForegroundColor Blue

# 1. Update version.txt (master version file)
Set-Content $VersionFile -Value $Version -NoNewline -Encoding UTF8
Write-Host "UPDATED: $VersionFile" -ForegroundColor Green
Write-Host "   Master version file" -ForegroundColor Gray

# 2. Update Product.wxs (WiX installer version)
$ProductWxsPath = Join-Path $ScriptDir "Product.wxs"
Update-VersionInFile -FilePath $ProductWxsPath `
    -Pattern '(<Product[^>]+Version=")[\d\.]+(")' `
    -Replacement ('${1}' + $Version + '${2}') `
    -Description "WiX installer product version"

# 3. Update AssemblyInfo.cs (.NET assembly versions)
$AssemblyInfoPath = Join-Path $ScriptDir "..\revit-addin\Properties\AssemblyInfo.cs"
Update-VersionInFile -FilePath $AssemblyInfoPath `
    -Pattern '\[assembly: AssemblyVersion\("[\d\.]+"\)\]' `
    -Replacement ('[assembly: AssemblyVersion("' + $Version + '")]') `
    -Description "Assembly version"

Update-VersionInFile -FilePath $AssemblyInfoPath `
    -Pattern '\[assembly: AssemblyFileVersion\("[\d\.]+"\)\]' `
    -Replacement ('[assembly: AssemblyFileVersion("' + $Version + '")]') `
    -Description "Assembly file version"

# 4. Update package.json (MCP server version)
$PackageJsonPath = Join-Path $ScriptDir "..\mcp-server\package.json"
Update-VersionInFile -FilePath $PackageJsonPath `
    -Pattern '"version":\s*"[\d\.]+"' `
    -Replacement ('"version": "' + $Version + '"') `
    -Description "MCP server package version"

# 5. Update Bundle.wxs (WiX bootstrapper version)
$BundleWxsPath = Join-Path $ScriptDir "Bundle.wxs"
Update-VersionInFile -FilePath $BundleWxsPath `
    -Pattern '(<Bundle[^>]+Version=")[\d\.]+(")' `
    -Replacement ('${1}' + $Version + '${2}') `
    -Description "WiX bootstrapper bundle version"

# 6. Update package-lock.json (MCP server lock file)
$PackageLockPath = Join-Path $ScriptDir "..\mcp-server\package-lock.json"
if (Test-Path $PackageLockPath) {
    Update-VersionInFile -FilePath $PackageLockPath `
        -Pattern '"version":\s*"[\d\.]+"' `
        -Replacement ('"version": "' + $Version + '"') `
        -Description "MCP server package-lock version"
} else {
    Write-Host "NOT FOUND: $PackageLockPath" -ForegroundColor Yellow
    Write-Host "   Run 'npm install' to regenerate package-lock.json" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Version update completed!" -ForegroundColor Green
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "   Old Version: $CurrentVersion" -ForegroundColor Gray
Write-Host "   New Version: $Version" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "   1. Review changes: git diff" -ForegroundColor Gray
Write-Host "   2. Build installer: .\Build.ps1" -ForegroundColor Gray
Write-Host "   3. Test installation" -ForegroundColor Gray
Write-Host "   4. Commit changes: git add . && git commit -m 'Version $Version'" -ForegroundColor Gray
