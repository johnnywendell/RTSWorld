# Validação INTEGRATION-001

## Auditoria de entrada

| Item | RTSCameraDemo | RTSInteractionDemo |
| --- | --- | --- |
| Seed serializada na árvore local | 0, alteração preexistente preservada | 7 |
| Terreno e chunks | TerrainGenerator v1, 32 células | TerrainGenerator v1, 32 células |
| Escalas e raiz | H=2, V=1,2, raiz X=-5807 | H=2, V=1,2, raiz na origem |
| Camada e material | EnvironmentPresentation ativa, ambiente v4/mar 38, EnvironmentPalette | Referência `environment` nula e componente desativado, TerrainSage |
| Geometria resultante | Mesh combinada com biomas, água e representações de Tree, Stone e Gold | `TerrainGeometry` base, material único |
| Câmera | Rig WORLD-005, zoom inicial 24 e máximo 48 | Mesmo rig, zoom inicial 44 e máximo 110 |
| Streaming e iluminação | ChunkManager, raio inicial 2 ajustado pelo observador, 1 chunk/frame com orçamento entre chunks de 4 ms. Luz direcional e URP | Mesmo pipeline e controles de streaming |
| Bootstrap | RtsCameraDemo, câmera e ponte visual | RtsInteractionDemo, incluindo sessão ECS e picking |

A diferença visual vem da configuração da camada ambiental e da seleção do caminho de mesh no ChunkManager. A seed e o enquadramento também mudam o local observado. O terreno simplificado da ECS-004 é um caminho suportado de apresentação.

A navegação já consulta o mesmo algoritmo procedural no contrato lógico, usa quatro alturas por célula, classifica água se receber uma EnvironmentDefinition, bloqueia pelas duas inclinações triangulares e admite obstáculos por máscara. As células usam floor global, incluindo negativos. O picking converte pela raiz e escalas do ChunkManager e consulta a superfície triangular da câmera. Recursos e chunks visuais nunca entram nas consultas de navegação. Na ECS-004, configuração sem ambiente e spawn em duas grades fixas só asseguram a cobertura do terreno base.

Resources nascem em células half-open. ResourceKey inclui seed, versão ambiental e X/Z global. Offset subcélula, altura triangular e NaturalResource descrevem a posição e o tipo. A mesh contém suas representações e não conserva GameObjects por recurso. O gerador pode reconstruir o catálogo, sem persistência de mutações. Chaves são interpretadas dentro da configuração completa do mundo, que inclui nível do mar e versão do terreno.

Decisão inicial: habilitar ambiente v4 na nova cena, resolver parâmetros a partir do mesmo ChunkManager/EnvironmentPresentation e reutilizar a ECS-004. Usar declividade lógica máxima 2,25 e spawn por células alcançáveis. Tree, Stone e Gold permanecem atravessáveis nesta integração. A escolha prepara consulta lógica sem colisores e mantém o mapa transitável enquanto alcance/ocupação de recursos ainda não têm mecânica.

Sondagem MCP na seed 7, mar 38, N32, janela [-248,-56) × [256,448): 31.414 células alcançáveis a partir de (-152,352), 989 vértices molhados na faixa central 64 × 64 e biomas Plains, TemperateForest, Coast, Ocean, Snow e Tundra.

## Resultado e reprodução

Abra `Assets/_Project/Scenes/RTSWorldIntegratedDemo.unity` e entre em Play. A configuração inicial usa seed 7, terreno v1, ambiente v4, mar 38, chunks de 32, Câmera WORLD-005 e janela lógica 192 × 192. A câmera enquadra a região de (-152,352). Use WASD ou botão do meio para pan, scroll para zoom, Q/E para rotação, clique ou retângulo para seleção, Shift/Ctrl para modificar, botão direito para Move e Espaço para Stop. O painel oferece 100, 500, 1.000 e 5.000 unidades. A seed é editável no ChunkManager e, em Play, mudar a configuração recria a sessão.

[Ambiente, biomas e unidades](integrated-demo.png) · [Seleção após chegada](integrated-selected.png)

| Verificação | Estado | Evidência |
| --- | --- | --- |
| Compilação e Console | PASS | Unity 6000.6.9f1, sem falha de compilação, zero erros e avisos na consulta final |
| Edit Mode | PASS | 223/223 em 41,1239444 s, sem falhas ou ignorados |
| Play Mode | PASS | 12/12 em 2,3133038 s, sem falhas ou ignorados |
| Consistência lógica e recursos | PASS | Dois chunks de sinais diferentes, catálogo repetível, fronteira molhada, gradiente, altura da água, altura e origem dos recursos |
| Streaming durante simulação | PASS | Unidades em células navegáveis atravessaram limite de chunk com mesh inicial descarregada. Recarregamento reproduziu todos os vértices, cores e contagens de recursos |
| Câmera e controles | PASS | Pan, zoom, rotação e retorno via rig existente. Input virtual no MCP confirmou clique 1, Shift 2, Ctrl 1, arraste 100, Move aceito e destino molhado rejeitado |
| Chegada e Stop | PASS | 100 unidades Idle em 100 posições, raio máximo 7,071 células do destino coletivo. Stop por Espaço preservou seleção. Em execução separada, Stop interrompeu 100 unidades em movimento |
| Configuração em Play | PASS | Seed 7→8→7 recriou o World e reproduziu as posições iniciais da seed 7 |
| Mouse físico, build de player e GPU timing | NOT_RUN | Entrada validada por dispositivos virtuais no Editor. Sem build ou temporização de GPU |

O Unity MCP abriu a cena, executou Play, dirigiu o input e rodou os dois casos focados de Edit Mode diretamente. As suítes completas usaram o runner isolado em `Logs/INTEGRATION-001-validation`. O XML adicional e caches da cópia ficaram dentro do projeto. A rodada intermediária de Play falhou porque o teste tentou selecionar antes de LateUpdate. A suíte final incluiu a espera e a verificação adicional de travessia.

## Desempenho observado

Unity Linux Editor, 2560 × 1440, Intel Core i5-14600KF. Cada população teve 20 frames de aquecimento e 60 amostras após esvaziar a fila do streaming. O retângulo cobriu a câmera e uma ordem Move foi emitida na janela de amostragem. ECS mede Advance e sincronização por frame, incluindo frames sem tick. Apresentação de unidades inclui captura e submissão instanciada de CPU. As duas cenas usaram 81 chunks residentes e suas configurações padrão, com enquadramento, geometria e janelas lógicas diferentes. As medidas incluem o Editor.

| População | Integrada ms/frame | FPS observado | Simplificada ms/frame | FPS observado | Picking integrado (ms) |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 100 | 2,578 | 387,9 | 2,385 | 419,3 | 0,702 |
| 500 | 2,736 | 365,5 | 2,770 | 361,0 | 3,938 |
| 1.000 | 3,397 | 294,3 | 2,956 | 338,3 | 5,990 |
| 5.000 | 6,048 | 165,4 | 5,432 | 184,1 | 28,299 |

Na cena integrada com 5.000 unidades, 4.965 centros estavam visíveis no retângulo padrão. Move para esse lote usou 15,164 ms em navegação, 0,226 ms para distribuir destinos e 37,204 ms para aplicar a ordem. O maior frame da amostra foi 86,117 ms. Os custos de preparação da ordem e de streaming são síncronos.

A rota de streaming usou pan de 12 segundos em passo controlado, voltou ao foco inicial, drenou os chunks e gerou 160 amostras na integrada e 158 na simplificada. Geração de terreno base por chunk teve médias de 0,159 e 0,156 ms, respectivamente. Construir a camada ambiental e sua mesh combinada teve média de 1,425 ms na integrada. A mesh base teve média de 0,318 ms na simplificada. O passo de streaming teve média de 1,614 ms e pico de 6,873 ms na integrada, contra média de 0,490 ms e pico de 4,159 ms na simplificada. A mesh integrada inclui terreno visual, água e recursos em uma única submissão por chunk. Os tempos de construção e renderização dos recursos não foram separados em contadores de GPU.

[Frames, seleção e ordens da integrada](profile-integrated.csv) · [Frames da simplificada](profile-simplified.csv) · [Streaming integrado](stream-integrated.csv) · [Streaming simplificado](stream-simplified.csv)

## Limites e preparação para ECS-005

A simulação cobre a janela regional. O mundo visual pode continuar fora dela. A margem de água é conservadora e um lote pode encontrar gargalos em terra estreita. Recursos são atravessáveis e o HUD conta apenas a população de recursos nas regiões visuais residentes. O catálogo lógico por chunk continua consultável após o descarregamento, com ResourceKey, tipo, posição subcélula e elevação. Não há persistência de remoções nem estado econômico.
