# ECS-005 — Resource Gathering and Economy Foundation

**Status:** DONE
**Priority:** HIGH
**Owner:** nenhum — concluída
**Branch:** main
**Base commit:** b207bd7
**Last checkpoint:** 2026-11-18 — validação concluída

## Objective

Selecionar aldeões e completar o ciclo de madeira e pedra, da aproximação ao depósito, na EconomyDemo.

## Scope

Estado de recurso e depósito, ordens, capacidade, entrega, esgotamento, atualização lógica e visual, HUD, testes, inspeção e profiling. Exclui persistência externa, construção livre, comércio, combate e tarefas posteriores.

## Dependencies

INTEGRATION-002 concluída, com ocupação ambiental, aproximação e cache seletivo validados.

## Acceptance criteria

- [x] Gather, aproximação, coleta em passo fixo e vários trabalhadores.
- [x] Capacidade, depósito, entrega e contabilidade sem duplicação.
- [x] Esgotamento, invalidação de navegação e atualização visual.
- [x] Move/Stop, streaming e determinismo preservados.
- [x] EconomyDemo e HUD testados.
- [x] Compilação, Console, Edit Mode, Play Mode, inspeção e profiling.
- [x] Documentação e handoff completos.

## Implementation plan

1. Estado econômico e componentes de trabalhador.
2. Ordem Gather e loop fixo de coleta e depósito.
3. Cena, seleção, picking e atualização visual de recursos.
4. Testes, perfil e validação MCP/Unity.

## Checkpoint final

- GatherWorker, ResourceEconomySystem, madeira/pedra, aproximação, carga, depósito, estoque, atualização de ocupação, reconstrução visual, controles e HUD concluídos. Retorno ao recurso, esgotamento e conservação verificados.
- Fontes principais: ResourceEconomy, UnitSession, UnitOrders, RtsInteractionDemo, EnvironmentPresentation, EnvironmentMeshBuilder e ChunkManager. Criados EconomyDemo, ResourceEconomyTests, EconomyDemoTests e metas. ADR-0010 registra política e limites.
- PASS compilação, Console sem erros/avisos, 233/233 Edit Mode, 13/13 Play Mode. Input virtual de Gather, 60 madeira e 80 pedra no estoque, trabalho simultâneo de dois grupos e profiling de 1/10/50/100. Evidências em [validação](../../docs/validation/ECS-005/README.md).
- NOT_RUN mouse físico, build e GPU. Editor em EconomyDemo limpa, Play desligado, zero Worlds de unidades.
- main/b207bd7, alterações locais sem commit. Gate preservado e INTEGRATION-002 continua concluída. Nenhuma próxima tarefa iniciada.

## Validation evidence

| Verificação | Estado |
| --- | --- |
| Gate INTEGRATION-002 | PASS |
| Compilação e Console | PASS |
| Edit Mode | PASS 233/233 |
| Play Mode | PASS 13/13 |
| Cena, input virtual e profiling | PASS |

## Handoff notes

ECS-005 finalizada. A economia dura pela sessão, com estoque persistente ao unload visual. Aguardar autorização expressa para outra tarefa.
