# ADR-0003 — Câmera RTS e residência visual por foco

**Status:** aceita no escopo autorizado da WORLD-005
**Data:** 2026-11-15

## Contexto
A tarefa autoriza câmera RTS sobre a apresentação/streaming existentes, mantendo o domínio independente da câmera e o Input System já ativo. O mundo ainda não possui floating origin nem colisores para todos os chunks, e um chunk visível pode estar pendente de geração.

## Decisão
- Novo módulo `RTSWorld.Camera` em Presentation/Cameras, referenciando apresentação, domínio e Unity.InputSystem já instalados. Nenhum pacote adicional. Cenas anteriores preservadas; nova RTSCameraDemo.
- Separar RtsCameraInput(dispositivos e bloqueios de UI), RtsCameraMotion(estado/matemática), RtsCameraController(pose e câmera), WorldSurfaceQuery(adaptador de altura) e CameraStreamingObserver(ponte de residência). Input produz struct por frame; modelo não conhece dispositivos, chunks ou gerador.
- Projeção ortográfica, inclinação20..80graus, yaw Q/E opcional. Zoom é meia altura da vista em unidades lógicas, escalada pela apresentação. Velocidade em unidades lógicas/segundo real; navegação usa tempo não escalado pela simulação.
- Movimento integra exatamente a resposta exponencial da velocidade ao alvo(aceleração/desaceleração por segundo). Zoom usa exponencial independente doFPS. Arraste converte deslocamento de pixels em deslocamento lógico no plano X/Z, incluindo foreshortening da inclinação; não multiplica o delta do mouse por deltaTime novamente. Rotação contínua usa o heading médio do passo.
- Foco X/Z em double; conversão parafloat apenas ao apresentar câmera/observador. Guarda de navegação configurável(default±32768unidades lógicas), sem limitar dados/autoridade do mundo. Frame deve ser vertical, com yaw/translação e escala uniforme positiva. Não implementar floating origin nesta tarefa; frame próximo da origem e escalas da demo são o alvo validado. Deslocamentos maiores exigirão rebase de apresentação.
- WorldSurfaceQuery interpola a mesma diagonal e o máximo terreno/água dos campos ambientais, inclusive sem chunks carregados. Cache de definição/gerador invalidado por seed/dimensão/camada/revisão. Não depende da câmera para gerar dados autoritativos e não instancia chunks.
- A altura base v1 é≤100; bermas ambientais legadas≤100.8 e água≤100. Usar limite conservador101×escala vertical e mínimo−3×escala vertical para as versões atuais. Distância da câmera é aumentada mantendo foco/ângulo até o ponto mais baixo do near plane ortográfico ficar acima desse limite+folga. Isso evita atravessar colinas ainda não carregadas, sem colisores ou consultas locais incompletas. Uma alteração futura desses limites de geração exige atualizar esse contrato.
- Ponte atualiza o observador pelo foco antes de ChunkManager.Update. Raio considera zoom,aspecto,yaw,inclinação e faixa vertical, limitando-se ao máximo8já existente. Não regenerar chunks quando foco/enquadramento/configuração não alteram a seleção. Se a configuração exigir mais que8, CoverageLimited sinaliza a limitação; não prometer cobertura de configurações arbitrárias.
- Preparar integração de UI com bloqueios independentes de teclado/ponteiro e exclusão do painel da demo. Seleção de unidades, action maps de gameplay e UI completa continuam fora do escopo.

## Consequências e limites
- Câmera não pode entrar em cavernas nem usar perspectiva nesta implementação; isso não é necessário para o RTS ortográfico autorizado. A distância conservadora pode ser maior em vales, sem alterar a escala ortográfica ou o foco.
- Suavização e matemática de navegação pertencem apenas à apresentação. Não há promessa de determinismo de câmera entre plataformas, nem mudança da versão do mundo.
- O deslocamento mantém coordenadas lógicas mais precisas, mas câmera/meshes/Transform continuam float. A guarda evita extrapolar indefinidamente o espaço visual; não é floating origin.
- Terreno/água fazem parte da segurança; árvores/recursos simples não são colisores. A geração síncrona e os buracos temporários durante a fila de streaming permanecem limites da WORLD-003.
