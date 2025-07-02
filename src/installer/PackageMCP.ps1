# Package MCP Server for Installer Distribution
param(
    [string]$OutputPath = "bin\Release"
)

Write-Host "Packaging MCP Server for Installer Distribution..." -ForegroundColor Green

# Create output directory
$mcpPackagePath = Join-Path $OutputPath "mcp-server"
if (Test-Path $mcpPackagePath) {
    Remove-Item $mcpPackagePath -Recurse -Force
}
New-Item -ItemType Directory -Path $mcpPackagePath -Force | Out-Null

# Copy complete MCP server directory structure
$mcpSourcePath = "..\mcp-server"
Write-Host "Copying complete MCP server from: $mcpSourcePath" -ForegroundColor Gray

# Copy everything except node_modules (will be installed by installer)
$excludePatterns = @("node_modules", ".git", "*.log", "*.tmp")

function Copy-DirectoryExcluding {
    param(
        [string]$Source,
        [string]$Destination,
        [string[]]$ExcludePatterns
    )

    if (-not (Test-Path $Source)) {
        Write-Warning "Source path does not exist: $Source"
        return
    }

    # Create destination directory
    if (-not (Test-Path $Destination)) {
        New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    }

    # Get all items in source
    $items = Get-ChildItem -Path $Source -Force

    foreach ($item in $items) {
        $shouldExclude = $false
        foreach ($pattern in $ExcludePatterns) {
            if ($item.Name -like $pattern) {
                $shouldExclude = $true
                Write-Host "  Excluding: $($item.Name)" -ForegroundColor DarkGray
                break
            }
        }

        if (-not $shouldExclude) {
            $destPath = Join-Path $Destination $item.Name
            if ($item.PSIsContainer) {
                Copy-DirectoryExcluding -Source $item.FullName -Destination $destPath -ExcludePatterns $ExcludePatterns
            } else {
                Copy-Item $item.FullName $destPath -Force
                Write-Host "  Copied: $($item.Name)" -ForegroundColor DarkGray
            }
        }
    }
}

Copy-DirectoryExcluding -Source $mcpSourcePath -Destination $mcpPackagePath -ExcludePatterns $excludePatterns

# Create a simple package.json if it doesn't exist
$packageJsonPath = Join-Path $mcpPackagePath "package.json"
if (-not (Test-Path $packageJsonPath)) {
    $packageJson = @{
        name = "tycoon-ai-bim-server"
        version = "1.0.4.0"
        description = "Tycoon AI-BIM MCP Server for Revit integration"
        main = "dist/index.js"
        scripts = @{
            start = "node dist/index.js"
            build = "tsc"
        }
        dependencies = @{
            "@modelcontextprotocol/sdk" = "^0.6.0"
            "ws" = "^8.18.0"
        }
    } | ConvertTo-Json -Depth 10

    Set-Content -Path $packageJsonPath -Value $packageJson
}

# Create ZIP file for installer distribution
$zipPath = Join-Path $OutputPath "mcp-server.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host "Creating ZIP archive: $zipPath" -ForegroundColor Gray
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($mcpPackagePath, $zipPath)

Write-Host "MCP Server packaged successfully: $zipPath" -ForegroundColor Green

# Verify ZIP contents
Write-Host "Verifying ZIP contents..." -ForegroundColor Gray
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
$entryCount = $zip.Entries.Count
$zip.Dispose()
Write-Host "  ZIP contains $entryCount files/directories" -ForegroundColor Gray

# Clean up temporary directory
Remove-Item $mcpPackagePath -Recurse -Force

Write-Host "MCP Server package ready for installer distribution!" -ForegroundColor Green
