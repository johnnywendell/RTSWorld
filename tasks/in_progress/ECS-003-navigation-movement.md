# ECS-003 — Navegação e Movimentação

**Status:** BLOCKED  
**Priority:** HIGH  
**Owner:** nenhum — encerrada por bloqueio  
**Phase:** 2 — DOTS/ECS  
**Dependencies:** ECS-001 e ECS-002 concluídas.  
**Branch:** main  
**Base commit:** 54ef57907c0e731f25adb0f8901e136ac8952326  
**Last checkpoint:** 2026-11-15 — auditoria e bloqueio de validação

---

## 1. Objective

Implementar o sistema inicial de navegação e movimentação inteligente das unidades ECS do RTSWorld, permitindo deslocamentos entre posições do mundo procedural, considerando terreno, obstáculos, chunks e grupos de unidades.

A arquitetura deverá suportar movimentação individual e movimentação em massa, utilizando técnicas adequadas para um RTS de grande escala.

Avaliar e implementar uma combinação de:

- Navegação hierárquica.
- A* para rotas globais ou regionais.
- Flow Fields para grupos com destinos compartilhados.
- Desvio local de obstáculos, em uma implementação inicial controlada.

A solução deverá ser orientada por medições e preparada para evolução.

## 2. Context

A ECS-002 deverá fornecer:

- Aldeões e soldados ECS.
- Componentes de posição e velocidade.
- Sistemas de atualização fixa.
- Instanciação em lote.
- Integração inicial com o terreno.
- Cena de demonstração.

A ECS-003 deverá substituir ou complementar a movimentação simples por navegação baseada em destinos e condições do mundo.

## 3. Scope

### 3.1. Representação navegável

Criar uma representação lógica de navegabilidade independente das meshes visuais.

Considerar:

- Elevação.
- Declividade.
- Água.
- Terreno transitável.
- Obstáculos estáticos.
- Regiões bloqueadas.
- Custos de deslocamento.

Não depender exclusivamente de colisores físicos para construir a navegação.

### 3.2. Integração com chunks

A navegação deverá utilizar coordenadas globais e ser compatível com o sistema procedural.

Implementar:

- Dados de navegação por região ou chunk.
- Conectividade entre chunks.
- Continuidade nas bordas.
- Identificação de regiões transitáveis.
- Tratamento de chunks sem dados carregados.
- Atualização controlada de dados de navegação.

Separar streaming visual e dados lógicos necessários à navegação.

### 3.3. A* e navegação hierárquica

Implementar ou validar uma solução de busca de rotas apropriada para:

- Destinos individuais.
- Travessia entre regiões.
- Obstáculos estáticos.
- Rotas de alto nível.

Avaliar a necessidade de hierarquia de regiões.

Não executar A* completo individualmente para milhares de unidades a cada frame.

### 3.4. Flow Fields

Implementar um sistema inicial de campos de fluxo para grupos de unidades com destinos semelhantes.

O sistema deverá:

- Construir um campo de custos.
- Calcular integração de custos até o destino.
- Produzir direções de movimento.
- Permitir compartilhamento do campo por várias unidades.
- Evitar reconstrução desnecessária.
- Permitir invalidação quando o terreno navegável mudar.

Documentar limitações e custo computacional.

### 3.5. Movimentação ECS

Integrar os dados de navegação aos sistemas da ECS-002.

As unidades deverão:

- Receber um destino.
- Consultar a rota ou campo de fluxo.
- Atualizar direção e velocidade.
- Deslocar-se no terreno.
- Parar ao alcançar o destino.
- Evitar atravessar regiões bloqueadas.
- Respeitar o passo fixo de simulação.

### 3.6. Desvio local

Implementar uma solução inicial para reduzir:

- Sobreposição excessiva de unidades.
- Congestionamento simples.
- Colisões evidentes com obstáculos estáticos.

Não exigir comportamento avançado de multidões nesta tarefa.

### 3.7. Diagnóstico visual

Criar ferramentas opcionais para visualizar:

- Células navegáveis.
- Células bloqueadas.
- Custos de movimento.
- Rotas A*.
- Campos de direção.
- Conexões entre chunks.
- Destinos das unidades.
- Regiões sem dados de navegação.

Essas ferramentas deverão ser desativáveis.

### 3.8. Cena de demonstração

Criar uma cena com:

- Terreno procedural.
- Unidades ECS.
- Obstáculos estáticos.
- Destinos configuráveis.
- Grupos de unidades.
- Visualização opcional de rotas e Flow Fields.
- Indicadores de desempenho.

Permitir testes de movimentação entre diferentes chunks.

## 4. Architecture requirements

1. A navegação deve consumir dados lógicos do terreno.
2. Evitar dependência direta de meshes renderizadas.
3. Separar geração de dados navegáveis, busca de rotas e movimentação.
4. Utilizar ECS para estado e atualização das unidades.
5. Utilizar Jobs e Burst quando adequado.
6. Evitar alocações por unidade por frame.
7. Compartilhar Flow Fields entre unidades quando possível.
8. Definir política de cache e invalidação.
9. Evitar recálculo completo de rotas sem necessidade.
10. Suportar coordenadas negativas.
11. Preservar continuidade entre chunks.
12. Evitar destruir a simulação quando um chunk visual é descarregado.
13. Preparar o sistema para obstáculos dinâmicos futuros.
14. Documentar limites de precisão e determinismo.
15. Manter a arquitetura preparada para futura autoridade de servidor.

## 5. Implementation plan

### Etapa A — Auditoria

- Recuperar contexto.
- Revisar ECS-001 e ECS-002.
- Inspecionar terreno, água e streaming.
- Definir requisitos de navegabilidade.

### Etapa B — Dados de navegação

- Criar representação por região.
- Definir custos.
- Classificar obstáculos.
- Validar bordas dos chunks.

### Etapa C — Busca de rotas

- Implementar A* inicial.
- Avaliar hierarquia.
- Validar rotas entre regiões.

### Etapa D — Flow Fields

- Criar campos de custo.
- Criar integração de custos.
- Criar campo de direções.
- Implementar cache e compartilhamento.

### Etapa E — Movimento

- Integrar com unidades ECS.
- Implementar destinos.
- Implementar chegada e parada.
- Ajustar orientação e velocidade.

### Etapa F — Desvio local

- Implementar solução inicial de separação.
- Testar grupos.
- Identificar limitações.

### Etapa G — Diagnóstico

- Criar visualização de rotas e campos.
- Criar cena de demonstração.
- Registrar métricas.

### Etapa H — Validação

- Executar testes.
- Avaliar desempenho.
- Verificar regressões.
- Atualizar documentação.

## 6. Out of scope

- Seleção RTS definitiva.
- Interface de comandos por clique.
- Combate.
- Formações militares avançadas.
- Construção dinâmica de cidades.
- Destruição de obstáculos.
- Navegação naval.
- Navegação aérea.
- Multiplayer.
- Sincronização de rede.
- IA estratégica.
- Simulação avançada de multidões.

## 7. Acceptance criteria

- [ ] Representação navegável independente da renderização.
- [ ] Custos de deslocamento definidos.
- [ ] Água e obstáculos tratados corretamente.
- [ ] Continuidade de navegação entre chunks.
- [ ] Suporte a coordenadas negativas.
- [ ] Busca A* funcional.
- [ ] Navegação hierárquica avaliada e decisão documentada.
- [ ] Flow Fields funcionais para grupos.
- [ ] Cache e invalidação básicos implementados.
- [ ] Unidades ECS recebem destinos.
- [ ] Unidades seguem rotas ou campos de fluxo.
- [ ] Unidades param ao chegar.
- [ ] Unidades não atravessam obstáculos conhecidos.
- [ ] Desvio local inicial implementado.
- [ ] Diagnóstico visual disponível.
- [ ] Cena de demonstração funcional.
- [ ] Testes automatizados aprovados.
- [ ] Cenários com múltiplas unidades avaliados.
- [ ] Profiling registrado.
- [ ] Nenhuma regressão conhecida nos sistemas anteriores.
- [ ] Documentação e handoff atualizados.

## 8. Tests and validation

Criar testes para:

- Classificação de navegabilidade.
- Custos de terreno.
- Água e regiões bloqueadas.
- Coordenadas negativas.
- Continuidade entre chunks.
- Rotas entre regiões.
- Destinos inacessíveis.
- A* em mapas controlados.
- Integração de custos dos Flow Fields.
- Direções de movimento.
- Cache e invalidação.
- Chegada ao destino.
- Obstáculos.
- Separação de unidades.
- Consistência após streaming.
- Cenários com grupos grandes.

Testar também:

- Grupos pequenos.
- Grupos médios.
- Centenas de unidades.
- Milhares de unidades, quando viável.

Medir separadamente custo de geração de navegação, cálculo de rotas e atualização de movimento.

## 9. Risks and decisions

Riscos principais:

- Custo de reconstrução de Flow Fields.
- Crescimento de memória em mapas extensos.
- Inconsistência entre chunks.
- Congestionamento em passagens estreitas.
- Rotas inválidas após mudanças ambientais.
- Acoplamento entre navegação e renderização.
- Uso excessivo de A* individual.

Registrar decisões arquiteturais e alternativas.

## 10. Continuity and checkpoints

Seguir AGENTS.md e TASK_TEMPLATE.md.

Ao iniciar:

- Recuperar contexto.
- Confirmar ECS-001 e ECS-002 concluídas.
- Verificar Git.
- Mover para tasks/in_progress/.
- Atualizar ACTIVE e BACKLOG.

Durante a implementação:

- Registrar checkpoints.
- Documentar decisões.
- Registrar arquivos modificados.
- Registrar testes e métricas.
- Atualizar HANDOFF.

Ao concluir:

- Marcar DONE somente com critérios obrigatórios satisfeitos.
- Mover para tasks/completed/.
- Atualizar arquitetura, estado atual e handoff.
- Informar limitações e Git.
- Não criar commits sem autorização.

**Não iniciar ECS-004 ou qualquer tarefa posterior automaticamente. Aguardar autorização explícita.**

## Checkpoint — BLOCKED (2026-11-15)

### Concluído e verificado
- Inicialização AGENTS.md: Git, CURRENT_STATE, ACTIVE, HANDOFF, tarefa, template e arquitetura consultados. ECS-001/ECS-002 constam DONE; fontes UnitSession, UnitMovementSystem, componentes e contrato procedural inspecionadas. Não há implementação ECS-003.
- Unity MCP confirmou o projeto correto, cena UnitsSimulationDemo limpa, Play desligado, isCompiling=false e scriptCompilationFailed=false.
- PASS: baseline EditMode 195/195, zero falhas/ignorados, 33,7525616 s; job a626e4ca73e848df9c5cc0d6973d6b90. Desconexão transitória no primeiro poll recuperada; resultado final consultado com sucesso. Console antes do teste: zero erros/avisos.

### Bloqueio e ocorrência
- A restrição explícita desta sessão proíbe alterar arquivos fora do projeto. O baseline foi acionado antes de conferir todos os efeitos de escrita do runner; o callback do pacote de performance grava automaticamente fora do projeto. Não repetir esse procedimento nesta sessão.
- Fonte efetivamente inspecionada (somente leitura): Library/PackageCache/com.unity.test-framework.performance@6272f70ed558/Editor/PerformanceTestRunSaver.cs, RunFinished: WriteResultToFile para Application.persistentDataPath/TestResults.xml sem override. ResultsPath também tem fallback/cópia para DefaultResultsPath. TestRunnerInitializer registra esse callback automaticamente.
- Unity confirmou persistentDataPath=/home/johnnywendell/.config/unity3d/DefaultCompany/RTSWorld. A chamada do baseline acionou esse callback; o arquivo externo não foi lido, editado manualmente nem removido. Não foi solicitada permissão ampliada, nem alterado pacote/cache para contornar a restrição.
- Conforme instrução do usuário, parar ao encontrar bloqueio. A implementação e suas verificações obrigatórias permanecem NOT_RUN; nenhum critério de navegação foi atendido ou dispensado.

### Arquivos e estado Git
- Somente continuidade ECS-003 editada: esta tarefa (movida para tasks/in_progress), tasks/ACTIVE.md, tasks/BACKLOG.md, docs/CURRENT_STATE.md e docs/HANDOFF.md. Nenhum fonte, cena ou pacote alterado intencionalmente nesta tarefa.
- main/54ef57907c0e731f25adb0f8901e136ac8952326; sem commits. Working tree já continha ECS-001/ECS-002, pacotes/EntitiesClientSettings, documentação, evidências, tarefas pending e RTSCameraDemo seed 7→123; tudo preservado. Testes de benchmark preexistentes podem atualizar evidências ECS-002 durante a suíte; não restaurar esses arquivos sem comparar com o baseline anterior.

### Próxima menor ação
- Resolver em sessão autorizada uma execução de testes cujo runner não escreva fora do projeto; validar a solução antes de retomar a etapa A. Não remover callbacks nem editar cache/pacotes nesta sessão. ECS-004 e demais tarefas não iniciadas.

### Verificações
| Verificação | Estado | Evidência |
| --- | --- | --- |
| Dependências e estado Editor | PASS | fontes e MCP; ambas tarefas anteriores DONE |
| Baseline EditMode | PASS | 195/195; job acima |
| Compilação anterior | PASS | scriptCompilationFailed=false |
| Implementação/compilação ECS-003 | NOT_RUN | bloqueio na auditoria |
| PlayMode ECS-003 e profiling | NOT_RUN | bloqueio na auditoria |

Console final: dois avisos MCP WebSocket da desconexão transitória; conexão recuperada. Nenhum erro retornado pela consulta final. `git diff --check`: PASS após correção de linha vazia final no handoff.
