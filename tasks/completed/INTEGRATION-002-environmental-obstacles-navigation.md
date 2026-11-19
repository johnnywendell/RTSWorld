# INTEGRATION-002 — Environmental Obstacles and Navigation Integration

**Status:** DONE
**Priority:** HIGH
**Owner:** nenhum — concluída
**Branch:** main
**Base commit:** b207bd7
**Last checkpoint:** 2026-11-18 — gate aprovado

## Objective

Bloquear troncos e rochas na navegação regional e oferecer aproximação a recursos para futuras ordens econômicas.

## Scope

Ocupação lógica, classificação, atualização parcial, cache, aproximação, cena e testes. Coleta e economia aguardam o gate. ECS-006 excluída.

## Dependencies

WORLD-001 a WORLD-005, ECS-001 a ECS-004 e INTEGRATION-001 concluídas.

## Acceptance criteria

- [x] Auditoria e política registradas.
- [x] Troncos e rochas sólidos, ouro atravessável e margem configurável.
- [x] A*, fluxo, aproximação e invalidação seletiva.
- [x] Determinismo, streaming e movimento real em cena.
- [x] Compilação, Console, Edit Mode e Play Mode aprovados.
- [x] Inspeção visual, profiling ou limitação, documentação e handoff.

## Implementation plan

1. Indexar catálogo procedural independente das meshes.
2. Rasterizar ocupação na grade existente e invalidar campos afetados.
3. Expor aproximação e validar movimento, atualização e streaming.
4. Validar Unity e registrar o gate antes de criar ECS-005.

## Checkpoint final

- Ocupação lógica por ResourceKey, folga 0,22, troncos e pedras bloqueantes, ouro e copa livres. A* e fluxo reutilizados com invalidação seletiva e aproximação por célula acessível.
- PASS compilação, 226/226 Edit Mode e 12/12 Play Mode. Console final sem erros. Cena validada via MCP em Play com até 5.000 unidades e streaming de regressão. Evidências e medições em [validação](../../docs/validation/INTEGRATION-002/README.md).
- Fontes: EnvironmentalObstacles, NavigationGrid, NavigationSearch, UnitSession, RtsInteractionDemo e configuração da cena integrada. Novos testes EnvironmentalObstacleTests, com adaptação de destino do teste integrado.
- main/b207bd7, alterações anteriores preservadas, sem commit. Gate cumprido. Próxima ação autorizada: criar ECS-005.
