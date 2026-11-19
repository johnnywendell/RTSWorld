# Current Handoff

**Tarefa:** ECS-006 — DONE
**Arquivo:** tasks/completed/ECS-006-combat-performance.md
**Escritor ativo:** nenhum

- Combate dirigido, facções, recarga, Flow Fields, dano simultâneo, remoção e integração com seleção, economia e streaming em CombatDemo. Decisão em ADR-0011.
- PASS Unity 6000.6.9f1, Console com 0 erros e 0 avisos, 246/246 Edit Mode e 15/15 Play Mode. PASS mouse virtual e inspeção das capturas. Três desvios confirmados na grade procedural. Evidências e perfis de 100/500/1.000/5.000 em docs/validation/ECS-006.
- Perfil de 5.000 unidades mediu pico de oito em alcance no cenário integrado e onda sintética de 5.000 mortes em um tick. Evitação local proposta no backlog. Mouse físico, player e GPU NOT_RUN.
- Editor com CombatDemo limpa, Play desligado e zero Worlds de unidades. main/b207bd7, um commit à frente de origin/main, alterações locais anteriores preservadas, sem novos commits. Nenhuma tarefa posterior iniciada.
- Para reproduzir, siga docs/validation/ECS-006/README.md.
