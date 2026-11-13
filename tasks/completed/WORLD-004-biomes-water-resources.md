# WORLD-004 — Biomas, Água e Distribuição de Recursos

**Status:** DONE  
**Prioridade:** Alta  
**Dependências:** WORLD-001, WORLD-002 e WORLD-003 concluídas.

## 1. Objetivo

Evoluir o mundo procedural do RTSWorld para apresentar ambientes naturais variados, coerentes e reproduzíveis por seed, com inspiração visual em jogos RTS como Age of Empires.

Implementar biomas, oceanos, lagos, rios e distribuição inicial de recursos naturais, preservando a arquitetura determinística, a renderização 3D low-poly e o streaming de chunks.

## 2. Escopo funcional

### 2.1. Biomas

Implementar um sistema procedural de classificação de biomas, considerando:

- Elevação.
- Temperatura.
- Umidade.
- Distribuição geográfica e transições naturais.

Biomas iniciais:

- Planícies e campos.
- Florestas temperadas.
- Florestas densas.
- Desertos.
- Regiões áridas.
- Regiões nevadas.
- Tundra.
- Montanhas.
- Praias e regiões costeiras.
- Áreas oceânicas.

Permitir que diferentes biomas coexistam em um mundo contínuo, com transições visualmente coerentes.

### 2.2. Água

Implementar:

- Nível do mar configurável.
- Oceanos e regiões submersas.
- Lagos procedurais.
- Rios procedurais com continuidade entre chunks.
- Representação visual inicial de água compatível com URP.

A água deve respeitar a topografia e não apresentar descontinuidades entre chunks.

Priorizar uma solução simples e determinística. Não implementar simulação hidrodinâmica.

### 2.3. Recursos naturais

Implementar distribuição procedural inicial de:

- Árvores e áreas florestais.
- Pedra.
- Ouro.

Os recursos deverão possuir identificadores e coordenadas lógicas reproduzíveis pela seed.

A distribuição deve considerar o bioma e as condições do terreno.

Separar os dados lógicos dos recursos de sua representação visual.

### 2.4. Apresentação visual

- Materiais e cores coerentes com cada bioma.
- Vegetação low-poly inicial.
- Representação visual de rochas e depósitos de ouro.
- Água visualmente distinguível do terreno.
- Integração com a renderização e o streaming existentes.
- Transições entre chunks sem frestas geométricas.

## 3. Requisitos arquiteturais

1. Preservar o contrato determinístico do gerador v1 da WORLD-001.
2. Não modificar silenciosamente resultados já versionados.
3. Versionar novos algoritmos ou criar camadas determinísticas adicionais quando necessário.
4. Separar geração de dados, classificação de biomas, distribuição de recursos e apresentação visual.
5. Utilizar coordenadas globais para evitar descontinuidades entre chunks.
6. Integrar o ciclo de vida dos elementos visuais ao streaming da WORLD-003.
7. Evitar GameObjects individuais em grande quantidade quando técnicas de instancing ou agrupamento forem mais adequadas.
8. Evitar geração e descarregamento repetitivos desnecessários.
9. Garantir que a mesma seed e versão produzam a mesma distribuição lógica.
10. Preparar os recursos para futura interação com unidades e sistemas de coleta.

## 4. Plano incremental

- Etapa A: inspeção da arquitetura e definição dos contratos de biomas.
- Etapa B: geração determinística de temperatura, umidade e classificação de biomas.
- Etapa C: representação visual de biomas.
- Etapa D: nível do mar, oceanos e lagos.
- Etapa E: geração inicial de rios contínuos.
- Etapa F: distribuição lógica de árvores, pedras e ouro.
- Etapa G: apresentação visual eficiente dos recursos.
- Etapa H: integração com streaming, testes e validação.

Se alguma etapa exigir complexidade desproporcional, documentar alternativas e solicitar autorização antes de reduzir critérios obrigatórios.

## 5. Fora do escopo

- Coleta efetiva de recursos.
- Economia e inventários.
- Construção de cidades.
- Unidades e combate.
- Pathfinding.
- Simulação física de rios e oceanos.
- Sistema climático dinâmico.
- Estações do ano.
- Multiplayer e persistência autoritativa.
- Câmera RTS definitiva — WORLD-005.

## 6. Critérios de aceite

- [x] A mesma seed reproduz os mesmos biomas e recursos lógicos.
- [x] Temperatura e umidade influenciam a distribuição dos biomas.
- [x] Planícies, florestas, desertos, neve e montanhas são distinguíveis visualmente.
- [x] Existem regiões costeiras e oceanos.
- [x] Lagos e rios são gerados de forma coerente com a topografia.
- [x] Rios e superfícies de água não apresentam descontinuidades entre chunks.
- [x] Árvores, pedras e ouro possuem distribuição lógica determinística.
- [x] Os elementos visuais são carregados e descarregados junto com seus chunks.
- [x] Não existem duplicações indevidas nas bordas dos chunks.
- [x] A implementação não altera os resultados do gerador v1.
- [x] A cena de demonstração permite observar diferentes biomas e recursos.
- [x] O projeto compila sem erros.
- [x] Os testes automatizados aplicáveis são executados.
- [x] A inspeção visual é realizada no Editor e Play Mode.
- [x] Os documentos de continuidade são atualizados.

## 7. Testes

Validar:

- Determinismo por seed e versão.
- Continuidade entre chunks.
- Distribuição dos biomas.
- Regras de distribuição dos recursos.
- Ausência de duplicações.
- Integração com streaming.
- Liberação dos elementos visuais.
- Compilação e testes existentes.

Registrar resultados como PASS, FAIL ou NOT_RUN, com evidências.

## 8. Continuidade

Seguir AGENTS.md e TASK_TEMPLATE.md.

Ao iniciar, mover a tarefa para tasks/in_progress/ e atualizar tasks/ACTIVE.md.

Registrar checkpoints, decisões técnicas, arquivos modificados, testes e pendências.

Ao concluir todos os critérios obrigatórios, mover para tasks/completed/ e atualizar docs/CURRENT_STATE.md, docs/HANDOFF.md e tasks/BACKLOG.md.

Não iniciar WORLD-005 automaticamente.

**Aguardar autorização explícita do usuário antes de executar esta tarefa.**

## Checkpoint de execução — inicialização (2026-11-12)
- Autorização explícita do usuário recebida. Arquivo existente localizado já em in_progress (não pending), texto integral preservado; somente status atualizado e seções de execução acrescentadas. WORLD-005 localizada em in_progress e será mantida byte a byte, sem mover/ativar.
- Dependências WORLD-001/002/003 DONE verificadas nos arquivos e código. Base master/607e22a; alterações e arquivos não rastreados da WORLD-003 e .vscode preservados. Snapshot de hashes inicial em /tmp/world004-baseline.json; spec original em /tmp/world004-original.md para auditoria desta sessão.
- Unity MCP confirmou RTSWorld/raiz correta/Unity6000.6.9f1, StreamingDemo salva, Console zero erros/avisos. Estado inicial de geração/render/streaming inspecionado.
- Plano incremental: A/B contratos ambientais imutáveis e clima global; D/E hidrologia analítica determinística com bacias/rios descendentes e escavação somente na camada adicional; F recursos com identidade por seed/versão/célula e propriedade half-open; C/G geometria com cores/água/recursos agrupados; H integração opt-in ao ChunkManager e demo, testes de regressão/novos, Editor/Play/capturas e auditoria.
- Decisão de compatibilidade: não alterar algoritmo/saída v1. Camada EnvironmentVersion1 produz superfície derivada separada, clima/bioma/água/recursos. API aditiva de amostragem de elevação base poderá expor o cálculo existente sem duplicar ruído. Rios conectam bacias globais e descem entre níveis; sem hidrodinâmica. Uma mesh agrupada por chunk para superfície/água/recursos, sem objeto por árvore/célula.
- Compilação/testes ambientais/visual: NOT_RUN; nenhum código ambiental ainda. Próxima ação: implementar contratos e camada global, com testes determinismo/bordas/água/recursos. Sem bloqueador. Nada da WORLD-005 será executado.

## Checkpoint — camada e apresentação escritas
- Criados World/Environment/EnvironmentData, EnvironmentNoise e EnvironmentGenerator: contratos imutáveis separados, versão ambiental1/nível do mar, clima global, bacias analíticas com rios descendentes, escavação/bermas na superfície derivada e recursos half-open sem objeto visual individual. Cache de metadados de bacia limitado a9, sem reter chunks.
- TerrainGenerator recebeu apenas SampleElevation aditivo chamando Elevation existente; corpo do algoritmo v1/Generate intacto.
- Presentation/Environment contém builder agrupado, shader URP com cores por vértice e EnvironmentPresentation. ChunkManager recebeu referência opcional/invalidação por revisão e material ambiental; caminho antigo permanece padrão. Cada chunk continua com uma mesh/renderer, cleanup existente reutilizado.
- Parcial: fontes escritas, sem compilação/testes novos (NOT_RUN); demo ainda não criada. Próximo passo: testes ambientais, compilar e validar regressões. Alterações preexistentes WORLD-003 preservadas, master/607e22a, sem commit; WORLD-005 intocada.

## Checkpoint — dados/testes iniciais e demo
- Compilação PASS e89/89 EditMode (64 anteriores+25 ambientais),jobc3614f88acda4655ad02f34035c01e30,0 falhas/ignorados,3.719094s. Inclui determinismo/bordas/partição,10biomas/4estados água em varredura,recursos secos/IDs únicos e ciclo manager por revisão.
- EnvironmentDemo.unity e EnvironmentPalette.mat criados pelo Editor,sem alterar cenas anteriores. Demo reúne landmarks de todos os biomas/lago/rio,seed12345/mar38,painel de streaming e referências.
- PASS visual inicial Play: floresta/recursos/neve/montanha/costa/oceano visíveis e9 meshes para9 chunks,nenhum objeto por recurso. Captura play-forest-coast.png inspecionada. Lagos/rios e casos ambientais Play automatizados ainda NOT_RUN.
- Próximas ações: verificar água de perto/continuidade geométrica visual,acrescentar testes hidrologia/Play lifecycle ambiental,executar regressões e checkpoint final. Console sem erros,Warning WebSocket preexistente após reload. WORLD-005 hash preservado.

## Checkpoint — hidrologia e lifecycle validados
- PASS93/93 EditMode,jobc63cc32b75b74f43973ee31db7713967,0falhas/ignorados,3.8104795s. Novos casos confirmam centro de rios molhado e descendente entre bacias,além de vértices de água recortada coincidentes nas bordas (tolerância0.0001 unidade visual).
- PASS3/3 PlayMode,joba1b6e6e4836145438726e7bfa3278497,0falhas/ignorados,0.5828954s. Percurso ambiental8posições/N32/raio1/max1/budget4ms:48amostras,mediana1.1039ms/P954.8913ms/máximo10.1803ms,9meshes nativas estabilizadas,baseline após disable;seed/mar alterados sem manter meshes antigas. Budget não é teto rígido,chunk síncrono pode exceder4ms.
- RiverReach é metadado lógico de rota e nível para validação/evolução,sem simulaçãohidrodinâmica. Cena/capturas anteriores incluem transição de carregamento no primeiro lago;refazer captura estabilizada antes do aceite visual final.
- Próximo passo: capturas estáveis de lago/deserto/neve/Editor,documentar versão/limites e auditar critérios/especificação original/WORLD-005. Sem bloqueadores externos;sem alteraçãoWORLD-005.

## Ajuste após inspeção visual da água
- Captura estável do lago revelou que a margem do rio podia elevar faixas paralelas dentro de uma bacia já molhada. Corrigido: não aplicar bermas do rio sobre fundo já submerso de lago; a conexão continua escavada na margem seca. Suíte anterior não detectava esse artefato visual; revalidar antes de concluir.
- Iluminação simplificada do shader reduzida para não saturar cores claras (deserto/neve),sem alterar dados. Capturas iniciais preservadas como histórico,novas serão feitas com código final.
- Próximo passo: teste de lago sem bermas internas,regressão Edit/Play e novas capturas. NOT_RUN para este ajuste,sem bloqueador.

## Checkpoint — correções validadas
- PASS94/94 EditMode final,job2b9cbe1d9ba74fee856e553964333db7,0falhas/ignorados,3.7505354s;inclui regressão de lago sem bermas internas. PASS3/3 PlayMode final,job9440a6082e9f43e99e67a175cd84788e,0falhas/ignorados,0.5731566s. Resultados brutos em docs/validation/WORLD-004/release-test-results.json.
- Percurso ambiental final48amostras/N32/r1/max1/budget4ms:mediana1.0838ms,P954.1474ms,máximo7.1368ms;9meshes nativas porregiãoestabilizada e retornoaobaseline. É custoStep local,sem garantia deFPS;um chunk passou4ms conforme limitaçãoatômica registrada.
- Captura final-play-lake.png confirma lago semfaixas elevadas internas,água conectadaaocanal ebiomas neve/aridez distinguíveis. final-play-desert.png e floresta serão anexadas à auditoria visual;fasesantigas mantidas comopreservaçãohistórica.
- Restam:Editor/capturafinal,persistência do checkpoint,ADRda camada eauditoria de integridade da especificação/WORLD-005. Sem bloqueador;nenhumcritério reduzido.

## Relatório final de execução
### Concluído e verificado
- Etapas A–H implementadas: contratos e versão ambientais, temperatura/umidade/altitude, dez biomas, cores low-poly, oceanos/costas, bacias/lago e rios descendentes, recursos lógicos e visuais agrupados, integração opt-in com streaming.
- Geração base v1 preservada; somente API SampleElevation aditiva. BaseTerrain separado da superfície derivada e callbacks/ownership anteriores reutilizados. ADR-0002 registra decisão autorizada e limites sem substituir a especificação original.
- Visual final inspecionado no Play e Editor, shaderErrors=False e9 meshes/9 objetos para9 chunks; sem recurso individual em GameObject. Artefato inicial de berma dentro de lago corrigido, coberto por teste e recapturado. Capturas iniciais mantidas como histórico, as final-* são o estado aprovado.
- Nenhuma etapa obrigatória dispensada e nenhum bloqueador restante. Não há implementação parcial no escopo aceito. WORLD-005 não editada/movida/iniciada.

### Arquivos criados nesta tarefa
- World/Environment/EnvironmentData.cs, EnvironmentNoise.cs, EnvironmentGenerator.cs (sob Assets/_Project).
- Presentation/Environment/EnvironmentMeshBuilder.cs, EnvironmentPresentation.cs, EnvironmentDemo.cs e EnvironmentColors.shader.
- Tests/EditMode/EnvironmentGeneratorTests.cs, Tests/Presentation/EnvironmentStreamingTests.cs, Tests/StreamingPlayMode/EnvironmentLifecycleTests.cs.
- Materials/EnvironmentPalette.mat, Scenes/EnvironmentDemo.unity e todos os .meta correspondentes/diretórios.
- docs/decisions/ADR-0002-environment-layer.md e docs/validation/WORLD-004/ (capturas e JSONs de evidência).

### Arquivos modificados nesta tarefa
- Assets/_Project/World/Generation/TerrainGenerator.cs: somente método público SampleElevation que chama cálculo existente.
- Assets/_Project/Presentation/Streaming/ChunkManager.cs: referência ambiental opcional, invalidação e caminho visual ambiental; alterações WORLD-003 anteriores preservadas.
- Esta tarefa, tasks/ACTIVE.md, tasks/BACKLOG.md, docs/CURRENT_STATE.md, ARCHITECTURE.md e HANDOFF.md.
- Demais arquivos preexistentes, incluindo cenas anteriores, pacotes, ProjectSettings, testes/contratos anteriores, especificação WORLD-005 e checkpoints completed, preservados.

## Evidências finais
| Verificação | Status | Evidência |
| --- | --- | --- |
| Compilação C#/shader | PASS | refresh_unity e Console reais; tipos novos usados/testados; ShaderUtil.ShaderHasError=False. |
| EditMode final | PASS | job2b9cbe1d9ba74fee856e553964333db7:94/94,0 falhas/ignorados,3.7505354s;64 anteriores+30 novos. |
| PlayMode final | PASS | job9440a6082e9f43e99e67a175cd84788e:3/3,0 falhas/ignorados,0.5731566s;2 anteriores+1 ambiental. |
| Determinismo/compatibilidade | PASS | Repetição de todos os campos/recursos, seed distinta, API base versus chunks em negativos/extremos, fixtures v1 e regressões anteriores. |
| Clima/biomas | PASS | Regras de classificação, campos em[0,1], influência de temperatura/umidade/elevação e varredura global que encontra os10 biomas e4 estados de água. |
| Água/topografia | PASS | Rios molhados e descendentes entre bacias; fundo de lago sem bermas internas; água somente acima da superfície derivada; oceanos e costa observados. |
| Bordas/recursos | PASS | Clima/altura/água iguais nas bordas; repartição de chunks preserva ambiente e união de IDs; recursos secos, com densidades por bioma e dentro de sua célula, sem duplicações. |
| Geometria de água | PASS |3 pares de meshes com bordas molhadas coincidentes(tolerância0.0001 unidade visual); consulta real confirmou78/34/76 vértices molhados nas bordas, não testes vazios. |
| Streaming/limpeza | PASS | Reutiliza chunks imóveis; mar/seed/revisão recriam somente apresentação necessária;8 posições ambientais com9 meshes nativas estabilizadas e retorno ao baseline após disable, sem reter objetos/meshes descarregados. |
| Visual Editor/Play | PASS | final-editor-environment.png, final-play-forest-resources.png, final-play-desert.png, final-play-lake.png inspecionadas; florestas/campos,neve/montanhas,deserto,costa/oceano,lagos/rios e recursos distinguíveis. |
| Console | PASS | Leitura final retorna0 erros/avisos. Warning WebSocket do MCP apareceu após reload como nas tarefas anteriores; não tratado como corrigido. Agente não limpou Console; preferência Unity pode limpar ao entrar Play. |
| Custo local | PASS |48 amostras em8 posições,N32,r1,max1/budget4ms,mar38,ambientalv1:mediana1.0838ms,P954.1474ms,máximo7.1368ms;9 meshes após estabilização. JSON final contém dados brutos. |
| Build/player/outros alvos/heap/VRAM/configuração máxima | NOT_RUN | Não há promessa de FPS, determinismo entre plataformas ou ausência absoluta de todos os tipos de leak. |

## Reprodução e instruções de visualização
1. Abrir Assets/_Project/Scenes/EnvironmentDemo.unity na Unity6000.6.9f1 e aguardar fila0(9 chunks). Usar Play/Game para painéis e câmera fixa de teste.
2. Painel direito: landmarks da seed12345/mar38 — Floresta densa(0,0),Campos(64,0),Floresta temperada(48,0),Deserto(128,160),Árida(80,0),Neve(104,-32),Tundra(-144,24),Montanha(24,-8),Costa/Oceano(56,56),Lago(168,-64),Rio(120,-128). Coordenadas lógicas; botões convertem pela escala horizontal. Aguardar fila0 após teleporte antes de avaliar.
3. Painel esquerdo reutiliza StreamingDemo para X/Z,Seed+/Raio e contadores. Chunk Stream/ChunkManager no Inspector permite seed,raio,N,escalas/budget; EnvironmentPresentation permite Sea Level/material. Qualquer revisão invalida/reconstrói a camada. Landmarks fixos são referências para configuração padrão, não prometem o mesmo bioma em outra seed/mar.
4. Árvores verdes,troncobrum,pedra cinza eouro amarelo são geometria agrupada,sem GameObjects individuais; água azul é opaca. Não há coleta,colisores/economia/reflexão/simulaçãofísica. Cenas TerrainDemo e StreamingDemo continuam disponíveis sem alteração.
5. Repetir testes via Test Runner ou MCP:EditMode assemblies RTSWorld.World.Tests e RTSWorld.Presentation.Tests;PlayMode RTSWorld.Streaming.PlayMode.Tests. EnvironmentStreamingReleasesWaterAndResourcesAndReportsCost reproduz percurso8 posições X=(i-4)*64,Z=(i%3-1)*64,seed12345,mar38,N32,escalas2/1.2,r1,max1/budget4ms. Depois muda seed67890/mar55 e valida substituição/limpeza.

## Limitações técnicas declaradas
- Hidrologia analítica estática em bacias regionais256x256. Rios podem formar cânions, lagos têm margens esculpidas. Não é erosão/drenagem continental, não há confluências/hidrodinâmica. Todos os critérios de água inicial foram implementados sem reduzir escopo obrigatório.
- Água opaca e shader simples sem ondas/refração/reflexo ou sombras dedicadas. Recursos são pirâmides low-poly iniciais, sem interação ou estado coletável/persistido.
- Dados ambientais dependem da definição completa(seed,versões,N,mar); mudanças de saída futuras exigem nova versão ambiental. Determinismo validado neste Unity/Linux, uso de float/double/Math.Sin/Cos não garante identidade entre plataformas.
- Geração/upload de um chunk é atômica e pode exceder budget4ms(máximo medido7.1368ms); unload síncrono,sem garantia deFPS. Floating origin,configurações máximas,player e profiling detalhado permanecem não validados. Buracos durante fila pendente são temporários, não frestas de geometria estabilizada.

## Git, continuidade e próxima ação
- Base/HEAD master/607e22a0add91072b406c6a3b35f12bf168de237,sem commit. Alterações WORLD-003 e .vscode preexistentes preservadas. Inventário final combina essas alterações com os arquivos WORLD-004 acima; vários ainda não rastreados. Preservar workspace completo até versionamento.
- Especificação original mantida integralmente, salvo status e marcação dos critérios; plano/checkpoints/evidências somente acrescentados. O pedido explícito do usuário satisfez a autorização exigida no rodapé original.
- WORLD-005 permaneceu em tasks/in_progress/WORLD-005-rts-camera.md, pois já estava ali antes deste pedido; não foi movida para pending nem executada. Hash inicial/final7dfc17b0a55d38823288e4029e1cf6d0fee83e1ce8dcd62d0b47d1e94f246432.
- Após conclusão, execução encerrada. Aguardar autorização explícita para WORLD-005 ou qualquer próxima tarefa; não iniciar automaticamente. Na retomada, reler AGENTS/estado/handoff/ACTIVE/tarefa e inspecionar Git/Unity reais.

## Auditoria final do checkpoint
PASS — auditoria executada confirmou que o texto original da especificação é idêntico após normalizar somente status/checks (SHA256 original a26514959039b9b2f00fd7306386387c765060293c59cf0dcd537985e99c9c58); nenhum objetivo/escopo/critério foi substituído. WORLD-005 permanece byte a byte e no caminho original desta sessão. Comparação de hashes com baseline preserva todos os arquivos anteriores fora da lista explícita de alterações; o fonte v1 original é idêntico após remover exclusivamente o novo método aditivo.

PASS — metadados completos/GUIDs únicos,fontes sem espaços finais,status/caminhos/critérios coerentes,JSON final94+3 testes reais e9 capturas presentes (as4 final-* são evidência final). `git diff --check`:PASS. Console final MCP:0erros/avisos. Autorrevisão sem segundo agente;handoff inclui reprodução,limitações,arquivos,Git e próxima ação sem depender da conversa. Base final master/607e22a,sem commit,alterações anteriores WORLD-003 e .vscode preservadas e arquivos novos sem rastreamento. Nenhuma etapa pendente do escopo obrigatório;execução encerrada aguardando autorização explícita para WORLD-005.
