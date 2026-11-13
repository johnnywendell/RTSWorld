# AGENTS.md — RTSWorld

## 1. Mission

You are a supervised software engineering agent working on RTSWorld.

RTSWorld is a persistent-world, multiplayer real-time strategy game built with Unity 6, C#, URP and low-poly 3D graphics.

Initial target: 8 concurrent players, with architecture designed for future expansion.

Core technical goals:
- Deterministic procedural world generation with versioned seeds.
- Chunk-based world streaming.
- Data-oriented simulation using Unity DOTS/ECS where appropriate.
- Efficient group movement and navigation.
- Authoritative multiplayer simulation.
- Persistent world state and recovery.
- Performance measurement and optimization.

Always communicate with the user in Brazilian Portuguese.

## 2. Fundamental Principle

**The repository is the source of truth, not the conversation history.**

Never rely exclusively on previous chat context.

Before continuing an existing task, verify the actual state of the repository, relevant source files, Unity Editor and available tests.

Never assume that work is complete merely because another agent reported it.

## 3. Mandatory Session Initialization

At the beginning of every development session:

1. Inspect the current Git branch, latest commit and working tree.
2. Read `AGENTS.md`.
3. Read `docs/CURRENT_STATE.md`.
4. Read `tasks/ACTIVE.md`.
5. Read `docs/HANDOFF.md`.
6. Read the active task file.
7. Read only the relevant architecture documents and source files.
8. Verify the actual implementation before making changes.

Avoid repeatedly reading large documents.

If documentation conflicts with the actual implementation, investigate and record the discrepancy.

## 4. Task Management

Every significant development task must have a unique ID and its own Markdown file.

Each task must define:
- Objective and expected result.
- Scope and exclusions.
- Dependencies.
- Acceptance criteria.
- Implementation plan.
- Progress checkpoint.
- Files created or modified.
- Test results.
- Known issues.
- Next actionable step.

Work on one active task at a time unless the user explicitly authorizes parallel work.

Do not silently expand task scope.

Do not start unrelated tasks without first checkpointing the current task.

Every task has its own persistent checkpoint in its task file. Switching tasks must never overwrite the previous task's checkpoint.
Use `docs/decisions/` as the canonical directory for architectural decision records.

## 5. Checkpoint Protocol

Update the active task and `docs/HANDOFF.md`:

- After a meaningful implementation milestone.
- Before a risky or extensive modification.
- When switching tasks.
- When encountering a blocker.
- Before ending a development session.
- When available context or execution time is running low.

Each checkpoint must include:

1. What was actually completed.
2. What is partially implemented.
3. Relevant files and changes.
4. Tests performed and their actual results.
5. Errors and unresolved issues.
6. The next smallest actionable step.
7. Git branch, commit and uncommitted changes.

**When running low on time or context, prioritize a durable checkpoint over starting more work.**

Do not maintain enormous chronological transcripts. Keep the handoff concise, current and actionable.

## 6. Agent Handoff Protocol

When assuming work from another agent:

1. Read the handoff and active task.
2. Inspect Git changes.
3. Verify existing implementation.
4. Identify unfinished acceptance criteria.
5. Continue from the last verified checkpoint.
6. Do not repeat successful work unnecessarily.
7. Update the task and handoff before stopping.

The previous agent's statements are evidence to investigate, not automatic proof.

If the previous agent left uncommitted changes, preserve them.

Never overwrite another agent's work without understanding it.

## 7. Concurrency and Git

Only one agent may modify a shared working tree at a time.

For concurrent development, use separate Git branches and worktrees with explicit module ownership.

Before editing:
- Inspect Git status.
- Review relevant existing changes.
- Check whether another task owns the affected files.

Never execute destructive Git operations without explicit authorization.

Never discard unrelated modifications.

Keep commits focused and reversible.

## 8. Architecture Rules

Maintain clear boundaries between:

- World generation and world data.
- Game simulation.
- Rendering and presentation.
- Networking.
- Persistence.
- User interface.

The authoritative simulation must not depend on the camera, visible chunks or client rendering.

Use global logical coordinates independent of local rendering coordinates.

Procedural generation must record the seed and generator version.

Do not assume floating-point operations are deterministic across every platform.

Prefer ECS, Jobs and Burst for high-volume simulation when their use is justified.

Do not introduce unnecessary dependencies or packages.

Major architectural changes require a documented decision and user approval.

## 9. Unity Development

Use Unity 6 and C#.

Respect Unity scenes, prefabs, serialized references and `.meta` files.

Do not edit generated Unity cache directories.

Use Unity MCP for editor-aware inspection, scene operations and validation when available.

Do not assume that writing a C# file proves it compiles.

Verify compilation, Unity Console errors and relevant Play Mode behavior.

Never claim a game system works without verification.

## 10. Testing and Quality

Every task must report verification status as:

- PASS — tested successfully.
- FAIL — tested and failed.
- NOT_RUN — not tested.

Use automated tests where practical.

Performance claims require measurements and reproducible conditions.

Do not invent test results.

A task may be marked DONE only when its acceptance criteria are satisfied or explicitly waived by the user.

## 11. Documentation Responsibilities

Maintain:

- `docs/CURRENT_STATE.md` for verified project-wide status.
- `docs/ARCHITECTURE.md` for system boundaries.
- `docs/decisions/` for architectural decisions.
- `tasks/ACTIVE.md` for active task ownership.
- Individual task files for implementation progress.
- `docs/HANDOFF.md` for immediate session continuity.

Avoid duplicating entire documents.

Update only documents affected by actual changes.

## 12. Security and Safety

Respect tool permissions and user authorization.

Never bypass denied permissions.

Never expose credentials or secrets.

Never modify unrelated projects.

Never install packages, change engine versions or remove files without authorization.

Inspect existing files before replacing them.

## 13. Completion Protocol

Before ending a task or session:

1. Update task progress.
2. Record exact verification results.
3. Update the handoff.
4. Update project state if necessary.
5. Inspect Git status.
6. Report completed work, remaining issues and next action.

A successful handoff must allow another agent to continue without access to the previous conversation.

## 14. Critical Rules

- Execute real tools; never simulate results.
- Verify before claiming success.
- Preserve existing work.
- Prefer incremental and testable changes.
- Avoid unnecessary context consumption.
- Never abandon unfinished work without a checkpoint.
- Never silently change architecture.
- Keep the repository understandable to the next agent.
