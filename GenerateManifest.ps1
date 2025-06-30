# Generate repo.json manifest with script hashes and metadata
# Author: Tycoon AI-BIM Platform
# Version: 1.0.0

param(
    [string]$ScriptsPath = ".\scripts",
    [string]$OutputPath = ".\repo.json"
)

Write-Host "Generating GitHub Script Manifest..." -ForegroundColor Green

# Get current date and git info
$currentDate = Get-Date -Format "yyyy.MM.dd"
$gitCommit = ""
try {
    $gitCommit = git rev-parse --short HEAD 2>$null
    if ($LASTEXITCODE -ne 0) { $gitCommit = "unknown" }
} catch {
    $gitCommit = "unknown"
}

$buildVersion = "$currentDate-g$gitCommit"

# Initialize manifest structure
$manifest = @{
    version = $currentDate
    build = $buildVersion
    generated = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
    scripts = @{}
    templates = @{}
}

Write-Host "Scanning scripts directory: $ScriptsPath" -ForegroundColor Yellow

# Function to get file hash
function Get-FileHashString {
    param([string]$FilePath)
    $hash = Get-FileHash -Path $FilePath -Algorithm SHA256
    return $hash.Hash.Substring(0, 8).ToLower()
}

# Function to parse script metadata from header comments
function Parse-ScriptMetadata {
    param([string]$FilePath)
    
    $content = Get-Content -Path $FilePath -Raw
    $metadata = @{
        capability = "P1-Deterministic"
        description = "Script description"
        category = "Utilities"
        author = "Tycoon AI-BIM Platform"
        version = "1.0.0"
    }
    
    # Parse header comments
    $lines = $content -split "`n"
    foreach ($line in $lines) {
        $line = $line.Trim()
        if ($line.StartsWith("# Capability:")) {
            $metadata.capability = $line.Replace("# Capability:", "").Trim()
        }
        elseif ($line.StartsWith("# Description:")) {
            $metadata.description = $line.Replace("# Description:", "").Trim()
        }
        elseif ($line.StartsWith("# Category:")) {
            $metadata.category = $line.Replace("# Category:", "").Trim()
        }
        elseif ($line.StartsWith("# Author:")) {
            $metadata.author = $line.Replace("# Author:", "").Trim()
        }
        elseif ($line.StartsWith("# Version:")) {
            $metadata.version = $line.Replace("# Version:", "").Trim()
        }
        # Stop parsing after docstring starts
        elseif ($line.StartsWith('"""') -and $line.Length -gt 3) {
            break
        }
    }
    
    return $metadata
}

# Scan all Python scripts
$scriptFiles = Get-ChildItem -Path $ScriptsPath -Recurse -Filter "*.py"

foreach ($scriptFile in $scriptFiles) {
    $relativePath = $scriptFile.FullName.Replace((Resolve-Path $ScriptsPath).Path, "").TrimStart("\").Replace("\", "/")
    $scriptName = $scriptFile.Name
    
    Write-Host "  Processing: $scriptName" -ForegroundColor Cyan
    
    # Get file hash
    $fileHash = Get-FileHashString -FilePath $scriptFile.FullName
    
    # Parse metadata
    $metadata = Parse-ScriptMetadata -FilePath $scriptFile.FullName
    
    # Determine capabilities array based on capability string
    $capabilities = @()
    switch -Regex ($metadata.capability) {
        "P1.*Deterministic" { $capabilities = @("selection-optional", "deterministic") }
        "P2.*Analytic" { $capabilities = @("selection-required", "analytic", "ai-powered") }
        "P3.*Generative" { $capabilities = @("selection-required", "generative", "ai-powered", "advanced") }
        default { $capabilities = @("selection-optional") }
    }
    
    # Add to manifest
    $manifest.scripts[$scriptName] = @{
        hash = $fileHash
        path = $relativePath
        description = $metadata.description
        category = $metadata.category
        capability = $metadata.capability
        capabilities = $capabilities
        author = $metadata.author
        version = $metadata.version
        size = $scriptFile.Length
        lastModified = $scriptFile.LastWriteTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
    }
}

Write-Host "Found $($manifest.scripts.Count) scripts" -ForegroundColor Green

# Check for templates
$templatesPath = ".\templates"
if (Test-Path $templatesPath) {
    $templateFiles = Get-ChildItem -Path $templatesPath -Filter "*.json"
    foreach ($templateFile in $templateFiles) {
        $templateHash = Get-FileHashString -FilePath $templateFile.FullName
        $manifest.templates[$templateFile.Name] = $templateHash
        Write-Host "  Template: $($templateFile.Name)" -ForegroundColor Magenta
    }
}

# Convert to JSON and save
$jsonOutput = $manifest | ConvertTo-Json -Depth 10 -Compress:$false

# Pretty format the JSON (remove problematic regex)
# $jsonOutput = $jsonOutput -replace '(?m)^(\s*)"([^"]+)":\s*', '$1"$2": '

Set-Content -Path $OutputPath -Value $jsonOutput -Encoding UTF8

Write-Host "Manifest generated successfully!" -ForegroundColor Green
Write-Host "Output: $OutputPath" -ForegroundColor Yellow
Write-Host "Version: $($manifest.version)" -ForegroundColor Yellow
Write-Host "Build: $($manifest.build)" -ForegroundColor Yellow
Write-Host "Scripts: $($manifest.scripts.Count)" -ForegroundColor Yellow
Write-Host "Templates: $($manifest.templates.Count)" -ForegroundColor Yellow

# Display script summary by category
Write-Host "`nSCRIPT SUMMARY BY CATEGORY:" -ForegroundColor Green
$categories = $manifest.scripts.Values | Group-Object -Property category
foreach ($category in $categories) {
    Write-Host "  $($category.Name): $($category.Count) scripts" -ForegroundColor Cyan
}

# Display capability summary
Write-Host "`nCAPABILITY SUMMARY:" -ForegroundColor Green
$capabilityGroups = $manifest.scripts.Values | Group-Object -Property capability
foreach ($capGroup in $capabilityGroups) {
    Write-Host "  $($capGroup.Name): $($capGroup.Count) scripts" -ForegroundColor Cyan
}
