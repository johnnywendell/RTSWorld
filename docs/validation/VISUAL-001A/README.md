# Validação VISUAL-001A

**Resultado.** VISUAL-001A concluída. A VisualStyleDemo usa fundações niveladas, vegetação coerente nos três LODs e retenção da vista durante o streaming de uma nova região.

## A — Reprodução anterior à correção

- VisualStyleDemo em Play via MCP, seed 7, 2560 × 1440. [Depósito parcialmente enterrado](building-before.png) e [fronteira das árvores](streaming-before.png).
- Depósito em (-151, 351), centro lógico 43,87969, mínimo 42,34067 e máximo 45,50093 no footprint. Com escala vertical 1,2, o vértice mais alto excede o apoio central em 1,945 m. Centro principal em (-153, 355), centro 43,51621 e faixa de 41,66670 a 45,74365. A implementação colocava ambos os modelos rígidos na altura central, sem fundação adaptada. As superfícies de terreno usam a mesma diagonal da interpolação do snapshot. As regras lógicas existentes avaliam ocupação, água e declividade da célula.
- Raio visual anterior de 24 células em torno de navigationCenter, com 161 placements candidatos. No zoom 16 havia 25 chunks residentes, fila vazia e 1.881 recursos ainda incorporados à malha procedural. A fronteira não era causada por descarregamento, culling ou LOD ambiental.
- VisualStyleDemo, ConstructionDemo, PersistenceDemo e RTSWorldIntegratedDemo têm 32 células por chunk, escala horizontal 2, raio inicial 2 e geração máxima de um chunk por frame. O observador cobre o frustum inclinado por uma estimativa conservadora, limitada a oito chunks de raio. Personagens leem ECS independentemente da residência. Obstáculos usam catálogo regional lógico.

## B — Apoio e fundação

A posição lógica continua sendo a origem inteira do footprint. O centro é a origem mais metade das dimensões. A apresentação amostra o terreno, sem água, em todos os vértices da grade dentro da implantação. A face superior da fundação usa a maior altura. Uma caixa de pedra cobre a implantação até a menor altura, sem inclinar paredes ou telhados. No caso plano a espessura adaptativa é zero. O pivô do prédio é a base nivelada, usada também por obras em progresso. A seleção de obras usa o apoio visual sem mudar as condições de posicionamento. O footprint e a navegação ficam sob autoridade da sessão.

Nos dois footprints iniciais, todos os vértices coincidiram com a superfície seca, confirmados em Play. O depósito tem 3,792 m de desnível entre os extremos na escala da cena. O perfil visual representa esse desnível numa fundação de pedra. [Antes](building-before.png) · [Depois](building-after.png)

## C e D — Residência, continuidade e LOD

O carregador usa os placements emitidos pela geração do chunk, inclusive fora do catálogo regional da navegação. A montagem da malha emite rochas e árvores para a apresentação em instâncias e mantém ouro no caminho de markers. Isso garante uma representação por placement. Árvores usam a amostra triangulada sob o tronco. O corpo principal da rocha compartilha a altura de apoio e tem interseção natural no pé em todos os níveis. O hash da chave determina a espécie e a rotação. A variação de escala mantém as proporções anteriores. O cache de matrizes é descartado com a malha que lhe deu origem. Um recurso consumido agenda a troca do chunk como unidade para manter o terreno residente.

Frustum culling é feito sobre cada chunk antes de enfileirar grupos. Em câmera ortográfica, a maior influência no tamanho projetado é o zoom. A classificação usa altura de referência de 3 m, os limiares 56 e 26 px e histerese de 12%. As três malhas compartilham pivô, escala, materiais e massas principais. Os níveis médio e distante simplificam troncos, galhos e copas, e deixam de lançar sombras de vegetação. A redução do custo não exige desenho simultâneo das duas malhas. Existe troca discreta de geometria e sombra.

O observador continua pedindo a janela da câmera inclinada. Durante uma mudança brusca, a última vista com cobertura suficiente permanece exibida enquanto a nova janela é gerada com o orçamento existente. A memória transitória fica limitada à união de duas janelas e a anterior deixa de ser retida após a troca. A primeira entrada e as mudanças de seed/estilo ocultam a vista parcial e mostram “Carregando terreno” até a cobertura. O limite existente de oito chunks de raio permanece.

Capturas da seed 7 no Editor a 2560 × 1440. O comparativo de construção usa o mesmo ponto de vista diagnóstico e a mesma altura de câmera. O estado de seleção e o texto do HUD podem variar entre as capturas. [Antes](streaming-before.png) · [Depois](streaming-after.png) · [Perto](lod-near.png) · [Intermediário](lod-mid.png) · [Distante](lod-far.png)

## E — Verificações

- Testes direcionados Play Mode PASS 2/2. A região original retornou com os mesmos placements, alturas e chaves, com árvore e rocha verificadas contra o terreno. O teste de pan lento, salto, inversão e retorno confirmou que a vista coberta permanece durante o salto. A sequência de zoom alcançou os três LODs sem alterar a população lógica.
- O teste em Play confirmou fundações de estruturas iniciais junto à água, criação e conclusão de um depósito dinâmico, destruição, reconstrução após Save/Load e estabilidade do apoio. O saldo usado para construir foi definido apenas no arquivo de teste. Edit Mode inclui terreno plano, pico interior, retenção da janela e histerese.
- Primeira execução direcionada FAIL porque o teste da VISUAL-001 consultava o contador ambiental antes do primeiro chunk residente. A condição de leitura agora aguarda a apresentação. Os asserts de renderização e simulação foram preservados.

- Rodada final via Unity MCP. **PASS** compilação, **300/300 Edit Mode**, **27/27 Play Mode** e Console com zero erros e avisos. Editor na VisualStyleDemo, cena limpa, Play desligado. A suíte verificou seleção, Move, Stop, combate, coleta, IA, Local Avoidance, construção e persistência.
- Em Play via MCP, seleção por projeção e Move produziram uma unidade em movimento com pose Walk. Stop aceito e contagem posterior zero. Uma atualização de recurso manteve a malha antiga durante a espera e publicou em seguida 60 placements únicos, ante 61, com a chave removida ausente.
- Para exibir diagnósticos de terreno, ative “Chunks, apoios e LOD”. O painel mostra centro, raio, residência, fila de chunks e contagens de LOD. Com Gizmos ligados, as fronteiras recebem cores por LOD, o centro fica amarelo e os pontos de apoio e pivôs de construção são desenhados na cena.
- Mouse físico e build de Player **NOT_RUN**. As capturas, o teste de câmera, a seleção por tela programática e os dispositivos virtuais dos testes cobrem o percurso validado.

## Benchmark no Editor

Unity 6000.6.9f1, URP 17.6.0, Entities 6.6.0, qualidade PC, seed 7, 2560 × 1440, Linux com Intel Core i5-14600KF e GeForce RTX 4070 SUPER em OpenGL Core. O teste `ProfilesThreeBackendsAtFivePopulations` mede 120 frames por configuração após drenagem da fila. São 27 amostras agregadas, com 10, 100, 500, 1.000 e 5.000 unidades. Os zooms 16, 40 e 72 expõem densidades observáveis diferentes no mesmo mundo. O comparativo sem culling/LOD roda as três apresentações. O comparativo padrão mantém culling/LOD e alterna sombras para medir o custo no mesmo enquadramento.

Os contadores de vértices, triângulos e draw calls abrangem o frame inteiro. A CPU de apresentação inclui consulta do ECS, propostas de pose, coleta dos lotes ambientais e submissão. A CPU de streaming mede o Step do ChunkManager, que inclui seleção, geração e montagem. O marcador CPU Render Thread é do Unity. O OpenGL do Editor opera em modo direto e esse contador não comprova execução numa thread dedicada. Batches instanciados foram observados. Batches totais e GPU Frame Time ficaram NOT_RUN por falta de amostra válida. Build de Player NOT_RUN.

O teste de movimento mede desde o salto de 209 células por eixo até a cobertura e fila vazia. Esse intervalo inclui a espera do Editor. O Step admite sempre um chunk inteiro, portanto a duração máxima pode passar do orçamento configurado de 4 ms mesmo com apenas uma geração por frame. [CSV de resultados](benchmark.csv)

| Modo, 5.000 unidades | Zoom | CPU apresentação anterior (ms) | CPU apresentação atual (ms) | Draw calls atuais | Chunks | Instâncias de ambiente submetidas |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Blocos | 16 | 1.7426 | 1.6319 | 103.0 | 25 | 0 / 0 / 0 |
| Instancing sem culling/LOD | 16 | 2.6588 | 2.5545 | 118.0 | 25 | 1924 / 0 / 0 |
| MeshRenderer sem culling/LOD | 16 | 4.1765 | 4.2829 | 3015.2 | 25 | 1924 / 0 / 0 |
| Instancing padrão | 16 | 1.9601 | 1.9276 | 132.6 | 25 | 957 / 0 / 0 |
| Instancing sem sombras | 16 | — | 1.9185 | 96.5 | 25 | 957 / 0 / 0 |
| Instancing intermediário, sem sombras | 40 | — | 3.0307 | 137.0 | 81 | 0 / 2321 / 0 |
| Instancing distante, sem sombras | 72 | — | 2.9435 | 189.0 | 169 | 0 / 0 / 3971 |

A configuração padrão passou de 1,9601 para 1,9276 ms de apresentação. Os triângulos de frame passaram de 3,15 para 3,85 milhões, cerca de 22% a mais. Nesta amostra, manter sombras em zoom 16 passou de 96,5 para 132,6 draw calls e de 1,30 para 3,85 milhões de triângulos. O custo de apresentação mudou de 1,9185 para 1,9276 ms. A Main Thread passou de 4,491 para 4,980 ms. Com mais cobertura nos zooms 40 e 72, a CPU de apresentação chegou a cerca de 3 ms sem sombras. A comparação mede o Editor e tem a variabilidade de uma execução de 120 frames por cenário.

Em estado estável, o Step de streaming foi de 0,0009 ms na configuração padrão. O teste de movimento gerou 25 chunks para o salto de 209 células por eixo e levou 127,2 ms da ordem até a vista pronta e a fila vazia. O maior Step na sequência de movimento e zoom foi 7,1639 ms. Nesse percurso, 225 chunks foram gerados e 200 descarregados até o retorno final.

## Limitações da verificação

- A cobertura tem limite de raio 8. A validação visual cobriu os zooms 16, 40 e 72, com mudanças rápidas de direção e retorno em Play. No zoom máximo 110 da cena, 25 raios de amostragem no viewport chegaram a chunks residentes, com fila vazia e raio 8. Outros ângulos, proporções de tela e seeds podem atingir o limite de cobertura.
- A transição usa malhas discretas. A histerese elimina alternância por pequenas variações perto do limiar. As sombras de árvores e rochas cessam ao sair do LOD próximo.
- A câmera mantém a vista anterior durante solicitações rápidas, o que pode causar uma espera perceptível. O pico de geração de um chunk continua dependente do terreno.
- A fundação é provisória e pode ficar alta em encostas. A VISUAL-001A mantém as restrições de colocação existentes.

## Recomendações para VISUAL-002

- Medir em Player os custos separados de cobertura, geometria e sombras antes de ampliar densidade ou alcance.
- Manter placements, pivôs e materiais coerentes nos LODs de bioma. Usar o catálogo regional somente para consumo lógico.
- Avaliar malhas mais econômicas e agrupamento espacial de lotes. A geometria da vegetação ampliada é o principal aumento observado.
- Estilizar as paredes de fundação em áreas de maior desnível. Qualquer restrição nova de construção em terreno inclinado deve ser uma decisão de gameplay.
