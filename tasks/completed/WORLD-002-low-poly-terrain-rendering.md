# WORLD-002 — Renderização de terreno 3D low-poly

**Status:** DONE
**Dependência:** WORLD-001 — DONE
**Prioridade:** Alta

## Objetivo

Implementar a primeira representação visual 3D do mundo procedural RTSWorld na Unity 6, utilizando os dados determinísticos produzidos pela WORLD-001.

A renderização deverá utilizar meshes geradas a partir dos chunks, mantendo separação entre dados, geração procedural e apresentação visual.

## Escopo

1. Criar um sistema de geração de meshes 3D a partir de `TerrainChunkData`.
2. Utilizar as amostras de elevação para posicionar os vértices do terreno.
3. Gerar triângulos, índices e normais adequados à estética low-poly.
4. Garantir continuidade geométrica entre chunks adjacentes.
5. Permitir configurar a escala horizontal e vertical do terreno.
6. Criar materiais simples e uma apresentação visual inicial do terreno.
7. Criar uma cena de demonstração que permita visualizar múltiplos chunks adjacentes.
8. Implementar uma forma simples de selecionar a seed para testes.
9. Manter a implementação compatível com o pipeline URP da Unity 6.
10. Criar testes automatizados para a geometria gerada, sempre que aplicável.

## Requisitos técnicos

- Utilizar o gerador procedural da WORLD-001 como fonte única dos dados.
- Não duplicar o algoritmo de ruído.
- Não modificar o contrato do gerador v1 sem justificativa e versionamento.
- Não criar um GameObject por célula.
- Preferir uma mesh por chunk, salvo justificativa técnica.
- Separar a construção dos dados geométricos da criação dos objetos Unity.
- Evitar alocações desnecessárias e processamento repetido.
- Definir uma política consistente de normais e sombreamento low-poly, evitando frestas ou artefatos visuais nas bordas.
- Preservar compatibilidade com os futuros sistemas de streaming e biomas.

## Fora do escopo

- Streaming e descarregamento dinâmico de chunks — WORLD-003.
- Biomas completos, florestas, desertos e neve — WORLD-004.
- Oceanos, rios e sistemas de água — WORLD-004.
- Distribuição de árvores e recursos — WORLD-004.
- Câmera RTS definitiva — WORLD-005.
- Construções, unidades, pathfinding e combate.
- Multiplayer, servidor e persistência.

## Critérios de aceite

- [x] A Unity consegue renderizar um terreno 3D gerado a partir de uma seed.
- [x] O terreno utiliza exclusivamente os dados produzidos pela WORLD-001.
- [x] Pelo menos quatro chunks adjacentes podem ser visualizados simultaneamente.
- [x] Os chunks não apresentam frestas geométricas nas bordas.
- [x] A escala horizontal e vertical pode ser configurada sem alterar os dados procedurais.
- [x] A estética inicial utiliza geometria low-poly.
- [x] O sistema não cria um GameObject para cada célula.
- [x] A cena de demonstração funciona no Editor.
- [x] O projeto compila sem erros.
- [x] Os testes automatizados aplicáveis são executados e documentados.
- [x] O checkpoint e o handoff são atualizados.

## Validação

Utilizar Unity MCP para inspecionar a cena, verificar compilação e executar os testes disponíveis.

Registrar resultados como PASS, FAIL ou NOT_RUN, incluindo avisos preexistentes que não tenham relação com a implementação.

## Continuidade

Seguir integralmente o protocolo de `AGENTS.md`.

Registrar decisões arquiteturais, arquivos modificados, resultados dos testes, pendências e próximos passos.

Não iniciar WORLD-003 automaticamente.

## Plano incremental e checkpoint inicial
- Responsável Codex (/root), único escritor; master/a1c205d. Alterações preexistentes .vscode e movimentações ENV-001/WORLD-001 preservadas. Permissão corrigida pelo usuário e reconfirmada; bloqueio encerrado.
- Etapas: geometria e testes; apresentação/demo URP; compilação e testes; inspeção visual Editor/Play; auditoria e conclusão.
- Decisão local: vértices separados por triângulo e normais geométricas planas, mesma diagonal em todas as células. Facetas intencionais tanto dentro quanto entre chunks; sem médias truncadas na borda. Uma mesh por chunk, UInt32 quando exceder 65535 vértices. Coordenadas locais de apresentação separadas das coordenadas globais do domínio.
- Nenhum código novo ainda; compilação/testes/visualização WORLD-002 NOT_RUN. Próxima ação: implementar geometria independente de GameObjects, sem modificar v1. Sem bloqueadores restantes.

## Checkpoint — geometria e apresentação implementadas
- Criados Presentation/Terrain (TerrainGeometry, TerrainMeshFactory, TerrainDemo), inspector em Presentation/Editor e testes em Tests/Presentation. Arrays dimensionados uma vez por build; geração/rebuild explícitos, sem Update e sem GameObject por célula; meshes próprias liberadas em rebuild/disable. Demo fixa 2x2, meshes temporárias DontSave regeneradas ao abrir cena/Play.
- Normais flat calculadas por face, política uniforme independente de partição; UInt32 para meshes grandes. Escalas finitas limitadas 0.001..10000. WORLD-001 intacta.
- Compilação/testes novos NOT_RUN neste checkpoint. Próxima ação: importar/compilar e executar testes; criar material e cena após sucesso. Git master/a1c205d, documentação modificada e Presentation/Tests novos, alterações anteriores preservadas. Sem bloqueador.

## Checkpoint — demo e validação concluídas
- Cena Assets/_Project/Scenes/TerrainDemo.unity e material URP Lit TerrainSage criados pelo Editor; câmera ortográfica fixa, Directional Light e Terrain Demo. Quatro objetos chunk com 6144 vértices/2048 triângulos cada, posições locais (-64,0,-64), (0,0,-64), (-64,0,0), (0,0,0). Dados v1 intactos.
- PASS: compilação (início/fim Unix ms 1794530917461/1794530920317), 44/44 EditMode (29 regressão +15 novos), job 83b1f7f84b6044f89d926d7b5439e642, 0 falhas/ignorados, 2.1228053s.
- PASS: Play Mode real, seeds 12345 e 67890 visualizadas, retorno ao Editor restaura seed 12345 e quatro renderers; reabertura da cena reconstrói quatro chunks. Capturas em docs/validation/WORLD-002; Scene View e captura final Editor inspecionadas, terreno visível com material verde e sem frestas de chunk visíveis. Primeira captura imediata após criar cena estava escura; capturas posteriores Editor/Play mostram iluminação correta.
- Console antes/depois da compilação: Warning WebSocket preexistente, sem erros. Após Play/stop/reabertura: 0 erros/avisos retornados; agente não limpou Console (Unity pode limpar ao entrar em Play conforme preferência existente).
- URP Editor adicionou automaticamente m_ProjectSettingFolderPath: URPDefaultResources em ProjectSettings/URPProjectSettings.asset; registrar esta alteração real, sem ocultar. Nenhum pacote instalado.
- Próxima ação: documentar contrato/uso/limitações, auditar metadados e checkpoints, mover tarefa a completed se critérios permanecerem satisfeitos. Nenhum bloqueador; WORLD-003 não iniciada.

## Resultado final e arquivos
- DONE — todos os critérios acima atendidos. Nenhuma parte do escopo ficou parcialmente implementada. Revisão local de implementação compatível com arquitetura existente, sem mudança do algoritmo v1 e sem nova dependência.
- Criados: `Assets/_Project/Presentation/RTSWorld.Presentation.asmdef`; `Terrain/TerrainGeometry.cs`, `TerrainMeshFactory.cs`, `TerrainDemo.cs`; `Editor/RTSWorld.Presentation.Editor.asmdef` e `TerrainDemoEditor.cs`; `Assets/_Project/Tests/Presentation/RTSWorld.Presentation.Tests.asmdef` e `TerrainGeometryTests.cs`; `Assets/_Project/Materials/TerrainSage.mat`; `Assets/_Project/Scenes/TerrainDemo.unity`; respectivos .meta e diretórios; cinco capturas em `docs/validation/WORLD-002/`.
- Modificados: docs/ARCHITECTURE.md, CURRENT_STATE.md, HANDOFF.md, tasks/ACTIVE.md, BACKLOG.md e esta tarefa (movida pending → in_progress → completed). Alteração automática do Editor em ProjectSettings/URPProjectSettings.asset descrita acima. Checkpoints anteriores e .vscode preservados. Sem commit.

## Matriz final de evidências
| Verificação | Resultado | Evidência |
| --- | --- | --- |
| Compilação | PASS | refresh_unity compile=request; editor/state is_compiling=false e ready_for_tools=true após fim/reload; tipos novos executados no Editor e testes. |
| Testes EditMode | PASS | Job 83b1f7f84b6044f89d926d7b5439e642: succeeded, 44/44, 0 failed, 0 skipped; 2.1228053 s. |
| Regressão v1 | PASS | 29 testes WORLD-001 incluídos; diff de World e Tests/EditMode vazio. |
| Geometria/índices/normais | PASS | 3 casos TopologyHasValidUpwardFacesAndFlatUnitNormals (N1/32/256), 2 NativeMeshUsesCorrectIndicesAndBounds, incluindo UInt32. |
| Continuidade geométrica e de política de normais | PASS | 3 SharedEdgesHaveIdenticalWorldPositions (inclui negativos) e PartitioningDoesNotChangeFaceNormalsOrGeometry. Normais de faces iguais entre grade única e partição; arestas sem frestas nas capturas. |
| Escalas e validação | PASS | ScaleChangesGeometryWithoutChangingSourceData e 4 InvalidScalesAreRejected. |
| Rebuild/seed/ownership | PASS | DemoRebuildReplacesFourObjectsAndReleasesOwnedMeshes; inspeção real após reabrir cena: seed12345, 4 chunks/4 renderers. |
| Material e hierarquia | PASS | Shader URP Lit localizado via MCP e material persistente; get_hierarchy retornou 3 roots e 4 filhos nas posições registradas; meshes 6144 vertices/2048 triângulos cada. |
| Visual Editor | PASS | editor-seed-12345-final.png e scene-view.png capturados via MCP e inspecionados pelo agente; terreno verde iluminado, facetas e superfície contínua. Primeira captura editor-seed-12345.png estava escura imediatamente após criação; preservada como diagnóstico, não usada isoladamente como aprovação. |
| Visual Play Mode | PASS | Entrou/saiu Play via MCP; play-seed-12345.png e play-seed-67890.png inspecionadas, mudanças de relevo visíveis, quatro chunks simultâneos; retorno restaura seed salva. |
| Console | PASS | Sem erros novos durante desenvolvimento; leitura final retornou 0 erros/avisos. Warning WebSocket existia antes e após compile. Console não foi limpo pelo agente; preferências do Editor podem limpar ao entrar Play. Não declarar bug MCP corrigido. |
| Build/player/performance/outros alvos | NOT_RUN | Fora da validação deste demonstrador; sem promessa de escalabilidade/desempenho medido. |

## Como abrir e usar
1. Abra `Assets/_Project/Scenes/TerrainDemo.unity` na Unity 6000.6.9f1.
2. Veja Game e pressione Play para usar a câmera fixa; os quatro chunks são reconstruídos automaticamente. Em Scene, selecione Terrain Demo e use F para enquadrar.
3. No Inspector de Terrain Demo altere Seed, Cells Per Chunk, Horizontal Scale ou Vertical Scale, depois clique **Rebuild Terrain**. Não há reconstrução a cada frame. Configuração padrão seed12345/N32/escalas2 e1.2. Fora de Play, salve a cena para persistir configuração; em Play, as alterações são temporárias.
4. Testes em Unity Test Runner/EditMode ou MCP run_tests com assemblies RTSWorld.World.Tests e RTSWorld.Presentation.Tests.

## Limitações e próxima ação
- Demonstração fixa 2x2, sem streaming, collider, biomas, água, gameplay ou câmera RTS definitiva. Flat shading aumenta vértices; caminho UInt32 validado mas nenhuma medição de performance. Floating origin de apresentação distante ainda não implementado.
- Mudança de escala/seed/dimensão exige Rebuild; a câmera é fixa e pode precisar reenquadrar com escalas extremas. Primeiro frame de uma captura imediata pode preceder atualização da iluminação do Editor; capturas finais Editor/Play mostram renderização correta.
- Nenhuma pendência bloqueadora nos critérios. Aguardar revisão/instrução do usuário, sem iniciar WORLD-003. Próximo agente deve reler AGENTS/estado/handoff/ACTIVE/tarefa, verificar Git real e preservar todos os arquivos não commitados.

## Auditoria final do checkpoint
PASS — script de auditoria confirmou .meta de todos os arquivos/diretórios novos, GUIDs únicos, asmdefs válidos, fontes sem espaços finais, WORLD-002 DONE e caminhos coerentes, nenhum critério desmarcado, arquivo somente em completed. Comparação byte a byte confirmou checkpoints ENV-001/WORLD-001 intactos; diff de World/Tests/EditMode/Packages/SampleScene vazio; WORLD-003 PENDING. Cinco capturas presentes. `git diff --check`: PASS. Console final MCP: 0 erros/avisos. Base final master/a1c205d302f28b9b3a983e8fdc2527a1187a4905, sem commit; arquivos novos e docs ainda no workspace conforme inventário acima. Outro agente dispõe de instruções de abertura, testes, limitações e próximo passo neste arquivo e HANDOFF, sem precisar da conversa.
