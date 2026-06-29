---
trigger: always_on
---

# Workspace Rules for Codebase Memory
- **Always Consult the Index First**: Before answering architectural questions, locating files, or suggesting new code, always query the `codebase-memory` MCP server.
- **Do Not Guess Paths**: If a file path or dependency structure is ambiguous, use the codebase index tool to verify its existence and location.
- **Maintain Consistency**: Ensure all code suggestions align with the indexing data regarding design patterns and project conventions.
