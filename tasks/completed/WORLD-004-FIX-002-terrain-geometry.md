# WORLD-004-FIX-002 — Diagnóstico e correção de geometria do terreno

**Status:** DONE

## Contexto

A correção anterior da WORLD-004 melhorou a aparência de algumas seeds, mas os artefatos geométricos reapareceram em outras configurações.

Foi identificado um cenário reproduzível na cena `EnvironmentDemo.unity`:

- Seed: 7
- Cells Per Chunk: 32
- Horizontal Scale: 2
- Vertical Scale: 1.2
- Load Radius: 2
- Sea Level: 38
- Environment Version: 2

Artefatos observados:

- Paredes triangulares nas margens.
- Faixas estreitas de terreno atravessando superfícies de água.
- Encostas com triângulos alongados e quase verticais.
- Transições abruptas de elevação.

## Objetivo

Identificar a causa raiz dos artefatos geométricos e implementar uma correção robusta, sem comprometer o determinismo e a continuidade entre chunks.

## Instruções

1. Leia AGENTS.md, os documentos de arquitetura e continuidade e os relatórios da WORLD-004 e da correção anterior.
2. Inspecione o gerador de alturas, a hidrologia, a construção de meshes e a integração com streaming.
3. Reproduza a configuração da seed 7.
4. Compare os valores de elevação antes e depois das transformações ambientais.
5. Instrumente temporariamente a geração para identificar gradientes de altura excessivos e triângulos com geometria anormal.
6. Verifique coordenadas globais, índices, normais e continuidade entre chunks.
7. Diferencie artefatos de triangulação de características legítimas do relevo.
8. Identifique e documente a causa raiz antes de implementar a correção.

## Correção

- Corrija a origem dos artefatos, evitando soluções apenas cosméticas.
- Preserve o gerador determinístico v1.
- Preserve a compatibilidade com os sistemas de biomas, água, recursos e streaming.
- Evite suavização indiscriminada que elimine montanhas e outras características legítimas.
- Não altere silenciosamente o significado de versões procedurais existentes; documente e versione mudanças incompatíveis.

## Testes obrigatórios

- Regressão com seed 7 e parâmetros informados.
- Testes com diferentes seeds e níveis do mar.
- Verificação de gradientes de elevação.
- Continuidade de vértices entre chunks.
- Ausência de triângulos degenerados.
- Comparação da geometria antes e depois da correção.
- Inspeção visual no Editor e Play Mode.
- Regressão dos testes existentes.

Registrar PASS, FAIL ou NOT_RUN com evidências reais.

## Finalização

Seguir o fluxo de tarefas do projeto, com checkpoints, documentação e relatório final.

Não marcar DONE enquanto a seed 7 continuar apresentando os artefatos descritos.

Não iniciar WORLD-005.

Não criar commits sem autorização.

## Controle de execução
- Responsável: Codex, único escritor; main/6a9966b. Sem commits autorizados.
- Baseline da FIX-001 ainda não commitado preservado em /tmp/rtsworld-fix002-baseline; hashes em docs/validation/WORLD-004-FIX-002/baseline-hashes.json. Não confundir diff contra HEAD com alterações desta tarefa.
- Dependências: WORLD-004 e WORLD-004-FIX-001. WORLD-005 não será iniciada/editada.

## Plano de implementação
1. Reproduzir seed7/N32/escalas2,1.2/r2/mar38/v2 e medir BaseTerrain versus Surface, gradientes e triângulos.
2. Documentar a causa antes da edição; implementar correção versionada restrita à hidrologia, preservando o relevo base e versões anteriores.
3. Criar regressões de gradiente/geometria/continuidade e múltiplas seeds/mar; comparar antes/depois no Editor/Play.
4. Executar regressões Unity, registrar evidências/limites e atualizar continuidade; concluir somente sem os artefatos da seed7.

## Critérios de aceite acompanhados
- [x] Configuração da seed7 reproduzida com diagnóstico numérico/visual antes da correção.
- [x] Causa raiz documentada antes da edição dos fontes.
- [x] Seed7 corrigida sem paredes/faixas artificiais ou triângulos anormais no Editor/Play.
- [x] Gradientes, continuidade e degeneração verificados em múltiplas seeds/níveis do mar.
- [x] Terreno v1 e resultados ambientais já versionados preservados; mudança incompatível explicitamente versionada.
- [x] Testes existentes/novos, compilação e Console validados; evidências e documentação atualizadas.

## Checkpoint — início
- Concluído: documentos, relatórios e código relevantes inspecionados; working tree contém FIX-001 e tarefa pendente do usuário, tudo preservado.
- Em andamento: diagnóstico da transformação hidrológica v2; nenhum fonte alterado nesta tarefa.
- Verificações: Git/arquivos PASS; compilação/testes/visual desta tarefa NOT_RUN. MCP localhost:8080 indisponível no primeiro teste; confirmar fora do sandbox.
- Próxima menor ação: instrumentar fontes reais do domínio na configuração seed7 e capturar baseline Unity.
- Arquivos desta etapa: esta tarefa, ACTIVE,HANDOFF,CURRENT_STATE e baseline-hashes.json.

## Checkpoint — causa raiz reproduzida antes da correção
- PASS reprodução real no Unity6000.6.9f1/URP,Editor ePlay,seed7,N32,h2/v1.2,r2,mar38,v2,25chunks/fila0. Capturas before/editor-seed7-origin.png e play-seed7-lake-close.png mostram uma parede plana alta e faixas artificiais; não são apenas facetas legítimas do low-poly.
- Instrumentação temporária fora dos fontes do jogo compila World real e mede a janela lógica[-64,96]²: gradiente base máximo2.861179 versus Surface14.330421;149arestas acima de4,todas induzidas pelo ambiente;elevação artificial máxima+3.182419 e escavação máxima26.188896. Dados em seed7-before.json.
- Pior aresta(64,59)→(64,60):base49.83183→49.20860,Surface49.83183→35.50141. O corte t>=0/t<=1 do rio interrompe uma escavação profunda num plano transversal. O lago receptor no nível do mar é ignorado por ApplyLake,portanto não existe transição/cap na ponta. A largura fixa da escavação não depende da diferença de altura do relevo.
- A berma usa max(terrain,water+0.8),elevando terreno originalmente baixo e produzindo faixas sobre água. O booleano insideLake troca abruptamente o operador de terreno em fronteiras molhadas; aplicação sequencial também pode reerguer um corte anterior.
- Índices/diagonal são consistentes; dados são avaliados em coordenadas globais. Mesma fronteira de chunk não explica o defeito: os valores já coincidem,mas os gradientes hidrológicos são excessivos. Malha/Console/continuidade terão regressões adicionais.
- Correção planejada: versão ambiental3 com campos contínuos de escavação/água,distância ao trajeto com pontas arredondadas e composição min/max,sem elevar bermas. Envelope de escavação com declive limitado amplia a transição conforme a profundidade,sem suavizar o ruído base. Avaliar influências globais de bacias vizinhas se atravessarem limites regionais,independentemente de chunks carregados. Manter dados1/2reproduzíveis.
- main/6a9966b,alterações FIX-001 preservadas;nenhum fonte editado ainda nesta tarefa. MCP indisponível dentro/fora do sandbox. Próximo passo:fixar fingerprints1/2e implementar caminho3,então medir antes/depois.

## Checkpoint — primeira implementação e comparação
- Implementado caminho3 separado do legado: envelopes de escavação com grade1.25,distância do rio contínua com caps arredondados,incluindo lago receptor ao nível do mar; composição min nunca eleva terreno nem desfaz outro corte. Campo de água contínuo(max)mantém núcleo plano e decai até o mar onde o relevo baixo não contém a água. Sem bermas artificiais,sem suavização global do ruído.
- Influências compactas avaliadas por coordenadas globais em até3x3bacias vizinhas,com filtro espacial; não depende de chunks carregados. Envelope/potencial retornam ao terreno/mar fora do suporte. Versões1/2mantidas; fingerprints completos de amostras/clima/biomas/recursos em12chunks e3seeds coincidem antes/depois para ambas.
- Seed7,mesma janela[-64,96]²:gradiente máximo2.861179,igual ao base,zeroarestas>4e zeroelevações artificiais;antes14.330421/149arestas>4/82amostras elevadas. Corte máximo24.048306,com transição de largura proporcional à profundidade. Evidência seed7-after-initial.json.
- Renderização inicial real Editor/Play após correção mostra a parede substituída por uma margem arredondada; montanhas externas permanecem iguais. Capturas em /tmp/rtswater-unity-validation/fix002-after-initial. Ainda validar testes completos e outras vistas,seeds/mar.
- Criadas regressões de gradiente(4seeds×4níveis,inclusive limites regionais),preservação de montanhas/ausência de berma,hashes legados,bordas; teste de meshes dos25chunks e PlayMode de revisita/liberação. EditMode em execução;nenhum PASS de suíte nova ainda.
- Arquivos alterados nesta tarefa:EnvironmentGenerator/Data,EnvironmentDemo.unity,testes ambientais existentes e novoHydrologyRegressionTests.cs/.meta,documentos/evidências. Builder e gerador base ainda intocados em relação à entrada desta tarefa.
- Git main/6a9966b,sem commits;FIX-001 preservada. Próxima ação:analisar resultados EditMode,corrigir qualquer falha,validarPlayMode/Console/visual final.

## Checkpoint — regressões e precisão da malha
- Primeira suíte real:127EditMode,122PASS/5FAIL(30.7497925s). Todas as16varreduras de gradiente,seed7e25meshes passaram. Falhas:uma borda da seed12345continha um vértice de água isolado em(160,-72);dois testes de água v2usavam limiar absoluto<1também na diagonal√2;duas fixtures SHA calculadas no.NET diferiam das do MonoUnity.
- Investigação nativa:seed7,v2=44triângulos de superfície quase verticais(normalY<0.25),normalmínima0.08298275;v3=0,normalmínima0.497627616. Ambas sem degenerados,área projetada total102400nos25chunks. Evidência geometry-before-snap.txt.
- Borda:quando a margem analítica passa por um vértice da grade,a profundidade residual de poucosULPs cria um fan minúsculo;ele colapsa de modo diferente em coordenadas locais0/N. Corrigido Point do builder:colapsar entrada terra/água num único ponto apenas dentro de tolerância relativa de4epsilonFloat×altura,antes da escala/recorte,tornando todos os triângulos incidentes consistentes. Sem offset ou mudança dos dados.
- O teste estrito<1da FIX-001continua íntegro,agora selecionando explicitamente sua versão2;versão3é verificada por declive normalizado pela distância(<2na água),incluindo diagonais/em16combinações. Não reinterpretar1.405ao longo de√2como salto de1.405por unidade.
- Fingerprints nativos coletados usando gerador anterior preservado e atual:iguais nas versões1/2,mas diferentes dos.NET. Fixtures agora usam os hashes do próprio Unity validado;isso confirma a limitação já declarada entre runtimes. legacy-unity-before/after.txt e fingerprints.NET preservados. Nenhuma fixture foi trocada sem comparação ao código anterior.
- Próxima ação:segunda suíte127EditMode,PlayMode4ecomparações visuais finais. Mesma branch/HEAD,sem commits;alterações anteriores preservadas.

## Checkpoint — tangência de borda esclarecida
- Segunda suíte126/127PASS(32.583033s);somente a comparação de conjuntos de vértices de água permaneceu diferente13/14. O experimento de snap de4epsilon não mudou o resultado;foi removido. Builder agora está byte a byte igual à entrada da FIX-002,preservando FIX-001.
- A diferença corresponde a contato isolado da água com o vértice de fronteira(160,-72). Água em um chunk pode tocar esse ponto enquanto o vizinho é seco;isso não implica segmento de água ou fissura no vizinho. O teste anterior exigia marcação de água idêntica nesse contato de área zero.
- Regressão corrigida para comparar segmentos positivos da interface de água,sem aceitar caso vazio. O teste de25chunks também verifica todos os vértices da superfície visível,índices,normais,cobertura e degeneração. Não foi aplicada correção cosmética ou tolerância de dados/meshing para ocultar o contato.
- Documentação arquitetural atualizada para a composição contínua v3e para a semântica correta de tangências. Fingerprints legados nativos conferidos contra o gerador original preservado.
- Próximo passo:executar suíte final após essa correção de teste,PlayMode e capturas comparativas adicionais;nenhuma conclusão final ainda. Sem commits/WORLD-005.

## Checkpoint — suítes finais PASS
- PASS127/127EditMode,0falhas/ignorados,31.7535708s;PASS4/4PlayMode,0falhas/ignorados,0.3869893s. XMLs finais em validation/WORLD-004-FIX-002. Inclui regressões anteriores,16varreduras de gradiente,legados1/2,25meshes,bordas,recursos,biomas,revisita determinística e liberação.
- Controle:dois testes de aceite selecionando explicitamentev2na cópia falham(gradiente14.330421e normalY0.083695);mesmos testes passam emv3. Resultado em regression-v2-failed.xml.
- Geometria nativa final:seed7/r2,v2tem44faces quase verticais,v3zero;ambas sem degenerados e área projetada102400. A amostra de tangência(160,-72)tem exatamente profundidade0no próprioUnity,confirmando interpretação do teste de segmentos;nenhuma alteração no builder foi mantida.
- Fontes da cópia validados por SHA256:160inputs Assets/ProjectSettings iguais ao workspace. MCP indisponível;Editor interativo original não controlado. Utilitários de instrumentação/captura apenas na cópia e nas evidências como.cs.txt.
- MedidaPlaybatch,N32/r1,max1,budget4ms,seed12345/mar38/v3,48amostras:mediana1.2670ms/P952.9459ms/máximo11.5468ms,9meshes estáveis/baselineapósdisable. Não é limite rígido ouFPS;novo teste seed7/r2verifica25meshes e retorno ao baseline.
- Próxima menor ação:inspecionar capturas finais Editor/Play(multiseed/mar),Console e concluir documentação apenas se a seed7estiver sem os artefatos reportados. main/6a9966b,sem commits;WORLD-005preservada.

## Relatório final — 2026-11-15

**Resultado:** DONE. Todos os critérios desta correção foram satisfeitos para o caminho ambiental atual, versão3. Nenhum critério foi dispensado. A versão2 continua reproduzindo a falha original por compatibilidade; use versão3 para conferir a correção.

### Diagnóstico confirmado
A parede da seed7 já estava nos dados de altura: a condição longitudinal do rio interrompia uma escavação profunda num plano transversal, sem transição para o receptor no nível do mar. O perfil de largura fixa e as bermas que elevavam terreno baixo criavam taludes excessivos e faixas artificiais. A mudança booleana em insideLake também podia trocar o operador abruptamente. Não era uma troca de índices, normais ou winding; a triangulação apenas expunha a deformação dos dados.

### Alterações finais
- Ambiente3 com envelopes contínuos de escavação, pontas arredondadas do rio e receptores inclusive ao nível do mar. Composição por min nunca eleva terreno ou desfaz outro corte; a largura da transição acompanha a profundidade, sem suavizar globalmente o ruído base.
- Água por potenciais contínuos/max e influências regionais vizinhas avaliadas em coordenadas globais, com suporte e cache limitados. Núcleos de lagos permanecem planos; o potencial externo pode inclinar-se onde o relevo não contém o nível.
- Ambiente1/2 preservado: fingerprints completos anteriores/atuais iguais dentro do Unity e dentro do.NET. As diferenças entre runtimes foram explicitamente verificadas e documentadas; não há promessa de identidade entre plataformas.
- EnvironmentDemo seleciona3. Gerador de terreno v1, builder/presentation da FIX-001, ChunkManager, shader, materiais, cenas antigas, pacotes e ProjectSettings não foram modificados nesta tarefa.
- Regressões de borda comparam segmentos de água positivos; tangência em um único ponto com vizinho seco não é fissura. O experimento de snap foi removido e não integra a solução.

### Validação final
| Verificação | Status | Evidência |
| --- | --- | --- |
| Compilação C#/shader | PASS | Unity6000.6.9f1 executa tipos/testes; shaderError=False nas18capturas finais. |
| EditMode | PASS |127/127,0falhas/ignorados,31.7535708s; editmode-final.xml. |
| PlayMode | PASS |4/4,0falhas/ignorados,0.3869893s; playmode-final.xml. |
| Sensibilidade da regressão | PASS |Dois casos centrais selecionando versão2 falham no degrau e na normal quase vertical; passam em3. |
| Seed7/gradientes | PASS |Máximo derivado14.330421→2.861179(igual ao base);149→0arestas artificiais>4;82→0amostras elevadas. |
| Geometria real dos25chunks | PASS |44→0faces com normalY<0.25;0degenerados;área projetada102400antes/depois. |
| Múltiplas seeds/mar | PASS |4seeds×4mares,grade de terra/água e diagonais em[-32,288]²,com limites regionais;matriz comparativa.NET também preservada. |
| Continuidade/determinismo/biomas/recursos | PASS |Suíte anterior+novas bordas regionais,fixtures1/2,revisita com vértices idênticos e IDs por versão. |
| Streaming/liberação | PASS |Seed7/r2 mantém25meshes após estabilizar;revisita/liberação até baseline. Caminho anterior r1 também passa. |
| Visual Editor/Play | PASS |9enquadramentos/18PNGs reais URP;25chunks/fila0;pares Editor/Play idênticos por SHA256. Parede/bermas artificiais da seed7 removidas;relevo legítimo preservado. |
| Console | PASS |Editor0erros/1aviso de metadado URP;Play0erros/0avisos. Nenhum Clear do agente;aviso externo preservado,não tratado como corrigido. |
| Integridade | PASS |160inputs Assets/ProjectSettings da cópia iguais por hash;GUIDs únicos;arquivos protegidos e trabalho anterior preservados;diff --check sem erros. |
| Editor interativo original/MCP | NOT_RUN |MCP indisponível dentro/fora do sandbox. Validação feita no próprio Unity em cópia isolada com os mesmos fontes e pacotes locais existentes. |
| Build/player/outras plataformas/configuração máxima/heap/VRAM | NOT_RUN |Não são inferidos das validações locais. |

### Arquivos desta tarefa
Modificados em relação à entrada(não confundir com o diff acumulado da FIX-001):
- Assets/_Project/World/Environment/EnvironmentGenerator.cs e EnvironmentData.cs.
- Assets/_Project/Scenes/EnvironmentDemo.unity(somente versão ambiental).
- Assets/_Project/Tests/EditMode/EnvironmentGeneratorTests.cs; Tests/Presentation/EnvironmentStreamingTests.cs; Tests/StreamingPlayMode/EnvironmentLifecycleTests.cs.
- docs/ARCHITECTURE.md,CURRENT_STATE.md,HANDOFF.md; tasks/ACTIVE.md,BACKLOG.md e esta tarefa.
Criados: HydrologyRegressionTests.cs/.meta e docs/validation/WORLD-004-FIX-002/. Utilitários temporários apenas na cópia de validação e como.cs.txt nas evidências; nenhum utilitário foi instalado no runtime do projeto.

### Limitações remanescentes
- Hidrologia analítica estática, sem erosão/drenagem continental/equilíbrio hidrostático. Potenciais externos da água podem formar superfícies inclinadas, com grade limitada. Água continua opaca; não há gameplay novo.
- Facetas e declives legítimos do terreno base permanecem; nenhuma suavização indiscriminada de montanhas foi aplicada. Não se promete inspeção de todas as seeds/ângulos; as classes de deformações novas possuem limites de gradiente e regressões.
- Aviso de metadado do pacote URP já existente documentado; cache/pacote não alterados. Testes/capturas não controlaram a sessão interativa original.
- Chunk permanece atômico. Medida local de48amostras/8posições,N32/r1/max1/budget4ms,seed12345/mar38/v3,escalas2/1.2,batch semGPU:mediana1.2670ms/P952.9459ms/máximo11.5468ms. Não é teto de frame,comparação controlada de desempenho ou garantia deFPS.

### Checkpoint de encerramento
- main/6a9966b0168d3ba553c97ed7f7c8a2818f5bbca4; sem commits. Alterações acumuladas da FIX-001 preservadas e seis arquivos de fonte/cena/testes modificados nesta tarefa, além do novo teste/meta e documentação.
- Concluído: diagnóstico anterior à edição, correção de origem versionada, regressões, visual/Console, evidências e continuidade. Nenhuma implementação parcial neste escopo.
- Relatório de evidências: docs/validation/WORLD-004-FIX-002/README.md; auditoria final em final-audit.json.
- Próxima ação: revisão do usuário. Para reproduzir, reabrir EnvironmentDemo e selecionar seed7/N32/h2/v1.2/r2/mar38/**versão3**,aguardando25chunks/fila0. Não usar versão2como se fosse a correção; ela preserva o resultado anterior.
- WORLD-005 não editada/movida/iniciada. Não criar commit sem autorização; não iniciar outra tarefa automaticamente.

## Auditoria final
PASS — final-audit.json confirma127EditMode+4PlayMode,dois controles negativos emv2,160inputs sem divergências,8capturas anteriores+18finais,legados1/2preservados nos dois runtimes,arquivos protegidos intactos e diff --check limpo. HEAD não mudou;nenhum commit criado.
