# 🛠️ Augment AI - Temporal Neural Nexus Troubleshooting Guide

## 🚨 Common Issues & Solutions

### 1. Memory Bank Not Initialized

**Problem**: Functions fail with "Memory bank not initialized" error

**Solution**:
```javascript
// ALWAYS run this first
await initialize_memory_bank_temporal-neural-nexus({});
```

**Prevention**: Check initialization status before operations
```javascript
try {
  await get_current_context_temporal-neural-nexus({});
} catch (error) {
  // If this fails, memory bank needs initialization
  await initialize_memory_bank_temporal-neural-nexus({});
}
```

### 2. Context Not Found

**Problem**: Context switching fails or context doesn't exist

**Solution**:
```javascript
// Check if context exists
const allContexts = await get_all_contexts_temporal-neural-nexus({});
const contextExists = allContexts.some(ctx => ctx.name === "target-context");

if (!contextExists) {
  // Create the context
  await create_context_temporal-neural-nexus({
    name: "target-context",
    description: "Auto-created context for current session"
  });
}

// Then switch
await switch_context_temporal-neural-nexus({
  contextName: "target-context"
});
```

### 3. Poor Search Results

**Problem**: Search returns irrelevant or no results

**Solutions**:

#### A. Try Different Search Types
```javascript
// Start with semantic search
let results = await search_memories_temporal-neural-nexus({
  text: "query",
  searchType: "semantic",
  maxResults: 10
});

// If poor results, try fuzzy search
if (results.length === 0 || results[0].score < 0.6) {
  results = await search_memories_temporal-neural-nexus({
    text: "query",
    searchType: "fuzzy",
    maxResults: 10
  });
}

// Last resort: exact search with broader terms
if (results.length === 0) {
  results = await search_memories_temporal-neural-nexus({
    text: "broader query terms",
    searchType: "exact",
    maxResults: 20
  });
}
```

#### B. Broaden Search Criteria
```javascript
// Remove restrictive filters
await search_memories_temporal-neural-nexus({
  text: "query",
  // Remove: categories, importance filters, date ranges
  searchType: "all",
  maxResults: 20
});
```

#### C. Use Tag-Based Search
```javascript
// Search by tags instead of text
await search_memories_temporal-neural-nexus({
  tags: ["relevant", "tag"],
  searchType: "tags"
});
```

### 4. Memory Not Found

**Problem**: Cannot read or update specific memory

**Solution**:
```javascript
// Verify memory exists
const memory = await read_memory_temporal-neural-nexus({ id: "memory-id" });

if (!memory) {
  // Search for similar memories
  const similar = await search_memories_temporal-neural-nexus({
    text: "memory topic or title",
    searchType: "semantic",
    maxResults: 5
  });
  
  if (similar.length > 0) {
    console.log("Found similar memories:", similar);
    // Use similar memory ID instead
  } else {
    // Create new memory if needed
    const newMemory = await create_memory_temporal-neural-nexus({
      title: "Replacement Memory",
      content: "Content for missing memory",
      category: "general",
      importance: 5
    });
  }
}
```

### 5. Slow Performance

**Problem**: Memory operations are taking too long

**Solutions**:

#### A. Limit Search Results
```javascript
await search_memories_temporal-neural-nexus({
  text: "query",
  maxResults: 5, // Reduce from default 50
  searchType: "semantic"
});
```

#### B. Use Specific Filters
```javascript
await search_memories_temporal-neural-nexus({
  text: "query",
  categories: ["specific-category"], // Narrow scope
  importance: { min: 7 }, // Filter by importance
  maxResults: 10
});
```

#### C. Avoid Complex Queries
```javascript
// Instead of complex multi-criteria search
// Break into simpler, focused searches
const results1 = await search_memories_temporal-neural-nexus({
  categories: ["architecture"],
  maxResults: 10
});

const results2 = await search_memories_temporal-neural-nexus({
  text: "specific term",
  searchType: "exact",
  maxResults: 5
});
```

### 6. Duplicate Memories

**Problem**: Creating duplicate or similar memories

**Prevention**:
```javascript
// Search before creating
const existing = await search_memories_temporal-neural-nexus({
  text: "proposed memory title or content",
  searchType: "semantic",
  maxResults: 3
});

if (existing.length > 0 && existing[0].score > 0.8) {
  // High similarity - update existing instead
  await update_memory_temporal-neural-nexus({
    id: existing[0].memory.id,
    content: "updated content",
    importance: Math.max(existing[0].memory.importance, newImportance)
  });
} else {
  // Create new memory
  await create_memory_temporal-neural-nexus({
    title: "New Memory",
    content: "Content"
  });
}
```

### 7. Tag Management Issues

**Problem**: Inconsistent or cluttered tags

**Solutions**:

#### A. Standardize Tags
```javascript
// Get all existing tags first
const allTags = await get_all_tags_temporal-neural-nexus({});
console.log("Existing tags:", allTags);

// Use consistent naming patterns
const standardTags = {
  priority: ["urgent", "important", "low-priority"],
  status: ["completed", "in-progress", "planned"],
  type: ["decision", "insight", "pattern", "solution"]
};
```

#### B. Clean Up Tags
```javascript
// Remove outdated tags
await remove_tags_temporal-neural-nexus({
  memoryId: "memory-id",
  tags: ["outdated", "deprecated", "old-version"]
});

// Add current tags
await add_tags_temporal-neural-nexus({
  memoryId: "memory-id",
  tags: ["current", "active", "relevant"]
});
```

### 8. Time Zone Issues

**Problem**: Incorrect time information or timezone handling

**Solution**:
```javascript
// Set preferred timezone
await set_preferred_timezone_temporal-neural-nexus({
  timezone: "America/New_York" // or user's timezone
});

// Always specify timezone in datetime requests
await get_current_datetime_temporal-neural-nexus({
  timezone: "America/New_York",
  format: "detailed"
});
```

## 🔧 Diagnostic Commands

### Health Check Sequence
```javascript
// 1. Check initialization
try {
  await get_current_context_temporal-neural-nexus({});
  console.log("✅ Memory bank initialized");
} catch (error) {
  console.log("❌ Need to initialize");
  await initialize_memory_bank_temporal-neural-nexus({});
}

// 2. Check contexts
const contexts = await get_all_contexts_temporal-neural-nexus({});
console.log("Available contexts:", contexts.length);

// 3. Check memory count
const timeline = await get_memory_timeline_temporal-neural-nexus({
  startDate: "2024-01-01T00:00:00Z",
  endDate: new Date().toISOString()
});
console.log("Memory activity:", timeline);

// 4. Check tags and categories
const tags = await get_all_tags_temporal-neural-nexus({});
const categories = await get_all_categories_temporal-neural-nexus({});
console.log("Tags:", tags.length, "Categories:", categories.length);
```

### Performance Test
```javascript
// Test search performance
const start = Date.now();
const results = await search_memories_temporal-neural-nexus({
  text: "test query",
  maxResults: 10
});
const duration = Date.now() - start;
console.log(`Search took ${duration}ms, found ${results.length} results`);
```

## 🚨 Error Recovery Strategies

### Graceful Degradation
```javascript
async function safeMemoryOperation(operation) {
  try {
    return await operation();
  } catch (error) {
    console.warn("Memory operation failed:", error.message);
    
    // Try alternative approach
    if (error.message.includes("not initialized")) {
      await initialize_memory_bank_temporal-neural-nexus({});
      return await operation(); // Retry
    }
    
    // Return safe fallback
    return { success: false, error: error.message };
  }
}
```

### Fallback Search Strategy
```javascript
async function robustSearch(query) {
  const strategies = [
    { searchType: "semantic", maxResults: 10 },
    { searchType: "fuzzy", maxResults: 15 },
    { searchType: "exact", maxResults: 20 }
  ];
  
  for (const strategy of strategies) {
    try {
      const results = await search_memories_temporal-neural-nexus({
        text: query,
        ...strategy
      });
      
      if (results.length > 0) {
        return results;
      }
    } catch (error) {
      console.warn(`Search strategy ${strategy.searchType} failed:`, error);
    }
  }
  
  return []; // No results found with any strategy
}
```

## 📊 Monitoring & Maintenance

### Regular Health Checks
- Monitor search result quality
- Check context switching success
- Verify memory creation/retrieval balance
- Review tag consistency
- Analyze performance metrics

### Optimization Recommendations
- Regularly clean up outdated memories
- Standardize categorization schemes
- Optimize search queries based on usage patterns
- Update importance scores as projects evolve
- Maintain consistent tagging practices

This troubleshooting guide helps resolve common issues and maintain optimal Temporal Neural Nexus MCP server performance.
