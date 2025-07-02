# Custom Icon Setup for Tycoon AI-BIM Platform Setup

## 🎯 **Icon Requirements**

To customize the icon that appears in the Tycoon AI-BIM Platform Setup window, you need to provide:

### **Required File**
- **File Name**: `TycoonIcon.ico`
- **Location**: `Resources\TycoonIcon.ico`
- **Format**: Windows Icon (.ico) format
- **Recommended Sizes**: 16x16, 32x32, 48x48, 256x256 pixels

## 🔧 **How to Add Your Custom Icon**

1. **Create or obtain your icon file** in .ico format
2. **Name it exactly**: `TycoonIcon.ico`
3. **Place it in**: `tycoon-ai-bim-platform\src\installer\Resources\TycoonIcon.ico`
4. **Rebuild the bootstrapper**: `.\Build.ps1 -BuildBootstrapper`

## 📋 **Icon Specifications**

### **Technical Requirements**
- **Format**: Windows Icon (.ico)
- **Color Depth**: 32-bit with alpha channel (recommended)
- **Multiple Sizes**: Include 16x16, 32x32, 48x48, and 256x256 for best quality
- **File Size**: Keep under 1MB for reasonable installer size

### **Design Guidelines**
- **Simple Design**: Icons should be recognizable at small sizes
- **High Contrast**: Ensure visibility on different backgrounds
- **Brand Consistent**: Match your company/product branding
- **Professional**: Clean, modern appearance for enterprise software

## 🛠️ **Icon Creation Tools**

### **Free Tools**
- **GIMP** - Free image editor with ICO export
- **Paint.NET** - With ICO plugin
- **IcoFX** - Dedicated icon editor
- **Online Converters** - Convert PNG/JPG to ICO

### **Professional Tools**
- **Adobe Illustrator** - With ICO export plugins
- **Axialis IconWorkshop** - Professional icon editor
- **Greenfish Icon Editor Pro** - Advanced icon creation

## 🔍 **Current Status**

✅ **COMPLETED**: The Bundle.wxs file has been updated to reference both icon files:

```xml
<Bundle Name="Tycoon AI-BIM Platform Setup"
        IconSourceFile="Resources\TycoonIcon.ico"
        ... >
    <BootstrapperApplicationRef Id="WixStandardBootstrapperApplication.HyperlinkLicense">
      <bal:WixStandardBootstrapperApplication
        LicenseUrl="https://flcrane.com/tycoon-license"
        LogoFile="$(var.ResourcesDir)\TycoonLogo.png" />
    </BootstrapperApplicationRef>
```

✅ **COMPLETED**: Custom icons created from `C:\RevitAI\Icons\Tycoon Logo.png`:
- **TycoonIcon.ico** (3.73 KB) - EXE file icon (taskbar, file explorer)
- **TycoonLogo.png** (2.54 KB) - Dialog logo (64x64, replaces red CD square)

✅ **COMPLETED**: Bootstrapper builds successfully with complete custom branding

**Result**: The Tycoon AI-BIM Platform Setup now displays your custom Tycoon logo in BOTH the window chrome AND the dialog content!

## 📝 **Example Icon Sizes**

When creating your ICO file, include these sizes:
- **16x16** - Small icons, taskbar
- **32x32** - Standard desktop icons
- **48x48** - Large icons, file explorer
- **256x256** - High-resolution displays

## ⚠️ **Important Notes**

- The icon file must exist before building the bootstrapper
- If the file is missing, the build will fail
- The icon appears in the setup window title bar and taskbar
- Changes require rebuilding the bootstrapper to take effect
