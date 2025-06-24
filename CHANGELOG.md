# Tycoon AI-BIM Platform Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.4.0] - 2025-06-24

### Fixed
- **WebSocket DLL Installation** - Fixed installer to properly include websocket-sharp.dll
- **UI Threading Issues** - Fixed connection success/failure dialogs not appearing
- **Connection Feedback** - Success dialog now appears immediately after connection
- **Installer Dependency Management** - All required DLLs now included in MSI package
- **UX Confusion** - Removed confusing "Connecting..." dialog that required manual dismissal
- **Dialog Z-Order** - Fixed success dialogs appearing behind Revit window
- **Installer File Sources** - Fixed WiX variables causing outdated DLL packaging
- **Build Process** - Removed PostBuildEvent that caused sharing violations
- **CRITICAL: Installer Not Deploying** - Removed blocking conditions that prevented file installation

### Changed
- Removed WPF Dispatcher calls that were incompatible with Revit
- Improved connection status logging and error handling
- Enhanced installer to use direct file paths for dependencies

### Tested
- ✅ Dynamic port discovery working (finds server on port 8767)
- ✅ WebSocket connection established successfully
- ✅ UI feedback working properly
- ✅ Complete installation and connection workflow verified

## [1.0.3.0] - 2025-06-24

### Added
- **Dynamic Port Assignment** - Server automatically finds available ports
- **Port Conflict Resolution** - No more "EADDRINUSE" errors
- **Smart Port Discovery** - Tries 100 ports starting from preferred port (8765)
- **Production-Ready Networking** - Handles multi-user environments gracefully

### Changed
- MCP Server now displays actual port being used in startup messages
- RevitBridge constructor accepts preferred port but finds available alternative
- Improved error handling for port conflicts

### Fixed
- Eliminated hardcoded port conflicts in multi-user environments
- Server startup no longer fails when preferred port is in use

## [1.0.2.0] - 2025-06-24

### Changed
- Removed automatic version incrementing from build script
- Version updates now require manual control using UpdateVersion.ps1
- This ensures proper semantic versioning based on actual change significance

### Fixed
- Fixed XML corruption issues in Product.wxs during automatic version updates
- Build script no longer modifies version numbers automatically

## [1.0.1.0] - 2025-06-24

### Added
- Automatic version management system
- Version tracking across all components (MSI, DLL, MCP server)
- Manual version update script (UpdateVersion.ps1)
- Changelog documentation

### Fixed
- Added missing AddInId nodes in Revit manifest file
- Fixed Revit add-in registration errors
- Updated publisher information to include developer name

### Changed
- Installer now uses per-user installation (no admin privileges required)
- Simplified installer to focus on Revit add-in deployment

## [1.0.0.0] - 2025-06-24

### Added
- Initial release of Tycoon AI-BIM Platform
- Revit add-in with AI-BIM integration
- MCP server for AI communication
- Professional MSI installer with WiX
- Multi-Revit version support (2022-2025)
- WebSocket communication between Revit and AI
- Steel framing automation workflows
- Real-time selection monitoring
- Comprehensive logging system

### Components
- **Revit Add-in**: TycoonRevitAddin.dll
- **MCP Server**: Node.js/TypeScript server
- **Installer**: Professional MSI package
- **Dependencies**: Newtonsoft.Json, WebSocketSharp

---

## Version Numbering

- **Major.Minor.Build.Revision** format
- **Major**: Breaking changes, major new features
- **Minor**: New features, backwards compatible
- **Build**: Bug fixes, small improvements (auto-incremented)
- **Revision**: Hotfixes, patches

## Release Process

1. Update version: `.\UpdateVersion.ps1 -Increment Minor`
2. Update changelog with new features/fixes
3. Build installer: `.\Build.bat`
4. Test installation and functionality
5. Tag release in version control
6. Distribute MSI installer
