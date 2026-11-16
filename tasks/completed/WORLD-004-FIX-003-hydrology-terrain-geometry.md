# WORLD-004-FIX-003 — Hydrology and Terrain Geometry

**Status:** DONE
**Priority:** HIGH
**Owner:** Codex
**Branch:** main
**Base commit:** 6a9966b0168d3ba553c97ed7f7c8a2818f5bbca4
**Last checkpoint:** 2026-11-15T21:20:03-03:00

## Objective
Corrigir na origem paredes/torções de água, bermas/faixas de terreno e serrilhados anormais, preservando terreno v1,determinismo,recursos e streaming.

## Scope
- Included: diagnóstico por camadas/amostras/triângulos, versão ambiental 4, regressões e inspeção real Editor e Play nas duas cenas.
- Excluded: novas funcionalidades/tarefas, refatorações alheias, câmera RTS, shader/material, pacotes, ProjectSettings e commits.
- Alterações preexistentes preservadas; nenhuma tarefa concluída anterior modificada.

## Dependencies
WORLD-001/002/003/004, WORLD-004-FIX-001/002, WORLD-005.

## Acceptance criteria
- [x] Causa raiz comprovada antes da correção, com funções e valores documentados.
- [x] Instrumentação opcional separa OriginalTerrain/EnvironmentalTerrain/WaterOnly/Combined e localiza amostras/triângulos.
- [x] Seed7/N=32/H=2/V=1,2/raio=2/mar=38/v2 reproduzida; v4 sem os defeitos artificiais anteriores.
- [x] RegiãoB reproduzida com pose mundial fornecida e frame inicial comprovado por hash; frame atual transladado também validado, sem inventar foco/zoom históricos.
- [x] Correção na origem, sem offset de água, bermas ou suavização global.
- [x] Lagos/oceanos constantes e rios sem taper transversal,com perfil longitudinal contido/limitado.
- [x] Sem paredes, torções, faixas elevadas ou serrilhados anormais nos cenários validados; relevo/ilhas legítimos preservados.
- [x] Continuidade positiva/negativa e entre chunks/regiões;reconstrução após streaming preservada.
- [x] Determinismo por definição e terreno v1; biomas/árvores/pedras/ouro preservados; versões 1/2/3 intactas e migração 4 explícita.
- [x] Regressões de índices, degeneração, orientação, alturas, perfis, margens e escalas,com tolerâncias/unidades justificadas.
- [x] EditMode 172/172 e PlayMode 6/6 PASS.
- [x] Compilação, Console e shader verificados no Unity 6000.6.9f1/URP via MCP.
- [x] Inspeção visual real nas duas cenas em Editor e Play, camadas separadas, três seeds/mar/escalas e ambos os frames de B.
- [x] Evidências e documentos de continuidade atualizados; nenhum critério dispensado.

## Implementation plan
1. Preservar baseline e confirmar Editor/MCP — concluído.
2. Instrumentar, reproduzir e provar causa — concluído.
3. Implementar correção versionada e regressões — concluído.
4. Validar dados, mesh, streaming, Console e capturas nas duas cenas — concluído.
5. Auditar preservação, documentar migração e finalizar somente esta tarefa — concluído.

## Diagnosis and solution
- V3: `EnvironmentGenerator.ApplyHydrology` calcula distância transversal e`ApplyField` reduz WaterLevel fora do núcleo. Em(-153,357),água45.68278 coincide com perfil46.609078−(distância6.926295−6);derivada na seção transversal0.9999995293. A superfície inclinada existe antes da triangulação. V2 tem bermas de`ApplyContinuousBasin`,corte booleano das pontas e transição de campos interpolada em triângulos molhados;seed7(64,59)→(64,60)difere>14alturas.
- WaterOnly(-5,11):v2máx69.06777°,v3máx31.34414°,v4máx8.53290°;todossem índices inválidos/degenerados/invertidos. Não era problema primário de winding/índices.
- V4`ContainedHydrology`:nível constante por lago/seção de rio;capacidades derivadas do relevo original;perfil longitudinal monotônico/grade≤0.25;leito por envelope mínimo sem elevar terreno. Troca de domínio apenas em buffer seco4intervalos,fora de triângulos molhados. Cachenove metadados globais,não chunks;gerador reutilizado por definição na apresentação.
- Terreno v1 e versões ambientais1/2/3 preservados por fixtures/fingerprints. Cenas selecionam4;ResourceKey mantém versão,migração exige regeneração ambiental e não reinterpretar IDs antigos.
- Relatório técnico completo,27amostras com direções/derivadas e reprodução:docs/validation/WORLD-004-FIX-003/README.md.

## Checkpoint
### Completed and verified
- Inicial2026-11-15:baseline de fontes/tarefas concluídas eGit status preservado;instrumentação separada da correção, compilada/testada via MCP.
- Diagnóstico:25 chunks residentes/fila0 antes das capturas;comparação v2/v3 comprovou causas numéricas e geométricas antes da implementação v4.
- Implementação:classes/versão 4,testes de dados/mesh/câmera/streaming e migração explícita das duas cenas.
- Final:172 EditMode + 6 PlayMode PASS;20 replays × 4 camadas,duas capturas de câmeras originais e uma captura inicial inválida preservada(83 PNGs);14 relatórios WaterOnly v4 sem índices inválidos/áreas zero/faces invertidas.38 arquivos protegidos idênticos ao baseline;GUIDs únicos; final-audit.json PASS.
- Console final Editor e ambas cenas emPlay:0 erros/avisos,sem Clear pelo agente. Shader sem erro; replays liberados;25 meshes nativas para 25 chunks ativos, fila 0.

### In progress
Nenhuma implementação ou validação obrigatória pendente.

### Files touched
- World:novos ContainedHydrology.cs/.meta;EnvironmentData.cs eEnvironmentGenerator.cs.
- Presentation:EnvironmentMeshBuilder.cs,EnvironmentPresentation.cs;novos EnvironmentGeometryDiagnostics.cs/.meta eEditor/HydrologyDiagnosticSession.cs/.meta;asmdef Editor usa referências URP já instaladas.
- Scenes:EnvironmentDemo eRTSCameraDemo,seleção explícita v4;geometryView padrão0 serializada. Frame atualRTSX−5807 preservado.
- Tests:novos ContainedHydrologyTests,HydrologyGeometryTests,HydrologyStreamingTests/.meta;HydrologyRegressionTests,EnvironmentStreamingTests,RtsCameraTests,EnvironmentLifecycleTests.
- Docs:ARCHITECTURE,CURRENT_STATE,HANDOFF;ACTIVE/BACKLOG;este arquivo evalidation/WORLD-004-FIX-003/.

### Git state
- main/6a9966b0168d3ba553c97ed7f7c8a2818f5bbca4,sem commits.
- Working tree inclui FIX001/002 eWORLD005 não commitadas,preexistentes e preservadas;baseline-git-status.txt distingue o estado de entrada.
- Translação da raizRTS mudou desde o baseline inicial:SHA256 confirma raiz0 no baseline e−5807 atual;preservada,não introduzida pelo algoritmo. O código de câmera eChunkManager permanecem idênticos ao baseline FIX003.

### Risks, blockers and decisions
- Nenhum bloqueio restante. Versões legadas mantêm seus defeitos históricos para reprodução;demos usam4.
- Durante validação,o agente solicitouPlay antes do término de uma rodada EditMode;RestoreSceneSetupTask falhou. A rodada foi invalidada,Play encerrado e 172 casos repetidos até PASS. validation-sequence-incident.txt registra o erro de orquestração;nenhum resultado dessa rodada conta como sucesso.
- Frame/zoom históricos não gravados:replay usa root0 comprovado no baseline eortho48 explicitamente escolhido do zoom inicial24×H2. Frame atual−5807também validado;rayhit é inferência,não foco histórico.
- Modelo analítico estático,sem hidrodinâmica/drenagem continental. Facetas/ilhas naturais e exagero visual de escalas extremas não foram removidos. Validação finita,não prova universal de todas as coordenadas.

## Validation evidence
| Check | Status | Evidence |
| --- | --- | --- |
| Unity compilation/URP | PASS | Execução real via MCP;final-editor-state.txt,shaderError=False |
| EditMode completo | PASS | 172/172,0 falhas/ignorados,35.9704167 s;editmode-final.json |
| PlayMode completo | PASS | 6/6,0 falhas/ignorados,1.8620796 s;playmode-final.json |
| Reprodução e inspeção visual | PASS | 20replays/80PNGs porcamada,duas cenas/Editor e Play;README e*-capture.txt |
| Console final | PASS | console-environment-play.json,console-rts-play.json,console-final-editor.json,todosvazios |
| Preservação/continuidade/determinismo | PASS | Regressões,fingerprints,validated-inputs.json efinal-audit.json |
| Build/player/outros alvos | NOT_RUN | Fora do aceite solicitado;sem alegação de validação |

## Handoff notes
Próxima ação:revisão do usuário. Não criar commit ou iniciar tarefa posterior. Editor deixado naRTSCameraDemo, fora de Play, sem cena suja, versão 4 e raiz −5807 preservada. Detalhes e comandos de reprodução noREADME das evidências. Não tratar captura inicial vazia ou rodada interrompida comoPASS.
