# 🧠 Monster Memory Bank - Usage Guide

## 🚀 Quick Start

### 1. Installation & Setup

```bash
# Install dependencies
npm install

# Build the project
npm run build

# Start the server
npm start
```

### 2. MCP Client Configuration

Add to your MCP client (Claude Desktop, etc.):

```json
{
  "mcpServers": {
    "monster-memory": {
      "command": "node",
      "args": ["path/to/monster-memory-mcp/dist/index.js"]
    }
  }
}
```

## 🎯 Core Workflows

### Initialize Your Memory Bank

```javascript
// First, initialize the Temporal Neural Nexus
await callTool('initialize_memory_bank', {});
```

### Create Enhanced Memories

```javascript
// Create a memory with auto-enhancement
await callTool('create_memory', {
  title: "AI Architecture Insights",
  content: "Discovered that transformer models work best with attention mechanisms for long-range dependencies. The key insight is that self-attention allows the model to focus on relevant parts of the input sequence regardless of distance.",
  category: "ai-research",
  importance: 9,
  mood: "excited",
  environment: "research-lab"
});

// The system automatically:
// ✅ Generates vector embeddings for semantic search
// ✅ Creates auto-tags: ["transformer", "attention", "ai", "research"]
// ✅ Generates summary: "Key insight about transformer models and attention mechanisms"
// ✅ Extracts key phrases: ["attention mechanisms", "transformer models", "self-attention"]
```

### Advanced Search Capabilities

```javascript
// Semantic search - finds conceptually similar content
await callTool('search_memories', {
  text: "neural networks",
  searchType: "semantic",
  maxResults: 10
});

// Multi-criteria search
await callTool('search_memories', {
  text: "machine learning",
  categories: ["ai-research", "development"],
  importance: { min: 7 },
  dateRange: {
    start: "2024-01-01T00:00:00Z",
    end: "2024-12-31T23:59:59Z"
  }
});

// Find similar memories
await callTool('find_similar_memories', {
  memoryId: "your-memory-id",
  maxResults: 5
});
```

### Context Management for Projects

```javascript
// Create a project context
await callTool('create_context', {
  name: "ai-assistant-project",
  description: "Development of advanced AI assistant",
  settings: {
    autoTag: true,
    defaultCategory: "development",
    importanceThreshold: 6
  }
});

// Switch to project context
await callTool('switch_context', {
  contextName: "ai-assistant-project"
});

// All new memories will now be in this context
await callTool('create_memory', {
  title: "API Design Decision",
  content: "Decided to use REST API with GraphQL for complex queries..."
});
```

### Smart Organization

```javascript
// Add tags to existing memory
await callTool('add_tags', {
  memoryId: "memory-id",
  tags: ["important", "review-later", "architecture"]
});

// Get all available tags and categories
await callTool('get_all_tags', {});
await callTool('get_all_categories', {});
```

### Time Awareness Features

```javascript
// Get comprehensive time information
await callTool('get_current_datetime', {
  timezone: "America/New_York",
  format: "detailed"
});

// Calculate time since an event
await callTool('get_time_since', {
  timestamp: "2024-01-01T00:00:00Z"
});

// Set preferred timezone
await callTool('set_preferred_timezone', {
  timezone: "Europe/London"
});
```

## 🎨 Advanced Use Cases

### 1. Research Knowledge Base

```javascript
// Create research context
await callTool('create_context', {
  name: "research-papers",
  description: "Academic research and papers",
  settings: { autoTag: true, defaultCategory: "research" }
});

// Add research findings
await callTool('create_memory', {
  title: "Attention Is All You Need - Key Insights",
  content: "The transformer architecture revolutionized NLP by replacing recurrence with self-attention...",
  importance: 10,
  tags: ["transformer", "attention", "nlp", "breakthrough"]
});

// Find related research
await callTool('search_memories', {
  text: "attention mechanisms",
  searchType: "semantic"
});
```

### 2. Project Documentation

```javascript
// Switch to project context
await callTool('switch_context', { contextName: "web-app-project" });

// Document decisions
await callTool('create_memory', {
  title: "Database Choice: PostgreSQL",
  content: "Chose PostgreSQL over MongoDB because we need ACID compliance and complex relationships...",
  category: "architecture",
  importance: 8
});

// Later, find all architecture decisions
await callTool('search_memories', {
  categories: ["architecture"],
  contexts: ["web-app-project"]
});
```

### 3. Learning Journal

```javascript
// Create learning context
await callTool('create_context', {
  name: "learning-journal",
  description: "Personal learning and growth",
  settings: { autoTag: true }
});

// Record learning
await callTool('create_memory', {
  title: "React Hooks Pattern",
  content: "Learned that custom hooks are a powerful way to share stateful logic between components...",
  category: "learning",
  importance: 7,
  mood: "accomplished"
});

// Find learning patterns
await callTool('search_memories', {
  text: "learned",
  searchType: "fuzzy",
  contexts: ["learning-journal"]
});
```

## 🔧 Power User Tips

### 1. Bulk Operations
- Use bulk operations for importing existing knowledge bases
- Batch tag multiple memories for better organization
- Bulk categorize memories by topic

### 2. Search Strategies
- Use `semantic` search for conceptual similarity
- Use `exact` search for specific terms
- Use `fuzzy` search for approximate matches
- Combine search types with `all` for comprehensive results

### 3. Context Organization
- Create contexts for different projects/areas
- Use context-specific settings for auto-tagging
- Switch contexts to maintain focus

### 4. Memory Enhancement
- Let auto-tagging work, then refine manually
- Use importance scores to prioritize memories
- Add mood and environment for richer context

### 5. Analytics & Insights
- Monitor memory creation patterns
- Identify knowledge gaps
- Track learning progress over time

## 🎯 Best Practices

1. **Consistent Categorization**: Use a consistent category system
2. **Meaningful Titles**: Write descriptive, searchable titles
3. **Rich Content**: Include context, reasoning, and examples
4. **Regular Review**: Periodically search and review old memories
5. **Context Switching**: Use contexts to maintain project focus
6. **Tag Strategy**: Develop a consistent tagging vocabulary
7. **Importance Scoring**: Use 1-10 scale consistently

## 🚀 Advanced Features Coming Soon

- Machine learning-based memory recommendations
- Advanced visualization dashboards
- Real-time collaboration features
- Voice-to-memory capabilities
- Integration with external knowledge bases
- Predictive memory suggestions

---

**Happy Memory Banking! 🧠✨**

*Transform your AI assistant into a knowledge powerhouse with Temporal Neural Nexus.*
