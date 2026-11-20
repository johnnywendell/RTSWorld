# VISUAL-001 — Art Direction & Rendering Architecture

**Status:** DONE  
**Priority:** HIGH  
**Owner:** Codex  
**Branch:** main  
**Base commit:** c690c43  
**Last checkpoint:** F — Validação concluída

## Objective
Exibir uma vila medieval jogável com protótipos low-poly e comparar custo e legibilidade de três modos de apresentação.

## Scope
- Inclui: cena, escala, modelos, materiais, animação experimental, LOD, auditoria, testes e profiling.
- Exclui: multiplayer, VISUAL-002 e posteriores, mudanças de regras, atualização de pacotes, arte final.

## Dependencies
- WORLD-001 a WORLD-005, ECS-001 a ECS-010, INTEGRATION-001/002 e NAV-001/002.

## Acceptance criteria
- [x] Auditoria gráfica concluída.
- [x] Direção artística documentada.
- [x] Escala visual definida.
- [x] Arquitetura ECS de apresentação definida.
- [x] Backend escolhido e justificado.
- [x] Comparação técnica documentada.
- [x] Protótipo de aldeão humanoide.
- [x] Protótipo de soldado humanoide.
- [x] Animação experimental Idle.
- [x] Animação experimental Walk.
- [x] Protótipos de árvores.
- [x] Protótipo de rocha.
- [x] Protótipo visual de depósito.
- [x] Protótipo visual de centro principal.
- [x] Materiais URP compartilhados.
- [x] Iluminação diurna configurada.
- [x] Estratégia de LOD definida.
- [x] Protótipo de LOD validado.
- [x] Cena VisualStyleDemo funcional.
- [x] Seleção funcional.
- [x] Move funcional.
- [x] Stop funcional.
- [x] Navegação preservada.
- [x] Simulação independente da apresentação.
- [x] Testes Edit Mode aprovados.
- [x] Testes Play Mode aprovados.
- [x] Compilação sem erros.
- [x] Console sem erros relacionados.
- [x] Benchmark executado.
- [x] Documentação artística criada.
- [x] Pipeline de assets documentado.
- [x] ADR registrado.
- [x] Handoff atualizado.

## Implementation plan
1. A — auditar código, assets e versões reais.
2. B — definir snapshot e comparar instancing, MeshRenderer e Entities Graphics.
3. C — construir malhas e paleta URP.
4. D — integrar na cena e conferir controles e distâncias.
5. E — perfil pareado, LOD e culling.
6. F — regressões, ADR, relatório e handoff.

## Checkpoint
### Completed and verified
- A: auditados a renderização de unidades e chunks, URP 17.6.0, Entities 6.6.0 e ausência de Entities Graphics.
- B/C/D: projeção ECS, renderizadores, protótipos e VisualStyleDemo em execução. MCP registrou comparação visual, três distâncias, LOD e unload/reload de chunk com cardinalidade ECS constante.
- E: benchmark executado nas cinco populações. CSV final e limitações registrados.
- F: Edit Mode 296/296 e Play Mode 26/26. Console final sem erros ou avisos. Unity fora de Play na VisualStyleDemo.

### In progress
- Nenhum item da VISUAL-001 pendente.

### Next smallest action
- Revisar o trabalho local e executar manualmente a VisualStyleDemo para aprovação visual quando solicitado.

### Files touched
- `Assets/_Project/Presentation/Units/Visual/`, integração de `RtsInteractionDemo` e streaming de chunks.
- Cena `VisualStyleDemo.unity`, pipeline e material VisualStyle em `Assets/_Project/Materials/`.
- `Assets/_Project/Tests/SimulationPlayMode/VisualStyleDemoTests.cs`.
- `docs/visual/`, `docs/validation/VISUAL-001/`, ADR-0013 e planejamento.

### Git state
- `main` em `c690c43`, sincronizada com `origin/main`. Alterações locais anteriores e da VISUAL-001. Sem commits.

### Risks, blockers and decisions
- Entities Graphics ausente. GPU Frame Time e lotes totais sem amostra válida no OpenGL do Editor. Benchmarks do Editor não definem desempenho de player.
- Culling de unidade faz teste de frustum e submissões não são agrupadas por região. A câmera pode acabar desenhando lotes com instâncias fora do quadro se o recorte da aplicação estiver desligado.

## Validation evidence
| Check | Status | Evidence |
| --- | --- | --- |
| Unity compilation | PASS | Importação, execução da suíte e entrada em Play |
| Edit Mode | PASS | 296/296 via Unity MCP |
| Play Mode | PASS | 26/26 na rodada final via Unity MCP |
| Console | PASS | Zero erros e avisos na inspeção final |
| Benchmark | PASS | 20 configurações em cinco populações |
| Editor/MCP | PASS | Câmera, LOD, streaming, captura e Gather/Stop |
| Entities Graphics, GPU Frame Time e batches totais | NOT_RUN | Pacote ausente e contadores sem amostra válida |

## Handoff notes
[Relatório VISUAL-001](../../docs/validation/VISUAL-001/README.md)
