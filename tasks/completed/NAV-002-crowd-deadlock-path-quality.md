# NAV-002 — Crowd Deadlock Recovery & Path Quality

**Status:** DONE  
**Priority:** HIGH  
**Owner:** Codex  
**Branch:** main  
**Base commit:** c690c43  
**Last checkpoint:** 2026-11-19

## Objective
Medir progresso, recuperar bloqueios persistentes e reduzir desvios evitáveis em ordens existentes.

## Scope
- Incluído: classificação de progresso, recuperação, trânsito bidirecional, qualidade dos caminhos, destinos de grupo, cena e validação integrada.
- Excluído: teleporte, travessia de obstáculos, formações avançadas, multiplayer e VISUAL.

## Dependencies
- NAV-001 concluída com 14/14 cenários, 290/290 Edit Mode e 22/22 Play Mode.

## Acceptance criteria
- [x] Métricas e estados de progresso para unidades em movimento, congestionadas, bloqueadas, sem rota e que chegaram.
- [x] Recuperação com limites, comparação antes/depois de travamentos, oscilações, trajetórias e conclusão.
- [x] Flow Fields, destinos do grupo e segurança de obstáculos preservados.
- [x] Combate, economia, construções, IA, streaming e Save/Load validados.
- [x] CrowdRecoveryDemo com os cenários determinísticos e diagnósticos.
- [x] Compilação, Console, Edit Mode e Play Mode aprovados.
- [x] Profiling comparável em 100, 500, 1.000 e 5.000 unidades, relatório e handoff atualizados.

## Implementation plan
1. Reproduzir um bloqueio e medir progresso em condições controladas.
2. Separar falhas de rota de congestionamento e aplicar recuperação com histerese.
3. Avaliar posições finais, fluxos opostos, desvios e integração com as ordens existentes.
4. Construir a cena, executar regressões e comparar perfil de carga.

## Checkpoint
### Completed and verified
- Gate NAV-001 passou. Baseline 8/8 nos testes CrowdMovement e desvio de ida até a âncora reproduzido via MCP. [Linha de base](../../docs/validation/NAV-002/README.md)

- Comparação de rota via MCP: 15,628 → 11,000 unidades na trajetória de referência após permitir entrada antecipada no trecho final.
- Regressão de formação adicionada. PASS Edit Mode 291/291 e Play Mode 22/22.
- Fluxo oposto na faixa reproduziu separação mínima de 0,100 unidade com dois soldados e bolsões livres em ambas as extremidades.

- Recuperação lateral limitada validada em corredor. Chegada no tick 161 em quatro tentativas. Sem recuperação, ainda em rota no tick 220.
- CrowdRecoveryDemo criada e validada em Play. Perfil em 100, 500, 1.000 e 5.000 unidades com tempos de simulação e de frame.

- Arbitragem de fluxo oposto validada com prioridades invertidas e caso sem bolsões. Na comparação com destinos idênticos, a sobreposição caiu de quatro para zero ticks.
- Matriz dos 12 roteiros concluída na CrowdRecoveryDemo. PASS 296/296 Edit Mode e 24/24 Play Mode.

### In progress
- Nenhum critério obrigatório pendente.

### Next smallest action
- Nenhuma tarefa posterior iniciada.

### Files touched
- `UnitMovementSystem.cs`, `UnitSession.cs`, `UnitComponents.cs`, `UnitOrders.cs`, `NavigationGrid.cs`, `Construction.cs` e `EnvironmentalObstacles.cs`.
- `RtsInteractionDemo.cs`, `RtsNavigationDiagnostics.cs`, `RtsCrowdScenarios.cs`, `CrowdRecoveryDemo.unity` e testes de ordens e de multidão.
- Tarefa, arquitetura, estado atual, relatório, capturas, perfis e handoff.

### Git state
- Branch: main
- HEAD: c690c43
- Uncommitted changes: alterações anteriores e da NAV-001 preservadas.

### Risks, blockers and decisions
- Faixas retas de até oito células usam arbitragem limitada. Ausência de espaço, saída ocupada ou amostragem incompleta podem manter a espera.
- Evitar consultas de caminho por unidade a cada frame e evitar instabilidade nas ordens persistidas.

## Validation evidence
| Check | Status | Evidence |
| --- | --- | --- |
| Unity compilation | PASS | Unity 6000.6.9f1, Console sem erros ou avisos |
| Automated tests | PASS | Edit Mode 296/296 e Play Mode 24/24 |
| Editor/MCP test | PASS | Perfil inicial, comparação do desvio e linha de base de fluxos opostos |
| Play Mode | PASS | 12 roteiros validados. Perfil Editor em quatro cargas |

## Handoff notes
[Validação da NAV-002](../../docs/validation/NAV-002/README.md).
