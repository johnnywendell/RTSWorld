# WORLD-001 — Deterministic Terrain Data Generator

**Status:** PENDING  
**Priority:** HIGH  
**Owner:** unassigned  
**Dependencies:** ENV-001  
**Last checkpoint:** not started

## Objective
Implement a seed- and version-based procedural terrain **data** generator independent of Unity scene rendering, as a foundation for low-poly chunks.

## Scope
- Included: chunk coordinate model, deterministic terrain/biome/resource data contract, generator version, unit tests.
- Excluded: chunk mesh rendering, network replication, persistence implementation, full world streaming.

## Acceptance criteria
- [ ] Same seed, version and chunk coordinate yield identical data in repeat runs on the validated environment.
- [ ] Different seeds produce distinguishable outputs in representative tests.
- [ ] Generator does not depend on camera or scene objects.
- [ ] Chunk border continuity is verified by tests.
- [ ] Unity compiles without new errors; relevant tests are recorded.
- [ ] Documentation and checkpoint updated with precise evidence.

## Implementation plan
1. Inspect architecture and current project; propose data types and test approach.
2. Implement minimal generator with explicit seed and version.
3. Add repeatability and border tests.
4. Validate through Unity and record results.

## Checkpoint
### Completed and verified
- None.

### Next smallest action
- Wait until ENV-001 is completed and the user authorizes WORLD-001.

### Git state
- Branch/HEAD/changes: not yet inspected.

## Validation evidence
| Check | Status | Evidence |
| --- | --- | --- |
| Unity compilation | NOT_RUN | — |
| Determinism tests | NOT_RUN | — |
| Border continuity tests | NOT_RUN | — |

## Handoff notes
Do not start this task before environment validation and explicit instruction.
