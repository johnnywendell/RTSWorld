# WORLD-005 — Sistema de Câmera RTS

**Status:** PENDING  
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

- [ ] A câmera se movimenta por WASD.
- [ ] O arraste com botão do meio funciona.
- [ ] A movimentação pelas bordas funciona e pode ser desativada.
- [ ] O zoom por scroll funciona dentro dos limites.
- [ ] A movimentação é suave e independente do FPS.
- [ ] A câmera mantém um enquadramento RTS adequado.
- [ ] A câmera não atravessa o terreno em condições normais.
- [ ] O foco da câmera acompanha corretamente a movimentação.
- [ ] O streaming carrega novos chunks conforme a câmera se desloca.
- [ ] Chunks distantes são descarregados conforme as regras do streaming.
- [ ] O Inspector permite configurar os principais parâmetros.
- [ ] A cena de demonstração funciona em Play Mode.
- [ ] Não existem erros de compilação.
- [ ] Os testes automatizados aplicáveis são executados.
- [ ] Os documentos de continuidade são atualizados.

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