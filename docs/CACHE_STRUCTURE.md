# GitHub Script Cache Structure

## Overview
The Tycoon AI-BIM Platform uses a local cache system to store GitHub scripts with version tracking and rollback capabilities.

## Cache Directory Structure

```
%APPDATA%\Tycoon\
├── GitCache\                           # Main cache directory
│   ├── 2025.06.30-g77a38bd\           # Cache version (commit SHA)
│   │   ├── .meta                       # Version metadata file
│   │   ├── scripts\                    # Cached scripts
│   │   │   ├── Analysis\
│   │   │   │   ├── ElementCounter.py
│   │   │   │   └── WallAnalyzer.py
│   │   │   ├── Management\
│   │   │   │   └── PanelManager.py
│   │   │   └── Utilities\
│   │   │       ├── SelectionHelper.py
│   │   │       └── ParameterManager.py
│   │   └── templates\                  # Layout templates
│   │       └── default-layout.json
│   ├── rollback\                       # Rollback points
│   │   ├── 20250630-120000\           # Rollback timestamp
│   │   │   ├── .rollback               # Rollback metadata
│   │   │   └── [cached content]        # Backup of cache state
│   │   └── 20250630-130000\
│   └── version-history.json            # Global version history
├── settings.json                       # GitHub repository settings
├── user-layout.json                    # User's custom layout
├── github-layout-template.json         # GitHub default template
└── RibbonLayout_v1.json               # Current ribbon layout
```

## File Descriptions

### .meta Files
Each cache directory contains a `.meta` file with version information:
```json
{
  "commitSha": "77a38bd",
  "cacheDirectory": "2025.06.30-g77a38bd",
  "created": "2025-06-30T12:00:00Z",
  "manifestVersion": "2025.06.30",
  "manifestBuild": "2025.06.30-g77a38bd",
  "scriptCount": 5,
  "templateCount": 1,
  "downloadStats": {
    "downloaded": 5,
    "skipped": 0,
    "failed": 0
  },
  "scriptVersions": {
    "ElementCounter.py": {
      "hash": "3b1cab5f",
      "version": "1.0.0",
      "lastModified": "2025-06-30T12:51:24Z",
      "size": 2659,
      "path": "Analysis/ElementCounter.py"
    }
  }
}
```

### version-history.json
Global version tracking file:
```json
{
  "currentVersion": "77a38bd",
  "lastUpdated": "2025-06-30T12:00:00Z",
  "versions": [
    {
      "commitSha": "77a38bd",
      "cacheDirectory": "2025.06.30-g77a38bd",
      "created": "2025-06-30T12:00:00Z",
      "scriptCount": 5
    }
  ]
}
```

### settings.json
GitHub repository configuration:
```json
{
  "repositoryOwner": "your-username",
  "repositoryName": "tycoon-ai-bim-platform",
  "branch": "main",
  "githubToken": null,
  "lastChecked": "2025-06-30T12:00:00Z"
}
```

### .rollback Files
Rollback point metadata:
```json
{
  "id": "20250630-120000",
  "created": "2025-06-30T12:00:00Z",
  "description": "Before major update",
  "sourceCacheDirectory": "2025.06.30-g77a38bd"
}
```

## Cache Management

### Automatic Cleanup
- Keeps last 10 versions in history
- Automatically removes older cache directories
- Rollback points are preserved separately

### Selective Updates
- Downloads only changed files based on hash comparison
- Skips unchanged scripts to save bandwidth
- Maintains file timestamps and metadata

### Offline Mode
- Falls back to cached scripts when GitHub is unavailable
- Uses most recent cache directory
- Maintains full functionality without network

### Rollback Capability
- Create rollback points before major changes
- Restore previous cache states
- Preserve user customizations during rollbacks

## Integration Points

### GitCacheManager
- Manages cache lifecycle
- Handles GitHub API communication
- Implements selective update logic

### VersionTracker
- Creates and manages .meta files
- Maintains version history
- Handles rollback operations

### RibbonLayoutManager
- Loads scripts from cache directory
- Falls back to offline mode when needed
- Preserves user layout customizations

## Error Handling

### Network Failures
- Graceful fallback to offline mode
- Retry logic with exponential backoff
- Clear error messages for troubleshooting

### Cache Corruption
- Validates cache integrity on load
- Automatic cleanup of corrupted files
- Rollback to last known good state

### Disk Space Management
- Monitors cache size growth
- Automatic cleanup of old versions
- User notification for space issues
