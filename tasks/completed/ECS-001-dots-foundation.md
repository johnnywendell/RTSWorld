# ECS-001 — Fundação DOTS/ECS

**Status:** DONE  
**Priority:** HIGH  
**Owner:** nenhum — execução concluída  
**Phase:** 2 — DOTS/ECS  
**Dependencies:** WORLD-001 a WORLD-005 concluídas, incluindo as correções ambientais aplicáveis.  
**Branch:** main  
**Base commit:** 54ef57907c0e731f25adb0f8901e136ac8952326  
**Last checkpoint:** 2026-11-15 — validação final

---

## 1. Objective

Estabelecer a infraestrutura inicial de simulação baseada em Unity DOTS/ECS no RTSWorld, utilizando Unity Entities, C# Jobs e Burst, preservando a arquitetura existente de geração procedural, renderização de terreno, streaming e câmera RTS.

O objetivo não é apenas instalar pacotes, mas criar uma base funcional, testável, determinística dentro das condições definidas e preparada para a simulação de centenas ou milhares de unidades.

Ao final, o projeto deverá possuir um núcleo ECS mínimo, com componentes, sistemas, atualização em tempo fixo e uma cena de demonstração isolada.

## 2. Context

A Fase 1 entregou:

- Geração procedural determinística de terreno.
- Renderização low-poly por chunks.
- Streaming dinâmico.
- Biomas, hidrologia e recursos naturais.
- Câmera RTS.
- Ferramentas de diagnóstico e correções geométricas.

Esses sistemas não deverão ser migrados indiscriminadamente para ECS.

A arquitetura deverá manter uma separação clara entre:

1. Dados do mundo procedural.
2. Apresentação visual do mundo.
3. Simulação ECS.
4. Interface e controles do jogador.
5. Futuras camadas de persistência e multiplayer.

## 3. Scope

### 3.1. Configuração DOTS

Inspecionar o projeto e instalar/configurar versões compatíveis dos pacotes:

- Unity Entities.
- Unity Burst.
- Unity Collections, quando necessário.
- Unity Mathematics.
- Unity Jobs, conforme a API e os pacotes da versão instalada.
- Entities Graphics somente se necessário para a demonstração e se houver compatibilidade comprovada.

Não assumir números de versão sem consultar o Package Manager e a configuração real da Unity.

Preservar o URP e os pacotes existentes.

### 3.2. Estrutura arquitetural

Criar uma organização coerente com o projeto, preferencialmente em:

`Assets/_Project/Simulation/`

Sugestão de subdiretórios:

- Components/
- Systems/
- Authoring/
- Integration/
- Diagnostics/

Criar assembly definitions apropriadas, evitando dependências circulares.

### 3.3. Componentes fundamentais

Criar componentes ECS mínimos e reutilizáveis para:

- Identificação lógica de entidade.
- Estado de simulação.
- Posição e orientação, reutilizando componentes DOTS existentes quando adequado.
- Velocidade ou intenção de movimento.
- Configuração de atualização.
- Associação lógica com regiões/chunks, quando necessária.

Não duplicar componentes nativos sem justificativa.

### 3.4. Sistema de simulação

Implementar um sistema ECS inicial que:

- Atualize entidades em tempo fixo.
- Utilize APIs atuais de Entities.
- Demonstre leitura e escrita de componentes.
- Permita execução em Jobs.
- Utilize Burst onde for tecnicamente adequado.
- Evite alocações gerenciadas desnecessárias em caminhos críticos.
- Separe a simulação da apresentação.

O sistema deverá ser simples e demonstrável.

### 3.5. Integração com o mundo

Definir um contrato inicial para o ECS consultar dados do mundo procedural sem depender de MeshRenderer, GameObjects ou objetos visuais dos chunks.

Documentar:

- Como consultar elevação do terreno.
- Como identificar regiões do mundo.
- Como lidar com coordenadas globais.
- Como separar ciclo de vida visual e lógico.
- Como futuramente sincronizar dados do mundo com entidades ECS.

Não implementar ainda integração completa de navegação ou recursos.

### 3.6. Cena de demonstração

Criar uma cena de demonstração específica para a fundação ECS.

A cena deverá permitir:

- Inicializar um World ECS.
- Criar entidades de teste.
- Observar atualização de componentes.
- Verificar o passo fixo de simulação.
- Visualizar indicadores simples de quantidade de entidades e atualizações.

A representação visual pode ser mínima.

## 4. Architecture requirements

1. Utilizar APIs compatíveis com a versão instalada de Unity Entities.
2. Priorizar componentes unmanaged/blittable para dados de simulação.
3. Evitar lógica de gameplay em MonoBehaviours.
4. Usar MonoBehaviours somente para authoring, apresentação, configuração ou integração quando apropriado.
5. Evitar referências diretas a GameObjects dentro de componentes ECS de simulação.
6. Definir claramente os limites entre ECS e C# tradicional.
7. Evitar estado global mutável desnecessário.
8. Utilizar atualização em tempo fixo.
9. Documentar ordem de execução e grupos de sistemas.
10. Preparar a arquitetura para futura simulação autoritativa em servidor, sem implementar rede nesta tarefa.
11. Preservar o gerador procedural v1 e os algoritmos ambientais versionados.
12. Não converter o streaming visual em sistema de persistência de entidades.

## 5. Implementation plan

### Etapa A — Auditoria

- Recuperar contexto do projeto.
- Inspecionar packages e asmdefs.
- Confirmar versão da Unity.
- Verificar compatibilidade DOTS.
- Registrar baseline de compilação, testes e Git.

### Etapa B — Pacotes

- Instalar dependências necessárias.
- Resolver conflitos de assemblies.
- Validar compilação.

### Etapa C — Arquitetura

- Criar estrutura de Simulation.
- Definir componentes fundamentais.
- Criar sistemas e grupos necessários.

### Etapa D — Protótipo

- Criar entidades de teste.
- Implementar atualização fixa.
- Demonstrar Jobs e Burst.
- Criar cena funcional.

### Etapa E — Integração

- Definir contrato inicial com o mundo procedural.
- Demonstrar leitura controlada de dados de terreno.
- Documentar limites de responsabilidade.

### Etapa F — Validação

- Testes EditMode.
- Testes PlayMode.
- Inspeção via Unity MCP.
- Console.
- Documentação e handoff.

## 6. Out of scope

- Aldeões e soldados definitivos.
- Pathfinding.
- Flow Fields.
- Seleção de unidades.
- Combate.
- Economia.
- Multiplayer.
- Persistência de entidades.
- Conversão integral do mundo procedural para ECS.
- Otimização gráfica avançada.

## 7. Acceptance criteria

- [x] Pacotes DOTS necessários instalados e compatíveis.
- [x] Projeto compila sem erros.
- [x] Arquitetura ECS documentada.
- [x] Componentes fundamentais implementados.
- [x] Sistema ECS executando em tempo fixo.
- [x] Demonstração funcional de Jobs.
- [x] Demonstração funcional de Burst.
- [x] Entidades criadas e atualizadas corretamente.
- [x] Cena de demonstração funcionando.
- [x] Integração conceitual e contrato inicial com o terreno definidos.
- [x] Nenhuma regressão conhecida nos sistemas WORLD.
- [x] Testes automatizados aplicáveis aprovados.
- [x] Evidências de validação registradas.
- [x] Documentos de continuidade atualizados.

## 8. Tests and validation

Criar testes para:

- Criação e destruição de entidades.
- Inicialização de componentes.
- Atualização de sistemas.
- Comportamento do passo fixo.
- Integridade dos dados.
- Ausência de dependências inválidas entre assemblies.
- Execução de Jobs.
- Integração mínima com dados de terreno.

Verificar compilação, EditMode, PlayMode e Console via Unity MCP.

Registrar PASS, FAIL ou NOT_RUN com evidências.

## 9. Risks and decisions

Riscos principais:

- Incompatibilidade entre versões DOTS e Unity.
- Conflitos de assemblies.
- Uso incorreto de APIs ECS.
- Acoplamento excessivo com MonoBehaviours.
- Criação prematura de abstrações para multiplayer.

Documentar decisões e alternativas.

## 10. Continuity and checkpoints

Seguir AGENTS.md e TASK_TEMPLATE.md.

Ao iniciar:

- Recuperar contexto e verificar Git.
- Mover esta tarefa para tasks/in_progress/.
- Atualizar tasks/ACTIVE.md e tasks/BACKLOG.md.
- Registrar branch, commit base e checkpoint inicial.

Durante a implementação:

- Registrar progresso por etapa.
- Documentar arquivos modificados.
- Registrar resultados de testes.
- Atualizar docs/HANDOFF.md.
- Preservar informações para retomada por outro agente.

Ao concluir:

- Marcar DONE somente após cumprir os critérios obrigatórios.
- Mover para tasks/completed/.
- Atualizar docs/ARCHITECTURE.md, docs/CURRENT_STATE.md, docs/HANDOFF.md e tasks/BACKLOG.md.
- Informar o estado do Git.
- Não criar commits sem autorização.

**Não iniciar ECS-002 ou ECS-003 automaticamente. Aguardar autorização explícita.**
## Checkpoint — auditoria inicial

- Dependências WORLD-001..005 e FIX-001..003: arquivos completed com status DONE; fontes e assemblies presentes.
- Git inicial: main/54ef57907c0e731f25adb0f8901e136ac8952326; somente `?? tasks/pending/`, preservado. Documentos anteriores citam 6a9966b e alterações agora incorporadas; Git atual prevalece.
- Unity MCP: 6000.6.9f1, projeto correto, fora de Play, sem compilação em andamento; Console com zero entradas.
- Package Manager: Entities 6.6.0 disponível. Lock atual inclui Burst 2.0.0 e Collections 6.6.0; Entities e Mathematics ausentes.
- Implementação ECS: não iniciada. Próximo passo: baseline EditMode/PlayMode, depois adicionar Entities pelo Package Manager e validar resolução/compilação.
- Testes da sessão: NOT_RUN; resultados históricos não reutilizados como execução atual.
- Arquivos alterados: tarefa movida para in_progress, ACTIVE, BACKLOG e HANDOFF. Sem commits.

### Baseline antes dos pacotes
- PASS EditMode: 172/172, 35,2205676 s, job `721bfa8f14d04e2e94087f911247d154`.
- PASS PlayMode: 6/6, 1,8035805 s, job `505dd5ea242647f18beecc7d2129a8de`.
- Desconexão transitória MCP durante polling EditMode; reconexão recuperou resultado concluído. Dois avisos WebSocket de infraestrutura; não foram apagados.
- Próximo passo: instalar Entities 6.6.0 pelo Package Manager, autorizado pelo escopo ECS-001; preservar URP e verificar resolução antes de implementar.

### Checkpoint — implementação inicial
- Entities 6.6.0 resolvido pelo Package Manager; Burst 2.0.0, Collections 6.6.0 e URP 17.6.0 preservados. Mathematics/Jobs são módulos do Editor (confirmado por reflection); sem pacotes redundantes ou Entities Graphics.
- Compilação inicial PASS via MCP (scriptCompilationFailed=false); sessão criada e job de movimento executado: tick inicial=1 e X=-0.1+0.125=0.025.
- Criados Simulation/Components, Systems, Integration, Authoring, assembly próprio; testes EditMode/PlayMode escritos, ainda NOT_RUN.
- FixedStepSimulationSystemGroup explícito em World isolado; IJobEntity ScheduleParallel com Burst síncrono. Posição global double3 e rotação quaternion; contrato de terreno managed prepara amostras sem referências visuais.
- Assets/_Project/Scenes/RTSCameraDemo.unity apresentou seed 7→123 durante baseline; origem não confirmada, alteração preservada e fora do escopo ECS. Não sobrescrever.
- Package Manager gerou ProjectSettings/EntitiesClientSettings.asset. Sem edição manual de caches.
- Próximo passo: validar testes, criar cena independente e registrar arquitetura/evidências. Nenhuma próxima tarefa iniciada.

## Checkpoint final — DONE

### Concluído e verificado
- Etapas A–F concluídas exclusivamente para ECS-001. Entities 6.6.0 compatível com Editor real, sem alterar URP. World próprio, componentes unmanaged, passo fixo, Jobs/Burst, contrato de terreno e demo isolada implementados.
- Arquitetura/ordem/lifecycle/limites documentados em ARCHITECTURE e ADR-0004, dentro do escopo já autorizado. Não houve migração WORLD ou implementação de ECS-002/003.
- PASS: compilação final; 181/181 EditMode e 7/7 PlayMode; execução Burst nativa; cena em Play com 256 entidades atualizando; zero World próprio após stop; Console final sem entradas; GUIDs/metas e git diff --check.
- NOT_RUN: build/player, outras plataformas, benchmarks. Sem critério obrigatório parcialmente implementado ou bloqueio pendente.

### Arquivos criados/modificados
- Packages/manifest.json e packages-lock.json; ProjectSettings/EntitiesClientSettings.asset gerado pelo pacote.
- Assets/_Project/Simulation/{Components, Systems, Integration, Authoring}, RTSWorld.Simulation.asmdef e respectivos .meta.
- Assets/_Project/Scenes/ECSFoundationDemo.unity e .meta.
- Assets/_Project/Tests/Simulation e SimulationPlayMode, testes/asmdefs/.meta.
- docs/ARCHITECTURE.md; docs/decisions/ADR-0004-ecs-foundation.md; docs/validation/ECS-001/{README.md,mcp-results.json,demo-play.png}.
- docs/CURRENT_STATE.md, docs/HANDOFF.md, tasks/ACTIVE.md, tasks/BACKLOG.md e esta tarefa movida para completed.
- RTSCameraDemo seed 7→123 apareceu durante baseline, origem não confirmada: preservada, sem assumir autoria. Demais tarefas pending preservadas.

### Incidentes e próximos passos
- Falha de compilação intermediária DC0061 resolvida adicionando Unity.Collections ao assembly PlayMode. Desconexão MCP transitória recuperada. Evidências e limites no relatório.
- Próxima menor ação: revisão do usuário do diff ECS-001 e da alteração incidental de seed; nenhuma próxima tarefa autorizada.
- Git: main/54ef57907c0e731f25adb0f8901e136ac8952326; sem commits. Working tree contém a implementação/documentação ECS-001, arquivos pending preexistentes e a alteração de seed preservada.
