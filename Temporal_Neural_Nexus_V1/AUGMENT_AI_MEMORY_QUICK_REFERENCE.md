# 🧠 Augment AI - Temporal Neural Nexus Quick Reference

## 🚀 Essential Functions

### Initialization (ALWAYS FIRST)
```javascript
await initialize_memory_bank_temporal-neural-nexus({});
```

### Core Memory Operations
```javascript
// Create Memory
await create_memory_temporal-neural-nexus({
  title: "Descriptive Title",
  content: "Detailed content with context",
  category: "development|architecture|research|learning",
  importance: 8, // 1-10 scale
  tags: ["keyword1", "keyword2"],
  mood: "confident|excited|concerned",
  environment: "development|production|research"
});

// Read Memory
await read_memory_temporal-neural-nexus({ id: "memory-id" });

// Update Memory
await update_memory_temporal-neural-nexus({
  id: "memory-id",
  title: "New Title",
  importance: 9
});

// Delete Memory
await delete_memory_temporal-neural-nexus({ id: "memory-id" });
```

### Smart Search
```javascript
// Semantic Search (Best for concepts)
await search_memories_temporal-neural-nexus({
  text: "authentication patterns",
  searchType: "semantic",
  maxResults: 10
});

// Multi-Criteria Search
await search_memories_temporal-neural-nexus({
  text: "database optimization",
  categories: ["architecture", "performance"],
  importance: { min: 7 },
  dateRange: {
    start: "2024-01-01T00:00:00Z",
    end: "2024-12-31T23:59:59Z"
  },
  searchType: "all"
});

// Find Similar Memories
await find_similar_memories_temporal-neural-nexus({
  memoryId: "reference-memory-id",
  maxResults: 5
});
```

### Context Management
```javascript
// Create Context
await create_context_temporal-neural-nexus({
  name: "project-name",
  description: "Project description",
  settings: {
    autoTag: true,
    defaultCategory: "development",
    importanceThreshold: 6
  }
});

// Switch Context
await switch_context_temporal-neural-nexus({
  contextName: "project-name"
});

// Get Current Context
await get_current_context_temporal-neural-nexus({});

// Get All Contexts
await get_all_contexts_temporal-neural-nexus({});
```

### Tag Management
```javascript
// Add Tags
await add_tags_temporal-neural-nexus({
  memoryId: "memory-id",
  tags: ["urgent", "review-needed"]
});

// Remove Tags
await remove_tags_temporal-neural-nexus({
  memoryId: "memory-id",
  tags: ["outdated"]
});

// Get All Tags
await get_all_tags_temporal-neural-nexus({});

// Get All Categories
await get_all_categories_temporal-neural-nexus({});
```

### Time Awareness
```javascript
// Get Current DateTime
await get_current_datetime_temporal-neural-nexus({
  timezone: "America/New_York",
  format: "detailed"
});

// Set Timezone
await set_preferred_timezone_temporal-neural-nexus({
  timezone: "UTC"
});

// Time Since Event
await get_time_since_temporal-neural-nexus({
  timestamp: "2024-01-15T10:00:00Z"
});

// Memory Timeline
await get_memory_timeline_temporal-neural-nexus({
  startDate: "2024-01-01T00:00:00Z",
  endDate: new Date().toISOString(),
  groupBy: "week"
});
```

## 🎯 Quick Decision Guide

### When to Use Each Search Type
- **semantic**: Concept-based queries, finding related ideas
- **fuzzy**: Typo-tolerant, approximate matches
- **exact**: Precise technical terms, code snippets
- **all**: Default comprehensive search (recommended)

### Importance Scoring
- **10**: Critical decisions, major breakthroughs
- **8-9**: Important architecture, key insights
- **6-7**: Useful patterns, solutions
- **4-5**: General information
- **1-3**: Temporary notes

### Common Categories
- `architecture` - System design decisions
- `development` - Code patterns, implementations
- `research` - Learning, experiments
- `debugging` - Problem solutions
- `performance` - Optimization insights
- `security` - Security patterns, concerns
- `best-practices` - Proven approaches

### Essential Tags
- **Priority**: `urgent`, `important`, `low-priority`
- **Status**: `completed`, `in-progress`, `planned`, `blocked`
- **Type**: `decision`, `insight`, `pattern`, `solution`, `question`
- **Review**: `review-needed`, `approved`, `deprecated`

## 🔄 Common Workflows

### 1. Starting New Project
```javascript
// 1. Create project context
await create_context_temporal-neural-nexus({
  name: "new-project",
  description: "Project description"
});

// 2. Switch to context
await switch_context_temporal-neural-nexus({
  contextName: "new-project"
});

// 3. Search for relevant patterns
await search_memories_temporal-neural-nexus({
  text: "similar project patterns",
  searchType: "semantic"
});
```

### 2. Problem Solving
```javascript
// 1. Search for similar problems
await search_memories_temporal-neural-nexus({
  text: "error description or problem",
  categories: ["debugging", "solutions"],
  searchType: "semantic"
});

// 2. Find related memories
await find_similar_memories_temporal-neural-nexus({
  memoryId: "problem-memory-id"
});

// 3. Document solution
await create_memory_temporal-neural-nexus({
  title: "Solution: Problem Description",
  content: "Detailed solution...",
  category: "debugging",
  importance: 8
});
```

### 3. Code Review
```javascript
// Search for standards and patterns
await search_memories_temporal-neural-nexus({
  text: "code review checklist best practices",
  categories: ["best-practices", "standards"],
  importance: { min: 7 }
});
```

### 4. Learning Session
```javascript
// 1. Create/switch to learning context
await switch_context_temporal-neural-nexus({
  contextName: "learning-journal"
});

// 2. Document insights
await create_memory_temporal-neural-nexus({
  title: "Learning: New Concept",
  content: "What I learned...",
  category: "learning",
  importance: 7,
  mood: "enlightened"
});
```

## ⚡ Performance Tips

### Optimize Search
- Use `maxResults` to limit response size
- Apply importance filters: `{ min: 6 }`
- Use specific categories when possible
- Combine multiple search criteria

### Memory Quality
- Write descriptive, searchable titles
- Include sufficient context in content
- Use consistent categorization
- Tag appropriately for discoverability
- Set realistic importance scores

### Context Strategy
- Create focused, project-specific contexts
- Switch contexts based on conversation topics
- Use consistent naming conventions
- Leverage context settings for automation

## 🚨 Critical Reminders

### ALWAYS DO
✅ Initialize memory bank first
✅ Use semantic search for concepts
✅ Switch contexts for different projects
✅ Provide detailed memory content
✅ Use appropriate importance scoring

### NEVER DO
❌ Skip initialization
❌ Create memories without context
❌ Use vague titles
❌ Ignore importance scoring
❌ Forget to switch contexts

## 🎯 Success Metrics

### Quality Indicators
- High search result relevance
- Consistent categorization
- Meaningful memory relationships
- Effective context switching
- Appropriate importance distribution

### Usage Patterns
- Regular context switching
- Balanced memory creation/retrieval
- Effective tag utilization
- Time-aware memory management
- Progressive knowledge building

This quick reference ensures efficient and effective use of the Temporal Neural Nexus MCP server for optimal AI assistance.
