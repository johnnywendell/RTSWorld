# WORLD-003 — Carregamento dinâmico de chunks

**Status:** DONE
**Priority:** HIGH
**Owner:** Codex (/root); encerrado, sem escritor ativo
**Branch:** master
**Base commit:** 607e22a (Complete WORLD-002 low-poly terrain rendering)
**Last checkpoint:** conclusão validada, 2026-11-12 (America/Bahia)

## Objective
Navegar por terreno procedural carregando somente uma janela de chunks ao redor de um observador, com carregamento incremental, descarregamento e recursos limitados à região ativa. Reutilizar dados WORLD-001 e geometria WORLD-002; simulação/estado autoritativo não dependem dos objetos visuais.

## Scope
- Included: seleção lógica pura, conversão de posição com floor, ChunkManager, fila com orçamento por frame, liberação de recursos, métricas e demo própria de observador móvel; testes e evidências.
- Excluded: WORLD-004/WORLD-005, biomas/água/vegetação/recursos, câmera RTS definitiva, LOD completo, multiplayer, servidor, persistência, unidades/pathfinding/combate, Jobs/async/pooling antecipados.

## Dependencies
- WORLD-001 e WORLD-002 — DONE, código e checkpoints inspecionados.

## Requisitos técnicos e decisões locais
- Fonte única TerrainGenerator/TerrainChunkData; gerador v1 e contratos existentes intactos. Reutilizar TerrainGeometry/TerrainMeshFactory.
- Região quadrada de raio Chebyshev r em chunks: (2r+1)^2, r=0..8; ordem por anéis do centro para fora. Limite explícito evita seleção sem limite; coordenadas nos extremos int recortam a janela.
- Seleção pura sem UnityEngine, separada do manager de apresentação. Floor em coordenadas lógicas para negativos, transformação mundo→local para observador.
- Dicionário ativo e fila de faltantes reconstruída só em mudança de centro/raio; sobreposição preservada, requests antigos descartados. Sem cache de chunks descarregados ou referências a dados imutáveis após upload; estado persistente ainda não existe e não será vinculado a GameObjects.
- Orçamento por quantidade e tempo entre chunks; geração de um chunk é síncrona e não preemptível. Registrar tempo de geração/upload por Step e contadores. Recursos nativos próprios liberados em unload/disable; uma mesh/objeto por chunk.
- Cena nova preserva TerrainDemo. Observador por Inspector e controles simples no Play; seed/raio e contagem visíveis. Precisão float distante da origem é limitação de apresentação, sem mudar coordenadas globais do domínio.

## Acceptance criteria
- [x] Posição→chunk usa floor e funciona em positivos/negativos/fronteiras.
- [x] Seleção de raio configurável produz conjunto correto e único.
- [x] Transições e teleportes removem chunks distantes, preservam sobreposição e cancelam fila antiga; imóvel não regenera.
- [x] Budget limita número de gerações por Step e interrompe entre chunks ao exceder tempo; métricas reais registradas.
- [x] Unload/disable libera meshes/objetos próprios e remove referências; memória ativa não cresce com trajeto.
- [x] Geometria contínua e gerador v1 preservado, testes anteriores e novos executados.
- [x] Demo permite observador móvel, seed/raio configuráveis, contagem, renderização Editor/Play verificada; TerrainDemo preservada.
- [x] Compilação/Console e validações reais PASS/FAIL/NOT_RUN registradas.
- [x] Checkpoints e documentos coerentes, tarefa no diretório correspondente; tarefas posteriores não iniciadas.

## Implementation plan
1. Seleção pura e testes de posição/raio/fronteiras.
2. Manager incremental, ownership e testes de transição/limpeza/orçamento/continuidade.
3. Demo própria, controles, inspector e métricas; compilar e executar regressão.
4. Validar Editor/Play, deslocamento/seed/raio, capturas e medidas reproduzíveis.
5. Auditar critérios, documentar limites e encerrar sem tarefa posterior.

## Estratégia de testes
NUnit EditMode para seleção e integração do manager (sem depender de frame timing absoluto); PlayMode para descarte diferido e transições reais. Reexecutar 44 testes anteriores. Medir Step em percurso fixo (N32, raio1, limites por frame explícitos), sem declarar FPS/ausência de hitch sem medir. Capturas inspecionadas pelo agente; sem aprovação visual baseada só em compilação.

## Checkpoint
### Completed and verified
- Todos os documentos solicitados e implementação anterior consultados; Git master/607e22a, somente .vscode preexistente não rastreado. WORLD-003 não existia.
- MCP confirmou raiz, Unity6000.6.9f1, TerrainDemo salva. Editor estava em Play ao iniciar; parado antes de alterações. Console sem erros, Warning WebSocket preexistente.
- Documentos antigos ainda relatavam código WORLD-002 sem commit; HEAD real agora contém conclusão WORLD-002. Discrepância verificada e registrada.
### In progress
- Registro inicial; nenhum código novo ainda.
### Next smallest action
- Implementar ChunkSelection puro e testes; depois manager incremental.
### Files touched
- Esta tarefa, ACTIVE, BACKLOG, CURRENT_STATE e HANDOFF.
### Git state
- master/607e22a; sem commit; .vscode preservado. Documentos acima alterados; novo arquivo de tarefa.
### Risks, blockers and decisions
- Sem bloqueadores. Budget temporal não é teto rígido de um chunk síncrono. Sem novas dependências ou mudança arquitetural maior.

## Validation evidence
| Check | Status | Evidence |
| --- | --- | --- |
| Inicialização Git/MCP | PASS | status/log, instances/project/info, get_active e Console reais |
| Unity compilation | NOT_RUN | Código novo ainda não criado |
| Automated tests | NOT_RUN | — |
| Editor/MCP test | NOT_RUN | Demo nova ainda não criada |
| Play Mode | NOT_RUN | — |

## Handoff notes
Retomar lendo AGENTS/estado/handoff/ACTIVE/esta tarefa e Git/Unity reais. Preservar .vscode e checkpoints WORLD-001/002. Não criar/iniciar WORLD-004 ou WORLD-005, nem qualquer tarefa posterior.

## Checkpoint — seleção e ciclo de vida implementados
- Criados World/Chunks/ChunkSelection.cs, Presentation/Streaming/ChunkManager.cs e StreamingDemo.cs, inspector ChunkManagerEditor.cs; testes de seleção, manager e ciclo de vida PlayMode. Dados e gerador v1 intactos; TerrainDemo não alterada.
- Seleção floor, janela quadrada/anéis sem duplicatas; fila refeita somente em transição/raio, sobreposição preservada e fila obsoleta cancelada. Budget por contagem/tempo e métricas Step; meshes/objetos próprios removidos sem cache de dados após upload.
- Parcial: código escrito, ainda sem cena nova e sem compilação/testes executados (NOT_RUN). Próxima ação: refresh/Console e testes EditMode, corrigir falhas reais antes da demo.
- master/607e22a, .vscode preservado, docs de continuidade modificados, novos scripts/testes/metas a importar. Sem bloqueador.

## Checkpoint — compilação e testes iniciais
- Primeira compilação FAIL: CS0012 no Inspector novo por falta de referência direta a RTSWorld.World; asmdef Editor corrigido. Primeiro job 5362baa2aff540fa91deb0e568dc1037 executou somente 44 testes antigos durante erro e não serve como aceite da implementação nova.
- Após correção: PASS 63/63 EditMode (44 anteriores +19 novos), job3f5c9546c81a4fd89c0f08c80ab4ecc5, 0 falhas/ignorados,3.2166926s. PASS 2/2 PlayMode jobd57ff0faa18744519318cfb12ab2294d,0 falhas/ignorados.
- Percurso Play reproduzível N32,r1,max1/budget4ms,12 posições,70 amostras: Step mediana0.4647ms,p950.8632ms,máximo2.6884ms; frame observado máximo11.5218ms;70 gerados/61 removidos,9 meshes nativas em cada região estabilizada e retorno ao baseline após disable. Medida local no Editor/Test Runner, não garantia de FPS/player.
- Acrescentado depois um teste de orçamento temporal com N256/max8/budget0.1ms; ainda NOT_RUN neste checkpoint. Próxima ação: compilar/testar adição e criar demo/capturas. Sem outros bloqueadores. master/607e22a, arquivos conforme inventário anterior mais asmdef Editor corrigido.

## Checkpoint — demo e validação visual
- Criada StreamingDemo.unity pelo Unity MCP; TerrainDemo original sem diff. Novos objetos persistentes: Test Observer, Chunk Stream, Main Camera, Directional Light, Streaming Demo Controls; recursos gerados DontSave. Compartilha TerrainSage.
- PASS 64/64 EditMode final, job38f60fc7dea947c6a340508c9216e1c5,3.226744s,0 falhas/ignorados; inclui teste temporal N256/max8/budget0.1ms. PlayMode 2/2 já validado; evidências brutas salvas em docs/validation/WORLD-003/test-results.json.
- PASS visual real Editor/Play. Em Play: origem centro(0,0),9 ativos/0 fila,9 gerados; observador(-192,0,128) → centro(-3,2),9 ativos,18 gerados/9 removidos; seed67890/raio2 →25 ativos,43 gerados/18 removidos; reduzir raio0 →1 ativo,43 gerados/42 removidos. Capturas com overlay confirmam mudanças visuais e quantidade.
- Retorno ao Editor restaurou seed12345/raio1/centro(0,0)/9 ativos; deslocamento no Editor para(64,0,-64) validado. Console após Play: 0 erros/avisos; Warning MCP preexistente não tratado como resolvido e Console não limpo pelo agente.
- Máquina medida: Intel Core i5-14600KF,20 processadores lógicos,RTX4070SUPER, Unity6000.6.9f1/LinuxEditor. Medidas de Step não são benchmark de player/FPS nem garantia para N256/raio8.
- Próxima ação: revisão final do código/metadados, documentar limites e instruções, concluir somente após auditoria. master/607e22a, docs/asmdef Editor modificados; novos scripts/testes/cena/capturas/JSON e .vscode preexistente. Nenhum bloqueador; sem tarefas posteriores.

## Correção após validação Editor
- A primeira tentativa de mover o observador por ferramenta no Editor não atualizou automaticamente o centro (permaneceu0,0); registro anterior de PASS de deslocamento Editor era prematuro. Step explícito produziu centro(1,-1), mas a fila ficou pendente porque ExecuteAlways.Update não é um loop contínuo no Editor.
- Corrigido: EditorApplication.update registrado em OnEnable e removido em OnDisable, exclusivamente fora de Play; Update normal processa apenas Play, evitando tick duplo. Step sem QueuePlayerLoopUpdate; repaint somente quando gerar.
- Esta correção ainda NOT_RUN; próxima ação: restaurar observador, compilar, reexecutar regressão e Play, então confirmar fila Editor automaticamente. Sem bloqueio externo, tarefa permanece IN_PROGRESS.

## Checkpoint final — implementação e validação completas
- Concluído: seleção pura; manager incremental; Editor callback corrigido; demo independente; testes e medidas; liberação de recursos; documentação. Ajuste final em Clear zera LastGeneratedCount para não manter diagnóstico/repaint de uma geração anterior quando referências são removidas.
- Nenhum escopo parcialmente implementado e nenhum bloqueio restante. Limitações abaixo são explícitas, não funcionalidades prometidas como concluídas.
- Correção Editor confirmada sem Step manual: mover observador(64,0,-64) → centro(1,-1),9 ativos/0 fila,14 gerados/5 removidos; retorno à origem →9 ativos/0 fila,19 gerados/10 removidos. Reabertura da cena reconstrói configuração salva. Código final recompilado e ambas as suítes reexecutadas após último ajuste.

### Arquivos criados
- `Assets/_Project/World/Chunks/ChunkSelection.cs`.
- `Assets/_Project/Presentation/Streaming/ChunkManager.cs` e `StreamingDemo.cs`.
- `Assets/_Project/Presentation/Editor/ChunkManagerEditor.cs`.
- `Assets/_Project/Scenes/StreamingDemo.unity`.
- `Assets/_Project/Tests/EditMode/ChunkSelectionTests.cs`, `Tests/Presentation/ChunkManagerTests.cs`, `Tests/StreamingPlayMode/RTSWorld.Streaming.PlayMode.Tests.asmdef` e `StreamingLifecycleTests.cs`.
- Todos os .meta de arquivos/diretórios novos gerados pelo Unity.
- `docs/validation/WORLD-003/`: seis capturas PNG, JSONs test-results/final-test-results (histórico) e release-test-results (código final).
- Esta tarefa foi criada com template em pending, movida a in_progress antes de implementar e a completed na conclusão.

### Arquivos modificados
- `Assets/_Project/Presentation/Editor/RTSWorld.Presentation.Editor.asmdef` (referência direta a World necessária pelo novo Inspector).
- `docs/ARCHITECTURE.md`, CURRENT_STATE.md, HANDOFF.md, `tasks/ACTIVE.md`, BACKLOG.md e esta tarefa.
- Nenhum arquivo existente de geração v1, geometria, TerrainDemo, material, testes anteriores, pacote ou ProjectSettings alterado.

## Matriz final de verificação
| Verificação | Resultado | Evidência real |
| --- | --- | --- |
| Compilação final | PASS | refresh_unity compile=request, Console sem erros após compilação; tipos novos executados pelo Editor; suítes finais executadas. CS0012 inicial corrigido e mantido no checkpoint histórico. |
| EditMode final | PASS | Job d1738a76eae24624b51bd30ce2707fe2:64/64,0 failed/0 skipped,2.1016397s. |
| Regressão anterior | PASS | 44 testes WORLD-001/002 incluídos; fontes/contratos v1 e meshes originais sem diff. |
| Conversão/seleção | PASS |11 casos ChunkSelectionTests:5 posição/floor/fronteiras;4 raios;extremos int;entradas inválidas. |
| Streaming/ownership/budget | PASS |9 casos ChunkManagerTests:mesmo centro/contagem por Step;orçamento temporal real N256;transição/sobreposição;teleporte/fila cancelada;raio/seed;20 deslocamentos/disable;continuidade negativa;root transform/escalas;configuração inválida. |
| PlayMode final | PASS | Job6fd9129e575c4836a219d5554c68f153:2/2,0 failed/0 skipped,0.3261091s. Destroy nativo após frame e percurso incremental com região limitada. |
| Memória visual limitada | PASS | No percurso,9 meshes nativas após estabilizar em cada uma das12 posições;70 geradas/61 removidas;após disable volta ao baseline. Não é profiling do heap/VRAM nem prova de ausência absoluta de todo tipo de leak. |
| Medidas de custo local | PASS |70 amostras:Step mediana0.4476ms,P950.6993ms,máximo2.4668ms;maior frame observado10.0242ms. Configuração/ambiente e limites abaixo. |
| Editor automático | PASS | Callback EditorUpdate confirmado por movimento sem Step manual e fila convergindo9/0; inspeção MCP de9 filhos/posições corretas. Captura editor-after-move-final.png inspecionada. Falha anterior do loop ExecuteAlways registrada/corrigida. |
| Visual Play | PASS | Capturas play-origin,play-moved-negative,play-radius2-seed67890,play-radius0 inspecionadas;terrenos/mudança de janela,25→1 ativos e controles/contagens visíveis;sem frestas entre chunks estabilizados. |
| Console | PASS | Final sem erros/avisos;Warning WebSocket preexistente antes/após compilação, não considerado corrigido. Agente não limpou Console; preferência Unity pode limpar ao entrar Play. |
| Build/player/heap/VRAM/outros alvos | NOT_RUN | Fora desta validação;nenhuma afirmação de desempenho de player ou escala máxima. |

## Medição reproduzível e limites
- Rodar `RTSWorld.Streaming.PlayMode.Tests.StreamingLifecycleTests.RouteKeepsNativeMeshCountBoundedAndReportsTimings` no Test Runner/PlayMode ou MCP. Rotina12 posições: X=(waypoint-6)*64, Z=(waypoint%3-1)*64, Y0; aguardar fila esvaziar em cada uma,mais10 frames imóvel.
- Seed12345,geradorv1,N32,horizontal2,vertical1.2,raio1,max1 geração/frame,budget4ms. Unity6000.6.9f1/LinuxEditor,Intel i5-14600KF(20 processadores lógicos),RTX4070SUPER. Resultados brutos dos jobs finais em release-test-results.json; variação entre execuções anteriores preservada nos JSONs/checkpoints.
- Teste verifica bound de recursos e gera medidas; não estabelece garantia de FPS/ausência de hitches. Chunk síncrono grande pode ultrapassar budget; unload não é preemptível. Combinações N256/raio8 e player não foram benchmarkadas; memória gerenciada/VRAM detalhadas NOT_RUN.
- Raio é quadrado Chebyshev0..8, não círculo; raio1=9 chunks e raio2=25. Nos limites int a janela recorta coordenadas inválidas. Teleport pode deixar buracos temporários até carregar fila. Não há histerese/pooling/cache/async/LOD.
- Precisão visual float distante da origem é limitação; coordenadas lógicas e seed/versionamento permanecem separados. Não houve implementação de floating origin, estado mutável autoritativo ou persistência. Descarregamento só remove representação visual.

## Como visualizar e retomar
1. Abrir `Assets/_Project/Scenes/StreamingDemo.unity` na Unity6000.6.9f1. Configuração salva:seed12345,N32,escalas2/1.2,raio1,max1,budget4ms,Test Observer na origem. Aguardar fila=0:9 chunks.
2. No Play/Game, usar botões X-/X+/Z-/Z+ para passos64; Raio-/Raio+ e Seed+ alteram parâmetros. Painel mostra centro,ativos,fila,gerados/removidos e tempoStep/pico. Câmera acompanha apenas por offset fixo,não é câmera RTS.
3. Alternativamente mover Test Observer pelo Inspector, inclusive fora de Play. Selecionar Chunk Stream para mudar seed/raio/escalas/budget e ver diagnóstico; alterações são detectadas pelo manager. Botão processar um frame é ferramenta manual; limpar apresentação esvazia recursos, mas componente habilitado volta a carregar no tick seguinte.
4. Fora de Play,salvar cena persiste parâmetros/observador; objetos/meshes gerados DontSave nunca são persistência do mundo. Em Play mudanças são temporárias. TerrainDemo original permanece disponível e sem diff.
5. Testes:EditMode assemblies RTSWorld.World.Tests/RTSWorld.Presentation.Tests;PlayMode assembly RTSWorld.Streaming.PlayMode.Tests. Consultar Console sem apagá-lo.

## Git e próxima menor ação
- Base/HEAD final:`master/607e22a0add91072b406c6a3b35f12bf168de237`;nenhum commit criado. Preexistente `.vscode/` preservado. Mudanças desta sessão:asmdef Editor e cinco documentos rastreados;novos scripts/testes/cena/metas/evidências/tarefa conforme inventário.
- Código/artefatos novos ainda não rastreados;preservar workspace completo para transferência até versionamento. Checkpoints WORLD-001/002 não alterados.
- Aguardar revisão/autorização do usuário para qualquer próxima tarefa. WORLD-004/WORLD-005 não criadas,iniciadas ou implementadas por esta execução. Nenhum trabalho em andamento após conclusão da WORLD-003.

## Alteração externa identificada na auditoria
- A checagem de ausência de arquivos posteriores retornou FAIL porque apareceram durante a sessão `tasks/in_progress/WORLD-004-biomes-water-resources.md` e `WORLD-005-rts-camera.md` (mtimes22:25/22:26,America/Bahia). Eles não existiam no inventário inicial e não foram criados,alterados ou iniciados pelo agente nesta execução. Conteúdo/status dessas tarefas não foi assumido nem incorporado ao escopo.
- Arquivos externos preservados,sem remover ou mover. WORLD-003 continua sendo a única tarefa executada aqui;ACTIVE e BACKLOG não ativam tarefa posterior. Auditoria ajustada para registrar esses arquivos como alteração externa em vez de alegar ausência global. Sem bloqueio para concluir WORLD-003.

## Auditoria final de continuidade
PASS — script executado confirmou metadados/GUIDs únicos,asmdefs válidos,fontes sem espaços finais,status/caminhos/critérios coerentes,64+2 testes reais no JSON final,seis capturas,geradorv1/TerrainDemo/testes anteriores/pacotes/ProjectSettings sem diff. `git diff --check`:PASS. Console final MCP:0 erros/avisos. Git final master/607e22a,sem commit;inventário desta sessão acima,mais .vscode preexistente e duas tarefas externas em in_progress preservadas.

Hashes observados dos arquivos externos para continuidade:WORLD-004 a26514959039b9b2f00fd7306386387c765060293c59cf0dcd537985e99c9c58;WORLD-005 7dfc17b0a55d38823288e4029e1cf6d0fee83e1ce8dcd62d0b47d1e94f246432. Não foram editados ou executados. Autorrevisão sem segundo agente;handoff fornece arquivos,limites,reprodução de testes e próxima ação sem precisar da conversa. Execução WORLD-003 encerrada,sem trabalho em andamento no escopo autorizado.
