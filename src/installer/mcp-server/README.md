# 🧠 Temporal Neural Nexus MCP Server

A **comprehensive, AI-powered memory management system** that combines the best of memory banking with advanced time awareness, analytics, and intelligent features. This is the ultimate evolution of memory management for AI assistants.

## 🚀 Features

### 🧠 **Advanced Memory Intelligence**
- **Semantic Search**: Vector embeddings for content similarity search
- **Auto-categorization**: AI-powered tagging and classification
- **Memory Relationships**: Automatic linking of related memories
- **Importance Scoring**: Smart ranking of memory relevance
- **Memory Summaries**: AI-generated summaries and key insights

### 🔍 **Enhanced Search & Retrieval**
- **Multi-modal Search**: Exact, fuzzy, semantic, and tag-based search
- **Full-text Search**: Comprehensive content indexing
- **Date Range Queries**: Temporal filtering and search
- **Context-aware Search**: Search within specific project contexts
- **Similarity Detection**: Find memories similar to any given memory

### 🏷️ **Smart Organization**
- **Auto-tagging**: Intelligent tag generation from content
- **Dynamic Categorization**: Evolving category systems
- **Memory Clustering**: Automatic grouping by topic/project
- **Context Switching**: Multiple project workspaces
- **Bulk Operations**: Efficient batch processing

### 📊 **Analytics & Insights**
- **Usage Analytics**: Track patterns and access frequency
- **Memory Health Reports**: Identify gaps and redundancies
- **Trend Analysis**: Understand memory creation and access patterns
- **Correlation Detection**: Find connections between memories
- **Performance Metrics**: Comprehensive statistics and insights

### 🕒 **Time Awareness**
- **Comprehensive Time Info**: Enhanced date/time operations
- **Memory Timeline**: Temporal analysis of memory activities
- **Time-based Queries**: Search memories by creation/modification time
- **Timezone Support**: Global time awareness
- **Duration Calculations**: Smart time difference calculations

### 🔄 **Import/Export & Backup**
- **Multiple Formats**: JSON, Markdown, CSV, HTML export
- **Compressed Backups**: Automatic backup with compression
- **Data Migration**: Easy import/export capabilities
- **Version Control**: Git integration ready
- **Incremental Backups**: Smart backup strategies

### 🤖 **AI-Powered Features**
- **Content Analysis**: Extract key phrases and insights
- **Duplicate Detection**: Identify and merge similar memories
- **Readability Analysis**: Content quality assessment
- **Language Detection**: Multi-language support
- **Anomaly Detection**: Identify unusual patterns

## 📦 Installation

```bash
# Clone or create the project
cd temporal-neural-net-v1

# Install dependencies
npm install

# Build the project
npm run build

# Test the server
npm test
```

## 🛠️ Configuration

Add to your MCP client configuration:

```json
{
  "mcpServers": {
    "temporal-neural-nexus": {
      "command": "node",
      "args": ["path/to/temporal-neural-net-v1/dist/index.js"],
      "env": {},
      "autoApprove": [
        "initialize_memory_bank",
        "create_memory",
        "read_memory",
        "search_memories",
        "get_current_datetime"
      ]
    }
  }
}
```

## 🎯 Available Tools

### Core Memory Operations
- `initialize_memory_bank` - Initialize the Temporal Neural Nexus
- `create_memory` - Create new memories with auto-enhancement
- `read_memory` - Read memories with access tracking
- `update_memory` - Update existing memories
- `delete_memory` - Delete memories safely

### Advanced Search
- `search_memories` - Multi-modal search with filters
- `find_similar_memories` - Semantic similarity search

### Organization & Tagging
- `add_tags` / `remove_tags` - Tag management
- `get_all_tags` / `get_all_categories` - Browse organization

### Context Management
- `create_context` - Create project contexts
- `switch_context` - Switch between projects
- `get_current_context` / `get_all_contexts` - Context info

### Time Awareness
- `get_current_datetime` - Enhanced time information
- `set_preferred_timezone` - Timezone management
- `get_time_since` - Duration calculations
- `get_memory_timeline` - Temporal memory analysis

## 🏗️ Architecture

```
temporal-neural-net-v1/
├── src/
│   ├── index.ts              # Main MCP Server
│   ├── core/
│   │   ├── MemoryManager.ts  # Core memory operations
│   │   ├── SearchEngine.ts   # Advanced search capabilities
│   │   ├── AnalyticsEngine.ts # Analytics and insights
│   │   └── TimeManager.ts    # Time awareness features
│   ├── models/
│   │   ├── Memory.ts         # Memory data structures
│   │   ├── Analytics.ts      # Analytics models
│   │   └── TimeInfo.ts       # Time-related models
│   └── utils/
│       ├── VectorUtils.ts    # Vector operations
│       ├── TextUtils.ts      # Text processing
│       └── FileUtils.ts      # File operations
├── dist/                     # Compiled JavaScript
├── package.json
├── tsconfig.json
└── README.md
```

## 🎨 Usage Examples

### Basic Memory Operations

```javascript
// Initialize the memory bank
await callTool('initialize_memory_bank', {});

// Create a memory with auto-enhancement
await callTool('create_memory', {
  title: "Project Architecture Decision",
  content: "We decided to use microservices architecture for better scalability...",
  category: "architecture",
  importance: 8,
  mood: "confident"
});

// Search with multiple criteria
await callTool('search_memories', {
  text: "microservices",
  categories: ["architecture"],
  importance: { min: 7 },
  searchType: "semantic"
});
```

### Advanced Features

```javascript
// Create a project context
await callTool('create_context', {
  name: "mobile-app-project",
  description: "Mobile app development project",
  settings: {
    autoTag: true,
    defaultCategory: "development",
    importanceThreshold: 6
  }
});

// Switch context and create memories
await callTool('switch_context', { contextName: "mobile-app-project" });

// Find similar memories
await callTool('find_similar_memories', {
  memoryId: "memory-id-here",
  maxResults: 5
});

// Get comprehensive time info
await callTool('get_current_datetime', {
  timezone: "America/New_York",
  format: "detailed"
});
```

## 🔧 Development

```bash
# Development mode with watch
npm run dev

# Build for production
npm run build

# Run tests
npm test

# Lint code
npm run lint
```

## 🌟 Key Differentiators

1. **Integrated Intelligence**: Combines memory, time, and analytics in one system
2. **Vector Search**: Semantic similarity using embeddings
3. **Context Awareness**: Project-based memory organization
4. **Auto-Enhancement**: AI-powered tagging, summarization, and insights
5. **Comprehensive Analytics**: Deep insights into memory usage patterns
6. **Time Integration**: Full temporal awareness and analysis
7. **Scalable Architecture**: Modular design for extensibility
8. **Multiple Export Formats**: Flexible data portability

## 🤝 Contributing

This Temporal Neural Nexus represents the evolution of memory management systems. Contributions are welcome to make it even more powerful!

## 📄 License

MIT License - Feel free to use and modify for your projects.

## 🎯 Roadmap

- [ ] Machine learning-based memory recommendations
- [ ] Real-time collaboration features
- [ ] Advanced visualization dashboards
- [ ] Integration with external knowledge bases
- [ ] Voice-to-memory capabilities
- [ ] Advanced relationship mapping
- [ ] Predictive memory suggestions

---

**Built with ❤️ by Augment Agent**

*Making AI assistants smarter, one memory at a time.*
