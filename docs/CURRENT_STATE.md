# Current State

## VISUAL-002 — biblioteca ambiental e biomas

- VisualStyleDemo com 30 variantes ambientais, três LODs, paletas de terreno por clima, água por profundidade e vegetação de margem. Placements lógicos, terreno, navegação e persistência preservados.
- PASS Edit Mode 302/302 e Play Mode 28/28. EnvironmentBenchmarkDemo reutiliza a cena e controla densidade, sombras, rasteira, pan e zoom. Player Linux Development e Editor executaram 17 configurações cada.
- Em 5.000 unidades, densidade média e sombras, CPU de apresentação foi de 1,909 ms no Editor e 1,437 ms no Player. GPU Frame Time e batches totais NOT_RUN. [Capturas e benchmarks](validation/VISUAL-002/README.md)


## VISUAL-001A — apoio e continuidade de streaming

- Fundação nivelada na maior altura do footprint, com base adaptativa até a menor amostra. Árvores e rochas instanciadas a partir dos placements dos chunks residentes. Três LODs com histerese e cobertura de câmera protegida durante carregamento.
- PASS 300/300 Edit Mode, 27/27 Play Mode e Console sem erros/avisos. Fundação perto da água, criação dinâmica, destruição, Save/Load, mudança rápida de direção e retorno verificados. No zoom máximo da cena, 25 amostras do viewport cobertas.
- Em 5.000 unidades, instancing padrão a 2560 × 1440 mediu 1,928 ms de apresentação, frente a 1,960 ms na referência. Triângulos de frame aumentaram cerca de 22%. GPU Frame Time, total de batches e Player NOT_RUN. [Relatório e CSV](validation/VISUAL-001A/README.md)


## VISUAL-001 — direção artística e renderização

- VisualStyleDemo verificada com cinco aldeões, cinco soldados, dois prédios, três árvores de referência e rocha procedural, sob iluminação diurna. A apresentação lê o ECS e compartilha material URP Lit. Idle, Walk, culling e LOD operam na apresentação.
- PASS 296/296 Edit Mode, 26/26 Play Mode e Console sem erros/avisos. MCP verificou zoom, seleção, Gather/Stop e descarregamento/recarregamento visual sem perder unidades.
- Em 5.000 unidades a 2560 × 1440 no Editor, o caminho instanciado apresentou 2,659 ms e 111 draw calls, frente a 4,177 ms e cerca de 3.008 no MeshRenderer. GPU Frame Time e Entities Graphics ficaram NOT_RUN. [Validação VISUAL-001](validation/VISUAL-001/README.md)


**Atualizado:** 2026-11-20 (America/Bahia)
**Estado:** VISUAL-002 DONE. Nenhuma tarefa ativa.

## NAV-002 — progresso de navegação em grupos

- A unidade entra no trecho final de formação quando já está numa célula do percurso. No ensaio de nove soldados, o trajeto de referência caiu de 15,628 para 11,000 unidades.
- Recuperação lateral e arbitragem de passagem restrita verificadas. A CrowdRecoveryDemo executou os 12 roteiros. PASS 296/296 Edit Mode, 24/24 Play Mode e profiling pareado em 100 a 5.000 unidades. [Relatório NAV-002](validation/NAV-002/README.md)

## NAV-001 — diagnóstico de navegação

- NavigationDiagnosticsDemo expõe grade, ocupação, campo e rota selecionada no Editor. A rasterização inclui tangências simétricas. Os raios padrão permanecem 0,22 para aldeão e 0,30 para soldado e a folga ambiental da cena é 0,22.
- Na PersistenceDemo, o posicionamento inicial preserva a conexão em torno do retângulo candidato. Seed 7 com 100 unidades e três prédios. A ordem reproduzida alcança as 50 origens do jogador, ante 43 com a ocupação antiga. Regressão passou com avoidance ligado e desligado.
- Matriz com 14 PASS. Faixas centrais compartilham a transitabilidade entre A*, Flow Fields e movimento. Edit Mode 290/290 e Play Mode 22/22, com Console sem erros ou avisos. [Validação NAV-001](validation/NAV-001/README.md)

## ECS-010 — persistência local da simulação

- PersistenceDemo salva e restaura mundo procedural, recursos modificados, unidades, ordens, combate, IA, estoques e obras. O arquivo v1 usa IDs estáveis e verificação de integridade. A apresentação passa a usar a sessão reconstruída e seus recursos após o carregamento.
- PASS compilação, 280/280 Edit Mode e 21/21 Play Mode. Console final sem erros ou avisos. A reconstrução ainda ocupa o thread principal. [Validação e profiling ECS-010](validation/ECS-010/README.md)

## ECS-009 — autonomia de unidades

- UnitAIDemo integra trabalho de aldeões, depósitos, obra planejada e soldados em guarda. A decisão por entidade roda no passo fixo e dá prioridade às ordens explícitas. Stop suspende a autonomia até nova ordem válida ou reativação.
- PASS compilação, 273/273 Edit Mode, 19/19 Play Mode, streaming, inspeção MCP e profiling de 100 a 5.000 unidades. Com 2.500 aldeões autônomos e 5.000 unidades presentes, o ensaio sintético registrou tick médio de 1,571 ms e 33 entregas em 12 s simulados. [Validação ECS-009](validation/ECS-009/README.md)

## ECS-008 — construções e depósitos físicos

- ConstructionDemo oferece posicionamento lógico com prévia, custos em madeira e pedra, ordem Build com múltiplos aldeões, progresso, cancelamento e vida. Construções reservam a área desde o planejamento. Destruição libera a navegação. EconomyDemo entrega em depósito físico e preserva a carga quando perde acesso.
- PASS compilação, 262/262 Edit Mode, 17/17 Play Mode e inspeção da ConstructionDemo via Unity MCP com mouse virtual. Console final sem erros ou avisos. [Validação e profiling](validation/ECS-008/README.md)

## ECS-007 — evitação local e multidões

- CrowdMovementDemo habilita índice espacial nativo e steering Burst no passo fixo. Inclui fluxos opostos e convergentes, coleta, combate, streaming e diagnóstico. Move mantém posições individuais. O atlas mantém oito campos.
- PASS compilação, 258/258 Edit Mode, 16/16 Play Mode e inspeção Unity MCP. O ensaio pareado de 5.000 unidades reduziu a média de unidades sobrepostas de 4.994,1 para 4.628,3, com tick médio de 0,3059 ms usando crowd. Destinos compartilhados pequenos continuam sujeitos a congestionamento. [Validação ECS-007](validation/ECS-007/README.md)

## ECS-006 — combate e desempenho

- CombatDemo integra facções, Attack corpo a corpo, seleção, indicadores, remoção e economia ao mundo procedural. A navegação reutiliza a grade ambiental e os Flow Fields.
- PASS 246/246 Edit Mode, 15/15 Play Mode, input virtual e inspeção visual via Unity MCP. Em 5.000 unidades, o cenário integrado teve 4.500 ordens e pico de oito unidades no alcance. O ensaio sintético confirmou 5.000 golpes e mortes no mesmo tick. Dados e limitações em [validação ECS-006](validation/ECS-006/README.md). Mouse físico, player e GPU NOT_RUN.

## ECS-005 — coleta e economia

- EconomyDemo permite coletar madeira e pedra com aldeões, transportar, entregar ao depósito e retomar o trabalho. Estoque só aumenta na entrega. Move e Stop mantêm a carga.
- Esgotamento libera ocupação e atualiza mesh. Coleta e estoque sobrevivem ao streaming visual. Economia em memória por sessão, sem persistência externa.
- PASS 233/233 Edit Mode, 13/13 Play Mode, input virtual e 60 madeira + 80 pedra entregues. Profiling para 1 a 100 trabalhadores e capturas em docs/validation/ECS-005. Player e GPU NOT_RUN.

## INTEGRATION-002 — obstáculos ambientais

- Troncos e rochas bloqueiam a grade com folga configurável. Ouro atravessável. Cache seletivo e aproximação lógica verificados, independentes da residência visual.
- PASS 226/226 Edit Mode, 12/12 Play Mode e inspeção MCP com 100/1.000/5.000 unidades. Evidências em docs/validation/INTEGRATION-002.

## INTEGRATION-001 — cena unificada (2026-11-18)

- RTSWorldIntegratedDemo junta biomas, água, recursos, streaming, câmera e controles ECS. A configuração comum mantém altura, água e navegação coerentes. Spawn usa células alcançáveis. Tree, Stone e Gold são atravessáveis e têm catálogo lógico por chunk.
- PASS 223/223 Edit Mode e 12/12 Play Mode. Inspeção MCP, 100 posições distintas, Stop, rejeição de água, troca de seed e streaming com reconstrução determinística verificados. Profiling e capturas em `docs/validation/INTEGRATION-001`. Build de player, mouse físico e GPU timing NOT_RUN.
- Git main/b207bd7, alterações locais sem commit.

## ECS-004 — seleção e ordens RTS (2026-11-18)

- RTSInteractionDemo conecta a câmera WORLD-005, terreno procedural e streaming com clique, retângulo, Shift, Ctrl, feedback e ordens Move/Stop em entidades ECS.
- Grupo compartilha campo de navegação com posições finais distintas e espaçadas. Stop usa Espaço e preserva a seleção. A validação de comandos é independente da câmera.
- PASS Unity 6000.6.9f1, 221/221 Edit Mode, 11/11 Play Mode, inspeção MCP com input virtual e chegada de 100 unidades a 100 posições. Profiling de 100 a 5.000 e captura em `docs/validation/ECS-004`. Player e mouse físico NOT_RUN.
- Git main/b207bd7, alterações locais sem commit.

## ECS-003 — navegação regional (2026-11-18)

- A* e campos de fluxo em janela lógica global de até 256 células por eixo. Custos por declividade e bloqueio por água, obstáculos e indisponibilidade do chunk lógico. Comando em lote no passo fixo ECS, campos em atlas nativo, dispersão por pontos internos e chegada sem ultrapassar obstáculos.
- NavigationDemo com grupos, barreiras, destinos configuráveis, diagnóstico e HUD de tempos. ADR-0006 registra a decisão de adiar hierarquia e os limites do sistema.
- PASS Unity 6000.6.9f1, 204/204 Edit Mode, 10/10 Play Mode, auditoria de metas e inspeção visual. 5.000/5.000 unidades chegaram na cena. Profiling em `docs/validation/ECS-003/README.md`. Build/player e outras plataformas NOT_RUN.
- Git main/b207bd7, alterações locais sem commit.

## Unidades ECS verificadas — 2026-11-15

- ECS-002 concluída: aldeões/soldados, definições configuráveis, ownership/IDs por sessão, spawn/destruição em lote, movimento fixo Jobs/Burst com limites/orientação, pausa/retomada e elevação triangular por snapshot do contrato ECS-001.
- Simulação independente da apresentação/residência visual. Sem dados de terreno, congela pose; unload real testado sem perda de unidades. Snapshot limitado a área inteira de até 256 células/eixo, sem navegação, coleta, combate, rede ou persistência.
- UnitsSimulationDemo usa malha compartilhada e RenderMeshInstanced, tipos distintos, controles de carga/pausa/apresentação e HUD; sem GameObject individual por unidade nem pacote adicional. Fundação anterior preservada.
- PASS atual: compilação, 195/195 EditMode, 9/9 PlayMode, cargas 100/500/1000/5000, cena real em Play e teardown. Console sem erros/avisos; Play desligado, cena limpa, zero Worlds próprios.
- Profiling local Editor: média por tick completo para 5000 = 0,05286 ms (120 amostras, 16 ticks de aquecimento); cena real 5000 = 2,60764 ms/frame médio em 120 amostras. Condições, CSVs e limites em docs/validation/ECS-002/README.md; não são metas de player. Build/player/outras plataformas/heap/VRAM/GPU timing NOT_RUN.
- Decisão ADR-0005 e arquitetura atualizadas. Git main/54ef579, sem commits, alterações preexistentes preservadas (incluindo RTSCameraDemo seed 7→123). Nenhuma próxima tarefa iniciada.

## Fundação ECS verificada — 2026-11-15

- Entities 6.6.0; Burst 2.0.0 e Collections 6.6.0 existentes preservados; Mathematics/Jobs módulos do Unity 6000.6.9f1. URP 17.6.0 preservado.
- Simulation tem World isolado, componentes unmanaged com pose global double3/quaternion, passo fixo padrão 0,05 s, movimento IJobEntity/Burst e contrato managed com terreno versionado. Nenhuma migração WORLD ou dependência da apresentação.
- ECSFoundationDemo inicializa 256 entidades com HUD de quantidade/ticks/passo. Elevação somente no spawn; sem navegação, rede, persistência ou benchmark.
- PASS nesta sessão: 181/181 EditMode e 7/7 PlayMode; compilação, Burst nativo, inspeção da cena em Play e liberação do World. Console final vazio; cena limpa, Play desligado. Build/player/outras plataformas/performance NOT_RUN.
- Evidências: docs/validation/ECS-001/README.md; arquitetura/limites em ADR-0004 e docs/ARCHITECTURE.md.
- Git real: main/54ef57907c0e731f25adb0f8901e136ac8952326, sem commits. tasks/pending preexistente preservado. RTSCameraDemo apresentou seed 7→123 durante baseline, origem não confirmada, preservada para revisão. Nenhuma próxima tarefa iniciada.

## Projeto verificado

- WORLD-001 a WORLD-005 concluídas: terreno determinístico v1, renderização low-poly, streaming, biomas/água/recursos e câmera RTS. World e Presentation mantêm suas fronteiras; esta correção não acrescentou rede, persistência ou unidades.
- A versão ambiental atual é **4**, selecionada explicitamente em EnvironmentDemo e RTSCameraDemo. O terreno v1 e as versões ambientais 1, 2 e 3 foram preservados. A definição reproduzível inclui seed e versão; ResourceKey também inclui a versão ambiental.
- A FIX-003 comprovou dois problemas anteriores: o potencial lateral de água da v3 produzia superfícies inclinadas; bermas e cortes de campos da v2 produziam paredes e faixas de terreno. A v4 usa lagos constantes e rios nivelados por seção transversal, com perfil longitudinal monotônico limitado a 0,25 e à capacidade das margens. A troca de domínios ocorre em faixa seca; o leito somente corta o relevo. Não há suavização global nem offset de água.
- Permanece uma mesh/renderer por chunk, com o mesmo ownership de streaming. A apresentação reutiliza o gerador por definição, com cache de nove metadados regionais e sem reter chunks ambientais. Instrumentação opcional separa camadas e exporta amostras/triângulos; o replay do Editor verifica residência antes de capturar.
- A câmera RTS mantém InputSystem, WASD/MMB/scroll/QE e bordas opcionais. Foco lógico double, consulta de superfície triangulada independente dos chunks visíveis e ponte de streaming. Seu código permaneceu intacto na FIX-003; a consulta foi testada nas versões ambientais 1 a 4.

## Validação atual — FIX-003

- **PASS:** Unity 6000.6.9f1/LinuxEditor/URP via MCP; 172/172 EditMode e 6/6 PlayMode, sem falhas ou ignorados. Inclui regressões anteriores, perfis/margens/lagos, índices/normais/degeneração, coordenadas positivas e negativas, fronteiras e reconstrução após unload.
- **PASS:** inspeção visual real nas duas cenas, em Editor e Play. Vinte replays com quatro camadas e 25 chunks residentes/fila vazia, três seeds, múltiplos níveis do mar/escalas e frames X=0 e X=−5807. As 83 imagens incluem uma captura inicial inválida, preservada e excluída do aceite. Na região B, a maior inclinação de WaterOnly passou de 69,07° na v2 e 31,34° na v3 para 8,53° na v4, com H=2 e V=1,2.
- Console final do Editor e das duas cenas em Play: zero erros/avisos. Shader sem erro, nenhum replay temporário restante. Estado final: 25 meshes para 25 chunks, fila vazia, cena limpa e Play desligado. Auditoria dos 38 arquivos protegidos e dos GUIDs: PASS.
- Relatório, dados de 27 amostras, triângulos, reprodução, migração e incidentes: `docs/validation/WORLD-004-FIX-003/README.md`. Tarefa concluída em `tasks/completed/WORLD-004-FIX-003-hydrology-terrain-geometry.md`.

## Limites e continuidade

- Hidrologia analítica estática, sem hidrodinâmica ou drenagem continental. Relevo, ilhas e facetas legítimas permanecem; escalas extremas podem exagerar declives. A validação não cobre todas as seeds/coordenadas. Build/player, outras plataformas e medições de heap/VRAM: NOT_RUN.
- Geração atômica de chunks e carregamento transitório permanecem. Medição local de Step em Play, oito waypoints, raio 1, máximo de um chunk/frame e orçamento de 4 ms: mediana 1,3669 ms, p95 3,0432 ms, máximo 4,5793 ms. Não é uma medição de FPS; um chunk pode exceder o orçamento.
- Câmera sem floating origin, guarda lógica padrão ±32768 e frame vertical com escala uniforme; cobertura limitada ao raio 8. Segurança considera terreno/água, não colisores dos recursos.
- A raiz atual da RTSCameraDemo em X=−5807 foi preservada. O baseline inicial tinha X=0, comprovado por hash. A mesma pose mundial aponta para regiões lógicas diferentes nesses frames; zoom e foco históricos não foram inventados.
- Referência histórica da FIX-003: main/6a9966b. Na entrada ECS-001, HEAD já era 54ef579 e os trabalhos WORLD estavam incorporados; consultar o estado Git atual acima.
