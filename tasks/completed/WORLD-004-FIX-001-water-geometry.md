# WORLD-004-FIX-001 — Correção de artefatos geométricos em rios, lagos e margens

**Status:** DONE  
**Priority:** HIGH  
**Owner:** Codex  
**Branch:** main  
**Base commit:** 6a9966b  
**Last checkpoint:** 2026-11-15 — validação final e conclusão

## Objective
Diagnosticar e corrigir fissuras, serrilhados e transições água/terreno na EnvironmentDemo, preservando terreno v1, biomas, recursos e streaming.

## Scope
- Included: hidrologia e malha ambiental, testes de regressão, validação Unity e documentação.
- Excluded: WORLD-005, nova arquitetura, pacotes, commits e alteração do gerador de terreno v1.

## Dependencies
- WORLD-001, WORLD-002, WORLD-003 e WORLD-004.

## Acceptance criteria
- [x] Causa reproduzida antes da correção.
- [x] Geometria água/margem sem fissuras, sobreposição coplanar ou winding inválido.
- [x] Continuidade entre chunks e determinismo preservados.
- [x] Terreno v1, biomas, recursos e streaming preservados.
- [x] Regressões executadas, compilação/EditMode/PlayMode/Console e visual Editor/Play validados.
- [x] Evidências e continuidade atualizadas; mover a completed somente com aceite satisfeito.

## Implementation plan
1. Inspecionar dados, triangulação, fronteiras e Editor; reproduzir falhas.
2. Corrigir a causa geometricamente e criar regressões.
3. Validar suítes, Console e visual na EnvironmentDemo; documentar resultados.

## Checkpoint
### Completed and verified
- Causa reproduzida antes da edição; ambiente2 e malha corrigidos. Caminhos ambientais1/2 disponíveis e terreno v1 preservado.
- PASS103/103 EditMode e3/3 PlayMode reais; cinco casos novos detectam o código original; visual URP Editor/Play e Console inspecionados na cópia isolada.
- Evidências em docs/validation/WORLD-004-FIX-001; documentação atualizada. Nenhum critério dispensado.

### In progress
- Nenhuma implementação parcial no escopo desta correção.

### Next smallest action
- Revisão pelo usuário; não iniciar WORLD-005 e não fazer commit sem autorização.

### Files touched
- Assets/_Project/World/Environment/{EnvironmentData,EnvironmentGenerator}.cs.
- Assets/_Project/Presentation/Environment/{EnvironmentMeshBuilder,EnvironmentPresentation}.cs.
- Assets/_Project/Scenes/EnvironmentDemo.unity.
- Assets/_Project/Tests/EditMode/EnvironmentGeneratorTests.cs, Tests/Presentation/EnvironmentStreamingTests.cs e Tests/StreamingPlayMode/EnvironmentLifecycleTests.cs.
- docs/ARCHITECTURE.md, CURRENT_STATE.md, HANDOFF.md, validation/WORLD-004-FIX-001/, tasks/ACTIVE.md, BACKLOG.md e esta tarefa.

### Git state
- Branch: main
- HEAD: 6a9966b
- Uncommitted changes: somente os arquivos desta correção listados acima; baseline inicialmente limpo. Nenhum commit.

### Risks, blockers and decisions
- Não há bloqueador para o escopo validado. MCP/Editor interativo original indisponível; validação feita no Unity real em cópia com os mesmos158inputs Assets/ProjectSettings (hashes iguais).
- Taludes/facetas low-poly permanecem; água opaca sem apresentação subaquática. Build/player/outros alvos/configuração máxima/heap/VRAM NOT_RUN.
- Aviso de metadado do pacote URP observado no Editor isolado; cache não alterado e aviso não tratado como corrigido. Console Play e leitura final sem erros/avisos.
- Versão ambiental2 evita alterar silenciosamente os resultados1; reproduzir ambiente antigo requer version:1. Terreno base v1 não alterado.

## Validation evidence
| Check | Status | Evidence |
| --- | --- | --- |
| Unity compilation | PASS | Unity6000.6.9f1,103 testes carregados; shaderError=False nas10capturas. |
| Automated tests | PASS | EditMode103/103,0falhas/ignorados; 3.1347382s. XML final preservado. |
| Editor visual | PASS | Cinco renders URP da EnvironmentDemo inspecionados;9chunks/fila0. MCP e sessão original NOT_RUN. |
| Play Mode | PASS |3/3,0falhas/ignorados,0.2203956s;cinco renders URP inspecionados. |
| Console | PASS | Capturas:Editor0erros/1aviso URP,Play0/0;leitura final0/0. Nenhum Clear do agente. |
| Regressões contra baseline | PASS |5FAIL/1PASS em6casos contra código original;6PASS na suíte final. |
| Integridade Git | PASS | Terreno v1/WORLD-005 hashes iguais;diff --check sem erros. |

## Handoff notes
Tarefa concluída e movida a completed após validação. Ler README das evidências para repetir testes/capturas isolados e interpretar limites. Aguardar revisão do usuário; nenhum commit/WORLD-005 autorizado.

## Checkpoint inicial
- Baseline limpo em main/6a9966b; handoff anterior descreve master/607e22a e alterações não commitadas, divergência histórica.
- Unity MCP indisponível em localhost:8080 dentro e fora do sandbox; solicitado início do servidor ao usuário. Nenhum resultado Unity desta sessão ainda.
- WORLD-005 não iniciada nem editada. Nenhum commit autorizado.

## Checkpoint — diagnóstico reproduzido antes da alteração
- Harness .NET 8 compila os fontes reais do domínio, sem Unity nem tradução do algoritmo. Varredura seed12345, X64..223/Z-192..-1: 359 arestas orientadas molhado→seco; salto máximo WaterLevel19.37196 em(106,-146). Evidência em docs/validation/WORLD-004-FIX-001/diagnostic-before.txt e fonte .cs.txt.
- Causa de dados: ApplyBasin restaura o terreno externo entre raios1.2..1.5, mas interrompe o nível local em1.2. Pode haver água na restauração antes do corte, adjacente a amostras com nível do mar. A interpolação de WaterLevel derruba a superfície da água artificialmente na margem. Berma estreita na grade unitária amplifica facetas.
- Causa geométrica: offset fixo+0.015 deixa o contorno da água acima da interseção real; terreno completo permanece sob água, permitindo interseções rasantes/z-fighting. Nenhum indício de índices/winding trocados: ambos usam diagonal(c,b) e winding ascendente. Verificação executável de malha ainda pendente.
- Correção planejada: faixa seca entre a margem interna e a queda do campo de água; perfil de berma amostrado mais largo; recortar terreno e água na mesma interseção sem offset. Alteração de saída ambiental exige versão2, mantendo caminho1 íntegro. Não altera arquitetura nem terreno v1; recursos/clima continuam derivados de sua versão ambiental.
- Antes da edição: main/6a9966b, somente documentos alterados. MCP indisponível; Unity/EditMode/PlayMode/visual atuais NOT_RUN. WORLD-005 hash7dfc17b0a55d38823288e4029e1cf6d0fee83e1ce8dcd62d0b47d1e94f246432 preservado.

## Checkpoint — implementação e primeiras validações Unity
- Implementado ambiente v2 com perfil de margem mais largo, crista seca antes do corte do campo de água e patamares de nível do rio cobrindo a influência completa do lago. Versão1 segue disponível, sem alterar os cálculos legados; TerrainGenerator.cs intocado. EnvironmentPresentation guarda a versão e EnvironmentDemo declara2.
- Malha recorta terreno seco e água como polígonos complementares da mesma triangulação. Interseções canônicas compartilhadas, sem offset, sem terreno quase coplanar sob água opaca; preserva recursos agrupados, cores e normais planas. Não depende de chunks vizinhos carregados.
- Unity6000.6.9f1 real em cópia isolada /tmp/rtswater-unity-validation, Assets/ProjectSettings copiados; manifest temporário resolve59 pacotes já existentes por caminhos locais. Projeto real/pacotes/cache não alterados para executar validação. MCP permanece indisponível.
- Primeira rodada94 EditMode:92PASS/2FAIL (fixture totalmente submersa expunha terreno oculto no teste antigo; consulta de nível usava t antes do arredondamento). Fixture agora contém terreno/água/recursos reais; comparação usa projeção das coordenadas efetivamente amostradas. A versão intermediária falha foi preservada em XML temporário.
- Segunda rodada PASS101/101 EditMode,2.9267939s; PASS3/3 PlayMode,0.2694887s. Novas regressões: contorno fechado/cobertura/winding em3 regiões, gradiente de margem em3 seeds e fixture legada1. Após esta rodada apenas removida alocação de buffers temporários inutilizados e assert legada tornada exata; reexecutar suíte final.
- Harness pós-ajuste:375 arestas orientadas wet→dry, salto máximo0.2104034 na região do defeito (antes19.37196). Ainda não equivale a validação visual.
- Próxima ação: capturar/inspecionar EnvironmentDemo Editor/Play com renderização URP real; verificar Console/logs e regressões finais. Sem commits/WORLD-005.

## Relatório final — diagnóstico, alterações e validações
- Diagnóstico: corte abrupto do campo WaterLevel na restauração externa de uma berma estreita; interpolação entre rio elevado e nível do mar, mais offset da água e faces de terreno submerso quase coplanares. Dados compartilhados de chunks já coincidiam; não foi encontrado índice ou winding invertido como causa. Os novos testes verificam também winding/normais/índices e fechamento topológico.
- Correção de dados: EnvironmentDefinition.CurrentVersion2, suporte explícito à1; perfis de margem com crista seca protegendo a queda do campo, patamares rio–lago coerentes. Clima/biomas/recursos continuam determinísticos e derivados da superfície da versão selecionada. IDs incluem essa versão; não se promete distribuição idêntica entre1/2. Nenhuma alteração no algoritmo base v1.
- Correção geométrica: recorte complementar seco/molhado sobre a diagonal original; interseção canônica comum e sem offset; nenhuma face de fundo quase coplanar emitida sob a água opaca. Dados do terreno continuam íntegros. Uma mesh/renderer por chunk, material compartilhado e streaming/lifecycle anteriores preservados.
- Regressões finais PASS103/103 EditMode e3/3 PlayMode; bordas molhadas X/Z não vazias, superfície sem arestas internas abertas/cobertura duplicada, normal/winding/índices válidos, três seeds, legado1, versões/reload, biomas/recursos e fixtures de terreno. Fontes efetivamente validados com158hashes iguais na cópia. Seis regressões contra gerador/builder originais:5FAIL/1PASS,confirmando sensibilidade.
- Visual real Editor/Play PASS nos cinco enquadramentos registrados: rio, margem aproximada, lago, costa e floresta. Contornos sem fissuras observadas nas capturas. Facetas e cânions em terrenos íngremes continuam possíveis; não há promessa de eliminar toda característica triangular do estilo low-poly ou de todas as seeds.
- Console lido sem limpar:0erros/1aviso no Editor durante capturas,0/0 no Play. Aviso de metadado URP investigado e preservado como limite externo; utilitário temporário teve duas chamadas obsoletas corrigidas. Leitura final em outro processo0/0/0,sem alegar correção do pacote.
- Percurso ambiental final:48amostras,8posições,N32,r1,max1,budget4ms,seed12345,mar38,v2,escalas2/1.2; mediana1.2488ms/P952.9830ms/máximo12.9819ms em batch semGPU,9meshes estáveis e retorno ao baseline. Não é teto de frame/FPS; comparação com medições interativas anteriores não é válida.
- Limitações: MCP e Editor interativo original NOT_RUN; todos os critérios Unity foram executados no próprio Editor Unity em cópia isolada (pacotes locais existentes,sem instalação). Build/player/outros alvos/configuração máxima/heap/VRAM NOT_RUN. Renderização subaquática/transparência e hidrodinâmica fora do escopo.
- Integridade: main/6a9966b,sem commit; TerrainGenerator.cs SHA256f45c0e6b7f92538986fa040f227fd1f6d256514bfed3287693bf6a588afce8ae e WORLD-005 SHA2567dfc17b0a55d38823288e4029e1cf6d0fee83e1ce8dcd62d0b47d1e94f246432 iguais ao baseline. WORLD-005 não movida/editada/iniciada. Próxima ação: revisão do usuário.

## Auditoria de encerramento
PASS — final-audit.json confirma103/103 EditMode,3/3 PlayMode,10PNGs,158inputs sem divergências,cálculo legado da berma literalmente preservado,arquivos protegidos byte a byte iguais ao HEAD,Console isolado final0/0/0 e git diff --check sem erros. main/6a9966b0168d3ba553c97ed7f7c8a2818f5bbca4,sem commits. Tarefa em completed;nenhuma tarefa em execução.
