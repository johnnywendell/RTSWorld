# ADR-0001 — Technology Stack and Architectural Direction

**Status:** Accepted as project direction; individual packages not yet validated.  
**Date:** 2026-11-12

## Context
The project targets a persistent multiplayer RTS with many independently simulated units and a low-poly 3D strategy view.

## Decision
- Unity 6, C#, Universal Render Pipeline (URP).
- Low-poly 3D world with isometric/orthographic camera.
- Simulation and rendering separated by design.
- Evaluate DOTS/Entities, Jobs and Burst for high-volume simulation early.
- Authoritative multiplayer server; evaluate Netcode for Entities compatibility before adopting a network stack.
- Procedural terrain with seed and generator version, chunks and explicit mutable-world persistence.
- Git-tracked, agent-independent task/checkpoint/handoff protocol.

## Consequences
- Extra initial design/testing work; easier to scale and change presentation later.
- ECS, networking and performance claims remain unverified until measured.
- Major reversals require a new ADR, not silent edits to this record.
