# Temporal Neural Nexus V1 - Installation Instructions

## Quick Start

1. **Navigate to the distribution directory:**
   ```bash
   cd Temporal_Neural_Nexus_Distribution
   ```

2. **Start the MCP server:**
   ```bash
   npm start
   ```

## Alternative Installation (if node_modules issues)

If you encounter any issues with the included node_modules:

1. **Remove node_modules and reinstall:**
   ```bash
   rm -rf node_modules
   npm install
   ```

2. **Build if needed:**
   ```bash
   npm run build
   ```

3. **Start the server:**
   ```bash
   npm start
   ```

## Verification

Run the test suite to verify everything is working:
```bash
npm test
```

## Documentation

- **README.md** - Overview and features
- **USAGE_GUIDE.md** - Detailed usage instructions
- **TEST_RESULTS.md** - Compilation and test results

## Memory Bank

The memory bank starts empty and will be created automatically when you first use the system.
All memory data is stored in the `temporal-memory-bank/` directory.

## Support

For issues or questions, refer to the documentation files included in this distribution.
