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
| ECS-001 | Fundação DOTS/ECS | DONE | WORLD-001..005 e correções ambientais | `tasks/completed/ECS-001-dots-foundation.md` |
| ECS-002 | Unidades e Simulação | DONE | ECS-001 | `tasks/completed/ECS-002-units-simulation.md` |
| ECS-003 | Navegação e Movimentação | DONE | ECS-001/ECS-002 | `tasks/completed/ECS-003-navigation-movement.md` |
| ECS-004 | RTS Unit Selection and Commands | DONE | WORLD-005/ECS-003 | `tasks/completed/ECS-004-rts-selection-commands.md` |
| INTEGRATION-001 | Unified World and ECS Simulation | DONE | WORLD-001..005/ECS-001..004 | `tasks/completed/INTEGRATION-001-unified-world-ecs.md` |
| INTEGRATION-002 | Environmental Obstacles and Navigation Integration | DONE | INTEGRATION-001 | `tasks/completed/INTEGRATION-002-environmental-obstacles-navigation.md` |
| ECS-005 | Resource Gathering and Economy Foundation | DONE | INTEGRATION-002 | `tasks/completed/ECS-005-resource-gathering-economy.md` |
| ECS-006 | Combat and Performance | DONE | ECS-005/INTEGRATION-002 | `tasks/completed/ECS-006-combat-performance.md` |
| UNITS-001 | ECS simulation spike and benchmark | PENDING | ENV-001 | To create when scoped |
| NET-001 | Authoritative two-client proof of concept | PENDING | ECS-002 | Unidades e Simulação | DONE | ECS-001 | `tasks/completed/ECS-002-units-simulation.md` |
| UNITS-001 | To create when scoped |
| ECS-LOCAL-AVOIDANCE | Evitação dinâmica local para grupos e gargalos | PROPOSTA | ECS-006 | A detalhar se autorizado |

| NAV-001 | Navigation Diagnostics & Clearance Calibration | DONE | ECS-003/004/007/010 e INTEGRATION-002 | `tasks/completed/NAV-001-navigation-diagnostics-clearance.md` |

| NAV-002 | Crowd Deadlock Recovery & Path Quality | DONE | NAV-001 | `tasks/completed/NAV-002-crowd-deadlock-path-quality.md` |

| VISUAL-001 | Art Direction & Rendering Architecture | DONE | Base técnica até NAV-002 | `tasks/completed/VISUAL-001-art-direction-rendering-architecture.md` |

| VISUAL-001A | Terrain Alignment, Streaming & LOD Continuity | DONE | VISUAL-001 | `tasks/completed/VISUAL-001A-terrain-alignment-streaming-lod.md` |

| VISUAL-002 | Environment Art & Biome Overhaul | DONE | VISUAL-001/001A | `tasks/completed/VISUAL-002-environment-art-biome-overhaul.md` |

Backlog order is tentative. A task becomes DONE only after its acceptance criteria have evidence.
