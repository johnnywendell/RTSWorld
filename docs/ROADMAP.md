# Roadmap

| Milestone | Goal | Exit evidence |
| --- | --- | --- |
| M0 — Environment | Unity project, Git, Codex, MCP, handoff protocol | Verified Unity/MCP test and handoff dry run |
| M1 — World data | Seeded, versioned terrain/chunk data independent of rendering | Repeatability tests |
| M2 — World visualization | Low-poly chunk meshes, isometric camera, streaming | Play Mode and chunk load/unload tests |
| M3 — Simulation | ECS unit data, group navigation and local RTS selection and commands (ECS-004) | Unit tests and measured baseline |
| M3 — World/ECS integration | Unified playable procedural-world demo (INTEGRATION-001) | Integrated scene, reload tests and measured Editor baseline |
| M3 — Environmental navigation | Logical resource obstacles and interaction points (INTEGRATION-002) | Obstacle paths, regional cache and streaming tests |
| M3 — Economy foundation | Gather, deposit and resource state (ECS-005) | Playable economy demo and conservation tests |
| M3 — Combat and Performance | Directed ECS melee combat in the procedural world (ECS-006) | CombatDemo, fixed-step tests and measured battle scenarios |
| M4 — Networking | Two clients and authoritative command processing | Repeatable multiplayer integration test |
| M5 — Persistence | Save/recover mutable world and dynamic entities | Restart/recovery test |
| M6 — RTS MVP | Gathering, construction, training, combat | End-to-end playtest |
| M7 — Scale test | Eight players and controlled unit loads | CPU, RAM, bandwidth and latency report |

Milestones are planning targets, not evidence of implementation. Do not begin an unrelated milestone without approval.
