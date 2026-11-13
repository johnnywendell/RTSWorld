# Development Rules

## Work protocol
- Inspect before editing. Read only task-relevant files.
- Use small, reviewable changes; do not modify unrelated systems.
- Preserve Unity `.meta` files and serialized references; never commit `Library/`, `Temp/`, or generated build caches.
- Do not install packages or change Unity/editor versions without approval.
- Prefer reusable, testable C# modules with clear simulation/presentation boundaries.

## Verification
- For each acceptance criterion, record PASS, FAIL or NOT_RUN and the exact evidence.
- A successful source edit is not proof of Unity compilation.
- Check Unity Console and run Edit Mode/Play Mode tests where applicable.
- Never claim a test passed without running it.
- Measure performance under reproducible conditions; avoid speculative claims.

## Git and multi-agent safety
- Before modifying, inspect `git status --short`, branch and recent commit.
- One agent writes per working tree; parallel agents need separate worktrees and ownership.
- Do not auto-commit unrelated changes or silently discard user modifications.
- Checkpoint task progress before switching tasks, before risky changes and when context is running low.
- On interruption, leave a concise, actionable handoff; never erase another task's checkpoint.

## Unity MCP
- Use MCP to inspect editor state and validate editor-specific changes when available.
- If MCP is disconnected, record the limitation and use available local tests; do not fabricate results.
