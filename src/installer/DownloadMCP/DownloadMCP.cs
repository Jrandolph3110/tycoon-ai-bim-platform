using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Microsoft.Win32;
using System.Net;

namespace TycoonInstaller
{
    class InstallMCP
    {
        static int Main(string[] args)
        {
            // Use a path that is almost always writable, even by the SYSTEM account.
            string canaryLogPath = Path.Combine(Path.GetTempPath(), "DownloadMCP_Execution.log");

            try
            {
                // First action: prove the process started and can write a file.
                File.AppendAllText(canaryLogPath, $"[{DateTime.UtcNow:O}] Main method started. Current Directory: {Environment.CurrentDirectory}\n");

                Console.WriteLine("Tycoon AI-BIM Platform: Installing MCP Server...");

                // Get AppData path from installer
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string tycoonPath = Path.Combine(appDataPath, "Tycoon");
                string mcpServerPath = Path.Combine(tycoonPath, "mcp-server");

                Console.WriteLine($"Target directory: {mcpServerPath}");

                // Create directories
                Directory.CreateDirectory(mcpServerPath);

                // During MSI installation, look for ZIP file in the target directory first
                string mcpServerZipPath = Path.Combine(Path.GetDirectoryName(mcpServerPath), "mcp-server.zip");

                // If not found there, try the installer directory (for manual execution)
                if (!File.Exists(mcpServerZipPath))
                {
                    string installerDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    mcpServerZipPath = Path.Combine(installerDir, "mcp-server.zip");
                }

                Console.WriteLine($"Looking for MCP server ZIP: {mcpServerZipPath}");

                if (File.Exists(mcpServerZipPath))
                {
                    Console.WriteLine("Extracting bundled MCP server ZIP...");
                    ExtractMCPServerZip(mcpServerZipPath, mcpServerPath);
                }
                else
                {
                    Console.WriteLine("Bundled MCP server ZIP not found, creating minimal setup...");
                    CreateMinimalMCPServer(mcpServerPath);
                }

                // Install Node.js dependencies (production only, no build needed)
                try
                {
                    InstallNodeDependencies(mcpServerPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not install Node.js dependencies: {ex.Message}");
                    Console.WriteLine("The MCP server files have been extracted, but Node.js dependencies are not installed.");
                    Console.WriteLine("Please install Node.js manually and run 'npm install --omit=dev' in the MCP server directory.");
                }

                Console.WriteLine("MCP Server installation completed successfully!");
                File.AppendAllText(canaryLogPath, $"[{DateTime.UtcNow:O}] Main method completed successfully.\n");
                return 0;
            }
            catch (Exception ex)
            {
                // If anything fails, log the full exception here.
                File.AppendAllText(canaryLogPath, $"[{DateTime.UtcNow:O}] CRITICAL EXCEPTION: {ex.ToString()}\n");
                Console.WriteLine($"Error installing MCP server: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                // Return 1603 (ERROR_INSTALL_FAILURE) to signal failure to MSI.
                return 1603;
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

        private static void ExtractMCPServerZip(string zipPath, string extractPath)
        {
            try
            {
                Console.WriteLine($"Extracting {zipPath} to {extractPath}...");

                // Ensure target directory exists
                Directory.CreateDirectory(extractPath);

                // Extract ZIP file (try to delete existing directory for clean extraction)
                if (Directory.Exists(extractPath))
                {
                    try
                    {
                        Directory.Delete(extractPath, true);
                        Console.WriteLine("Cleaned existing MCP server directory");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Could not clean existing directory (may be in use): {ex.Message}");
                        Console.WriteLine("Attempting to extract over existing files...");
                    }
                }

                Directory.CreateDirectory(extractPath);

                ZipFile.ExtractToDirectory(zipPath, extractPath);

                Console.WriteLine("MCP server ZIP extracted successfully!");

                // Fix nested directory structure - move files from subdirectories to proper locations
                FixDirectoryStructure(extractPath);

                // Verify extraction by checking for key files
                string indexJsPath = Path.Combine(extractPath, "index.js");
                string coreDir = Path.Combine(extractPath, "core");

                if (File.Exists(indexJsPath) && Directory.Exists(coreDir))
                {
                    Console.WriteLine("Extraction verification passed - key files found");
                }
                else
                {
                    Console.WriteLine("Warning: Some expected files may be missing after extraction");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting MCP server ZIP: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private static void FixDirectoryStructure(string extractPath)
        {
            try
            {
                // Fix core/core -> core
                string nestedCoreDir = Path.Combine(extractPath, "core", "core");
                string targetCoreDir = Path.Combine(extractPath, "core");

                if (Directory.Exists(nestedCoreDir))
                {
                    Console.WriteLine("Fixing nested core directory structure...");
                    MoveDirectoryContents(nestedCoreDir, targetCoreDir);
                    Directory.Delete(nestedCoreDir, true);
                }

                // Apply similar fixes for other nested directories if needed
                string[] dirsToFix = { "flc", "models", "revit", "utils" };
                foreach (string dirName in dirsToFix)
                {
                    string nestedDir = Path.Combine(extractPath, dirName, dirName);
                    string targetDir = Path.Combine(extractPath, dirName);

                    if (Directory.Exists(nestedDir))
                    {
                        Console.WriteLine($"Fixing nested {dirName} directory structure...");
                        MoveDirectoryContents(nestedDir, targetDir);
                        Directory.Delete(nestedDir, true);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not fix directory structure: {ex.Message}");
            }
        }

        private static void MoveDirectoryContents(string sourceDir, string targetDir)
        {
            // Move all files from source to target
            foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(file);
                string targetPath = Path.Combine(targetDir, fileName);
                File.Move(file, targetPath);
            }

            // Move all subdirectories from source to target
            foreach (string subDir in Directory.GetDirectories(sourceDir, "*", SearchOption.TopDirectoryOnly))
            {
                string dirName = Path.GetFileName(subDir);
                string targetSubDir = Path.Combine(targetDir, dirName);

                if (!Directory.Exists(targetSubDir))
                {
                    Directory.Move(subDir, targetSubDir);
                }
                else
                {
                    // If target subdirectory exists, merge contents
                    MoveDirectoryContents(subDir, targetSubDir);
                    Directory.Delete(subDir, true);
                }
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
        

        
        private static void InstallNodeDependencies(string mcpServerPath)
        {
            string canaryLogPath = Path.Combine(Path.GetTempPath(), "DownloadMCP_Execution.log");
            File.AppendAllText(canaryLogPath, $"[{DateTime.UtcNow:O}] InstallNodeDependencies method started for path: {mcpServerPath}\n");

            try
            {
                // 1. Find the path to npm.cmd using robust search strategy
                string npmPath = FindNpmPath();

                Console.WriteLine($"Working Directory: {mcpServerPath}");
                Console.WriteLine("Installing npm dependencies...");

                var npmProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        // 2. Use the full path and execute via cmd.exe
                        //    This is the most reliable way to run batch files.
                        FileName = "cmd.exe",
                        Arguments = $"/C \"\"{npmPath}\" install --omit=dev\"",
                        WorkingDirectory = mcpServerPath,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                npmProcess.Start();
                string output = npmProcess.StandardOutput.ReadToEnd();
                string error = npmProcess.StandardError.ReadToEnd();
                npmProcess.WaitForExit();

                // 3. Improved Logging
                string logContent = $"NPM Install Log:\n\nExit Code: {npmProcess.ExitCode}\n\nOutput:\n{output}\n\nError:\n{error}";
                File.WriteAllText(Path.Combine(mcpServerPath, "npm_install.log"), logContent);

                if (npmProcess.ExitCode != 0)
                {
                    Console.WriteLine($"Initial npm install failed with exit code {npmProcess.ExitCode}. Attempting SQLite3 rebuild...");

                    // Try to rebuild SQLite3 specifically
                    RebuildSqlite3(mcpServerPath, npmPath);
                }
                else
                {
                    Console.WriteLine("npm dependencies installed successfully.");

                    // Verify SQLite3 bindings are available
                    VerifySqlite3Bindings(mcpServerPath, npmPath);
                }
            }
            catch (Exception ex)
            {
                // Catch-all for any other exception, including the Process.Start failure
                File.WriteAllText(Path.Combine(mcpServerPath, "custom_action_error.log"), $"An exception occurred: {ex.ToString()}");
                // Re-throw to ensure the MSI installer knows it failed and can roll back.
                throw;
            }
        }

        /// <summary>
        /// Finds the full path to npm.cmd by searching multiple common locations.
        /// This is necessary because the MSI custom action runs as LocalSystem, which has a minimal PATH.
        /// The search order is from most-to-least reliable:
        /// 1. Windows Registry (64-bit and 32-bit views) - HKEY_LOCAL_MACHINE\SOFTWARE\Node.js\InstallPath
        /// 2. NODE_HOME environment variable
        /// 3. Standard Program Files and Chocolatey installation folders
        /// 4. System-wide PATH environment variable
        /// </summary>
        private static string FindNpmPath()
        {
            // A list of named search strategies in order of reliability
            var searchStrategies = new List<Tuple<string, Func<string>>>
            {
                Tuple.Create<string, Func<string>>("Registry (64-bit)", () => FindInRegistry(RegistryView.Registry64)),
                Tuple.Create<string, Func<string>>("Registry (32-bit)", () => FindInRegistry(RegistryView.Registry32)),
                Tuple.Create<string, Func<string>>("NODE_HOME Environment Variable", () => FindInEnvVar("NODE_HOME")),
                Tuple.Create<string, Func<string>>("Program Files Path", () => FindInStandardPath(Environment.SpecialFolder.ProgramFiles, @"nodejs\npm.cmd")),
                Tuple.Create<string, Func<string>>("Program Files (x86) Path", () => FindInStandardPath(Environment.SpecialFolder.ProgramFilesX86, @"nodejs\npm.cmd")),
                Tuple.Create<string, Func<string>>("Chocolatey Path", () => FindInChocolateyPath()),
                Tuple.Create<string, Func<string>>("System PATH", () => SearchSystemPath("npm.cmd"))
            };

            foreach (var strategy in searchStrategies)
            {
                string path = strategy.Item2.Invoke();
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    // Log both the method and the result for clear diagnostics
                    Console.WriteLine($"Found npm via {strategy.Item1} at: {path}");
                    return path;
                }
            }

            // Enhanced error message with clear instructions for fresh Windows installations
            string errorMessage = @"
❌ Node.js is required but not found on this system.

INSTALLATION REQUIREMENTS:
1. Download and install Node.js LTS from: https://nodejs.org/
2. Restart this installer after Node.js installation
3. Ensure 'Add to PATH' option is selected during Node.js installation

TECHNICAL DETAILS:
The Tycoon AI-BIM Platform requires Node.js to run the MCP server.
npm.cmd could not be found in registry, environment variables, or standard paths.

For enterprise deployments, consider installing Node.js via:
- Official MSI installer: https://nodejs.org/dist/latest/
- Chocolatey: choco install nodejs
- Winget: winget install OpenJS.NodeJS";

            throw new FileNotFoundException(errorMessage);
        }

        /// <summary>
        /// Search for npm.cmd in Windows registry
        /// </summary>
        private static string FindInRegistry(RegistryView view)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (var nodeKey = baseKey.OpenSubKey(@"SOFTWARE\Node.js"))
                {
                    if (nodeKey?.GetValue("InstallPath") is string installPath)
                    {
                        return Path.Combine(installPath, "npm.cmd");
                    }
                }
            }
            catch { /* Registry access might fail */ }
            return null;
        }

        /// <summary>
        /// Search for npm.cmd using environment variable
        /// </summary>
        private static string FindInEnvVar(string envVarName)
        {
            string envPath = Environment.GetEnvironmentVariable(envVarName);
            if (!string.IsNullOrEmpty(envPath))
            {
                return Path.Combine(envPath, "npm.cmd");
            }
            return null;
        }

        /// <summary>
        /// Search for npm.cmd in standard installation paths
        /// </summary>
        private static string FindInStandardPath(Environment.SpecialFolder folder, string relativePath)
        {
            string basePath = Environment.GetFolderPath(folder);
            if (!string.IsNullOrEmpty(basePath))
            {
                return Path.Combine(basePath, relativePath);
            }
            return null;
        }

        /// <summary>
        /// Search for npm.cmd in Chocolatey installation path
        /// </summary>
        private static string FindInChocolateyPath()
        {
            string programData = Environment.GetEnvironmentVariable("ProgramData");
            if (string.IsNullOrEmpty(programData)) return null;

            // The canonical path for the tool within the package
            return Path.Combine(programData, @"chocolatey\lib\nodejs\tools\npm.cmd");
        }

        /// <summary>
        /// Search for npm.cmd in system PATH environment variable
        /// </summary>
        private static string SearchSystemPath(string fileName)
        {
            try
            {
                string systemPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);
                if (string.IsNullOrEmpty(systemPath)) return null;

                string[] pathDirectories = systemPath.Split(';');
                foreach (string directory in pathDirectories)
                {
                    if (!string.IsNullOrEmpty(directory))
                    {
                        string fullPath = Path.Combine(directory.Trim(), fileName);
                        if (File.Exists(fullPath))
                        {
                            return fullPath;
                        }
                    }
                }
            }
            catch { /* PATH parsing might fail */ }
            return null;
        }

        private static void RebuildSqlite3(string mcpServerPath, string npmPath)
        {
            try
            {
                Console.WriteLine("Attempting to rebuild SQLite3 native bindings...");

                var rebuildProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/C \"\"{npmPath}\" rebuild sqlite3\"",
                        WorkingDirectory = mcpServerPath,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                rebuildProcess.Start();
                string output = rebuildProcess.StandardOutput.ReadToEnd();
                string error = rebuildProcess.StandardError.ReadToEnd();
                rebuildProcess.WaitForExit();

                string logContent = $"SQLite3 Rebuild Log:\n\nExit Code: {rebuildProcess.ExitCode}\n\nOutput:\n{output}\n\nError:\n{error}";
                File.WriteAllText(Path.Combine(mcpServerPath, "sqlite3_rebuild.log"), logContent);

                if (rebuildProcess.ExitCode == 0)
                {
                    Console.WriteLine("SQLite3 rebuild completed successfully.");
                }
                else
                {
                    Console.WriteLine($"SQLite3 rebuild failed with exit code {rebuildProcess.ExitCode}. See sqlite3_rebuild.log for details.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during SQLite3 rebuild: {ex.Message}");
            }
        }

        private static void VerifySqlite3Bindings(string mcpServerPath, string npmPath)
        {
            try
            {
                Console.WriteLine("Verifying SQLite3 native bindings...");

                // Check if SQLite3 .node file exists
                string[] possiblePaths = {
                    Path.Combine(mcpServerPath, "node_modules", "sqlite3", "build", "Release", "node_sqlite3.node"),
                    Path.Combine(mcpServerPath, "node_modules", "sqlite3", "build", "Debug", "node_sqlite3.node")
                };

                bool bindingsFound = false;
                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        Console.WriteLine($"SQLite3 bindings found at: {path}");
                        bindingsFound = true;
                        break;
                    }
                }

                if (!bindingsFound)
                {
                    Console.WriteLine("SQLite3 bindings not found. Attempting rebuild...");
                    RebuildSqlite3(mcpServerPath, npmPath);
                }
                else
                {
                    Console.WriteLine("SQLite3 bindings verification passed.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during SQLite3 verification: {ex.Message}");
            }
        }
    }
}
