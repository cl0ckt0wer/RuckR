---
name: remember
description: Save an insight, decision, bug, or pattern to agentmemory
category: memory
---

# Remember

Save important context to agentmemory so future sessions can recall it.

**Use the `memory_save` tool** to persist:
- `content`: The insight or finding (required)
- `type`: One of `pattern`, `architecture`, `bug`, `workflow`, `preference`, or `fact`
- `concepts`: Comma-separated keywords for future search
- `files`: Comma-separated relevant file paths

## When to remember

Save after:
1. Fixing a non-trivial bug (type: `bug`)
2. Making an architectural decision (type: `architecture`)
3. Discovering a project convention (type: `pattern`)
4. Learning a gotcha or workaround (type: `workflow`)
5. User expresses a preference (type: `preference`)
6. Discovering a fact about the codebase (type: `fact`)
