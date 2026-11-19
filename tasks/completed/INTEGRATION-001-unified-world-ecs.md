# INTEGRATION-001 — Unified World and ECS Simulation

**Status:** DONE
**Priority:** HIGH
**Owner:** nenhum — concluída
**Branch:** main
**Base commit:** b207bd7
**Last checkpoint:** 2026-11-18 — critérios validados

## Objective

Entregar RTSWorldIntegratedDemo, primeira cena jogável que une o ambiente procedural WORLD com as unidades, navegação e interação ECS.

## Justificativa

A demo de interação usa o terreno base. A demo de câmera usa a camada ambiental completa. Integrar ambas mantendo uma configuração reproduzível, simulação desacoplada e as cenas existentes.

## Scope

- Cena nova, configuração compartilhada, spawn em células navegáveis, recursos lógicos, testes, inspeção gráfica e profiling.
- Exclui coleta, economia, combate, rede, construções, nova câmera, nova navegação, novo gerador e tarefas posteriores.

## Dependencies

WORLD-001 a WORLD-005 e ECS-001 a ECS-004 concluídas.

## Requisitos arquiteturais

A seed e a versão definem o mundo. Camada visual, câmera, navegação e posicionamento consomem os mesmos parâmetros procedurais. Residência visual não controla sessão ECS. Recursos usam ResourceKey e catálogo WORLD existente.

## Acceptance criteria

- [x] Auditoria e causa da diferença registradas.
- [x] Nova cena com terreno, biomas, água, árvores e recursos visíveis.
- [x] Câmera, seleção, Shift/Ctrl, Move, Stop e indicadores integrados.
- [x] Spawn e movimento respeitam altura, água, declividade e política de obstáculos.
- [x] Streaming visual preserva unidades e dados durante unload e reload.
- [x] Determinismo, catálogo lógico e cenas anteriores preservados.
- [x] Compilação, Console, Edit Mode, Play Mode e inspeção visual aprovados.
- [x] Profiling ou limitação demonstrada, documentação e handoff atualizados.

## Implementation plan

1. Auditar cenas, contratos e relatórios.
2. Centralizar a leitura da configuração e adaptar a sessão existente.
3. Criar cena nova e spawn validado pelo campo de navegação.
4. Testar mundo, recursos, movimento, streaming e interação.
5. Medir cargas, registrar evidências e concluir apenas com todos os critérios verificados.

## Checkpoint final — 2026-11-18

- Nova RTSWorldIntegratedDemo usa configuração comum para terreno v1, ambiente v4, câmera, navegação e spawn. Recursos Tree, Stone e Gold são atravessáveis. Catálogo reconstruível por chunk independente de mesh. A arquitetura e os limites constam no ADR-0008.
- Criados RTSWorldIntegratedDemo, IntegratedSpawn, IntegratedWorldTests e IntegratedWorldDemoTests, com metas. Modificados RtsInteractionDemo, ChunkManager, EnvironmentPresentation, WorldSurfaceQuery, ProceduralTerrainContract, NavigationGrid e UnitSession. Cenas anteriores e suas alterações preexistentes preservadas.
- PASS compilação e Console no Unity MCP. Edit Mode 223/223, 41,1239444 s. Play Mode 12/12, 2,3133038 s. Teste final verificou movimento entre chunks, unload/reload determinístico, cor, vértices, recursos e retenção ECS.
- PASS inspeção de screenshot, input virtual, Move em terra, recusa da água, 100 destinos distintos e Stop. Pan/zoom/rotação e a sequência de seed 7→8→7 verificados. Populações 100/500/1.000/5.000 medidas nas duas cenas. Evidências e limitações no [relatório](../../docs/validation/INTEGRATION-001/README.md).
- NOT_RUN mouse físico, build de player, GPU timing e outras plataformas.
- Git main/b207bd7, mudanças locais sem commit. Nenhuma ação restante nesta tarefa. Aguardar autorização para selecionar outra.

## Validation evidence

| Verificação | Estado | Evidência |
| --- | --- | --- |
| Compilação e Console | PASS | Unity 6000.6.9f1, zero erros ou avisos finais |
| Edit Mode | PASS | 223/223 |
| Play Mode | PASS | 12/12 |
| Inspeção e input MCP | PASS | Capturas, 100 unidades em posições distintas, seed, water picking, Stop, streaming |
| Profiling | PASS | Quatro populações e medições de streaming e comandos nas duas cenas |

## Handoff notes

Editor em RTSWorldIntegratedDemo limpa, Play desligado e zero Worlds de unidades. Resultados do runner ficam em Logs. CSV e capturas versionáveis no relatório. ECS-005 e demais tarefas seguem fora da autorização.
