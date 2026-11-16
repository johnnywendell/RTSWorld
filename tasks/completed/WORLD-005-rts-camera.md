# WORLD-005 — Sistema de Câmera RTS

**Status:** DONE
**Prioridade:** Alta  
**Dependências:** WORLD-001, WORLD-002, WORLD-003 e WORLD-004 concluídas.

## 1. Objetivo

Implementar um sistema de câmera RTS 3D inspirado em jogos como Age of Empires, permitindo ao jogador navegar livremente pelo mundo procedural RTSWorld.

A câmera deverá funcionar com o carregamento dinâmico de chunks e proporcionar movimentação fluida, previsível e adequada a um jogo de estratégia em tempo real.

## 2. Escopo funcional

### 2.1. Movimentação

Implementar:

- Movimentação horizontal com WASD.
- Movimentação pelas bordas da tela, configurável.
- Arrastar a câmera com o botão do meio do mouse.
- Velocidade de deslocamento configurável.
- Movimentação independente da taxa de quadros.
- Movimento suave com aceleração e desaceleração configuráveis.

### 2.2. Zoom

- Zoom com scroll do mouse.
- Limites mínimo e máximo configuráveis.
- Transição suave.
- Preservação de enquadramento adequado para RTS.
- Evitar atravessar o terreno ou posicionar a câmera abaixo da superfície.

### 2.3. Orientação

- Perspectiva inicial isométrica ou ortográfica, conforme a arquitetura e a apresentação visual existentes.
- Inclinação configurável.
- Rotação horizontal opcional por comando do usuário.
- Limites de inclinação apropriados.
- Evitar movimentos bruscos e inversões inesperadas.

### 2.4. Integração com o mundo

- Utilizar a posição lógica do foco da câmera para determinar a região observada.
- Integrar com o streaming de chunks da WORLD-003.
- Carregar e descarregar regiões conforme a movimentação.
- Preservar compatibilidade com os biomas e a água da WORLD-004.
- Evitar geração desnecessária quando a câmera permanece estacionária.
- Considerar limites de precisão de ponto flutuante para mundos extensos.

### 2.5. Configuração

Expor no Inspector:

- Velocidade de movimento.
- Sensibilidade do mouse.
- Velocidade de zoom.
- Zoom mínimo e máximo.
- Inclinação.
- Suavização.
- Habilitação da movimentação pelas bordas.

## 3. Requisitos arquiteturais

1. Separar leitura de entrada, movimentação da câmera e controle do observador utilizado pelo streaming.
2. Não acoplar o gerador procedural diretamente à câmera.
3. Preservar os contratos das tarefas anteriores.
4. Evitar alocações desnecessárias por frame.
5. Preparar a arquitetura para futuras interfaces, seleção de unidades e controles de jogador.
6. Manter compatibilidade com Unity 6 e URP.
7. Utilizar o sistema de entrada já adotado pelo projeto, evitando dependências duplicadas.
8. Documentar a estratégia de coordenadas e precisão para mapas extensos.

## 4. Plano incremental

- Etapa A: inspeção da cena, controles e sistema de streaming.
- Etapa B: arquitetura do controlador e foco da câmera.
- Etapa C: movimentação WASD e arraste.
- Etapa D: zoom e limites.
- Etapa E: movimentação pelas bordas e suavização.
- Etapa F: integração com streaming.
- Etapa G: configuração no Inspector e cena de demonstração.
- Etapa H: testes e validação.

## 5. Fora do escopo

- Seleção e movimentação de unidades.
- Construção de estruturas.
- Interface completa de gameplay.
- Minimap.
- Fog of war.
- Combate.
- Multiplayer.
- Controles mobile e gestos multitouch.
- Sistema completo de floating origin, salvo necessidade técnica demonstrada.

## 6. Critérios de aceite

- [x] A câmera se movimenta por WASD.
- [x] O arraste com botão do meio funciona.
- [x] A movimentação pelas bordas funciona e pode ser desativada.
- [x] O zoom por scroll funciona dentro dos limites.
- [x] A movimentação é suave e independente do FPS.
- [x] A câmera mantém um enquadramento RTS adequado.
- [x] A câmera não atravessa o terreno em condições normais.
- [x] O foco da câmera acompanha corretamente a movimentação.
- [x] O streaming carrega novos chunks conforme a câmera se desloca.
- [x] Chunks distantes são descarregados conforme as regras do streaming.
- [x] O Inspector permite configurar os principais parâmetros.
- [x] A cena de demonstração funciona em Play Mode.
- [x] Não existem erros de compilação.
- [x] Os testes automatizados aplicáveis são executados.
- [x] Os documentos de continuidade são atualizados.

## 7. Testes

Validar:

- Deslocamento em diferentes direções.
- Independência do FPS.
- Limites de zoom.
- Limites de inclinação.
- Funcionamento dos controles.
- Integração com o streaming.
- Comportamento próximo às bordas dos chunks.
- Compilação e regressões dos testes anteriores.

Registrar PASS, FAIL ou NOT_RUN com evidências reais.

## 8. Continuidade

Seguir AGENTS.md e TASK_TEMPLATE.md.

Ao iniciar, mover a tarefa para tasks/in_progress/ e atualizar tasks/ACTIVE.md.

Registrar checkpoints, decisões, arquivos modificados, testes e pendências.

Ao concluir os critérios obrigatórios, mover para tasks/completed/ e atualizar docs/CURRENT_STATE.md, docs/HANDOFF.md e tasks/BACKLOG.md.

Não iniciar automaticamente nenhuma tarefa posterior.

**Aguardar autorização explícita do usuário antes de executar esta tarefa.**

## Controle de execução — 2026-11-15
- Autorização explícita recebida do usuário nesta sessão. O arquivo já estava em in_progress com status PENDING; agora iniciado sem recriar a especificação.
- Responsável: Codex, único escritor. Branch main; HEAD6a9966b0168d3ba553c97ed7f7c8a2818f5bbca4. Sem commits autorizados.
- Alterações não commitadas das FIX-001/002 preservadas; baseline em docs/validation/WORLD-005/baseline-hashes.json e /tmp/rtsworld-world005-baseline. A tarefa original foi copiada antes de alterar status/checkpoints.

## Plano concreto
1. Separar frame de entrada(Input System existente), modelo de movimento/zoom/orientação, consulta de superfície e ponte do observador de streaming.
2. Integrar movimento WASD/bordas/arraste, Q/E opcional, zoom ortográfico suave e configurações no Inspector. Foco lógico double e guarda de navegação para precisão float da apresentação.
3. Garantir câmera/near plane acima do limite superior conhecido do terreno, sem depender de colisores/chunks carregados. Consulta de superfície é adaptador de apresentação, não geração acoplada ao controlador.
4. Criar RTSCameraDemo.unity independente; preservar TerrainDemo/StreamingDemo/EnvironmentDemo. Ajustar residência visual ao enquadramento dentro do raio máximo existente.
5. Validar matemática/FPS, dispositivos reais do Input System por eventos sintéticos, zoom/inclinação, superfície, streaming/revisita/cleanup, Inspector e demo real em Editor/Play; registrar evidências e regressões.

## Checkpoint inicial
- Concluído: inicialização obrigatória e inspeção do código/cenas/entrada. Projeto usa InputSystem1.20.1, activeInputHandler1; não há controlador RTS nem InputActions específico existente. StreamingDemo move apenas o observador por botões e segue com offset fixo.
- Em andamento: implementação dos componentes de apresentação. Nenhum fonte alterado nesta tarefa ainda.
- Verificação desta tarefa: Git/arquivos PASS; compilação/testes/visual NOT_RUN. A disponibilidade MCP será conferida; se ausente, repetir validação no próprio Unity em cópia isolada.
- Próxima menor ação: criar módulo de câmera, contratos de entrada e movimento, consulta de altura/ponte e testes.
- Fora do escopo: floating origin completo, unidades/seleção/gameplay, rede/persistência, dependências novas ou commits. Nenhuma tarefa posterior será iniciada.

## Checkpoint — componentes e demo criados
- Módulo isolado RTSWorld.Camera criado em Presentation/Cameras,usando Unity.InputSystem já instalado. Leitura de entrada/frame, movimento lógico, controlador/pose, consulta de superfície e ponte de streaming separados.
- Movimento integra exatamente a resposta exponencial da velocidade; arraste usa deslocamento em pixels uma vez, zoom suave com limites, Q/E opcional e inclinação20..80. Bloqueios de teclado/ponteiro/foco e exclusão do painel preparam integração de UI.
- Foco X/Z double e guarda configurável(default32768unidades lógicas). Apresentação usa frame vertical/uniforme,permite translação/yaw/escala uniforme; semfloatingorigin. Câmera/nearplane acima do limite conservador101×escala vertical,independentemente de chunks carregados. Distância ajusta-se preservando foco/ângulo/projeção ortográfica.
- WorldSurfaceQuery interpola a mesma diagonal e o máximo de terreno/água;reage a seed/mar/versão sem carregar chunks. ChunkManager ganhou apenas3getters de configuração,sem mudar algoritmos/ownership. Ponte atualiza observador antes do manager e ajusta raio ao enquadramento,limitado ao máximo8existente.
- Nova RTSCameraDemo.unity criada/salva pelo próprio Unity em cópia isolada e transferida com.meta. Remove controles antigos apenas na nova cena;três demos anteriores preservadas. Inspector/configurações serializadas e painel de ajuda/bordas/reset presentes.
- Compilação inicial FAIL por string multilinha e using ausente;corrigidos. Compilação/execução do criador nativo PASS,sem errosC#no log atual. Testes novos ainda NOT_RUN;primeira tentativa foi bloqueada pela compilação antes da correção.
- Próxima ação:executar EditMode(com modelagem FPS/dispositivos reais via eventos/superfície/frame) ePlayMode(streaming/nearplane/semregeneração parada),corrigir falhas e inspecionar visual da demo.
- main/6a9966b,sem commits;alterações anteriores FIX-001/002 preservadas. Arquivos novos de câmera/testes/cena;asmdefs de testes ampliadas para referências já existentes.

## Checkpoint — primeira validação de câmera PASS
- PASS145/145EditMode(31.1614048s) e5/5PlayMode(0.596439s),0falhas/ignorados. Inclui127+4regressões anteriores e18testes de câmera+1Playintegrado.
- Dispositivos do InputSystem exercitamWASD,bordas,scroll,MMB eQ/E;Playverifica foco/frustum,novos chunks/unload,sem regeneração quando parado,reuso de meshes,seed/mar e cleanup. Frame transladado/rotacionado/escala uniforme também validado.
- Revisão do frustum revelou necessidade de limite far exato na inclinação mínima sobre foco alto:alterado cálculo para fazer o canto superior do far plane alcançar a altura mínima conhecida;teste de20/55/80graus ampliado. Suíte final em execução;repetirPlayapós isso.
- Utilitário temporário de captura falhou inicialmente por nomeKeyem conflito com enum doInputSystem;corrigido apenas na cópia,não é erro do módulo de câmera. Visual final aindaNOT_RUN.
- Próxima ação:analisar suíte final,Playfinal,capturas reais eInspector/Console. main/6a9966b,sem commits;trabalho anterior preservado.

## Relatório final — WORLD-005

**Resultado:** DONE. EtapasA–Hconcluídas no escopo autorizado; nenhum critério dispensado. Sem commits e sem iniciar tarefa posterior.

### Implementado
- Câmera ortográfica RTS com WASD,bordas configuráveis/alternáveis,arraste com botão do meio,zoom de scroll suave e limitado,Q/E opcional,inclinação20..80 e velocidades/sensibilidade/respostas configuráveis no Inspector.
- Entrada,movimento,pose,consulta de altura e observador de streaming separados no módulo RTSWorld.Camera. Input System1.20.1 já adotado,sem novo pacote ou uso do Input Manager legado. Bloqueios independentes de teclado/ponteiro,foco de aplicação e exclusão do painel para UI futura.
- Integração exponencial exata da velocidade para comando constante;tempo não escalado pela simulação;arraste em pixels sem deltaTime duplicado. Foco X/Zdouble e guarda configurável(default±32768lógicos),com conversãofloat na apresentação e frame vertical/uniforme. Estratégia e limites emADR-0003.
- Consulta do mesmo terreno/água triangulados,sem depender de chunks carregados. Pose conserva foco/ângulo ao afastar a câmera até todo near plane ficar acima do limite conservador do terreno. Far plane inclui solo baixo atrás de foco alto mesmo na inclinação mínima. Sem colisores/câmera acoplada diretamente ao gerador.
- Ponte atualiza observador antes do streaming e dimensiona a residência pelo enquadramento dentro do máximo8existente. Movimento/zoom carregam/descarregam regiões;ficar parado não regenera/recria meshes.
- RTSCameraDemo.unity criada/salva no Unity,com camada ambiental3,painel de ajuda,controle de bordas e reset. Cenas anteriores e contratos do mundo preservados.

### Validação final
| Verificação | Status | Evidência |
| --- | --- | --- |
| Compilação C#/shader | PASS |Módulo/cena executados no Unity6000.6.9f1;22capturas finais shaderError=False. |
| EditMode | PASS |145/145,0falhas/ignorados,30.6922861s;editmode-final.xml. |
| PlayMode | PASS |5/5,0falhas/ignorados,0.593707s;playmode-final.xml. |
| FPS/direções/suavização | PASS |30/60/144comparados a240FPS;cardinais,diagonal,aceleração/coasting,frame longo,drag sem deltaTime duplicado. |
| Dispositivos/controles | PASS |Eventos reais do Input System em dispositivos de teste:WASD,MMB,scroll,Q/E,bordas e desativação,UI/foco. Não foi alegado teste manual humano. |
| Zoom/inclinação/segurança | PASS |Limites de zoom/yaw/20..80;near acima do teto de terreno e far cobrindo mínimo,frame transladado/rotacionado/escala uniforme. |
| Foco/superfície | PASS |Mesma diagonal e max de água/terra;versões1/2/3;consulta sem carregar chunks;foco projetado no centro da câmera. |
| Streaming/lifecycle | PASS |Fronteiras,cargas/unloads,raio adaptativo,sem regeneração parada,reuso de meshes,seed/mar e limpeza atébaseline. |
| Inspector | PASS |Configurações serializadas pelo Unity em RTSCameraDemo;inspector-serialization.json. Campos efetivamente utilizados pelos componentes. |
| Visual Editor/Play | PASS |11cenários/22PNGsURP reais:inicial,WASD,drag,zoom min/max,rotação,bordas,bordas desativadas,água,colina e atualização automática parada. Fila0após estabilizar. |
| Console | PASS com aviso externo |Editor0erros/1aviso de metadado URP;Play0erros/0avisos. Nenhum Clear do agente;cache/pacote não alterados. |
| Alocação/custo local | PASS como medida limitada |1000chamadas aquecidas estacionárias de controlador+superfície+observador:2.3398ms totais,0bytes gerenciados;excluiGUI,renderização e geração. Não é garantia deFPS. |
| Regressões/integridade | PASS |127+4testes anteriores incluídos;181inputs iguais à cópia;domínio/cenas antigas/Shader/Materials/Packages/ProjectSettings preservados. |
| Editor interativo original/MCP | NOT_RUN |MCP indisponível dentro/fora do sandbox;Unity real em cópia isolada com os mesmos fontes/pacotes existentes. |
| Player/build/outras plataformas/configuração máxima/heap/VRAM | NOT_RUN |Não inferidos a partir dos testes locais. |

As primeiras tentativas de compilação do rascunho tiveram erro de string e using ausente;foram corrigidas antes das suítes PASS. O utilitário de captura também teve conflito de nomeKey,corrigido apenas na cópia. Evidências de erros iniciais preservadas separadamente do resultado final.

### Arquivos
Criados em Assets/_Project:
- Presentation/Cameras/RTSWorld.Camera.asmdef,RtsCameraMotion.cs,RtsCameraInput.cs,WorldSurfaceQuery.cs,RtsCameraController.cs,CameraStreamingObserver.cs,RtsCameraDemo.cs e respectivas.meta/diretório.
- Scenes/RTSCameraDemo.unity/.meta.
- Tests/Presentation/RtsCameraTests.cs/.meta e Tests/StreamingPlayMode/RtsCameraLifecycleTests.cs/.meta.
Modificados:
- Presentation/Streaming/ChunkManager.cs:apenas3getters de configuração,sem alteraralgoritmos/ownership.
- Dois asmdefs de testes:referências aRTSWorld.Camera eUnity.InputSystem existentes.
- docs/ARCHITECTURE.md,CURRENT_STATE.md,HANDOFF.md;tasks/ACTIVE.md,BACKLOG.md e esta tarefa.
Documentação nova:ADR-0003-rts-camera.md e docs/validation/WORLD-005/. Utilitários de criação/captura só na cópia e publicados como.cs.txt;nenhuma ferramenta extra instalada no runtime.

### Limitações remanescentes
- Sem floating origin completo. Foco double não remove limitesfloat da câmera/meshes;guarda de navegação é configurável e não limita o mundo autoritativo. Frame precisa ser vertical e ter escala uniforme positiva.
- Cobertura visual limitada ao máximo8do streaming. Configurações arbitrariamente grandes podem excedê-la(CoverageLimited). A fila incremental e o custo atômico de chunks permanecem limites anteriores.
- Segurança considera terreno/água,sem colisores de árvores/recursos. O limite superior é conservador e aumenta a distância em vales sem alterar zoom/enquadramento. Alterações futuras nos limites de altura do gerador exigem atualizar o contrato de consulta.
- Câmera ortográfica desktop;sem seleção/unidades/UI completa/minimap/mobile/rede/persistência. As imagens são renders da câmera,não incluem o overlayIMGUI. Medida de0bytes refere-se somente ao trecho instrumentado,não ao frame inteiro.
- Aviso URP externo documentado;Editor interativo original não foi controlado. Nenhum critério obrigatório foi substituído por uma afirmação não verificada.

### Encerramento e reprodução
- Abrir Assets/_Project/Scenes/RTSCameraDemo.unity,entrar emPlay,dar foco à janelaGame e aguardar a fila estabilizar. WASD/MMB/scroll/QE;alternar bordas no painel;Settings no objetoRTS Camera Rig. Alterações feitas emPlay podem ser temporárias.
- Nenhuma implementação parcial no escopo. Próxima ação:revisão do usuário. Não iniciar tarefa posterior automaticamente;nenhum commit sem autorização.
- Git main/6a9966b0168d3ba553c97ed7f7c8a2818f5bbca4,sem commits. Alterações anteriores FIX-001/002 preservadas;diff contraHEAD combina trabalhos anteriores com aWORLD-005. Baseline desta tarefa em baseline-hashes.json.
- Evidências detalhadas e reprodução em docs/validation/WORLD-005/README.md;auditoria em final-audit.json.

## Auditoria final
PASS — final-audit.json confirma145EditMode+5PlayMode,181inputs sem divergências,22PNGs reais,especificação original preservada salvostatus/checks,88GUIDs únicos,56arquivos protegidos intactos,manager somente3getters e diff --check limpo. HEAD não mudou;nenhum commit.
