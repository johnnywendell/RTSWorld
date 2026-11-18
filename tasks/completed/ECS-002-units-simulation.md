# ECS-002 — Unidades e Simulação

**Status:** DONE  
**Priority:** HIGH  
**Owner:** nenhum — execução concluída  
**Phase:** 2 — DOTS/ECS  
**Dependencies:** ECS-001 concluída.  
**Branch:** main  
**Base commit:** 54ef57907c0e731f25adb0f8901e136ac8952326  
**Last checkpoint:** 2026-11-15 — validação final

---

## 1. Objective

Implementar o primeiro sistema funcional de unidades do RTSWorld utilizando ECS, permitindo criar, atualizar e visualizar centenas ou milhares de entidades com arquitetura preparada para um RTS de grande escala.

Criar os tipos iniciais de unidades:

- Aldeão.
- Soldado.

As unidades deverão possuir identificação lógica, posição, orientação, velocidade, atributos e estado de simulação.

A tarefa deverá estabelecer uma base reutilizável para futuras implementações de navegação, coleta, combate e multiplayer.

## 2. Context

A ECS-001 deverá fornecer:

- Infraestrutura DOTS.
- Componentes fundamentais.
- Sistemas de atualização.
- Jobs e Burst.
- Contratos iniciais com o mundo procedural.

A ECS-002 utilizará essa infraestrutura para construir a primeira simulação de unidades.

A movimentação implementada nesta tarefa será simples e controlada, sem pathfinding completo.

## 3. Scope

### 3.1. Modelo de unidades

Criar uma representação ECS de unidade com componentes para:

- Identificador lógico estável.
- Tipo de unidade.
- Facção ou proprietário lógico.
- Posição.
- Orientação.
- Velocidade máxima.
- Velocidade atual ou intenção de movimento.
- Vida atual e máxima.
- Estado da unidade.
- Atributos básicos.

Os atributos poderão variar por tipo de unidade.

Evitar estruturas excessivamente complexas.

### 3.2. Tipos iniciais

**Aldeão**

Atributos iniciais configuráveis:

- Vida.
- Velocidade.
- Capacidade de transporte futura.
- Estado básico de atividade.

**Soldado**

Atributos iniciais configuráveis:

- Vida.
- Velocidade.
- Ataque.
- Defesa.
- Alcance futuro.

Não implementar coleta nem combate nesta tarefa.

### 3.3. Criação de entidades

Implementar:

- Definições de unidades reutilizáveis.
- Prefabs ECS ou mecanismo equivalente.
- Authoring e Baking, quando apropriados.
- Instanciação em lote.
- Identificação única.
- Destruição controlada.

Criar testes para quantidades configuráveis de entidades.

### 3.4. Simulação

Implementar atualização em tempo fixo utilizando ECS.

As unidades deverão:

- Atualizar posição com base em velocidade ou intenção de movimento.
- Respeitar limites configuráveis.
- Manter atributos independentes da apresentação visual.
- Permitir pausa e retomada da simulação.
- Produzir resultados consistentes para entradas e passos de tempo equivalentes.

A movimentação poderá utilizar destinos diretos ou trajetórias de teste, sem contornar obstáculos.

### 3.5. Integração com terreno

Utilizar os contratos da ECS-001 para consultar a altura do terreno.

As unidades deverão ser posicionadas sobre o terreno quando houver dados disponíveis.

Evitar dependência direta de MeshCollider ou MeshRenderer como fonte única da elevação.

Definir comportamento seguro para regiões sem dados disponíveis.

Não permitir que o descarregamento visual de um chunk destrua automaticamente suas entidades lógicas.

### 3.6. Apresentação visual

Criar representações simples para aldeões e soldados.

Exemplos:

- Cápsulas.
- Cubos.
- Modelos low-poly provisórios.

Distinguir visualmente os tipos.

Avaliar Entities Graphics ou outra solução de renderização eficiente compatível com a arquitetura.

Evitar um GameObject individual por unidade quando isso comprometer o objetivo de escala.

### 3.7. Demonstração de carga

Criar uma cena específica para testar diferentes populações.

Cenários sugeridos:

- 100 unidades.
- 500 unidades.
- 1.000 unidades.
- 5.000 unidades, se viável.

Permitir ajustar a quantidade de entidades e visualizar:

- Total de unidades.
- Quantidade por tipo.
- Entidades atualizadas.
- Tempo de simulação.
- FPS.
- Tempo médio de frame, quando disponível.

Os números não representam metas obrigatórias de desempenho. O desempenho deverá ser medido e documentado.

## 4. Architecture requirements

1. Toda lógica central das unidades deve utilizar ECS.
2. Componentes de dados devem permanecer separados dos sistemas.
3. Evitar lógica individual de atualização por MonoBehaviour.
4. Utilizar Jobs e Burst quando apropriado.
5. Evitar alocações por entidade a cada frame.
6. Utilizar instanciamento e processamento em lote.
7. Separar simulação e renderização.
8. Evitar referências gerenciadas em componentes críticos.
9. Preservar a identidade lógica das unidades.
10. Preparar o modelo para futuros comandos de jogador.
11. Não acoplar a simulação ao streaming visual.
12. Evitar otimizações prematuras sem medição.

## 5. Implementation plan

### Etapa A — Auditoria

- Recuperar contexto.
- Revisar ECS-001.
- Verificar infraestrutura disponível.
- Registrar baseline.

### Etapa B — Componentes

- Criar tipos e atributos.
- Criar estados.
- Definir identificação e propriedade.

### Etapa C — Instanciação

- Implementar authoring/baking.
- Criar mecanismo de spawn.
- Validar criação em lote.

### Etapa D — Simulação

- Implementar sistemas de atualização fixa.
- Integrar Jobs/Burst.
- Criar movimentação simples.

### Etapa E — Terreno

- Consultar altura procedural.
- Posicionar unidades.
- Definir comportamento em regiões descarregadas.

### Etapa F — Visualização

- Criar representações.
- Integrar renderização eficiente.
- Criar cena de demonstração.

### Etapa G — Carga e validação

- Executar cenários de escala.
- Registrar medições.
- Executar testes.
- Atualizar documentação.

## 6. Out of scope

- Pathfinding completo.
- Flow Fields.
- A*.
- Desvio de obstáculos.
- Seleção RTS.
- Comandos por clique.
- Coleta de recursos.
- Combate real.
- IA estratégica.
- Multiplayer.
- Persistência definitiva.
- Animações complexas.

## 7. Acceptance criteria

- [x] Aldeões representados como entidades ECS.
- [x] Soldados representados como entidades ECS.
- [x] Componentes fundamentais implementados.
- [x] Identificadores lógicos únicos.
- [x] Instanciação em lote funcionando.
- [x] Atualização em tempo fixo.
- [x] Movimentação simples funcional.
- [x] Integração inicial com elevação do terreno.
- [x] Simulação independente da renderização.
- [x] Pausa e retomada funcionando.
- [x] Cena de demonstração funcional.
- [x] Cenários de 100, 500 e 1.000 unidades testados.
- [x] Cenário de 5.000 unidades executado quando viável, com limitação documentada se não for.
- [x] Profiling e métricas registrados.
- [x] Testes automatizados aprovados.
- [x] Nenhuma regressão conhecida nos sistemas anteriores.
- [x] Documentação atualizada.

## 8. Tests and validation

Criar testes para:

- Instanciação de entidades.
- Identificação única.
- Inicialização de atributos.
- Atualização de posição.
- Passo fixo.
- Pausa e retomada.
- Destruição de entidades.
- Integridade de componentes.
- Posicionamento no terreno.
- Comportamento sem dados de terreno.
- Consistência após descarregamento visual.
- Cenários de carga.

Utilizar Unity MCP para compilação, testes e inspeção.

Registrar PASS, FAIL ou NOT_RUN.

## 9. Risks and decisions

- Renderização pode limitar o desempenho antes da simulação.
- Jobs podem introduzir problemas de sincronização.
- A integração com terreno pode gerar dependências indesejadas.
- Identificadores lógicos não devem depender exclusivamente de Entity.Index.
- Determinismo entre plataformas não deve ser presumido sem validação.

Registrar decisões técnicas.

## 10. Continuity and checkpoints

Seguir AGENTS.md e TASK_TEMPLATE.md.

Ao iniciar:

- Recuperar contexto.
- Confirmar ECS-001 DONE.
- Verificar Git.
- Mover para tasks/in_progress/.
- Atualizar ACTIVE e BACKLOG.

Durante a execução:

- Registrar checkpoints.
- Documentar decisões.
- Registrar testes e desempenho.
- Atualizar HANDOFF.

Ao concluir:

- Marcar DONE somente com critérios obrigatórios satisfeitos.
- Mover para tasks/completed/.
- Atualizar arquitetura, estado atual e handoff.
- Informar limitações e Git.
- Não criar commits sem autorização.

**Não iniciar ECS-003 automaticamente. Aguardar autorização explícita.**
## Checkpoint final — DONE

### Concluído e verificado
- Etapas A–G concluídas exclusivamente para ECS-002. Dependência ECS-001 verificada nas fontes/Editor e baseline atual PASS 181/181 EditMode e 7/7 PlayMode.
- Unidades aldeão/soldado com atributos configuráveis, proprietário, identidade monotônica sem reuso na sessão, pose global, intenção/velocidade/estado/vida; archetype e criação/destruição em lote.
- World isolado, fixed group e job Burst paralelo; limite de área/velocidade, orientação, pausa/retomada e replay consistente nas condições locais testadas. Fundação anterior preservada.
- Snapshot de elevação triangular preparado via contrato ECS-001; testes v1 e ambiente v4. Ausência/suspensão de dados congela pose; unload visual real não destrói unidades.
- UnitsSimulationDemo com câmera/luz, terreno v1, authoring configurável, tipos visualmente distintos e apresentação instanciada/HUD. Sem GameObject por unidade ou pacote novo.
- PASS compilação real; 195/195 EditMode (33,8572271 s), 9/9 PlayMode (3,5675784 s); todos cenários 100/500/1000/5000. Evidências/job IDs em docs/validation/ECS-002.
- Profiling: 120 ticks completos por população após 16 de aquecimento; média 0,01134/0,01843/0,02193/0,05286 ms respectivamente. Cena real 5000, 120 frames: média 2,60764 ms, p95 3,33233 ms; contadores globais Profiler documentados. Resultados locais de Editor, sem metas/garantias de player.
- PASS metadados presentes/GUIDs únicos, git diff --check; Editor final sem compilação/falha, Play desligado, cena limpa, zero Worlds RTSWorld, Console sem erros/avisos (logs informativos de testes preservados).

### Parcial, limites e problemas conhecidos
- Nenhum critério obrigatório parcial nem bloqueio. Build/player, outras plataformas, heap/VRAM e GPU timing: NOT_RUN.
- Snapshot cobre somente área lógica configurada (até 256 células/eixo); sem snapshot, não pode retomar disponibilidade sem criar uma sessão com dados. Movimento direto sem contorno de obstáculos; limite bloqueia deslocamento excedente. IDs estáveis somente na sessão.
- Refresh inicial scope scripts não importou arquivos novos; execução intermediária 181/181 validou somente baseline. Corrigido com refresh all e confirmação dos tipos; nenhum resultado omitido ou atribuído incorretamente ao novo código.
- Captura imediata MCP vazia e captura com enquadramento cortado preservadas como diagnósticos, excluídas do aceite. demo-5000-framed.png é a evidência válida, capturada após frames normais. Câmera final salva na altura do terreno.
- Aviso transitório WebSocket e inspeção World.All via LINQ falha corrigidos; Console final sem erros/avisos. Não foram bloqueios de implementação.

### Arquivos criados/modificados nesta tarefa
- Simulation/Components/UnitComponents.cs; Systems/UnitMovementSystem.cs; Integration/UnitSession.cs e metas.
- Presentation/Units/{RTSWorld.Units.Presentation.asmdef,UnitDemo.cs} e metas; Scenes/UnitsSimulationDemo.unity e meta.
- Tests/Simulation/{UnitTests.cs,UnitBenchmarkTests.cs} e metas; SimulationPlayMode/UnitDemoTests.cs e meta; asmdef PlayMode com referências de teste.
- docs/decisions/ADR-0005-unit-simulation.md; docs/ARCHITECTURE.md; docs/validation/ECS-002 (relatório, testes, métricas, capturas).
- docs/CURRENT_STATE.md, docs/HANDOFF.md, tasks/ACTIVE.md, tasks/BACKLOG.md; esta tarefa movida de pending para in_progress e completed.

### Git e próxima menor ação
- Branch main; HEAD 54ef57907c0e731f25adb0f8901e136ac8952326; nenhum commit criado.
- Preservado trabalho não commitado anterior: ECS-001 (fontes/testes/cena/metas), pacotes/configuração Entities, documentos/ADR/evidências, seed 7→123 em RTSCameraDemo e demais tasks/pending. Nenhuma alteração desta tarefa em World/câmera/streaming anteriores ou pacotes.
- Próxima ação: revisão do diff e evidências pelo usuário. Não iniciar nem implementar ECS-003 ou qualquer próxima tarefa sem autorização explícita.
