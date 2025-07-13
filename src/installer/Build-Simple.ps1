# Simple Tycoon AI-BIM Platform Build Script
# Clean build without protocols or fancy features

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [Parameter(Mandatory=$false)]
    [switch]$BuildBootstrapper
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionFile = Join-Path $ScriptDir "TycoonInstaller.sln"

Write-Host "Building Tycoon AI-BIM Platform Installer" -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Gray
Write-Host "Solution: $SolutionFile" -ForegroundColor Gray

# Find MSBuild
$MSBuildPath = ""
$MSBuildLocations = @(
    "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files (x86)\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files (x86)\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files (x86)\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
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

# Check for WiX Toolset
$WixPath = "${env:ProgramFiles(x86)}\WiX Toolset v3.11\bin"
if (-not (Test-Path $WixPath)) {
    $WixPath = "${env:ProgramFiles}\WiX Toolset v3.11\bin"
    if (-not (Test-Path $WixPath)) {
        Write-Error "WiX Toolset v3.11 not found. Please install from https://wixtoolset.org/"
        exit 1
    }
}

Write-Host "Prerequisites check passed" -ForegroundColor Green

# Build Revit Add-in only (skip WiX projects)
$RevitAddinProject = Join-Path $ScriptDir "..\revit-addin\TycoonRevitAddin.csproj"
Write-Host "Building Revit Add-in..." -ForegroundColor Yellow
& "$MSBuildPath" "$RevitAddinProject" /p:Configuration=$Configuration /p:Platform="AnyCPU" /verbosity:minimal

if ($LASTEXITCODE -ne 0) {
    Write-Error "Revit Add-in build failed"
    exit $LASTEXITCODE
}

# Build WiX installer manually
Write-Host "Building WiX Installer..." -ForegroundColor Yellow

$CandlePath = Join-Path $WixPath "candle.exe"
$LightPath = Join-Path $WixPath "light.exe"
$OutputDir = Join-Path $ScriptDir "bin\$Configuration"

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

# Compile WiX source files
$WixSources = @("Product.wxs", "RevitVersions.wxs", "UI\CustomUI.wxs")
$WixObjects = @()
$RevitAddinDll = Join-Path $ScriptDir "..\revit-addin\bin\Release\TycoonRevitAddin.dll"
$RevitAddinDir = Join-Path $ScriptDir "..\revit-addin\bin\Release\"

foreach ($source in $WixSources) {
    $sourcePath = Join-Path $ScriptDir $source
    $objectPath = Join-Path $OutputDir ([System.IO.Path]::GetFileNameWithoutExtension($source) + ".wixobj")
    $WixObjects += $objectPath

    Write-Host "Compiling $source..." -ForegroundColor Gray
    & "$CandlePath" -out "$objectPath" "$sourcePath" "-dTycoonRevitAddin.TargetPath=$RevitAddinDll" "-dTycoonRevitAddin.TargetDir=$RevitAddinDir"

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to compile $source"
        exit $LASTEXITCODE
    }
}

# Link WiX objects
$MsiPath = Join-Path $OutputDir "TycoonAI-BIM-Platform.msi"
Write-Host "Linking MSI..." -ForegroundColor Gray

$LightArgs = @("-out", "`"$MsiPath`"")
foreach ($obj in $WixObjects) {
    $LightArgs += "`"$obj`""
}
$LightArgs += @("-ext", "WixUIExtension", "-ext", "WixUtilExtension", "-ext", "WixNetFxExtension", "-sval")

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

Write-Host "Build completed successfully!" -ForegroundColor Green

# Build bootstrapper if requested
if ($BuildBootstrapper) {
    Write-Host "Building bootstrapper..." -ForegroundColor Blue
    
    $CandlePath = Join-Path $WixPath "candle.exe"
    $LightPath = Join-Path $WixPath "light.exe"
    $OutputDir = Join-Path $ScriptDir "bin\$Configuration"
    
    if (!(Test-Path $CandlePath) -or !(Test-Path $LightPath)) {
        Write-Error "WiX tools not found"
        exit 1
    }
    
    # Create dummy Node.js MSI
    $DummyNodeMsi = Join-Path $ScriptDir "node-v20.11.0-x64.msi"
    "Dummy" | Out-File -FilePath $DummyNodeMsi -Encoding ASCII
    
    try {
        # Compile Bundle.wxs
        $BundleWxs = Join-Path $ScriptDir "Bundle.wxs"
        $BundleObj = Join-Path $ScriptDir "obj\Release\Bundle.wixobj"
        $MSIPath = Join-Path $OutputDir "TycoonAI-BIM-Platform.msi"
        
        $ObjDir = Join-Path $ScriptDir "obj\Release"
        if (!(Test-Path $ObjDir)) {
            New-Item -ItemType Directory -Path $ObjDir -Force | Out-Null
        }
        
        Write-Host "Compiling Bundle.wxs..." -ForegroundColor Gray
        & $CandlePath -ext WixBalExtension -ext WixUtilExtension "-dTycoonInstaller.TargetPath=$MSIPath" $BundleWxs -o $BundleObj
        
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Bundle compilation failed"
            exit $LASTEXITCODE
        }
        
        # Link bootstrapper
        $BootstrapperOutput = Join-Path $OutputDir "TycoonAI-BIM-Platform-Setup.exe"
        Write-Host "Linking bootstrapper..." -ForegroundColor Gray
        & $LightPath -ext WixBalExtension -ext WixUtilExtension $BundleObj -o $BootstrapperOutput
        
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Bootstrapper linking failed"
            exit $LASTEXITCODE
        }
        
        Write-Host "Bootstrapper build completed" -ForegroundColor Green
        
        if (Test-Path $BootstrapperOutput) {
            $BootstrapperSize = [math]::Round((Get-Item $BootstrapperOutput).Length / 1MB, 2)
            Write-Host "Bootstrapper: $BootstrapperOutput ($BootstrapperSize MB)" -ForegroundColor Green
        }
    }
    finally {
        if (Test-Path $DummyNodeMsi) {
            Remove-Item $DummyNodeMsi -Force
        }
    }
} else {
    Write-Host "Skipping bootstrapper build (use -BuildBootstrapper to build)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Build completed successfully!" -ForegroundColor Green
