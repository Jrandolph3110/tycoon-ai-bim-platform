@echo off
REM Tycoon AI-BIM Platform Build Script (Batch Wrapper)
REM This is just a wrapper that calls Build.ps1
REM Build.ps1 is the ONLY supported build method

echo.
echo ========================================
echo   TYCOON AI-BIM PLATFORM BUILDER
echo   F.L. Crane ^& Sons Development Team
echo   (Wrapper for Build.ps1)
echo ========================================
echo.
echo This batch file just calls Build.ps1
echo Build.ps1 is the ONLY supported build method
echo.

REM Check if PowerShell is available
powershell -Command "Get-Host" >nul 2>&1
if errorlevel 1 (
    echo ERROR: PowerShell is required but not found.
    echo Please install PowerShell and try again.
    pause
    exit /b 1
)

REM Run the PowerShell build script
echo Running PowerShell build script...
echo.

powershell -ExecutionPolicy Bypass -File "%~dp0Build.ps1" %*

if errorlevel 1 (
    echo.
    echo BUILD FAILED!
    echo Check the error messages above for details.
    pause
    exit /b 1
)

echo.
echo BUILD COMPLETED SUCCESSFULLY!
echo.
echo The installer is ready for deployment.
echo Check the bin\Release directory for output files.
echo.
pause
