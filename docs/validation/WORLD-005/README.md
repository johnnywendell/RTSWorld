# WORLD-005 — evidências e uso da câmera RTS

Validação em 2026-11-15, Unity6000.6.9f1/LinuxEditor, URP e Input System1.20.1 já instalados. Git main/6a9966b; nenhum commit.

## Abrir a demo

Abra `Assets/_Project/Scenes/RTSCameraDemo.unity`, entre em Play e dê foco à janela Game. Aguarde as regiões pendentes estabilizarem. A cena nova usa seed12345,N32,escalas2/1.2,mar38,ambiente3; as cenas TerrainDemo/StreamingDemo/EnvironmentDemo anteriores não foram alteradas.

- WASD: deslocamento relativo à orientação da câmera.
- Botão do meio: arraste; o primeiro frame ancora sem salto. Soltar/interromper o ponteiro encerra a captura.
- Scroll: aproxima/afasta suavemente entre os limites configurados.
- Q/E: rotação horizontal, opcional no Inspector.
- Bordas da tela: deslocamento; pode ser desativado no painel ou Inspector. Fora da janela/sem foco não gera entrada. O painel exclui o ponteiro para não mover a câmera ao usá-lo.
- `Voltar ao ponto inicial`: restaura foco/zoom/yaw iniciais. A fila pode levar alguns frames para carregar a região após uma mudança grande.

No objeto **RTS Camera Rig**, expanda **Settings** no RtsCameraController: MovementSpeed, Acceleration, Deceleration, MouseSensitivity, ZoomSpeed, MinimumZoom/MaximumZoom, Smoothing, Inclination20..80, EnableRotation/RotationSpeed, EnableEdgePan/EdgePixels, CoordinateLimit e TerrainClearance. A serialização nativa desses campos está em `inspector-serialization.json`.

Velocidade e zoom usam unidades lógicas; escala horizontal transforma a apresentação. Zoom é meia altura ortográfica. Acceleration/Deceleration e Smoothing são taxas de resposta por segundo, não forças da simulação. A navegação usa tempo não escalado pela simulação.

## Implementação e contratos

- Leitura de dispositivos, matemática, pose, consulta de superfície e observador estão em componentes distintos, assembly RTSWorld.Camera. Não foi instalado outro sistema de entrada.
- Foco X/Z double; conversão para float na apresentação. Guarda configurável±32768lógicos por padrão, sem limitar a autoridade ou os dados do mundo. Frame vertical, translação/yaw e escala uniforme positiva são suportados; escala não uniforme ou frame inclinado são rejeitados. Floating origin completo continua fora do escopo.
- WorldSurfaceQuery consulta campos do terreno/ambiente e interpola a mesma diagonal das meshes, usando max(terreno,água). Funciona sem chunks carregados e reage à configuração do mundo. O controlador não chama o gerador diretamente.
- A câmera conserva foco/ângulo/projeção ao aumentar a distância para deixar todo near plane acima do limite de altura conhecido101×escala vertical+folga. Far plane alcança o mínimo do relevo mesmo com foco alto/inclinação20. Não depende de colisores ou chunks visíveis. O contrato deverá ser revisto se versões futuras alterarem os limites de altura do mundo.
- A ponte atualiza o observador pelo foco antes do ChunkManager. O raio acompanha o enquadramento dentro do máximo8existente; testes confirmam que ficar parado não causa regeneração. Configurações muito grandes podem ultrapassar a cobertura possível; CoverageLimited expõe esse limite.
- ChunkManager recebeu somente três getters de configuração. Dados, versões ambientais, gerador v1, algoritmo de streaming, ownership, shader e materiais anteriores permanecem preservados.

## Testes reais

| Verificação | Resultado | Evidência |
| --- | --- | --- |
| EditMode final | PASS145/145,zero falhas/ignorados,30.6922861s | editmode-final.xml |
| PlayMode final | PASS5/5,zero falhas/ignorados,0.593707s | playmode-final.xml |
| FPS/direções/suavização | PASS |30/60/144comparados a240FPS;quatro direções,diagonal,aceleração/coasting,frame longo,drag sem deltaTime duplicado. |
| Controles/entrada | PASS |Teclado/mouse do próprio Input System com eventos sintéticos:WASD,Q/E,scroll,MMB,bordas,desativação,foco e bloqueios de UI. |
| Zoom/inclinação/frustum | PASS |Limites e wrap de yaw;near acima do teto,far incluindo vales com foco alto em20/55/80;frame com translação/yaw/escala uniforme. |
| Mundo/streaming | PASS |Consulta da superfície nas versões1/2/3sem carregar chunks;passagem de fronteiras,novas regiões/unload,sem regenerar parado,seed/mar,reuso e liberação até baseline. |
| Regressões anteriores | PASS |127EditMode+4Play anteriores incluídos nas suítes finais. |
| Demo/visual | PASS |22PNGs reais URP em Editor/Play,11cenários descritos abaixo,sem erros de shader. |
| Console | PASS com aviso externo |Editor0erros/1avisoURP;Play0erros/0avisos;sem limpar Console. |

As tentativas iniciais de compilação falharam por string/using no primeiro rascunho e por conflito do nomeKey no utilitário temporário. Os erros relevantes estão em `initial-compile-errors.txt`; não são o resultado final. `*-first-pass.xml` preservam145/5antes do reforço do far plane. Nenhum resultado de teste foi simulado.

## Inspeção visual

`captures/` contém renders da câmera da própria RTSCameraDemo em floresta,região após WASD,arraste,zoom mínimo/máximo,rotação,bordas,bordas desativadas,água da seed7,colinas no zoom mínimo e atualização automática parada. Cada cenário foi capturado no Editor e em Play. O controle é exercitado por eventos sintéticos no Input System e pela mesma leitura/movimento/ponte; não se afirma operação manual humana do mouse.

Nos cenários instrumentados o controlador/ponte são avançados explicitamente com passos fixos, para reproduzir poses. O cenário `auto-update-idle` também executa os componentes habilitados no loop real de Play, com entrada suprimida para não receber o mouse físico do host. O script de captura não salva alterações na cena. `captures/metrics.txt` registra foco,zoom,yaw,inclinação,raio,residência,fila,verificação do near plane e Console.

As capturas estabilizaram25a81chunks(conforme enquadramento),sempre fila0/shaderError=False. WASD moveu o foco para(56.25,0),arraste para(−13.118,3.174),bordas para(39.775,−39.775);bordas desativadas mantiveram(0,0). Zoom12..48e yaw45→135foram observados. As imagens são renders nativos,sem IA/retoque/composição; elas não incluem a sobreposição IMGUI,que é desenhada separadamente na janela Game.

## Ambiente de validação e reprodução

MCP localhost:8080 indisponível dentro/fora do sandbox. Usado o próprio Unity em cópia isolada `/tmp/rtswater-unity-validation`,com181inputs Assets/ProjectSettings iguais ao workspace por SHA256(`validated-inputs.json`) e59pacotes locais já existentes resolvidos via `file:`. Nenhum pacote foi instalado/atualizado no projeto original. A sessão interativa original não foi controlada.

Os arquivos `World005SceneBuilder.cs.txt` e `World005Validation.cs.txt` são os utilitários usados somente na cópia. Para reproduzir, colocá-los como `.cs` sob Assets/Editor na cópia e executar Unity com `-executeMethod World005SceneBuilder.Build`(criador) ou `World005Validation.Run`(captura com GPU,sem `-nographics`). Para testes, `-batchmode -nographics -runTests -testPlatform EditMode`/PlayMode com `-testResults`/`-logFile`. Não executar dois Unity sobre a mesma cópia simultaneamente.

O aviso de metadado do pacote URP é preservado em `console-warning.txt`;o cache não foi modificado. Logs integrais não publicados porque a inicialização do Editor inclui autenticação.

## Medida e limites

Após32chamadas de aquecimento,1000chamadas estacionárias de controlador+consulta+observador consumiram2.3398ms totais e0bytes gerenciados nesta execução. Stopwatch foi criado antes do baseline de alocação. Essa medição não inclui renderização,GUI,entrada sintética ou ChunkManager.Step; não é garantia deFPS nem medição do custo total da câmera no jogo.

Floating origin,player/build,outros sistemas operacionais,hardware/heap/VRAM e configurações máximas:NOT_RUN. Navegação muito distante continua limitada pela apresentaçãofloat e pela guarda configurável. Árvores/recursos não são colisores;segurança cobre terreno/água. O teto de altura é conservador e pode aumentar a distância da câmera em vales sem mudar o zoom ortográfico. Streaming é síncrono por chunk e pode exceder seu budget;fila pendente pode produzir carregamento visual transitório. Nenhum gameplay,seleção de unidades,rede ou persistência foi adicionado.
