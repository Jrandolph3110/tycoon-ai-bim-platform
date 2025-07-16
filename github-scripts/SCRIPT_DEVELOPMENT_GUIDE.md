# 🎯 Tycoon AI-BIM Platform Script Development Guide

## Overview

This guide provides comprehensive instructions for developing scripts for the **Tycoon AI-BIM Platform** used by F.L. Crane & Sons for prefabricated light gauge steel construction.

## 📁 Repository Structure

```
github-scripts/
├── .scriptconfig              # Script configuration and build settings
├── SCRIPT_DEVELOPMENT_GUIDE.md # This guide
├── ScriptTemplate/            # Comprehensive reference template
│   ├── script.json           # Complete configuration reference
│   ├── ScriptTemplate.cs     # C# source code template
│   ├── ScriptTemplate.csproj # Project file with dependencies
│   ├── Build.ps1            # Build automation script
│   ├── README.md            # Template documentation
│   └── icon.txt             # Icon placeholder
└── [YourScript]/             # Your script directory
```

## 🚀 Quick Start

### 1. Create New Script from Template

```bash
# Copy template directory
cp -r ScriptTemplate/ MyNewScript/

# Rename files
mv MyNewScript/ScriptTemplate.cs MyNewScript/MyNewScript.cs
mv MyNewScript/ScriptTemplate.csproj MyNewScript/MyNewScript.csproj

# Update script.json configuration
# Update class names in C# file
# Update project file references
```

### 2. Configure Script Properties

Edit `script.json` with your script details:

```json
{
  "name": "My New Script",
  "description": "Brief description of functionality",
  "author": "Your Name",
  "version": "1.0.0",
  "panel": "SmartTools",
  "stack": "MyScripts",
  "stackOrder": 1,
  "stackType": "stacked"
}
```

### 3. Implement Script Logic

Update the C# class with your specific functionality:

```csharp
public class MyNewScript
{
    public Result Execute(ExternalCommandData commandData, 
                         ref string message, 
                         ElementSet elements)
    {
        // Your implementation here
    }
}
```

### 4. Build and Test

```bash
# Build script
dotnet build MyNewScript.csproj --configuration Release

# Test locally (copy to test-scripts directory)
# Use "Reload Scripts" in Tycoon ribbon

# Commit to GitHub for distribution
git add MyNewScript/
git commit -m "Add MyNewScript"
git push origin main
```

## 📊 Configuration Reference

### script.json Properties

| Property | Type | Description | Example |
|----------|------|-------------|---------|
| `name` | string | Display name in ribbon | "Element Counter" |
| `description` | string | Brief functionality description | "Count selected elements" |
| `author` | string | Script author/team | "F.L. Crane & Sons" |
| `version` | string | Semantic version | "1.0.0" |
| `panel` | string | Target ribbon panel | "Production", "SmartTools", "Management" |
| `stack` | string | Grouping name for related scripts | "TestScripts" |
| `stackOrder` | number | Order within stack | 1, 2, 3... |
| `stackType` | string | Display type | "stacked" or "dropdown" |
| `requiresSelection` | boolean | Needs selected elements | true/false |
| `allowZeroDoc` | boolean | Can run without document | true/false |
| `requiresTransaction` | boolean | Needs model transaction | true/false |
| `isDevelopment` | boolean | Development/template script | true/false |

### Panel Options

- **Production**: Main production scripts for daily use
- **SmartTools**: Utility and helper scripts
- **Management**: Administrative and management scripts

### Stacking Options

- **"stacked"**: Vertical individual buttons (recommended)
- **"dropdown"**: Pulldown menu with options

## 🛠️ Development Workflow

### Local Development

1. **Create script** in `test-scripts/` directory
2. **Use "Reload Scripts"** button for hot-reload testing
3. **Iterate and debug** with immediate feedback
4. **Move to `github-scripts/`** when ready for distribution

### GitHub Distribution

1. **Commit script** to `github-scripts/` directory
2. **Push to repository** 
3. **Use "Refresh GitHub Scripts"** button in other installations
4. **Scripts appear automatically** in ribbon

### Build Configuration

Scripts support both Debug and Release configurations:

```xml
<!-- Debug: Full symbols, no optimization -->
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <DebugSymbols>true</DebugSymbols>
  <Optimize>false</Optimize>
</PropertyGroup>

<!-- Release: Optimized, no symbols -->
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <DebugSymbols>false</DebugSymbols>
  <Optimize>true</Optimize>
</PropertyGroup>
```

## 🔧 Platform Integration

### Revit API Integration

```csharp
// Standard Revit API references
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

// Platform integration
private Application _app;
private UIApplication _uiApp;
private Document _doc;
private UIDocument _uidoc;
```

### Transaction Management

```csharp
using (var transaction = new Transaction(_doc, "Operation Name"))
{
    transaction.Start();
    try
    {
        // Modify model
        transaction.Commit();
    }
    catch
    {
        transaction.RollBack();
        throw;
    }
}
```

### Error Handling

```csharp
try
{
    // Script logic
    return Result.Succeeded;
}
catch (Exception ex)
{
    LogError($"Script failed: {ex.Message}", ex);
    message = ex.Message;
    return Result.Failed;
}
```

## 📚 Best Practices

### Code Organization

- **Single responsibility**: One script, one primary function
- **Clear naming**: Descriptive class and method names
- **Comprehensive comments**: Document complex logic
- **Error handling**: Graceful failure with user feedback

### Performance

- **Cache collectors**: Reuse FilteredElementCollector results
- **Batch operations**: Process elements in groups
- **Transaction scope**: Minimize transaction duration
- **Memory management**: Dispose resources properly

### User Experience

- **Clear feedback**: Show progress and results
- **Validation**: Check prerequisites before execution
- **Undo support**: Use transactions for model changes
- **Consistent UI**: Follow platform patterns

## 🔍 Testing and Debugging

### Local Testing

1. Build script in Debug mode
2. Copy to `test-scripts/` directory
3. Use "Reload Scripts" for hot-reload
4. Test with various scenarios

### GitHub Testing

1. Commit to repository
2. Use "Refresh GitHub Scripts"
3. Verify script appears and functions
4. Test on different installations

### Common Issues

- **Assembly loading**: Check dependencies and paths
- **Transaction errors**: Ensure proper transaction scope
- **Selection issues**: Validate element selection
- **Permission errors**: Check document read/write status

## 📝 Documentation Standards

### Code Comments

```csharp
/// <summary>
/// Brief description of method purpose
/// </summary>
/// <param name="parameter">Parameter description</param>
/// <returns>Return value description</returns>
public Result ExecuteScript(ExternalCommandData commandData)
{
    // Implementation
}
```

### README Files

Each script should include:
- Purpose and functionality
- Usage instructions
- Configuration options
- Dependencies and requirements
- Examples and screenshots

## 🤝 Contributing

### Submission Process

1. **Follow template structure**
2. **Test thoroughly** in local and GitHub modes
3. **Document changes** with clear commit messages
4. **Update configuration** if needed
5. **Notify team** of new scripts

### Code Review

- **Functionality**: Does it work as intended?
- **Performance**: Is it efficient and responsive?
- **Standards**: Follows coding conventions?
- **Documentation**: Adequately documented?
- **Testing**: Thoroughly tested?

## 📞 Support

For questions or issues:
- **Template Reference**: Check `ScriptTemplate/` directory
- **Platform Documentation**: See main project docs
- **F.L. Crane Standards**: Follow company coding guidelines
- **Revit API**: Reference official Autodesk documentation

---

**Happy scripting! 🚀**
