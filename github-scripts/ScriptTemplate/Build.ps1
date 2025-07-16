# ============================================================================
# TYCOON AI-BIM PLATFORM - SCRIPT TEMPLATE BUILD SCRIPT
# ============================================================================
# This PowerShell script provides automated building for the script template
# and serves as a reference for building other Tycoon platform scripts.
# ============================================================================

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory=$false)]
    [switch]$Clean,
    
    [Parameter(Mandatory=$false)]
    [switch]$VerboseOutput
)

# Script configuration
$ScriptName = "ScriptTemplate"
$ProjectFile = "$ScriptName.csproj"
$ScriptRoot = $PSScriptRoot

Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "TYCOON AI-BIM PLATFORM - SCRIPT TEMPLATE BUILD" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "Script: $ScriptName" -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Green
Write-Host "Location: $ScriptRoot" -ForegroundColor Green
Write-Host ""

# Function to write colored output
function Write-Status {
    param([string]$Message, [string]$Color = "White")
    Write-Host $Message -ForegroundColor $Color
}

# Function to check prerequisites
function Test-Prerequisites {
    Write-Status "🔍 Checking prerequisites..." "Yellow"
    
    # Check if project file exists
    if (-not (Test-Path $ProjectFile)) {
        Write-Status "❌ Project file not found: $ProjectFile" "Red"
        return $false
    }
    
    # Check for dotnet CLI
    try {
        $dotnetVersion = dotnet --version
        Write-Status "✅ .NET CLI found: $dotnetVersion" "Green"
    }
    catch {
        Write-Status "❌ .NET CLI not found. Please install .NET SDK." "Red"
        return $false
    }
    
    # Check for MSBuild
    try {
        $msbuildPath = Get-Command "MSBuild.exe" -ErrorAction SilentlyContinue
        if ($msbuildPath) {
            Write-Status "✅ MSBuild found: $($msbuildPath.Source)" "Green"
        }
        else {
            Write-Status "⚠️ MSBuild not found in PATH, using dotnet build" "Yellow"
        }
    }
    catch {
        Write-Status "⚠️ MSBuild check failed, using dotnet build" "Yellow"
    }
    
    Write-Status "✅ Prerequisites check completed" "Green"
    return $true
}

# Function to clean build artifacts
function Invoke-Clean {
    Write-Status "🧹 Cleaning build artifacts..." "Yellow"
    
    $cleanPaths = @(
        "bin",
        "obj"
    )
    
    foreach ($path in $cleanPaths) {
        if (Test-Path $path) {
            Remove-Item $path -Recurse -Force
            Write-Status "  Removed: $path" "Gray"
        }
    }
    
    Write-Status "✅ Clean completed" "Green"
}

# Function to restore NuGet packages
function Invoke-Restore {
    Write-Status "📦 Restoring NuGet packages..." "Yellow"
    
    try {
        $restoreArgs = @(
            "restore"
            $ProjectFile
        )
        
        if ($VerboseOutput) {
            $restoreArgs += "--verbosity", "detailed"
        }
        
        & dotnet @restoreArgs
        
        if ($LASTEXITCODE -eq 0) {
            Write-Status "✅ Package restore completed" "Green"
            return $true
        }
        else {
            Write-Status "❌ Package restore failed" "Red"
            return $false
        }
    }
    catch {
        Write-Status "❌ Package restore failed: $($_.Exception.Message)" "Red"
        return $false
    }
}

# Function to build the project
function Invoke-Build {
    Write-Status "🔨 Building $ScriptName..." "Yellow"
    
    try {
        $buildArgs = @(
            "build"
            $ProjectFile
            "--configuration", $Configuration
            "--no-restore"
        )
        
        if ($VerboseOutput) {
            $buildArgs += "--verbosity", "detailed"
        }
        
        & dotnet @buildArgs
        
        if ($LASTEXITCODE -eq 0) {
            Write-Status "✅ Build completed successfully" "Green"
            return $true
        }
        else {
            Write-Status "❌ Build failed" "Red"
            return $false
        }
    }
    catch {
        Write-Status "❌ Build failed: $($_.Exception.Message)" "Red"
        return $false
    }
}

# Function to verify build output
function Test-BuildOutput {
    Write-Status "🔍 Verifying build output..." "Yellow"
    
    $outputPath = "bin\$Configuration\$ScriptName.dll"
    
    if (Test-Path $outputPath) {
        $fileInfo = Get-Item $outputPath
        Write-Status "✅ Output file created: $outputPath" "Green"
        Write-Status "  Size: $($fileInfo.Length) bytes" "Gray"
        Write-Status "  Modified: $($fileInfo.LastWriteTime)" "Gray"
        return $true
    }
    else {
        Write-Status "❌ Output file not found: $outputPath" "Red"
        return $false
    }
}

# Function to show build summary
function Show-BuildSummary {
    param([bool]$Success)
    
    Write-Host ""
    Write-Host "============================================================================" -ForegroundColor Cyan
    
    if ($Success) {
        Write-Status "🎉 BUILD SUCCESSFUL!" "Green"
        Write-Status "Script template is ready for use" "Green"
        
        $outputPath = "bin\$Configuration\$ScriptName.dll"
        Write-Status "Output: $outputPath" "White"
        
        Write-Host ""
        Write-Status "Next steps:" "Yellow"
        Write-Status "1. Copy script folder to test-scripts directory for local testing" "White"
        Write-Status "2. Use 'Reload Scripts' button in Tycoon ribbon" "White"
        Write-Status "3. Test script functionality" "White"
        Write-Status "4. Commit to repository for GitHub integration" "White"
    }
    else {
        Write-Status "❌ BUILD FAILED!" "Red"
        Write-Status "Please check the error messages above" "Red"
    }
    
    Write-Host "============================================================================" -ForegroundColor Cyan
}

# Main execution
try {
    # Change to script directory
    Set-Location $ScriptRoot
    
    # Check prerequisites
    if (-not (Test-Prerequisites)) {
        exit 1
    }
    
    # Clean if requested
    if ($Clean) {
        Invoke-Clean
    }
    
    # Restore packages
    if (-not (Invoke-Restore)) {
        Show-BuildSummary $false
        exit 1
    }
    
    # Build project
    if (-not (Invoke-Build)) {
        Show-BuildSummary $false
        exit 1
    }
    
    # Verify output
    if (-not (Test-BuildOutput)) {
        Show-BuildSummary $false
        exit 1
    }
    
    # Show success summary
    Show-BuildSummary $true
    exit 0
}
catch {
    Write-Status "❌ Unexpected error: $($_.Exception.Message)" "Red"
    Show-BuildSummary $false
    exit 1
}
