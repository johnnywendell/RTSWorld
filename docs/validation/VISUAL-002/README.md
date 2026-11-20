# Validação VISUAL-002

**Resultado.** VISUAL-002 concluída. A VisualStyleDemo usa a biblioteca ambiental por bioma e o benchmark é reproduzível no Editor e no Linux Player.

A cena de referência usa geometria e placements da geração v4. O catálogo de recursos e a grade lógica usam os mesmos dados de geração, enquanto o ChunkManager mantém residência visual. VISUAL-001A usa quatro grupos ambientais, duas paletas de vegetação e LOD por altura projetada. Sua medição histórica a 2560 × 1440 registra 1,928 ms de apresentação padrão em 5.000 unidades. Blender não foi encontrado no PATH. A estratégia inicial é produzir malhas próprias compartilhadas no Unity, reaproveitando o atlas, a escala, os pivôs e os lotes existentes.

## Produção e distribuição

- Geometria própria gerada em C# no Unity, com pivô na base, Y para cima, unidades em metros e sem importação de modelos. Os recursos de madeira, pedra e ouro conservam suas chaves e posições. A textura de paleta e as malhas são compartilhadas. O material URP Lit referenciado pelas duas cenas mantém a variante com instancing disponível no build.
- Oito famílias arbóreas em duas paletas por família. Carvalho de copa larga, copa arredondada, copa irregular, pinheiro em camadas, conífera estreita, árvore seca com galhos, copa rala e tronco quebrado. Curvatura, galhos, raízes e massas de folhagem são tratados de acordo com o arquétipo. A chave global determina cor, rotação, proporções e espécie local.
- Cinco formas de pedra, uma representação de ouro com veios minerais e oito decorações, incluindo dois tipos de gramínea, flor, samambaia, arbusto, junco, capim seco e pedras de margem. Lajes, seixos e afloramentos combinam planos assimétricos com fragmentos. O ouro usa uma malha baixa para preservar leitura sem encobrir unidades.
- A posição de recursos é fixa. Ruído global de baixa frequência altera a composição de espécies e as condições de vegetação decorativa, criando trechos mais cheios e clareiras. Um elemento decorativo por célula no máximo. Jitter global, amostragem triangulada, exclusão de célula de recurso (inclusive esgotado), inclinação e contato com a água controlam as plantas e seixos. As três densidades usam subconjuntos aninhados para benchmarking.
- Nenhum placement decorativo entra no catálogo, nos raios de obstáculos, nos footprints ou no Save. World e Simulation preservam o gerador e a identidade anterior. A reconstrução visual é derivada da definição de mundo e dos recursos ainda disponíveis.

## Biomas, terreno e água

Umidade e temperatura interpolam as paletas, com manchas de terra e variação de exposição em encostas. Pontos vizinhos usam as mesmas coordenadas globais nas bordas de chunks. A classificação de bioma continua a do WORLD-004:

| Bioma existente | Leitura visual |
| --- | --- |
| Plains | verdes quentes, tufos e clareiras |
| TemperateForest | verde musgo, decíduas e coníferas em manchas |
| DenseForest | verde úmido profundo e copas numerosas do catálogo |
| Desert / Arid | areia, solo seco e vegetação rasteira esparsa |
| Snow / Tundra | cinza frio ou neve, baixa densidade, coníferas onde houver recurso |
| Mountain | planos de pedra, vegetação seca esparsa e afloramentos |
| Coast | areia úmida, juncos e pequenas pedras no contato |
| Ocean e outras águas | gradiente de profundidade, ondulação tonal e espuma discreta |

A geometria recortada de hidrologia é a mesma. A profundidade alimenta cor e espuma no shader. A água é opaca, sem física, transparência ou captura de reflexos. A direção e intensidade da iluminação da cena foram mantidas.

## LOD, orçamento e streaming

Os lotes reaproveitam o cache de matrizes por chunk com culling de frustum, agrupamento final por mesh, LOD e sombra. Três LODs de árvores/rochas preservam pivô e família. Os limites de 56 e 26 px e a histerese de 12% vêm da VISUAL-001A. Rasteira desenha apenas no LOD0, sem lançar sombra. Ambiente lenhoso e rochas lançam sombra apenas no LOD0, com URP de referência a 190 m e quatro cascatas. O pool de matrizes aloca os buffers dos slots usados e os reaproveita. A vista retida e o orçamento de montagem por frame foram preservados.

A comparação usa “Ambiente VISUAL-002” na VisualStyleDemo. EnvironmentBenchmarkDemo usa a mesma apresentação e disponibiliza baixa, média ou alta densidade, rasteira, sombras, vistas de referência e pan rápido.


## Validação

Na revisão final, Edit Mode 302/302, Play Mode 28/28 e compilação PASS. Console do Editor com zero erros e avisos. VisualStyleDemo aberta, com cena limpa e Play desligado. As regressões compararam chaves, topologia triangulada, ordem de carregamento, posições em chunks negativos, decoração após recarga/Load, passagem para LOD sem rasteira, navegação, economia, combate, construção, IA e persistência.

O primeiro Development Player Linux compilou e abriu a 2560 × 1440, mas o Unity retirou o URP Unlit acessado por `Shader.Find` nos marcadores de seleção. O CSV dessa execução foi descartado. O material dos marcadores e o da paleta Lit passaram a ser referências serializadas nas duas cenas. Regenerações do build e dos testes históricos foram arquivadas temporariamente e os arquivos preexistentes foram repostos.

## Orçamento medido das malhas

| Família | LOD0, triângulos | LOD1 | LOD2 |
| --- | ---: | ---: | ---: |
| Decíduas | 316 | 192 | 88 |
| Pinheiros e coníferas estreitas | 176 | 112 | 60 |
| Árvores secas, ralas e troncos | 76–172 | 44–128 | 36–72 |
| Rochas e ouro | 28–68 | 20–56 | 12–44 |
| Vegetação baixa e seixos | 24–72 | Culling | Culling |

Contagem lida diretamente das malhas no Unity. A biblioteca usa 30 variantes visuais, com três meshes por variante. A paleta dos modelos tem 37 cores e um material URP Lit compartilhado. O terreno usa um segundo material de superfície. O Unlit compartilhado pelos marcadores de controle é separado. As densidades baixa, média e alta submetem 267, 553 e 841 decorações no zoom inicial da seed 7. Os limites aproveitam o comportamento observado nas medições a seguir.

## Benchmark Editor e Player Linux

O mesmo roteiro executou 17 configurações por ambiente. Em cada configuração, aguarda a cobertura, aquece por 18 frames e mede 120 frames. São 100, 500, 1.000 e 5.000 unidades em três densidades de decoração, mais cinco perfis de comparação a 5.000. Unity 6000.6.9f1, Linux, Core i5-14600KF, RTX 4070 SUPER, OpenGL Core, 2560 × 1440, qualidade PC, VSync desligado e alvo de FPS ilimitado. O Player é Development, sem Deep Profiling. Os contadores de draw calls, triângulos, vértices e memória são do frame inteiro. O contador CPU Render Thread é disponibilizado por Unity. O CSV registra a falta de amostra para GPU Frame Time e lotes totais.

Para repetir no Editor, executar `EnvironmentBenchmarkDemoTests.BenchmarkProfilesDensityGroundCoverAndShadows`. Para o Player, construir `EnvironmentBenchmarkDemo.unity` em Linux x64 com Development e invocar o executável com `-force-glcore -screen-width 2560 -screen-height 1440 -screen-fullscreen 0 -popupwindow -rts-environment-benchmark <arquivo.csv>`. O próprio cenário exporta o CSV e encerra ao completar.

As densidades variam apenas decoração. No CSV, `placements` conta dados entregues ao renderer pelo chunk. No perfil anterior esse contador exclui ouro incorporado à mesh. No perfil novo o ouro é instanciado e entra no contador. O catálogo lógico conserva a contagem e a identidade nos dois perfis.

[CSV do Unity Editor](benchmark-editor.csv) · [CSV do Linux Player](benchmark-player-linux.csv)

### Resultado em 5.000 unidades

| Modo, 5.000 unidades | CPU apresentação Editor / Player (ms) | Frame Editor / Player (ms) | P95 Editor / Player (ms) | Draw calls Editor / Player |
| --- | ---: | ---: | ---: | ---: |
| Novo, baixa | 2,0287 / 1,4623 | 5,970 / 2,184 | 9,170 / 2,730 | 235,325 / 234,000 |
| Novo, média | 1,9089 / 1,4368 | 5,463 / 2,133 | 6,315 / 2,691 | 235,150 / 237,867 |
| Novo, alta | 1,9811 / 1,5297 | 5,860 / 2,296 | 7,570 / 3,187 | 235,092 / 235,875 |
| Anterior | 1,8666 / 1,4433 | 5,159 / 2,159 | 5,984 / 3,177 | 148,542 / 150,600 |
| Novo sem sombras | 1,9188 / 1,4367 | 5,110 / 2,104 | 5,904 / 2,701 | 149,967 / 151,217 |
| Novo sem rasteira | 1,9410 / 1,3863 | 5,537 / 2,051 | 7,048 / 2,510 | 220,842 / 221,267 |
| Novo no zoom 40 | 2,9501 / 2,2151 | 6,545 / 2,951 | 8,464 / 3,550 | 180,000 / 181,000 |
| Novo no zoom 72 | 2,9879 / 2,4253 | 6,659 / 3,261 | 7,848 / 4,199 | 222,000 / 223,000 |

No cenário padrão, o novo ambiente elevou o total de triângulos de 3,85 para 4,33 milhões no Editor e no Player, cerca de 13%. A CPU de apresentação no Editor passou de 1,867 para 1,909 ms na comparação pareada. A referência histórica da VISUAL-001A foi 1,928 ms em execução distinta. No Player, 1,443 e 1,437 ms ficaram próximos. A variação de FPS entre casos curtos não demonstra ganho geral de desempenho.

No Player, ligar sombras com densidade média passou de 151,2 para 237,9 draw calls e de 1,49 para 4,33 milhões de triângulos. Ligar rasteira com sombras adicionou 553 instâncias, aproximadamente 16,6 chamadas e 40 mil triângulos. Essa amostra de rasteira elevou CPU de apresentação de 1,386 para 1,437 ms. O impacto gráfico dominante continua sendo sombras. A diferença de tempo de GPU permanece sem medição.

O pan rápido carregou 25 chunks. No Editor, pronto em 229,909 ms, pico de streaming de 4,829 ms e pico de frame de 18,167 ms. No Player, pronto em 125,899 ms, pico de streaming de 5,367 ms e pico de frame de 9,278 ms. O Step de streaming em estado estável ficou abaixo de 0,002 ms nos dois ambientes. As diferenças entre Editor e Player incluem o ambiente de renderização e a instrumentação.

O Player corrigido encerrou com código 0, com 17 resultados e sem exceções. O URP avisou sobre três shaders opcionais de pós-processamento removidos do build. Ambas as câmeras têm pós-processamento desativado.

## Capturas de referência

[Antes](environment-before.png) e [depois](environment-after.png) foram registrados na seed 7, no zoom 16, com a câmera inicial, 2560 × 1440, qualidade PC e a iluminação diurna da mesma cena. As capturas de inspeção de perto usam a câmera de diagnóstico. Os quadros de distância usam a câmera RTS.

- [Floresta próxima](forest-close.png) · [Intermediária](forest-medium.png) · [Distante](forest-far.png)
- [Coníferas](conifers.png) · [Decíduas](deciduous.png) · [Árvores secas](dry-trees.png)
- [Rochas](rocks.png) · [Vegetação rasteira](ground-vegetation.png) · [Margem](shoreline.png) · [Biomas](biome-transition.png)

## Limites e recomendações para VISUAL-003

As copas continuam facetadas e algumas formas permanecem semelhantes no LOD2. Os cortes originais do leito e das margens continuam perceptíveis onde a hidrologia gera transições retas. A espuma e a cor dão contato visual sobre essa geometria. O novo perfil amplia o número de lotes por diferenças de malha, cor e sombra. Antes de aumentar densidade ou distância de sombras, medir um orçamento combinado com a futura animação de personagens em Player. Manter material e identificação de facção legíveis contra as novas paletas de biomas.

## Avaliação artística

Na comparação pareada, decíduas, coníferas e árvores secas tornaram-se distinguíveis pelo desenho da copa, tronco e galhos. O ouro passou de marcador alto a afloramento baixo. Encostas e clareiras têm variação de solo e massas vegetais de densidades diferentes. Arbustos, gramíneas e plantas de margem permanecem abaixo da linha de leitura dos personagens. A inspeção próxima mostrou seleção, Walk durante Move e parada completa sobre o terreno novo. A vista afastada manteve a família visual das árvores e a leitura da transição entre floresta temperada, costas e colinas frias. Uma amostragem de 49 chunks ao redor da referência encontrou os dez biomas existentes.

Desligar a rasteira elimina suas submissões. A distribuição candidata continua residente para permitir a alternância. O custo incremental medido por essa chave cobre o desenho e a reunião de lotes por frame.
