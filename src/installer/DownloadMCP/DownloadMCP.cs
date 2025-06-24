using System;
using System.IO;
using System.IO.Compression;
using System.Net;

namespace TycoonInstaller
{
    class DownloadMCP
    {
        static int Main(string[] args)
        {
            try
            {
                Console.WriteLine("Tycoon AI-BIM Platform: Downloading MCP Server...");
                
                // Get AppData path from installer
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string tycoonPath = Path.Combine(appDataPath, "Tycoon");
                string mcpServerPath = Path.Combine(tycoonPath, "mcp-server");
                
                Console.WriteLine($"Target directory: {mcpServerPath}");
                
                // Create directories
                Directory.CreateDirectory(mcpServerPath);
                
                // Download latest MCP server from GitHub releases
                string downloadUrl = "https://github.com/Jrandolph3110/tycoon-ai-bim-platform/releases/latest/download/mcp-server.zip";
                string tempZipPath = Path.Combine(Path.GetTempPath(), "tycoon-mcp-server.zip");
                
                Console.WriteLine("Downloading MCP server from GitHub...");
                DownloadFile(downloadUrl, tempZipPath);

                Console.WriteLine("Extracting MCP server...");
                ZipFile.ExtractToDirectory(tempZipPath, mcpServerPath);

                // Clean up temp file
                File.Delete(tempZipPath);

                // Create package.json if it doesn't exist
                CreatePackageJsonIfNeeded(mcpServerPath);

                // Install Node.js dependencies
                InstallNodeDependencies(mcpServerPath);
                
                Console.WriteLine("MCP Server installation completed successfully!");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading MCP server: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return 1;
            }
        }
        
        private static void DownloadFile(string url, string filePath)
        {
            try
            {
                using (var webClient = new WebClient())
                {
                    webClient.DownloadFile(url, filePath);
                }
            }
            catch (WebException)
            {
                // Fallback: Copy from local installation if GitHub download fails
                Console.WriteLine("GitHub download failed, using local MCP server...");
                CopyLocalMCPServer(Path.GetDirectoryName(filePath));
            }
        }
        
        private static void CopyLocalMCPServer(string targetPath)
        {
            // This is a fallback - copy from the installer's embedded MCP server
            string installerDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string localMcpPath = Path.Combine(installerDir, "mcp-server");

            if (Directory.Exists(localMcpPath))
            {
                CopyDirectory(localMcpPath, targetPath);
            }
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
