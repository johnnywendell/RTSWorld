# RTSWorld — Project Vision

## Product
A persistent-world, multiplayer real-time strategy game inspired by the strategic depth of Age of Empires, with a shared procedural world, settlement building, resource collection, armies, territorial conflict and long-term progression.

## Initial target
- 8 concurrent players on one authoritative simulation server.
- A single shared world, initially bounded for testing, expandable through procedural chunks.
- Low-poly 3D visuals with an isometric/orthographic strategy camera.
- A minimal playable loop: town center, 3 villagers, gathering, building, unit training and combat.

## Long-term aspirations (not MVP commitments)
- More concurrent players, larger persistent world, region-based interest management and potentially distributed simulation.
- Thousands of entities subject to measured CPU, memory, rendering and network budgets.

## Product principles
- Server owns the authoritative game state.
- The world simulation must not depend on what a client camera renders.
- Gameplay correctness and recoverability precede visual polish.
- Performance targets are hypotheses until measured.

## Out of scope for the first milestone
- Massively distributed servers, full economy, player accounts, elaborate AI, final art assets and commercial release systems.
