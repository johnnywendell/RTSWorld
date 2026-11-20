# VISUAL-001A — Terrain Alignment, Streaming & LOD Continuity

**Status:** DONE
**Priority:** HIGH
**Owner:** Codex
**Branch:** main
**Base commit:** c690c43
**Last checkpoint:** F — Validação e handoff concluídos

## Objective
Corrigir o apoio visual das construções e garantir vegetação coerente em chunks residentes da VisualStyleDemo.

## Scope
- Alinhamento, fundações, streaming ambiental, LOD, diagnósticos, regressões e benchmarks.
- Fora de escopo: regras ECS, novos assets de produção, VISUAL-002, multiplayer e commits.

## Dependencies
VISUAL-001, NAV-001, NAV-002.

## Acceptance criteria
- [x] Diagnóstico inicial documentado.
- [x] Problema da construção reproduzido.
- [x] Causa do alinhamento identificada.
- [x] Construção corretamente posicionada.
- [x] Política de fundação definida.
- [x] Construções em terrenos inclinados validadas.
- [x] Construções próximas à água validadas.
- [x] Árvores e rochas auditadas.
- [x] Fronteira visual reproduzida.
- [x] Causa da fronteira identificada.
- [x] Estratégia de continuidade implementada.
- [x] LOD auditado.
- [x] Transições de LOD estabilizadas.
- [x] Histerese validada.
- [x] Streaming durante movimento da câmera validado.
- [x] Ausência de duplicação visual verificada.
- [x] Ausência de objetos visuais órfãos verificada.
- [x] Apresentação independente da simulação.
- [x] Save/Load preservado.
- [x] Navegação preservada.
- [x] Combate preservado.
- [x] Economia preservada.
- [x] Construções preservadas.
- [x] Edit Mode aprovado.
- [x] Play Mode aprovado.
- [x] Compilação aprovada.
- [x] Console sem erros relacionados.
- [x] Benchmarks documentados.
- [x] Comparação antes/depois registrada.
- [x] Handoff atualizado.

## Implementation plan
1. A — Diagnóstico e reprodução.
2. B — Alinhamento de construções.
3. C — Streaming e continuidade visual.
4. D — LOD e transições.
5. E — Performance e regressões.
6. F — Documentação e encerramento.

## Checkpoint
### Completed and verified
- VISUAL-001, NAV-001 e NAV-002 confirmadas no repositório. MCP reproduziu o depósito enterrado e o recorte de 24 células.
- Fundação nivelada, LOD ambiental em três níveis e streaming orientado à vista implementados. Capturas comparativas registradas.
- 300/300 Edit Mode, 27/27 Play Mode e Console sem erros ou avisos na revisão final. CSV de 27 configurações e sete capturas.

### In progress
- Nenhum item obrigatório pendente.

### Next smallest action
- Revisar o diff e a VisualStyleDemo. A VISUAL-002 depende de autorização própria.

### Files touched
- Apresentação de ambiente, câmera e streaming. Backend visual e integração de construções. Testes, planejamento, handoff e relatório.

### Git state
- main/c690c43, com alterações anteriores locais preservadas.

### Risks, blockers and decisions
- Os limites de montagem dos chunks permanecem relevantes sob teleporte e zoom rápido. Medir antes de alterar a agenda.

## Validation evidence
| Check | Status | Evidence |
| --- | --- | --- |
| Unity compilation | PASS | Suítes executadas via Unity MCP |
| Edit Mode | PASS | 300/300 |
| Play Mode | PASS | 27/27 na revisão final |
| Console e cena | PASS | VisualStyleDemo limpa, Play desligado, zero erros e avisos |
| Benchmark Editor | PASS | 27 configurações. Referência padrão com 5.000 unidades em 1,928 ms |
| Player, GPU Frame Time e total de batches | NOT_RUN | Contadores sem amostra válida e Player fora da execução de Editor |

## Handoff notes
[Relatório](../../docs/validation/VISUAL-001A/README.md)
