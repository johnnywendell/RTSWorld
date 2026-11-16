# Backlog

| ID | Title | Status | Dependencies | File |
| --- | --- | --- | --- | --- |
| ENV-001 | Verify Unity MCP and agent continuity | DONE | None | `tasks/completed/ENV-001-agent-continuity-validation.md` |
| WORLD-001 | Deterministic, versioned terrain data generator | DONE | ENV-001 | `tasks/completed/WORLD-001-deterministic-terrain.md` |
| WORLD-002 | Renderização de terreno 3D low-poly | DONE | WORLD-001 | `tasks/completed/WORLD-002-low-poly-terrain-rendering.md` |
| WORLD-003 | Carregamento dinâmico de chunks | DONE | WORLD-002 | `tasks/completed/WORLD-003-dynamic-chunk-streaming.md` |
| WORLD-004 | Biomas, Água e Distribuição de Recursos | DONE | WORLD-001/002/003 | `tasks/completed/WORLD-004-biomes-water-resources.md` |
| WORLD-004-FIX-001 | Correção de artefatos geométricos em rios, lagos e margens | DONE | WORLD-004 | `tasks/completed/WORLD-004-FIX-001-water-geometry.md` |
| WORLD-004-FIX-002 | Diagnóstico e correção de geometria do terreno | DONE | WORLD-004/FIX-001 | `tasks/completed/WORLD-004-FIX-002-terrain-geometry.md` |
| WORLD-005 | Sistema de Câmera RTS | DONE | WORLD-001/002/003/004 | `tasks/completed/WORLD-005-rts-camera.md` |
| WORLD-004-FIX-003 | Hydrology and Terrain Geometry | DONE | WORLD-004/FIX-001/FIX-002/WORLD-005 | `tasks/completed/WORLD-004-FIX-003-hydrology-terrain-geometry.md` |
| UNITS-001 | ECS simulation spike and benchmark | PENDING | ENV-001 | To create when scoped |
| NET-001 | Authoritative two-client proof of concept | PENDING | UNITS-001 | To create when scoped |

Backlog order is tentative. A task becomes DONE only after its acceptance criteria have evidence.
