# Package MCP Server for GitHub Release
param(
    [string]$OutputPath = "bin\Release"
)

Write-Host "Packaging MCP Server for GitHub Release..." -ForegroundColor Green

# Create output directory
$mcpPackagePath = Join-Path $OutputPath "mcp-server"
if (Test-Path $mcpPackagePath) {
    Remove-Item $mcpPackagePath -Recurse -Force
}
New-Item -ItemType Directory -Path $mcpPackagePath -Force | Out-Null

# Copy MCP server source files
$mcpSourcePath = "..\mcp-server"
$filesToCopy = @(
    "package.json",
    "tsconfig.json",
    "src\*",
    "dist\*"
)

foreach ($pattern in $filesToCopy) {
    $sourcePath = Join-Path $mcpSourcePath $pattern
    if (Test-Path $sourcePath) {
        $items = Get-Item $sourcePath
        foreach ($item in $items) {
            if ($item.PSIsContainer) {
                # Copy directory
                $destDir = Join-Path $mcpPackagePath $item.Name
                Copy-Item $item.FullName $destDir -Recurse -Force
            } else {
                # Copy file
                Copy-Item $item.FullName $mcpPackagePath -Force
            }
        }
    }
}

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

# Create ZIP file for GitHub release
$zipPath = Join-Path $OutputPath "mcp-server.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($mcpPackagePath, $zipPath)

Write-Host "MCP Server packaged successfully: $zipPath" -ForegroundColor Green

# Clean up temporary directory
Remove-Item $mcpPackagePath -Recurse -Force

Write-Host "MCP Server package ready for GitHub release!" -ForegroundColor Green
