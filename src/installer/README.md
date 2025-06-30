# Tycoon AI-BIM Platform Installer

Professional WiX MSI installer for the revolutionary AI-powered construction automation platform.

## ⚠️ **CRITICAL BUILD INSTRUCTIONS**

**ONLY USE Build.ps1 FOR ALL BUILDS**
- ✅ **Correct**: `.\Build.ps1` (in src/installer directory)
- ❌ **Wrong**: `dotnet build`, `msbuild`, `nuget restore`, or any manual commands
- ❌ **Wrong**: Building individual projects separately

**Why Build.ps1 is required:**
- Handles NuGet package restoration for old-style .csproj files
- Builds components in correct dependency order
- Manages MSBuild vs dotnet CLI compatibility issues
- Produces final MSI installer correctly

## 🏗️ **What This Installer Provides**

### **Complete Professional Installation**
- ✅ **MSI Package** - Industry-standard Windows installer
- ✅ **Bootstrapper** - Handles prerequisites automatically  
- ✅ **Multi-Revit Support** - Installs for Revit 2022-2025
- ✅ **Dependency Management** - Bundles all required libraries
- ✅ **Automatic Detection** - Finds installed Revit versions
- ✅ **Clean Uninstall** - Complete removal capability

### **Enterprise-Ready Features**
- ✅ **Group Policy Compatible** - IT department friendly
- ✅ **Silent Installation** - Command-line deployment
- ✅ **Digital Signing Ready** - Security and trust
- ✅ **Version Upgrades** - Handles updates gracefully
- ✅ **Autodesk App Store Ready** - Meets marketplace requirements

## 🚀 **Quick Start**

### **For End Users**
1. **Download** the installer: `TycoonAI-BIM-Platform-Setup.exe`
2. **Run as Administrator** (right-click → "Run as administrator")
3. **Follow the wizard** - Select Revit versions to install
4. **Launch Revit** - Look for "Tycoon AI-BIM" tab in ribbon

### **For Developers**
1. **Build the installer**: Run `Build.ps1` (ONLY method supported)
2. **Test installation**: Install on clean test machine
3. **Verify functionality**: Test Revit integration
4. **Deploy**: Distribute the setup executable

## 🛠️ **Building the Installer**

### **Prerequisites**
- **Visual Studio 2019/2022** with C# support
- **WiX Toolset v3.11** or later ([Download](https://wixtoolset.org/))
- **.NET Framework 4.8** SDK
- **Node.js** (for MCP server build)

### **Build Commands**

#### **ONLY Build Method - Use Build.ps1**
```powershell
# Standard release build (RECOMMENDED)
.\Build.ps1
```

**⚠️ IMPORTANT: Build.ps1 is the ONLY supported build method.**
- ✅ **Use Build.ps1** - Handles all dependencies, NuGet packages, and build order correctly
- ❌ **Do NOT use** `dotnet build`, `msbuild`, or `nuget restore` directly
- ❌ **Do NOT use** manual build commands - they will fail with package dependency issues

The Build.ps1 script:
- Automatically restores NuGet packages using MSBuild restore
- Builds all components in the correct order
- Handles old-style .csproj dependencies properly
- Produces the final MSI installer ready for deployment

## 📦 **Output Files**

After building, you'll find these files in `bin\Release\`:

### **Primary Installer**
- **`TycoonAI-BIM-Platform-Setup.exe`** - Main installer (recommended)
  - Includes all prerequisites
  - Handles .NET Framework installation
  - Professional branded UI

### **MSI Package**
- **`TycoonAI-BIM-Platform.msi`** - Core MSI package
  - For enterprise deployment
  - Requires prerequisites pre-installed
  - Group Policy compatible

## 🎯 **Installation Features**

### **Automatic Revit Detection**
The installer automatically detects installed Revit versions:
- ✅ **Revit 2022** - Supported
- ✅ **Revit 2023** - Supported  
- ✅ **Revit 2024** - Supported
- ✅ **Revit 2025** - Supported

### **Smart File Placement**
- **Add-in files** → `%APPDATA%\Autodesk\Revit\Addins\{Version}\`
- **Program files** → `%ProgramFiles%\F.L. Crane & Sons\Tycoon AI-BIM Platform\`
- **Dependencies** → Bundled with each Revit version

### **Dependency Handling**
- **Newtonsoft.Json** - JSON serialization
- **WebSocketSharp** - Real-time communication
- **.NET Framework 4.8** - Runtime requirement
- **Visual C++ Redistributable** - Native dependencies

## 🔧 **Customization**

### **Branding**
Replace these files to customize appearance:
- `Resources\TycoonIcon.ico` - Application icon
- `Resources\TycoonBanner.bmp` - Installer banner (493×58)
- `Resources\TycoonDialog.bmp` - Dialog background (493×312)
- `Resources\License.rtf` - License agreement

### **Version Information**
Update version numbers in:
- `Product.wxs` - Product version
- `Bundle.wxs` - Bundle version
- `..\revit-addin\Properties\AssemblyInfo.cs` - Assembly version

### **Company Information**
Modify company details in:
- `Product.wxs` - Manufacturer, URLs
- `Bundle.wxs` - Bundle information
- `Resources\License.rtf` - License text

## 🚀 **Deployment Options**

### **Option 1: Direct Distribution**
- Send `TycoonAI-BIM-Platform-Setup.exe` to users
- Users run installer with admin rights
- Automatic prerequisite installation

### **Option 2: Enterprise Deployment**
- Use `TycoonAI-BIM-Platform.msi` with Group Policy
- Pre-install .NET Framework 4.8
- Silent installation: `msiexec /i TycoonAI-BIM-Platform.msi /quiet`

### **Option 3: Autodesk App Store**
- Submit `TycoonAI-BIM-Platform-Setup.exe`
- Meets all Autodesk marketplace requirements
- Professional installer experience

## 🔍 **Testing & Validation**

### **Test Scenarios**
1. **Clean Windows 10/11** - Fresh installation
2. **Multiple Revit Versions** - 2022, 2023, 2024, 2025
3. **Upgrade Installation** - Install over previous version
4. **Uninstall Process** - Complete removal verification
5. **Silent Installation** - Command-line deployment

### **Validation Checklist**
- ✅ Installer runs without errors
- ✅ All Revit versions detected correctly
- ✅ Add-in appears in Revit ribbon
- ✅ Connection to MCP server works
- ✅ Uninstall removes all files
- ✅ No registry orphans left behind

## 📋 **Troubleshooting**

### **Build Issues**
- **WiX not found**: Install WiX Toolset v3.11+
- **MSBuild errors**: Install Visual Studio Build Tools
- **Node.js issues**: Ensure Node.js is in PATH

### **Installation Issues**
- **Admin rights required**: Run installer as administrator
- **Revit not detected**: Check Revit installation registry keys
- **.NET Framework missing**: Install .NET Framework 4.8

### **Runtime Issues**
- **Add-in not loading**: Check Revit Add-Ins dialog
- **Connection failed**: Ensure MCP server is running
- **Permission errors**: Check file system permissions

## 📞 **Support**

- **Documentation**: See `Resources\README.txt`
- **Support**: https://flcrane.com/tycoon-support
- **Updates**: https://flcrane.com/tycoon-updates
- **Contact**: support@flcrane.com

---

## 🎯 **Professional Installer Complete!**

This WiX installer provides a **professional-grade installation experience** that:
- ✅ **Meets industry standards** for software deployment
- ✅ **Handles all complexity** automatically for users
- ✅ **Supports enterprise deployment** scenarios
- ✅ **Ready for Autodesk App Store** submission

**The installer is now ready for production deployment!** 🚀
