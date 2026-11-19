# ECS-004 — RTS Unit Selection and Commands

**Status:** DONE
**Priority:** HIGH
**Owner:** nenhum — concluída
**Branch:** main
**Base commit:** b207bd7
**Last checkpoint:** 2026-11-18 — aceite validado

## Objective

Controlar aldeões e soldados ECS na RTSInteractionDemo com a câmera RTS existente, seleção por mouse e ordens Move e Stop.

## Scope

- Clique, retângulo, Shift aditivo e Ctrl alternável, oclusão, feedback instanciado e marcador de destino.
- Ordens validadas pelo domínio, distribuição navegável com espaçamento configurável, substituição de destino e Stop.
- Streaming visual, diagnósticos, testes Edit Mode e Play Mode, profiling e inspeção MCP.
- Exclui economia, combate, construções, rede, persistência, formações avançadas e tarefas posteriores.

## Dependencies

WORLD-001 a WORLD-005 e ECS-001 a ECS-003 concluídas. Reutilizar Unity Input System, câmera WORLD-005, sessão ECS e cache de fluxo ECS-003.

## Architecture requirements

Input e picking na apresentação. Seleção por identidade lógica local ao jogador. Comandos validados na sessão, independentes de câmera e residência visual. Reutilizar campo ao destino coletivo e seus caminhos para a distribuição. Nenhum GameObject por unidade.

## Acceptance criteria

- [x] Clique individual e limpeza em terreno vazio.
- [x] Retângulo com zoom, rotação e inclinação.
- [x] Shift/Ctrl e seleção independente de GameObjects individuais.
- [x] Feedback visual de seleção e destino.
- [x] Move, Stop e substituição.
- [x] Rejeição de destinos bloqueados, água, fora da janela e inalcançáveis.
- [x] Distribuição navegável compartilhando dados ECS-003.
- [x] Integração com câmera WORLD-005 e streaming.
- [x] Cena RTSInteractionDemo e indicadores funcionais.
- [x] Compilação, Console, Edit Mode e Play Mode aprovados.
- [x] Inspeção visual/interativa MCP.
- [x] Profiling ou limitação registrada.
- [x] Suítes anteriores sem regressões.
- [x] Arquitetura, validação e handoff atualizados.

## Implementation plan

1. Auditar câmera, superfície, UnitSession e testes.
2. Implementar contrato de comandos e distribuição compartilhada.
3. Implementar seleção projetada com picking e oclusão, input e indicadores.
4. Integrar cena, câmera, renderização e streaming.
5. Verificar seleção, modificadores, ordens, rejeições, chegada, streaming e escalas de 100/500/1000/5000 unidades.
6. Executar suítes, inspeção e profiling. Mover para completed somente com os critérios atendidos.

## Checkpoint final — 2026-11-18

- Implementados `UnitOrder`, `UnitSession.ApplyOrder`, distribuição por grade alcançável, campo compartilhado e caminho final por buffer de waypoints. Move rejeitado preserva intenção. Stop e substituição foram verificados.
- `RtsInteractionDemo`, `RtsSelection` e `RtsSurfaceRay` integram input, câmera e streaming, seleção por ID, feedback instanciado e diagnósticos. A fonte da câmera permaneceu intacta. A seed local de RTSCameraDemo e as alterações ECS-003 foram preservadas.
- `UnitOrdersTests`, `RtsSelectionTests` e `RtsInteractionTests` cobrem a interação, o domínio, a escala e a cena. ADR-0007 e arquitetura documentam o contrato. A matriz, captura e CSV estão em [docs/validation/ECS-004](../../docs/validation/ECS-004/README.md).
- **Git** main/b207bd7, alterações locais sem commit. `git diff --check` PASS. Tarefa concluída, nenhuma outra ativa.

## Validation evidence

| Check | Status | Evidence |
| --- | --- | --- |
| Compilação e Console | PASS | Unity 6000.6.9f1, zero erros ou avisos na verificação final |
| Edit Mode | PASS | 221/221, 40,9610971 s, zero falhas ou ignorados |
| Play Mode | PASS | 11/11, 2,3597547 s, zero falhas ou ignorados |
| Interação Play via MCP | PASS | Input virtual, clique, modificadores, retângulo, Move, Stop, 100 posições distintas e borda |
| Profiling | PASS | 100/500/1.000/5.000. Seleção real com oclusão a 5.000 = 26,64 ms em média na amostra do Editor |
| Entrada física, player e comparações de plataforma | NOT_RUN | roteiro de entrada física disponível no relatório |

## Handoff notes

O Editor ficou na RTSInteractionDemo em Edit Mode, cena limpa e zero Worlds de unidades. Resultados completos em Logs, resumo versionável no relatório. A formação passa pelo destino coletivo antes de distribuir e pode congestionar nesse trecho. Nenhuma tarefa posterior iniciada.
