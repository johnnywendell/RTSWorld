# ECS-006 — Combat and Performance

**Status:** DONE  
**Priority:** HIGH  
**Owner:** Codex  
**Branch:** main  
**Base commit:** b207bd7  
**Last checkpoint:** validação completa e encerramento

## Objective
Comandar soldados em combate corpo a corpo no mundo procedural, com duas facções, navegação, dano, morte e demonstração instrumentada.

## Scope
- Incluído: Attack direcionado, passo fixo, vida e dano simultâneo, remoção, integração econômica e visual, cenários de 100/500/1.000/5.000.
- Excluído: ataque automático, diplomacia, projéteis, física completa, multiplayer, IA estratégica, construção e evitação dinâmica completa.

## Dependencies
- WORLD-001..005, ECS-001..005 e INTEGRATION-001/002.

## Acceptance criteria
- [x] Facções
- [x] Vida
- [x] Dano
- [x] Intervalo de ataque
- [x] Comando Attack
- [x] Aquisição direcionada
- [x] Navegação existente
- [x] Ataque no alcance
- [x] Morte
- [x] Remoção consistente
- [x] Seleção após morte
- [x] Feedback visual
- [x] Move
- [x] Stop
- [x] Gather
- [x] Conservação econômica
- [x] Obstáculos ambientais
- [x] Streaming
- [x] CombatDemo
- [x] Edit Mode
- [x] Play Mode
- [x] Compilação
- [x] Console
- [x] Profiling ou limitação
- [x] Inspeção visual
- [x] Documentação
- [x] Handoff

## Implementation plan
1. Integrar combate à sessão fixa, ordens e fluxo regional.
2. Integrar feedback, facções e CombatDemo.
3. Executar testes, inspeção Unity MCP e cenários de escala.

## Checkpoint
### Completed and verified
- Attack direcionado por facção, aproximação com Flow Fields, alcance e cadência fixos, dano simultâneo, remoção e limpeza de seleção. Move, Stop e Gather validados.
- CombatDemo validada com mouse virtual, indicadores, streaming, água, árvores, pedras e coleta. Perfis integrado e sintético nas quatro populações.
- Suítes finais PASS com 246/246 Edit Mode e 15/15 Play Mode. Console final sem erros ou avisos.

### In progress
- Nenhum item da ECS-006.

### Next smallest action
- Para reprodução, abrir CombatDemo e seguir o roteiro de docs/validation/ECS-006. Nenhuma tarefa posterior iniciada.

### Files touched
- Simulation: UnitCombat, UnitSession, UnitOrders e UnitComponents. Presentation: IntegratedSpawn e RtsInteractionDemo. Cena CombatDemo, UnitCombatTests e CombatDemoTests. Registro em ACTIVE, BACKLOG, Handoff, Current State, Architecture, Roadmap, ADR-0011 e evidências ECS-006.

### Git state
- main/b207bd7, adiantada um commit. Alterações locais de ECS-003 a ECS-005 e integrações preservadas.

### Risks, blockers and decisions
- Atlas regional de oito campos limita destinos distintos simultâneos. O perfil de 5.000 unidades registrou pico de 16,043 ms de perseguição em um tick. O cenário sintético registrou 12,313 ms para um tick com 5.000 mortes.
- Até 32 centros compartilharam a mesma célula na aproximação, com posições locais distintas durante o ensaio. Evitação dinâmica proposta no backlog. Sem validação de build/player, GPU ou mouse físico.

## Validation evidence
| Check | Status | Evidence |
| --- | --- | --- |
| Unity compilation | PASS | Unity 6000.6.9f1 e Console final sem erros ou avisos. |
| Edit Mode | PASS | 246/246 em 33,965 s. |
| Play Mode | PASS | 15/15 em 14,129 s. |
| Editor/MCP input e inspeção | PASS | Mouse virtual, vida de 100 para 90, baixa, Move, Stop, Gather e capturas. |
| Profiling | PASS | Populações 100/500/1.000/5.000 em Play Mode e duelos letais em Edit Mode. |
| Mouse físico, player e GPU | NOT_RUN | Reprodução manual e limitações no relatório. |

## Handoff notes
Editor com CombatDemo aberta, Play desligado, cena limpa e zero Worlds de unidades. Evidências em docs/validation/ECS-006. A sessão preservou a base local preexistente e repôs apenas os artefatos de ECS-002 que os testes regravaram. main/b207bd7, sem commits.
