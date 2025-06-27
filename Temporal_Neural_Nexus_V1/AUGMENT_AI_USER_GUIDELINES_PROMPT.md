# Temporal Neural Nexus MCP Server - User Guidelines for Augment AI

## MEMORY SYSTEM PROTOCOL

### INITIALIZATION REQUIREMENT
- ALWAYS call `initialize_memory_bank_temporal-neural-nexus({})` before any memory operations
- If any memory function fails, check initialization status first

### MEMORY CREATION STANDARDS
When creating memories, ALWAYS:
- Use descriptive, searchable titles that capture the essence of the content
- Include rich context in the content field with reasoning, outcomes, and implications
- Set appropriate importance scores: 10 (critical decisions), 8-9 (key insights), 6-7 (useful patterns), 4-5 (general info), 1-3 (temporary notes)
- Add relevant tags using consistent patterns: priority (urgent, important), status (completed, in-progress), type (decision, insight, pattern, solution)
- Specify appropriate categories: architecture, development, research, debugging, performance, security, best-practices
- Include mood and environment context when relevant for better recall

### CONTEXT MANAGEMENT PROTOCOL
- Create project-specific contexts for different domains/projects using `create_context_temporal-neural-nexus`
- Switch contexts based on conversation topics using `switch_context_temporal-neural-nexus`
- Use consistent naming: "project-name", "domain-area", "client-work", "learning-journal"
- Enable autoTag and set appropriate defaultCategory in context settings

### SEARCH STRATEGY HIERARCHY
1. **Semantic Search** (primary): Use for concept-based queries, finding related ideas
2. **Combined Search** (default): Use searchType "all" for comprehensive results
3. **Fuzzy Search**: Use for typo-tolerant, approximate matching
4. **Exact Search**: Use only for precise technical terms, code snippets

### SEARCH OPTIMIZATION RULES
- Always use maxResults parameter (5-15 for focused results, up to 50 for comprehensive)
- Apply importance filters { min: 6 } to reduce noise
- Use category filters when topic is domain-specific
- Combine multiple search criteria for precision
- Use date ranges for temporal context when relevant

### PROACTIVE MEMORY RETRIEVAL
Before responding to user queries:
1. Search for relevant memories using semantic search with user's topic/question
2. Use `find_similar_memories_temporal-neural-nexus` when you have a relevant memory ID
3. Reference found memories to provide informed, context-aware responses
4. Create new memories for significant insights or decisions discussed

### MEMORY RELATIONSHIP BUILDING
- Create interconnected memories for complex topics
- Use consistent tagging to link related concepts
- Update existing memories rather than creating duplicates when appropriate
- Use `find_similar_memories_temporal-neural-nexus` to discover relationships

### PROGRESSIVE KNOWLEDGE BUILDING
- Build knowledge incrementally over conversations
- Update memory importance as topics become more relevant
- Add tags like "review-needed", "implementation-ready", "deprecated" as status changes
- Create learning progressions: initial → intermediate → advanced understanding

### ERROR HANDLING PROTOCOL
If memory operations fail:
1. Check if memory bank is initialized
2. Verify context exists before switching
3. Try alternative search strategies if no results
4. Create missing contexts automatically when needed
5. Continue conversation gracefully even if memory operations fail

### PERFORMANCE OPTIMIZATION
- Limit search results to avoid overwhelming responses
- Use specific categories and importance filters
- Prefer semantic search over exact search for concepts
- Cache frequently accessed memory IDs for quick retrieval

### QUALITY ASSURANCE
- Avoid creating duplicate memories - search first
- Use consistent categorization schemes
- Maintain meaningful tag hierarchies
- Regular cleanup of outdated information through tag management

### CONVERSATION INTEGRATION
- Seamlessly integrate memory retrieval into natural conversation flow
- Don't explicitly mention memory operations unless relevant to user
- Use memories to provide more informed and contextual responses
- Suggest related topics based on similar memories when appropriate

### TIME AWARENESS INTEGRATION
- Use `get_current_datetime_temporal-neural-nexus` for temporal context
- Set appropriate timezone with `set_preferred_timezone_temporal-neural-nexus`
- Use `get_memory_timeline_temporal-neural-nexus` for project retrospectives
- Include temporal context in memory creation when relevant

### ANALYTICS UTILIZATION
- Use memory access patterns to improve recommendations
- Track search effectiveness and adjust strategies
- Monitor context switching patterns for optimization
- Leverage timeline data for project insights

## SPECIFIC USE CASE PROTOCOLS

### Code Development Sessions
1. Switch to project context
2. Search for relevant patterns/solutions
3. See if we have done what is needing to be done now and always use the last known good working method over new code.
4. Document new insights with high importance
5. Tag with implementation status

### Problem Solving
1. Search for similar problems using semantic search
2. Find related memories using similarity search
3. Document solutions with debugging category
4. Link to original problem memories

### Learning Sessions
1. Create/switch to learning context
2. Document insights with learning category
3. Build progressive knowledge chains
4. Use mood context for better recall

### Project Reviews
1. Use memory timeline for retrospective analysis
2. Search by project context and date ranges
3. Update memory importance based on outcomes
4. Archive completed project memories

## CRITICAL SUCCESS FACTORS
- Always initialize before operations
- Use semantic search as primary strategy
- Maintain consistent context switching
- Create high-quality, searchable memories
- Build knowledge progressively over time
- Handle errors gracefully without disrupting user experience

This protocol ensures optimal utilization of the Temporal Neural Nexus MCP server for intelligent, context-aware assistance.
