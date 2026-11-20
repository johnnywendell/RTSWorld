# Validação VISUAL-001

**Resultado.** VISUAL-001 concluída. A cena de referência demonstra protótipos medievais com animação Idle/Walk em uma sessão ECS jogável. O backend principal usa instancing explícito e o MeshRenderer permanece disponível como comparação.

## Auditoria

Unity 6000.6.9f1, URP 17.6.0 e Entities 6.6.0, conforme ProjectVersion, manifesto e resolução de pacotes. Sem Entities Graphics ou Hybrid Renderer. Personagens existentes são cubos Unlit em Graphics.RenderMeshInstanced. Chunks usam MeshRenderer/MeshFilter, malhas compartilhadas por rodada de streaming e uma mesh ambiental por chunk. A mesh inclui terreno, água recortada e símbolos de recursos. O shader ambiental usa cores por vértice. SRP Batcher habilitado nos assets PC e Mobile. PC usa quatro cascatas e distância 50; Mobile usa uma cascata. Nenhum Animator, SkinnedMeshRenderer, MaterialPropertyBlock, GPU Skinning ou sistema LOD participa da apresentação de unidades da base. Há culling por renderer nos chunks e em blocos para chamadas instanciadas. Não há culling de oclusão pré-calculado nem GPU Resident Drawer na cena de referência.

A VisualStyleDemo adiciona um snapshot de pose, orientação, identidade visual, proprietário e atividade. A apresentação seleciona corpo, pose, recorte de câmera e LOD. O backend principal instancia malhas com um material compartilhado. O comparativo MeshRenderer usa as mesmas malhas e um pool de objetos. Entities Graphics ficou NOT_RUN por ausência de pacote. A apresentação não escreve no estado ECS. As outras cenas mantêm o modo anterior. A área de substituição de árvores e rochas está limitada aos recursos reais numa faixa de 24 células em torno do centro.

## Cena e verificação

Abra `Assets/_Project/Scenes/VisualStyleDemo.unity` e inicie Play Mode. Use clique ou arraste para selecionar, botão direito para mover ou coletar e Espaço para Stop. WASD desloca a câmera, scroll aproxima e Q/E gira. Os botões Blocos, Instâncias, MeshRenderer e Ocultar unidades estão no painel, junto aos controles de animação, LOD, culling, sombras e população.

- [Antes](before.png) e [depois](after.png) registrados com a mesma câmera RTS no zoom inicial 16, a 2560 × 1440, após o streaming. A cena contém cinco aldeões, cinco soldados, dois prédios e 161 placements elegíveis para substituição no entorno. São modelos provisórios próprios, com materiais compartilhados.
- [Zoom próximo 12](close.png), [zoom distante 72](far.png) e [detalhe de personagens obtido com câmera diagnóstica](characters.png). No zoom inicial, 10 visíveis, 10 animadas e 10 com sombras. No distante, 10 em LOD simplificado e zero sombras de unidade. O zoom próximo manteve os 10 em LOD detalhado.
- MCP confirmou 0 unidades visíveis e 0 animadas após pan de aproximadamente 209 células em cada eixo, com o chunk (-5, 11) descarregado e a sessão ainda com 10 unidades. Retornar restaurou o chunk e os 10 visuais. O modo MeshRenderer manteve um material compartilhado e desativou a representação reaproveitável de uma unidade destruída no teste Play Mode.
- MCP aceitou Gather de uma árvore para a unidade escolhida por coordenadas de tela e confirmou um trabalhador ativo e uma pose Walk durante o deslocamento. Stop zerou a contagem de movimento. A demo começa com uma facção jogável e o protótipo contém duas paletas. Attack foi coberto na suíte de combate existente, sem cenário de ataque em VisualStyleDemo.
- O teste Play Mode verificou Idle, Walk, seleção por projeção, Move, Stop, transformação, orientação, troca de backend, desligamento da animação, LOD e reconstrução após Save/Load. O teste moveu uma unidade com a apresentação oculta. Os testes históricos verificaram combate, coleta, construções, IA, navegação, Local Avoidance, interação e streaming.
- Na sessão anterior, a primeira classificação de LOD colocou todos os modelos no modo distante e a recarga externa da cena bloqueou o MCP. A classificação final usa tamanho projetado e a cena atual abriu, compilou e foi executada.

## Ambiente e limites da comparação

Linux, Intel Core i5-14600KF, GeForce RTX 4070 SUPER e OpenGL Core 4.5 (driver NVIDIA 615.71.09). O log do Unity confirma renderização direta, pois esse backend não suporta o modo Split Jobs escolhido pelo Editor. Qualidade PC e URP da cena com Shadow Distance 190 e quatro cascatas. A câmera usa luz direcional diurna e iluminação ambiente em gradiente. A cena não configura AO, neblina, tonemapping ou color grading. O fundo da câmera e o ambiente seguem a configuração da cena e do skybox padrão.

O teste `ProfilesThreeBackendsAtFivePopulations` reproduz as amostras. Ele aguarda o streaming e mede 120 frames por configuração. A comparação direta desliga o recorte da aplicação, o LOD e o lançamento de sombras dos protótipos. A câmera continua descartando renderers individuais ou volumes de lotes conforme o backend. A passagem adicional usa LOD, culling e sombras ligados. Os personagens ficam em Idle para controlar o cenário. Os contadores do Unity representam o frame inteiro, incluindo terreno e Editor. O modo legado troca também a apresentação de recursos e prédios. A comparação isolada de unidades com a mesma geometria fica entre Instanced e MeshRenderer.

Os tempos instrumentados na aplicação são médias de CPU. Transformações fazem parte do tempo de submissão. A classificação de visibilidade também é executada com culling desligado para contar unidades fora da câmera. O contador de submissões da biblioteca inclui ambiente e construções. O modo Legacy usa um caminho de submissão fora desse contador. Render Thread, GPU e qualquer contador sem amostra válida recebem NOT_RUN.

## Benchmark executado

2560 × 1440, mesma câmera, seed e população por comparação. Cada célula dos modos contém **ms de apresentação na CPU / draw calls de todo o frame**. Média de 120 frames por configuração.

| Unidades ECS | Visíveis | Blocos | Instancing | MeshRenderer | Instancing com culling, LOD e sombras |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 10 | 10 | 0.021 / 77.0 | 0.082 / 91.0 | 0.093 / 103.0 | 0.095 / 122.0 |
| 100 | 100 | 0.049 / 77.0 | 0.132 / 91.0 | 0.159 / 283.0 | 0.156 / 123.0 |
| 500 | 500 | 0.188 / 77.0 | 0.345 / 91.0 | 0.539 / 1083.0 | 0.385 / 126.0 |
| 1000 | 900 | 0.370 / 77.0 | 0.590 / 91.0 | 1.056 / 1879.0 | 0.625 / 126.0 |
| 5000 | 1485 | 1.743 / 101.0 | 2.659 / 111.0 | 4.176 / 3007.8 | 1.960 / 130.6 |

Em 5.000 unidades, o instancing custou 2,659 ms de apresentação e 111 draw calls, contra 4,177 ms e aproximadamente 3.008 com MeshRenderer. Esse modo manteve 5.000 GameObjects ativos e usou culling individual de renderer, enquanto o instancing com recorte da aplicação desligado submeteu volumes de lote maiores. A geometria enviada foi de 3,73 milhões contra 1,06 milhão de triângulos no frame inteiro. O modo anterior de blocos teve 1,743 ms e 101 calls. O custo extra do instancing de protótipos inclui mais geometria, poses e materiais de iluminação.

Com as opções padrão, das 5.000 unidades, 1.485 foram animadas e configuradas para lançar sombra e 3.515 ficaram fora do frustum. A apresentação custou 1,960 ms e a atualização de transforms, 0,079 ms. A leitura de pose levou 0,083 ms, o teste de frustum 0,297 ms e a classificação do LOD 0,110 ms. O enquadramento inicial não acionou o LOD distante. O ensaio no zoom 72 e o teste com alvo de 128 px verificaram a troca e o desligamento de sombras à distância.

O contador do Unity `CPU Render Thread Frame Time` registrou 2,452 ms com instancing e 3,422 ms com MeshRenderer em 5.000 unidades, com opções de comparação iguais. O dispositivo renderiza em modo direto neste Editor, portanto esse contador não demonstra execução numa thread dedicada. O marcador específico da câmera URP ficou abaixo da resolução exportada de 0,001 ms. O contador de lotes instanciados registrou 40 e 10, respectivamente. O agregado de batches do renderer e o GPU Frame Time ficaram NOT_RUN. O relatório inclui FPS, Main Thread, SetPass, triângulos, vértices, bytes de memória e tempos da aplicação. [Dados de 20 configurações](benchmark.csv)

## Testes finais

| Verificação | Resultado | Evidência |
| --- | --- | --- |
| Compilação e Console | PASS | Cena e testes compilados. Console final com zero erros e avisos. |
| Edit Mode | PASS | 296 aprovados de 296. |
| Play Mode | PASS | 26 aprovados de 26. |
| Benchmarks e capturas | PASS | 20 configurações, cinco populações e cinco capturas da revisão final. |
| Entities Graphics | NOT_RUN | Pacote ausente. Comparação técnica no ADR. |
| GPU Frame Time, batches totais | NOT_RUN | Sem amostra válida no Editor. |
| Mouse físico, build de Player, Attack nesta cena | NOT_RUN | Comandos verificados via projeção da cena e integração de entrada na suíte existente. Attack foi exercitado na CombatDemo. |

Unity ficou com VisualStyleDemo ativa, cena limpa e Play Mode desligado. Git em `main/c690c43`, alinhado com `origin/main`, com alterações locais anteriores e da tarefa. Os testes regravaram nove relatórios da ECS-002, repostos aos bytes anteriores após a validação.

## Evolução prevista

- VISUAL-002 pode generalizar o catálogo de vegetação por chunk, agrupar instâncias espacialmente e medir o custo das sombras de árvores.
- VISUAL-003 pode comparar animação em GPU com poses discretas usando o mesmo contrato ECS de apresentação.
- VISUAL-004 pode introduzir variações de construções e estágios de obra associados aos footprints e estados já disponíveis.
