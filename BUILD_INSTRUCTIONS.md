# 🚨 CRITICAL BUILD INSTRUCTIONS

## **ONLY ONE WAY TO BUILD - Use Build.ps1**

```powershell
cd src/installer
.\Build.ps1
```

**That's it. Nothing else.**

## ❌ **DO NOT USE THESE METHODS**

- `dotnet build` - Will fail with NuGet package errors
- `dotnet restore` - Doesn't work with old-style .csproj files  
- `msbuild` directly - Missing dependency resolution
- `nuget restore` - Wrong package manager for this project
- Building individual projects - Wrong build order

## ✅ **Why Build.ps1 Works**

- **Handles NuGet packages correctly** for .NET Framework 4.8 projects
- **Uses MSBuild restore** instead of dotnet CLI
- **Builds in correct order**: Setup Wizard → Revit Add-in → MCP Server → WiX Installer
- **Manages all dependencies** automatically
- **Produces final MSI** ready for deployment

## 🎯 **Build Output**

After successful build:
- **MSI Installer**: `src/installer/bin/Release/TycoonAI-BIM-Platform.msi`
- **Ready for deployment** - No additional steps needed

## 🔧 **If Build.ps1 Fails**

1. **Check prerequisites**: Visual Studio Build Tools, WiX Toolset, Node.js
2. **Run as Administrator** if needed
3. **Clean build**: Delete `bin` and `obj` folders, then retry
4. **Check error messages** - Build.ps1 provides detailed error reporting

---

**Remember: Build.ps1 is the ONLY supported build method. Don't get confused by other build approaches.**
