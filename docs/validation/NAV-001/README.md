# Validação NAV-001

## Linha de base

Unity 6000.6.9f1, Linux Editor. Uma consulta à PersistenceDemo já em Play via MCP retornou 24.180 células navegáveis e 12.684 bloqueadas na janela 192 × 192, com origem lógica (−248, 256) e 3.767 recursos no catálogo. A construção registrada pela cena levou 17,4643 ms para a grade e 33,065 ms para o catálogo com rasterização. O atlas registrou 765 construções, 6.883 acertos e 13.082 recusas até a amostra. O tempo de simulação anterior à consulta e a distribuição por tipo de ordem são desconhecidos. A amostra indica pressão no atlas, sem atribuir um travamento individual à capacidade.

## Auditoria inicial do código

| Área | Comportamento observado |
| --- | --- |
| Quantização | Uma célula lógica por intervalo inteiro. Floor em poses, seleção e chunks preserva coordenadas negativas. A janela lógica e o streaming visual são independentes. |
| Conectividade | A*, Dijkstra e componentes usam quatro vizinhos. O movimento contínuo verifica ambas as células laterais em cruzamentos diagonais exatos quando a máscara está ativa. |
| Recursos | Árvore usa o círculo circunscrito da base do tronco, pedra usa o da base visível, ambos com variação determinística. Copa e ouro não bloqueiam. Rasterização considera interseção com o quadrado inteiro e soma folga de 0,22 nas cenas integradas. Sobreposição é contada. |
| Construções | Reserva retângulo de células separado da máscara de recursos. O movimento valida células do segmento, com centro lógico como ponto móvel. |
| Unidade | Aldeão 0,22 e soldado 0,30. A folga ambiental comum é 0,22. O steering combina raios dos vizinhos, usa busca local adicional e não define a classificação do terreno. Interação no movimento usa tolerância de 1,25 até a célula de interação; alcance de ataque padrão do soldado é 1,50. |
| Terreno | Qualquer vértice submerso bloqueia a célula. Inclinação é o maior gradiente entre os dois triângulos, limite 2,25 nas cenas integradas. Áreas secas próximas de margem podem compartilhar uma célula bloqueada. |
| Campos | Dijkstra reverso, custo de entrada, oito slots, invalidação por revisão e componente. Células finitas apontam para um vizinho cardinal de potencial menor. Um Move de grupo segue até a âncora comum antes do trecho para a posição individual, o que pode aumentar o percurso. |
| Evitação | Correção, desaceleração e direção preferida podem competir. Congestionamento atual usa projeção do avanço por tick. Recuperação existente alterna lado em janelas de ticks e não equivale à detecção de deadlock. |
| Fronteira de obstáculo | A interseção aceita tangência, mas os limites candidatos usam floor em ambos os lados. Quando o mínimo coincide com uma borda inteira, a célula tangente do lado inferior nem chega a ser verificada. |

A rasterização de quadrados inteiros é conservadora e não resolve corredores subcélula. A divergência entre folga comum e raio de soldado precisa de avaliação com aproximações de coleta, combate e construções antes de alterar a calibração geral.

## Correção confirmada e regressões

No cenário sintético com centro (−0,5; 0,5), raio 0,50 e folga zero, o MCP mediu custo 2 em (−2, 0) e 0 em (0, 0) antes da correção. Ambas as células tangenciam o mesmo círculo. Agora o limite inferior de candidatos é `ceil(centro − raio) − 1`, consistente com quadrados fechados no teste de interseção. O teste em Edit Mode verificou custo zero em ambos os lados, inclusive na fronteira negativa, e abertura em ambos após reduzir o raio do cenário para 0,49. A folga de produção, os raios padrão, a declividade e o algoritmo de busca mantêm os mesmos valores.

Outras regressões focadas verificam rota no corredor entre árvores, bloqueio pela máscara do corredor junto à rocha, reabertura após remoção, A* e campo na borda do chunk e custo integrado igual ao custo da rota. O teste da rocha confirma a política conservadora atual. Ele não estabelece que a passagem contínua seja fisicamente impossível.

## Ferramentas no Editor

Em Play, habilite Gizmos na Scene View. No componente `RtsInteractionDemo`, o Inspector permite ligar individualmente grade, ocupação, unidades, campo e rota. As opções começam desligadas em PersistenceDemo e RTSWorldIntegratedDemo. A cena NavigationDiagnosticsDemo usa a seed 7 e abre com as opções ligadas. Selecione uma unidade na Game View e emita Move para desenhar a ordem solicitada, o destino efetivo e o resultado, inclusive para célula bloqueada. O painel existente também permite acionar fluxos opostos.

Verde a amarelo indica custo local crescente, vermelho bloqueios, cinza indisponibilidade e magenta células transitáveis que o campo ativo não alcança. Aros branco e amarelo distinguem raio lógico de recurso e raio com folga. As setas do campo usam ciano a azul para custo integrado, com limite de 96 passos por consulta. Azul também sinaliza custo fora desse limite. A grade tem até 33 × 33 células. Cada desenho mostra até 32 unidades selecionadas, 128 recursos e 128 segmentos de rota.

- [Grade e ocupação](grid-and-occupancy.png)
- [Traçado de uma ordem Move na seed 7](selected-route.png)

## Profiling de simulação

NavigationDiagnosticsDemo em Play via Unity MCP, sem computar a pintura da Scene View no trecho cronometrado. Após reconstruir cada população, o ensaio emitiu Fluxos opostos, buscou A* entre (−173, 351) e (−134, 351) e mediu 120 ticks síncronos após 16 de aquecimento. A invalidação inclui publicar indisponibilidade de um recurso e atualizar a sessão. A coluna de campo soma as duas construções do atlas e exclui o campo auxiliar de escolha dos destinos. Todos os comandos, buscas e alterações foram aceitos.

| Unidades | Catálogo e classificação ms | Grade ms | A* ms | Dois campos ms | Tick médio ms | Invalidação ms |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 100 | 33,9571 | 17,3782 | 8,5220 | 33,6048 | 0,02491 | 0,8203 |
| 500 | 32,6553 | 17,5764 | 15,4806 | 26,5554 | 0,04350 | 0,9349 |
| 1.000 | 32,6597 | 17,0249 | 8,6907 | 22,7974 | 0,06263 | 1,0757 |
| 5.000 | 41,0040 | 16,9940 | 8,6109 | 22,7764 | 0,20666 | 1,9400 |

Foram 0 bytes gerenciados na thread chamadora durante as janelas de 120 ticks. Bytes nativos e heap total NOT_RUN. As quatro buscas encontraram 244 células e cada alteração invalidou dois campos. São amostras únicas por carga, sem inferência de mudança de desempenho. [CSV](profiling.csv)

## Perfil da visualização

A amostra do marcador `RTSWorld.NavigationDiagnostics.Draw` foi coletada em NavigationDiagnosticsDemo com 100 unidades, Scene View visível, opções da cena ligadas, raio de 8 células e nenhuma unidade selecionada. Em 30 amostras após três repaints de aquecimento, a média foi 1,353 ms, mediana 1,223 ms e máximo 3,366 ms. Com as cinco opções desligadas, as 30 leituras do marcador ficaram em zero. O retorno antecipado ocorre antes da coleta de dados e do marcador. A amostra ligada mede a visualização de grade e ocupação, sem o custo adicional de uma rota selecionada. Visualização com seleção nas outras cargas NOT_RUN.

## Ordem rejeitada na PersistenceDemo

Com a sessão reconstruída na seed 7, 100 unidades e sem avanço manual antes do comando, `Move(-134, 351)` para as 50 unidades do jogador retornou `Unreachable` com crowd ligado e desligado. O destino e as 50 origens eram livres. O campo alcançava 43 origens. Outras sete, da região entre X −160 e −157, estavam em um componente separado. A rejeição do lote preserva as ordens existentes.

Em sessões reconstruídas separadamente, remover em memória o depósito concluído em (−151, 351) **ou** o centro principal em (−150, 348) passou a permitir o acesso das 50 origens. Remover apenas a obra em (−155, 348) manteve 43 origens alcançáveis. Os dois prédios concluídos formam um corte no grafo para essa ordem. O efeito apareceu também sem avoidance. Construir antes do spawn foi avaliado em uma sessão temporária e fez o spawn escolher outro componente, gerando apenas 51 das 100 unidades e nenhuma unidade do jogador alcançando o destino. A mudança foi descartada.

## Validação executada

- **PASS** Unity 6000.6.9f1, compilação e inspeção final do Console via MCP. Zero erros ou avisos na inspeção final.
- **PASS** Edit Mode, 283/283 em 35,857 s. Play Mode, 21/21 em 27,872 s. Sem falhas ou ignorados.
- **PASS** NavigationDiagnosticsDemo aberta em Play. Grade e ocupação desenhadas e capturadas. Uma ordem Move selecionada foi aceita e sua rota foi capturada.
- **PASS** Experimentos controlados na PersistenceDemo reproduziram a ordem rejeitada e isolaram a dependência dos prédios. O teste foi executado em Play e sem salvar alterações no arquivo da cena.
- O Editor ficou em NavigationDiagnosticsDemo, fora de Play.

## Lacunas registradas antes da continuação

- A classe de passagem bloqueada por rasterização foi reproduzida em teste sintético. Na variante árvore e rocha, a rocha a 1,0 célula da linha central rasteriza a célula adjacente com raio efetivo de 0,71. Uma trajetória pontual pelo centro daquela célula preservaria a folga ambiental do cenário. O produto ainda depende da máscara de células inteiras. Remover a célula da máscara sem validar a trajetória contínua também liberaria posições inseguras naquela mesma célula.
- Nenhum vínculo individual entre as recusas do atlas e os travamentos relatados na PersistenceDemo foi validado. Falta reproduzir uma ordem afetada com estados, composição do steering e revisão do campo registrados sob condições equivalentes.
- As regressões existentes cobrem classificação de água/declive, bloqueios, conectividade, construção, streaming e persistência. Falta validar todos os 14 cenários como matriz de aceitação, incluindo travessia junto à margem, construção junto de recurso e largura real insuficiente para o perfil de unidade.
- A leitura de grade e campo, os aros e o traçado foram observados em Play. Faltam a validação individual dos estados de rejeição no Inspector e uma medição com rota e direção desejada do steering. A ferramenta exibe a direção do campo e a intenção de movimento direto conforme os respectivos modos.

Nesse checkpoint, a NAV-002 aguardava a calibração segura e a matriz de aceitação.

## Continuação — posicionamento inicial na PersistenceDemo

O bloqueio era legítimo após a publicação dos footprints. O defeito estava na escolha das posições iniciais. O posicionamento verificava o próprio retângulo e pelo menos um acesso, sem verificar se as saídas livres antes conectadas continuavam ligadas. Com os dois prédios, a única ligação cardinal entre as origens afetadas e o destino deixava de existir.

A seleção de posições iniciais nas demos passou a verificar a conectividade do perímetro em busca cardinal, excluindo o retângulo candidato. Cada vizinho livre do perímetro poderia chegar aos demais pelo retângulo antes da reserva. Se todos os vizinhos continuam conectados depois de excluir o retângulo, qualquer trajeto externo que o cruzava pode contorná-lo. A busca para ao alcançar todos os vizinhos. O spawn e a semântica dos footprints foram preservados.

| Prédio na seed 7 | Origem anterior | Origem validada |
| --- | --- | --- |
| Depósito concluído | (−151, 351) | (−154, 349) |
| Centro principal | (−150, 348) | (−149, 349) |
| Depósito planejado | (−155, 348) | (−155, 353) |

A regressão `StartingBuildingsPreserveSpawnConnectivityWithAndWithoutAvoidance` passou com avoidance ligado e desligado. Verificou 100 unidades em células livres, ocupação de toda a área dos três prédios, conectividade das 100 origens com (−134, 351), aceitação da ordem para as 50 unidades do jogador e retorno `Unreachable` para uma célula livre de outra componente. Os cinco testes novos de acessibilidade em Edit Mode também passaram na suíte 288/288.

A inspeção MCP reconstruiu os footprints antigos sobre a mesma grade ambiental e as mesmas posições de spawn. O campo antigo alcançou 43/50 origens e o novo 50/50. As sete origens excluídas coincidiram com as do handoff. A busca inicial com preservação de passagens usa até 32 células para acomodar a população densa. Com o limite anterior de 16, o candidato legado a depósito em 5.000 unidades cortava a conectividade e o cenário ficava sem depósito. Com o limite ampliado, o depósito em (−179, 324) foi aceito. A configuração antiga também não encontrava centro nem obra em 5.000 unidades.

Amostras únicas do tempo total de reconstrução na PersistenceDemo em Play via MCP, incluindo terreno, catálogo, grade, spawn e construções. Valores absolutos, sem comparação de velocidade com a versão anterior.

| População solicitada | Criadas | Prédios iniciais | Reconstrução ms |
| ---: | ---: | ---: | ---: |
| 100 | 100 | 3 | 144,61 |
| 500 | 500 | 3 | 121,49 |
| 1.000 | 1.000 | 3 | 136,39 |
| 5.000 | 5.000 | 1 | 171,02 |

## Matriz anterior à representação de faixas

| Nº | Cenário | Resultado | Evidência |
| ---: | --- | --- | --- |
| 1 | Corredor suficientemente largo | PASS | `ParallelObstaclesClassifyWideAndPhysicallyNarrowCorridors`, faixa entre círculos de raio 0,25. |
| 2 | Corredor estreito demais | PASS | Mesmo teste com raio 0,85. Vão de 0,30, diâmetro do soldado de 0,60. |
| 3 | Duas árvores próximas | PASS | `AdjacentResourcesPreserveOnlyCorridorsThatFitTheGrid`, variante Tree. |
| 4 | Árvore próxima de rocha | FAIL | A* rejeitou o corredor. MCP mediu 0,51 de distância mínima livre ao longo da linha central, para soldado de raio 0,30. A rocha de raio 0,49 mais folga de 0,22 intercepta o quadrado da célula inteira. |
| 5 | Construção próxima de recurso | PASS | MCP, depósito em (0, 0) e árvore em (2, 0). Ocupações Building e Obstacle. A* contornou ambas em oito passos. |
| 6 | Margem de água | PASS | `DryShoreRouteDoesNotCrossWater`, rota entre células secas, destino adjacente molhado rejeitado. |
| 7 | Terreno inclinado | PASS | `ClassifiesWaterSlopeCostsAndMissingRegions`, mesma célula com custo acima de 1 e depois bloqueio com limite de declividade zero. |
| 8 | Obstáculo isolado | PASS | `IsolatedTreeBlocksItsCellAndCanBeCircumvented`, rota contorna tronco. |
| 9 | Região completamente bloqueada | PASS | `FullyBlockedRegionHasNoPath`. |
| 10 | Destino dentro de obstáculo | PASS | `IsolatedTreeBlocksItsCellAndCanBeCircumvented`, centro bloqueado rejeitado. |
| 11 | Destino em componente desconectado | PASS | `CacheSharesFieldsAndInvalidatesWhenGridChanges` e regressão da PersistenceDemo nos dois modos de avoidance. |
| 12 | Coordenadas negativas | PASS | `TangentCellsAreBlockedOnBothSidesOfNegativeChunkBoundary` e `OccupancyAndInteractionCrossNegativeChunkBoundary`. |
| 13 | Fronteiras de chunks | PASS | `PathAndFlowCrossChunkBoundariesThroughTheOnlyOpening` e `GroupCrossesChunksAndStopsAtDistinctSpacedCells`. |
| 14 | Obstáculo alterado em runtime | PASS | `RemovingOneNeighbourPreservesOverlapAndUnrelatedField`, remoção, reativação e mudança de raio. |

O teste que cobre a variante Stone da linha 4 verifica o comportamento atual do rasterizador e passa como caracterização. O critério de acessibilidade física da matriz segue em FAIL. Liberar o quadrado inteiro também liberaria pontos que interceptam a rocha. A capacidade de representar e validar trajetos com folga dentro de uma célula precisa ser tratada em conjunto pela busca e pelo movimento.


## Validação anterior às faixas centrais

- **PASS** compilação no Unity 6000.6.9f1, Edit Mode 288/288 (35,129 s) e Play Mode 22/22 (29,825 s) na versão final. Play Mode inclui streaming, persistência e a regressão de conectividade. Console inspecionado com zero erros e avisos.
- **PASS** inspeção MCP em Play da PersistenceDemo e da NavigationDiagnosticsDemo. Na cena de diagnóstico, 100 unidades, ordem de fluxos opostos aceita e dois campos construídos. Editor ao final na NavigationDiagnosticsDemo, fora de Play, cena limpa.
- **FAIL** matriz obrigatória, 13/14. NAV-001 bloqueada, NAV-002 ainda aguardando o gate.

## Verificação geométrica da passagem

Centros em (0,5; −0,5) e (0,5; 1,5), raios lógicos 0,17457121 e 0,49. O vão entre superfícies no eixo dos centros mede 1,33542879. Em Z=0,5 a distância mínima até a superfície é 0,51, deixando 0,21 após descontar o raio do soldado. O intervalo transversal para o centro de um soldado entre os círculos nesse ponto é de −0,02543 a 0,71. Valores confirmados por consulta MCP antes de alterar a simulação.

### Escolha de representação

A validação contínua contra círculos durante a busca e cada tick exigiria publicar geometria espacial nativa e consultar obstáculos repetidamente. Refinar a grade localmente exigiria mapear células de resoluções diferentes para o atlas, para os caches e para os waypoints. Manter um campo por raio aumentaria a pressão no atlas de oito slots. A faixa central representa um portal cardinal seguro sem esses custos. Cada célula parcialmente ocupada pode admitir uma faixa horizontal, uma vertical ou ambas. O centro é a interseção permitida para mudança de eixo.

Para a faixa, a distância entre o retângulo de centro navegável e cada círculo é testada com raio do recurso somado ao maior valor entre a folga configurada e o raio máximo suportado de unidade (0,49). A folga ambiental anterior funciona como afastamento mínimo do centro. Os raios e as regras das células inteiras permanecem os mesmos. A meia-largura da faixa é 0,01. O terreno, a água e as estruturas ainda podem invalidar toda a célula. A máscara grossa continua disponível para validar construção.

A*, Dijkstra, componentes conectados e posicionamento inicial verificam a mesma restrição cardinal. O job Burst usa a máscara nativa e valida o intervalo de cada segmento dentro de cada célula. Ao entrar na faixa, a unidade se alinha antes de cruzar a borda. O steering lateral é suspenso nas pernas restritas. O custo de busca continua limitado à janela e o campo continua compartilhado. A representação aceita passagens alinhadas às faixas comprovadas. Passagens deslocadas do centro ou que exijam uma curva fora da faixa continuam conservadoramente bloqueadas.

A atualização em runtime compara as máscaras antes e depois da alteração. A revisão de um campo é reavaliada se a faixa mudar mesmo quando a ocupação grossa permanece igual. O teste cobre a travessia, a rejeição de movimento transversal, a concordância entre os algoritmos e o fechamento da faixa ao ampliar o raio do cenário.

No teste de Save/Load, a atualização de recursos durante a restauração pode reconstruir a ocupação sem solicitar uma lista de revisão. Essa chamada foi preservada para reconstruir as faixas sem registrar revisões transitórias.


## Validação final — faixas centrais

- **PASS** compilação no Unity 6000.6.9f1. Edit Mode 290/290 em 38,384 s. Play Mode 22/22 em 33,293 s. Streaming, IA, combate, construções, economia e persistência incluídos. Console final sem erros ou avisos.
- **PASS** matriz completa. As linhas 1–3 e 6–14 foram reexecutadas nas suítes. Na linha 4, `SoldierCrossesTreeAndRockOnlyAlongTheSafeLane` aprovou com avoidance ligado e desligado, incluindo clearance de cada segmento, concordância A*/Flow Field e bloqueio do desvio transversal. A linha 5 foi repetida via MCP com o mesmo depósito e recurso. O caminho fez oito passos sem cruzar nenhuma das ocupações.
- **PASS** inspeção em NavigationDiagnosticsDemo com ordem válida e rejeição de Move em água. O campo válido anterior permaneceu ativo. A seta branca representa a direção nominal ao próximo ponto de rota e a seta amarela mede a velocidade efetiva. O Editor ficou fora de Play. [Captura da rejeição](destination-rejected-water.png)
- O teste de persistência agora controla o início da construção após a primeira entrega. A sequência anterior dependia de a entrega ocorrer antes de uma obra que, com a navegação revisada, terminava mais cedo. As verificações de carga, obra ativa, estado restaurado, continuidade e streaming foram mantidas.
- **DONE** NAV-001. NAV-002 liberada.

Repetição do roteiro de profiling da NavigationDiagnosticsDemo na mesma seed. Catálogo de 33,41–35,24 ms, grade de 16,62–17,19 ms, A* de 11,25–14,24 ms e dois campos de 31,03–41,24 ms. Na coleta anterior, A* ficou em 8,52–15,48 ms e dois campos em 22,78–33,60 ms. O custo de campo aumentou em parte das amostras. A rota ponderada passou de 244 para 258 células no par de pontos medido. O tick médio em 5.000 foi 0,20607 ms, ante 0,20666 ms na amostra anterior. As janelas de 120 ticks alocaram 0 bytes gerenciados na thread chamadora. A máscara de faixas agrega dois vetores de 36.864 inteiros, 288 KiB de payload na janela da cena. As amostras individuais variam com o Editor e não estabelecem meta de player. [Dados das quatro populações](profiling-central-lanes.csv)

As pernas restritas priorizam a folga do ambiente e suspendem a correção lateral. O comportamento de multidões no interior de uma faixa de uma unidade continua sob investigação na NAV-002.
