using System;
using System.IO;
using System.IO.Compression;
using System.Net;

namespace TycoonInstaller
{
    class InstallMCP
    {
        static int Main(string[] args)
        {
            try
            {
                Console.WriteLine("Tycoon AI-BIM Platform: Installing MCP Server...");

                // Get AppData path from installer
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string tycoonPath = Path.Combine(appDataPath, "Tycoon");
                string mcpServerPath = Path.Combine(tycoonPath, "mcp-server");

                Console.WriteLine($"Target directory: {mcpServerPath}");

                // Create directories
                Directory.CreateDirectory(mcpServerPath);

                // Get installer directory (where bundled MCP server files are)
                string installerDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string bundledMcpPath = Path.Combine(installerDir, "mcp-server");

                Console.WriteLine($"Source directory: {bundledMcpPath}");

                if (Directory.Exists(bundledMcpPath))
                {
                    Console.WriteLine("Copying bundled MCP server files...");
                    CopyDirectory(bundledMcpPath, mcpServerPath);
                }
                else
                {
                    Console.WriteLine("Bundled MCP server not found, creating minimal setup...");
                    CreateMinimalMCPServer(mcpServerPath);
                }

                // Create package.json if it doesn't exist
                CreatePackageJsonIfNeeded(mcpServerPath);

                // Install Node.js dependencies
                InstallNodeDependencies(mcpServerPath);

                Console.WriteLine("MCP Server installation completed successfully!");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error installing MCP server: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return 1;
            }
        }
        
        private static void CreateMinimalMCPServer(string mcpServerPath)
        {
            // Create minimal MCP server structure if bundled files not found
            Directory.CreateDirectory(Path.Combine(mcpServerPath, "dist"));
            Directory.CreateDirectory(Path.Combine(mcpServerPath, "src"));

            // Create a basic index.js file
            string indexJsPath = Path.Combine(mcpServerPath, "dist", "index.js");
            string basicMcpServer = @"#!/usr/bin/env node
// Tycoon AI-BIM MCP Server
// This is a placeholder - full server should be installed manually

const { Server } = require('@modelcontextprotocol/sdk/server/index.js');
const { StdioServerTransport } = require('@modelcontextprotocol/sdk/server/stdio.js');

const server = new Server(
  {
    name: 'tycoon-ai-bim',
    version: '1.0.6.0',
  },
  {
    capabilities: {
      tools: {},
    },
  }
);

async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error('Tycoon AI-BIM MCP Server running');
}

main().catch(console.error);
";
            File.WriteAllText(indexJsPath, basicMcpServer);
        }
        
        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = file.Substring(sourceDir.Length + 1);
                string targetFile = Path.Combine(targetDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                File.Copy(file, targetFile, true);
            }
        }
        
        private static void CreatePackageJsonIfNeeded(string mcpServerPath)
        {
            string packageJsonPath = Path.Combine(mcpServerPath, "package.json");
            if (!File.Exists(packageJsonPath))
            {
                string packageJson = @"{
  ""name"": ""tycoon-ai-bim-server"",
  ""version"": ""1.0.4.0"",
  ""description"": ""Tycoon AI-BIM MCP Server"",
  ""main"": ""dist/index.js"",
  ""scripts"": {
    ""start"": ""node dist/index.js"",
    ""build"": ""tsc""
  },
  ""dependencies"": {
    ""@modelcontextprotocol/sdk"": ""^0.6.0"",
    ""ws"": ""^8.18.0""
  }
}";
                File.WriteAllText(packageJsonPath, packageJson);
            }
        }
        
        private static void InstallNodeDependencies(string mcpServerPath)
        {
            try
            {
                Console.WriteLine("Installing Node.js dependencies...");

                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "npm",
                    Arguments = "install",
                    WorkingDirectory = mcpServerPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = System.Diagnostics.Process.Start(processInfo))
                {
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        Console.WriteLine("Warning: npm install failed, but continuing...");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not install Node.js dependencies: {ex.Message}");
                Console.WriteLine("User may need to run 'npm install' manually in the MCP server directory.");
            }
        }
    }
}
