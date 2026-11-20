# NAV-001 — Navigation Diagnostics & Clearance Calibration

**Status:** DONE  
**Priority:** HIGH  
**Owner:** Codex  
**Branch:** main  
**Base commit:** c690c43  
**Last checkpoint:** 2026-11-19

## Objective
Medir a diferença entre a geometria visível e a navegabilidade, corrigir discrepâncias reproduzíveis e expor o estado da navegação no Editor.

## Scope
- Incluído: auditoria da grade, terreno, ocupação, unidades, campos, ordens e avoidance, com testes e profiling.
- Excluído: recuperação de deadlocks, rede e mudanças amplas de arquitetura.

## Dependencies
- ECS-003/004/007/010 e INTEGRATION-002.

## Acceptance criteria
- [x] Auditoria com reprodução, cenários determinísticos e comparações antes/depois.
- [x] Ferramentas de grid, obstáculos, unidades, campos, caminhos e falhas com limites.
- [x] Cena dedicada e integração opt-in nas cenas existentes.
- [x] Segurança ambiental e streaming validados.
- [x] Compilação, Console, Edit Mode e Play Mode validados.
- [x] Capturas, profiling, documentação e handoff publicados.

## Implementation plan
1. Registrar linha de base e reproduzir discrepâncias.
2. Adicionar ferramentas e correções direcionadas com regressões.
3. Validar cenários, perfis e integração antes de liberar NAV-002.

## Checkpoint
### Completed and verified
- Estado Git e configuração das cenas conferidos. PersistenceDemo consultada em Play via MCP.
- Correção de rasterização tangencial validada. Diagnósticos da Scene View e NavigationDiagnosticsDemo observados e capturados.
- Na PersistenceDemo, dois prédios iniciais formam um corte que causa rejeição de Move para um lote com sete origens desconectadas. O cenário foi reproduzido com os diagnósticos de navegação e com crowd ligado e desligado.
- Capturas, amostras de tempo e alocação gerenciada para 100 a 5.000 unidades e amostra isolada de visualização publicadas no relatório.

- Correção na seleção dos prédios iniciais comparada em MCP com a máscara anterior sobre as mesmas origens. Move aceito em ambos os modos de avoidance na regressão de Play.
- Matriz dos 14 cenários executada e registrada. Foram 13 PASS e 1 FAIL.

- Travessia árvore–rocha validada com a mesma regra de faixas seguras em A*, Flow Fields e movimento Burst.

### In progress
- Nenhum item obrigatório pendente.

### Next smallest action
- Iniciar NAV-002 para progressos, congestionamentos e qualidade das rotas.

### Files touched
- `RtsNavigationDiagnostics.cs`, `RtsInteractionDemo.cs` e `NavigationDiagnosticsDemo.unity`.
- `EnvironmentalObstacles.cs`, `NavigationGrid.cs`, `NavigationSearch.cs`, `UnitSession.cs` e `UnitComponents.cs`.
- `Construction.cs`, `UnitMovementSystem.cs`, `PersistenceDemoTests.cs`, `EnvironmentalObstacleTests.cs` e `NavigationSearchTests.cs`.
- Planejamento, arquitetura, estado atual, handoff e relatório NAV-001.

### Git state
- Branch: main
- HEAD: c690c43
- Uncommitted changes: alterações anteriores ECS-007 a ECS-010 preservadas, inclusive na simulação e na apresentação.

### Risks, blockers and decisions
- A grade bloqueia células inteiras. Alterar folgas sem medir a capacidade de acesso aos recursos pode causar regressão.
- A variante de inicialização avaliada reservando a ocupação antes do spawn produziu 51/100 unidades numa componente incorreta. A variante foi descartada em memória.
- A ocupação grossa continua sendo usada para construção. Faixas de movimento exigem verificação do segmento em cada célula.
- Faixas conservadoras limitam passagem ao interior validado. O steering lateral fica suspenso nas pernas restritas.

## Validation evidence
| Check | Status | Evidence |
| --- | --- | --- |
| Unity compilation | PASS | Unity 6000.6.9f1, Console sem erros ou avisos na inspeção final |
| Automated tests | PASS | Edit Mode 290/290 e Play Mode 22/22, incluindo árvore-rocha, streaming e persistência |
| Editor/MCP test | PASS | Capturas da cena dedicada e reprodução de rejeição na PersistenceDemo |
| Play Mode | PASS | Cena dedicada em Play, ordem aceita, rota capturada. Estado final fora de Play |
| Matriz de cenários do gate | PASS | 14/14, incluindo travessia árvore-rocha nos dois modos de avoidance |

## Handoff notes
[Relatório e linha de base](../../docs/validation/NAV-001/README.md).
