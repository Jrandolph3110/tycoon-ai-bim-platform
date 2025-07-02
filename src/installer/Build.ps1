# Tycoon AI-BIM Platform Build Script
# Builds the complete installer package

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [Parameter(Mandatory=$false)]
    [ValidateSet("x86", "x64", "Any CPU")]
    [string]$Platform = "x86",

    [Parameter(Mandatory=$false)]
    [switch]$Clean,

    [Parameter(Mandatory=$false)]
    [switch]$SkipTests,

    [Parameter(Mandatory=$false)]
    [switch]$BuildBootstrapper
)

# Script configuration
$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionFile = Join-Path $ScriptDir "TycoonInstaller.sln"
$OutputDir = Join-Path $ScriptDir "bin\$Configuration"

# Version Management - Manual Only
$VersionFile = Join-Path $ScriptDir "version.txt"
if (Test-Path $VersionFile) {
    $CurrentVersion = Get-Content $VersionFile -Raw
    $CurrentVersion = $CurrentVersion.Trim()
} else {
    $CurrentVersion = "1.0.2.0"
}

Write-Host "Current Version: $CurrentVersion" -ForegroundColor Cyan
Write-Host "Use UpdateVersion.ps1 to change version numbers" -ForegroundColor Yellow

# No automatic version updating - use UpdateVersion.ps1 for manual control
Write-Host "For version updates, use: .\UpdateVersion.ps1 -Increment [Major|Minor|Build|Revision]" -ForegroundColor Cyan

Write-Host "Building Tycoon AI-BIM Platform Installer" -ForegroundColor Blue
Write-Host "Configuration: $Configuration" -ForegroundColor Gray
Write-Host "Platform: $Platform" -ForegroundColor Gray
Write-Host "Solution: $SolutionFile" -ForegroundColor Gray

# Function to check if a command exists
function Test-Command($cmdname) {
    return [bool](Get-Command -Name $cmdname -ErrorAction SilentlyContinue)
}

# Check prerequisites
Write-Host "Checking prerequisites..." -ForegroundColor Yellow

# Check for MSBuild
$MSBuildPath = ""
if (Test-Command "msbuild") {
    $MSBuildPath = "msbuild"
} else {
    # Try to find MSBuild in common locations
    $MSBuildLocations = @(
        "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    )

    foreach ($location in $MSBuildLocations) {
        if (Test-Path $location) {
            $MSBuildPath = $location
            Write-Host "Found MSBuild at: $location" -ForegroundColor Gray
            break
        }
    }

    if ($MSBuildPath -eq "") {
        Write-Error "MSBuild not found. Please install Visual Studio Build Tools or Visual Studio."
        exit 1
    }
}

# Check for WiX Toolset
$WixPath = "${env:ProgramFiles(x86)}\WiX Toolset v3.11\bin"
if (-not (Test-Path $WixPath)) {
    $WixPath = "${env:ProgramFiles}\WiX Toolset v3.11\bin"
    if (-not (Test-Path $WixPath)) {
        Write-Error "WiX Toolset v3.11 not found. Please install from https://wixtoolset.org/"
        exit 1
    }
}

# Add WiX to PATH for this session
$env:PATH = "$WixPath;$env:PATH"

Write-Host "Prerequisites check passed" -ForegroundColor Green

# Clean if requested
if ($Clean) {
    Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
    
    if (Test-Path $OutputDir) {
        Remove-Item $OutputDir -Recurse -Force
    }
    
    # Clean intermediate directories
    Get-ChildItem -Path $ScriptDir -Recurse -Directory -Name "obj" | ForEach-Object {
        $objPath = Join-Path $ScriptDir $_
        if (Test-Path $objPath) {
            Remove-Item $objPath -Recurse -Force
        }
    }
    
    Write-Host "Clean completed" -ForegroundColor Green
}

# Restore NuGet packages
Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow

$RevitAddinProject = Join-Path $ScriptDir "..\revit-addin\TycoonRevitAddin.csproj"

if (Test-Command "nuget") {
    & nuget restore $RevitAddinProject
} else {
    Write-Warning "NuGet command not found. Attempting to use MSBuild restore..."
    & "$MSBuildPath" $RevitAddinProject /t:Restore /p:Configuration=$Configuration /p:Platform="AnyCPU"
}

Write-Host "Package restore completed" -ForegroundColor Green

# Build Setup Wizard first
Write-Host "Building Setup Wizard..." -ForegroundColor Yellow

$SetupWizardProject = Join-Path $PSScriptRoot "SetupWizard\TycoonSetupWizard.csproj"

# Restore packages for Setup Wizard
Write-Host "Restoring packages for Setup Wizard..." -ForegroundColor Yellow
& "$MSBuildPath" $SetupWizardProject /t:Restore /verbosity:minimal
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to restore packages for Setup Wizard"
    exit 1
}

& "$MSBuildPath" $SetupWizardProject /p:Configuration=$Configuration /p:Platform="AnyCPU" /verbosity:minimal

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build Setup Wizard"
    exit 1
}

# Copy TycoonSetupWizard.exe to installer directory for WiX
$SetupWizardExe = Join-Path $PSScriptRoot "SetupWizard\bin\Release\net48\TycoonSetupWizard.exe"
Copy-Item $SetupWizardExe $PSScriptRoot -Force

Write-Host "Setup Wizard build completed" -ForegroundColor Green

# Build DownloadMCP utility
Write-Host "Building DownloadMCP utility..." -ForegroundColor Yellow
$DownloadMCPProject = Join-Path $PSScriptRoot "DownloadMCP\DownloadMCP.csproj"
if (Test-Path $DownloadMCPProject) {
    & $MSBuildPath $DownloadMCPProject /p:Configuration=$Configuration /p:Platform="Any CPU" /verbosity:minimal
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build DownloadMCP utility"
        exit 1
    }

    # Copy InstallMCP.exe to installer directory for WiX (rename to DownloadMCP.exe)
    $DownloadMCPSource = Join-Path $PSScriptRoot "DownloadMCP\bin\Any CPU\Release\net48\InstallMCP.exe"
    $DownloadMCPDest = Join-Path $PSScriptRoot "DownloadMCP.exe"
    if (Test-Path $DownloadMCPSource) {
        Copy-Item $DownloadMCPSource $DownloadMCPDest -Force
        Write-Host "Copied InstallMCP.exe to installer directory as DownloadMCP.exe" -ForegroundColor Gray
    } else {
        Write-Warning "InstallMCP.exe not found at $DownloadMCPSource"
    }
} else {
    Write-Warning "DownloadMCP project not found, skipping..."
}
Write-Host "DownloadMCP utility build completed" -ForegroundColor Green

# Build Revit Add-in
Write-Host "Building Revit Add-in..." -ForegroundColor Yellow

& "$MSBuildPath" $RevitAddinProject /p:Configuration=$Configuration /p:Platform="AnyCPU" /verbosity:minimal

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build Revit Add-in"
    exit $LASTEXITCODE
}

Write-Host "Revit Add-in build completed" -ForegroundColor Green

# Build MCP Server
Write-Host "Building MCP Server..." -ForegroundColor Yellow

$McpServerDir = Join-Path $ScriptDir "..\mcp-server"
Push-Location $McpServerDir

try {
    # Install dependencies if needed
    if (-not (Test-Path "node_modules")) {
        Write-Host "Installing Node.js dependencies..." -ForegroundColor Gray
        & npm install
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to install Node.js dependencies"
            exit $LASTEXITCODE
        }
    }
    
    # Build TypeScript
    Write-Host "Compiling TypeScript..." -ForegroundColor Gray
    & npm run build
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build MCP Server"
        exit $LASTEXITCODE
    }
    
    Write-Host "MCP Server build completed" -ForegroundColor Green

# Package MCP Server for installer distribution
Write-Host "Packaging MCP Server for installer distribution..." -ForegroundColor Yellow
$PackageMCPScript = Join-Path $PSScriptRoot "PackageMCP.ps1"
if (Test-Path $PackageMCPScript) {
    & powershell -ExecutionPolicy Bypass -File $PackageMCPScript
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "MCP packaging failed, but continuing with installer build..."
    } else {
        # Copy the ZIP file to installer directory for WiX
        $ZipSource = Join-Path $PSScriptRoot "mcp-server\bin\Release\mcp-server.zip"
        $ZipDest = Join-Path $PSScriptRoot "mcp-server.zip"
        if (Test-Path $ZipSource) {
            Copy-Item $ZipSource $ZipDest -Force
            Write-Host "Copied MCP server ZIP to installer directory" -ForegroundColor Gray
        } else {
            Write-Warning "MCP server ZIP not found at $ZipSource"
        }
    }
} else {
    Write-Warning "PackageMCP.ps1 not found, skipping MCP packaging..."
}
Write-Host "MCP Server packaging completed" -ForegroundColor Green
}
finally {
    Pop-Location
}

# Build WiX Installer using command line tools
Write-Host "Building WiX Installer..." -ForegroundColor Yellow

$WixBinPath = "C:\Program Files (x86)\WiX Toolset v3.11\bin"
$CandlePath = Join-Path $WixBinPath "candle.exe"
$LightPath = Join-Path $WixBinPath "light.exe"

# Create output directory
$WixOutputDir = Join-Path $ScriptDir "bin\$Configuration"
if (-not (Test-Path $WixOutputDir)) {
    New-Item -ItemType Directory -Path $WixOutputDir -Force | Out-Null
}

# Compile WiX source files
$WixSources = @(
    "Product.wxs",
    "RevitVersions.wxs",
    "UI\CustomUI.wxs"
)

$WixObjects = @()
$RevitAddinDll = (Resolve-Path (Join-Path $ScriptDir '..\revit-addin\bin\Release\TycoonRevitAddin.dll')).Path
$RevitAddinDir = (Resolve-Path (Join-Path $ScriptDir '..\revit-addin\bin\Release')).Path + "\"

foreach ($source in $WixSources) {
    $sourcePath = (Resolve-Path (Join-Path $ScriptDir $source)).Path
    $objectPath = Join-Path $WixOutputDir ([System.IO.Path]::GetFileNameWithoutExtension($source) + ".wixobj")
    $WixObjects += $objectPath

    Write-Host "Compiling $source..." -ForegroundColor Gray
    & "$CandlePath" -out "$objectPath" "$sourcePath" "-dTycoonRevitAddin.TargetPath=$RevitAddinDll" "-dTycoonRevitAddin.TargetDir=$RevitAddinDir"

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to compile $source"
        exit $LASTEXITCODE
    }
}

# Link WiX objects
$MsiPath = Join-Path $WixOutputDir "TycoonAI-BIM-Platform.msi"
Write-Host "Linking MSI..." -ForegroundColor Gray

# Build light command arguments
$LightArgs = @("-out", "`"$MsiPath`"")
foreach ($obj in $WixObjects) {
    $LightArgs += "`"$obj`""
}
$LightArgs += @("-ext", "WixUIExtension", "-ext", "WixUtilExtension", "-ext", "WixNetFxExtension", "-sval")

# Change to installer directory for linking to resolve relative paths correctly
Push-Location $ScriptDir
try {
    & "$LightPath" $LightArgs
} finally {
    Pop-Location
}

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build WiX Installer"
    exit $LASTEXITCODE
}

Write-Host "WiX Installer build completed" -ForegroundColor Green

# Build bootstrapper if requested (includes Node.js prerequisite)
if ($BuildBootstrapper) {
    Write-Host "Building WiX Bootstrapper with Node.js prerequisite..." -ForegroundColor Blue

    # Find WiX tools
    $WixBinPath = "C:\Program Files (x86)\WiX Toolset v3.11\bin"
    $CandlePath = Join-Path $WixBinPath "candle.exe"
    $LightPath = Join-Path $WixBinPath "light.exe"

    if (!(Test-Path $CandlePath) -or !(Test-Path $LightPath)) {
        Write-Host "Error: WiX Toolset v3.11 not found. Please install WiX Toolset." -ForegroundColor Red
        exit 1
    }

    # Create dummy Node.js MSI for build (will be downloaded at runtime)
    $DummyNodeMsi = Join-Path $ScriptDir "node-v20.11.0-x64.msi"
    "Dummy Node.js MSI for build" | Out-File -FilePath $DummyNodeMsi -Encoding ASCII

    try {
        # Compile Bundle.wxs
        $BundleWxs = Join-Path $ScriptDir "Bundle.wxs"
        $BundleObj = Join-Path $ScriptDir "obj\Release\Bundle.wixobj"
        $MSIPath = Join-Path $OutputDir "TycoonAI-BIM-Platform.msi"

        # Ensure obj directory exists
        $ObjDir = Join-Path $ScriptDir "obj\Release"
        if (!(Test-Path $ObjDir)) {
            New-Item -ItemType Directory -Path $ObjDir -Force | Out-Null
        }

        Write-Host "  Compiling Bundle.wxs..." -ForegroundColor Gray
        & $CandlePath -ext WixBalExtension -ext WixUtilExtension "-dTycoonInstaller.TargetPath=$MSIPath" $BundleWxs -o $BundleObj
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Bundle compilation failed" -ForegroundColor Red
            exit $LASTEXITCODE
        }

        # Link bootstrapper
        $BootstrapperOutput = Join-Path $OutputDir "TycoonAI-BIM-Platform-Setup.exe"
        Write-Host "  Linking bootstrapper..." -ForegroundColor Gray
        & $LightPath -ext WixBalExtension -ext WixUtilExtension $BundleObj -o $BootstrapperOutput
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Bootstrapper linking failed" -ForegroundColor Red
            exit $LASTEXITCODE
        }

        Write-Host "WiX Bootstrapper build completed" -ForegroundColor Green

        # Check for bootstrapper output
        if (Test-Path $BootstrapperOutput) {
            $BootstrapperSize = [math]::Round((Get-Item $BootstrapperOutput).Length / 1MB, 2)
            Write-Host "  Bootstrapper: $BootstrapperOutput ($BootstrapperSize MB)" -ForegroundColor Green
        }
    }
    finally {
        # Clean up dummy file
        if (Test-Path $DummyNodeMsi) {
            Remove-Item $DummyNodeMsi -Force
        }
    }
} else {
    Write-Host "Skipping bootstrapper build (use -BuildBootstrapper to include Node.js prerequisite)" -ForegroundColor Yellow
}

# Display results
Write-Host ""
Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Output files:" -ForegroundColor Yellow

$MsiFile = Join-Path $WixOutputDir "TycoonAI-BIM-Platform.msi"
$ExeFile = Join-Path $WixOutputDir "TycoonAI-BIM-Platform-Setup.exe"

if (Test-Path $MsiFile) {
    $MsiSize = [math]::Round((Get-Item $MsiFile).Length / 1MB, 2)
    Write-Host "  MSI Installer: $MsiFile ($MsiSize MB)" -ForegroundColor White
}

if (Test-Path $ExeFile) {
    $ExeSize = [math]::Round((Get-Item $ExeFile).Length / 1MB, 2)
    Write-Host "  Setup Executable: $ExeFile ($ExeSize MB)" -ForegroundColor White
}

Write-Host ""
Write-Host "Installation Instructions:" -ForegroundColor Yellow
Write-Host "  1. Run TycoonAI-BIM-Platform-Setup.exe as Administrator" -ForegroundColor White
Write-Host "  2. Follow the installation wizard" -ForegroundColor White
Write-Host "  3. Start Revit and look for the Tycoon AI-BIM tab" -ForegroundColor White
Write-Host ""
Write-Host "Tycoon AI-BIM Platform is ready for deployment!" -ForegroundColor Blue
