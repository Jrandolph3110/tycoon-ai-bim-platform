@echo off
REM Batch script to update manifest on build
REM Author: Tycoon AI-BIM Platform
REM Version: 1.0.0

echo Updating GitHub Script Manifest...

REM Run the PowerShell script to generate manifest
powershell -ExecutionPolicy Bypass -File ".\GenerateManifest.ps1"

if %ERRORLEVEL% EQU 0 (
    echo Manifest updated successfully!
) else (
    echo Failed to update manifest!
    exit /b 1
)

echo Done.
