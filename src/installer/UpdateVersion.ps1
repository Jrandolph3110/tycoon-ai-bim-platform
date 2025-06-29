# Tycoon AI-BIM Platform Version Management Script
# Use this script to manually set version numbers

param(
    [Parameter(Mandatory=$true)]
    [ValidatePattern("^\d+\.\d+\.\d+\.\d+$")]
    [string]$Version,
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("Major", "Minor", "Build", "Revision")]
    [string]$Increment
)

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$VersionFile = Join-Path $ScriptDir "version.txt"

Write-Host "🔢 Tycoon AI-BIM Platform Version Manager" -ForegroundColor Blue
Write-Host "=======================================" -ForegroundColor Blue

if ($Increment) {
    # Read current version
    if (Test-Path $VersionFile) {
        $CurrentVersion = Get-Content $VersionFile -Raw
        $CurrentVersion = $CurrentVersion.Trim()
    } else {
        $CurrentVersion = "1.0.0.0"
    }
    
    # Parse and increment
    $VersionParts = $CurrentVersion.Split('.')
    $Major = [int]$VersionParts[0]
    $Minor = [int]$VersionParts[1] 
    $Build = [int]$VersionParts[2]
    $Revision = [int]$VersionParts[3]
    
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
    Write-Host "Incremented $Increment version: $CurrentVersion -> $Version" -ForegroundColor Cyan
}

# Function to update version in files
function Update-VersionInFile {
    param(
        [string]$FilePath,
        [string]$Pattern,
        [string]$Replacement
    )
    
    if (Test-Path $FilePath) {
        $Content = Get-Content $FilePath -Raw
        $UpdatedContent = $Content -replace $Pattern, $Replacement
        Set-Content $FilePath -Value $UpdatedContent -NoNewline
        Write-Host "✅ Updated: $FilePath" -ForegroundColor Green
    } else {
        Write-Host "⚠️  Not found: $FilePath" -ForegroundColor Yellow
    }
}

Write-Host "Setting version to: $Version" -ForegroundColor Yellow

# Update all version references
Update-VersionInFile -FilePath (Join-Path $ScriptDir "Product.wxs") `
    -Pattern 'Version="[\d\.]+"' `
    -Replacement "Version=`"$Version`""

$AssemblyInfoPath = Join-Path $ScriptDir "..\revit-addin\Properties\AssemblyInfo.cs"
Update-VersionInFile -FilePath $AssemblyInfoPath `
    -Pattern '\[assembly: AssemblyVersion\("[\d\.]+"\)\]' `
    -Replacement "[assembly: AssemblyVersion(`"$Version`")]"

Update-VersionInFile -FilePath $AssemblyInfoPath `
    -Pattern '\[assembly: AssemblyFileVersion\("[0-9.]+"\)\]' `
    -Replacement "[assembly: AssemblyFileVersion(`"$Version`")]"

$PackageJsonPath = Join-Path $ScriptDir "..\mcp-server\package.json"
Update-VersionInFile -FilePath $PackageJsonPath `
    -Pattern '"version":\s*"[\d\.]+"' `
    -Replacement "`"version`": `"$Version`""

# Save version
Set-Content $VersionFile -Value $Version

Write-Host ""
Write-Host "🎉 Version successfully updated to: $Version" -ForegroundColor Green
Write-Host ""
Write-Host "Files updated:" -ForegroundColor Yellow
Write-Host "  • Product.wxs (MSI version)" -ForegroundColor White
Write-Host "  • AssemblyInfo.cs (DLL version)" -ForegroundColor White  
Write-Host "  • package.json (MCP server version)" -ForegroundColor White
Write-Host "  • version.txt (build tracking)" -ForegroundColor White
Write-Host ""
Write-Host "Next: Run Build.ps1 to compile with new version" -ForegroundColor Cyan
