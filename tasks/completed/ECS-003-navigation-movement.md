# ECS-003 — Navegação e Movimentação

**Status:** DONE
**Priority:** HIGH  
**Owner:** nenhum — concluída
**Phase:** 2 — DOTS/ECS  
**Dependencies:** ECS-001 e ECS-002 concluídas.  
**Branch:** main  
**Base commit:** b207bd7
**Last checkpoint:** 2026-11-18 — implementação e validação concluídas

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

- [x] Representação navegável independente da renderização.
- [x] Custos de deslocamento definidos.
- [x] Água e obstáculos tratados corretamente.
- [x] Continuidade de navegação entre chunks.
- [x] Suporte a coordenadas negativas.
- [x] Busca A* funcional.
- [x] Navegação hierárquica avaliada e decisão documentada.
- [x] Flow Fields funcionais para grupos.
- [x] Cache e invalidação básicos implementados.
- [x] Unidades ECS recebem destinos.
- [x] Unidades seguem rotas ou campos de fluxo.
- [x] Unidades param ao chegar.
- [x] Unidades não atravessam obstáculos conhecidos.
- [x] Desvio local inicial implementado.
- [x] Diagnóstico visual disponível.
- [x] Cena de demonstração funcional.
- [x] Testes automatizados aprovados.
- [x] Cenários com múltiplas unidades avaliados.
- [x] Profiling registrado.
- [x] Nenhuma regressão conhecida nos sistemas anteriores.
- [x] Documentação e handoff atualizados.

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

## Checkpoint final — 2026-11-18

- **Concluído:** malha lógica global limitada, classificação de água/declividade/obstáculos, regiões indisponíveis, A* regional, campos compartilhados, cache e revisão. Destinos em lote, Burst no passo fixo, parada nas células de destino e dispersão por pontos internos. Demo com dois grupos, barreiras, destinos configuráveis, diagnóstico e tempos.
- **Arquivos:** `Simulation/Integration` contém NavigationGrid, NavigationSearch e adaptação do contrato/UnitSession. UnitComponents e UnitMovementSystem integram estado e jobs. `Presentation/Units` contém UnitDemo e NavigationDemoOverlay. Nova NavigationDemo.unity, testes NavigationMovementTests, NavigationSearchTests e NavigationDemoTests, com respectivos metas. ADR-0006, arquitetura, estado, backlog, active, handoff e relatório de validação atualizados.
- **PASS:** compilação no Unity 6000.6.9f1, 204/204 Edit Mode em 36,9886907 s e 10/10 Play Mode em 2,2888051 s, sem falhas ou ignorados nas rodadas finais. O teste de streaming foi corrigido para usar a mesma escala e o mesmo tamanho de chunk da navegação. A cópia isolada redirecionou também o XML do callback de performance para dentro do projeto.
- **PASS:** Play real com 1.000 unidades e diagnóstico. Em execução adicional, 5.000/5.000 chegaram e pararam no destino em até 1.601 ticks. O Editor voltou à RTSCameraDemo com suas mudanças locais não salvas, Play desligado, zero Worlds de unidades e zero erros. Um aviso transitório de WebSocket MCP foi observado.
- **PASS:** perfis com 8, 100, 500 e 5.000 unidades. Na rodada final, 5.000 unidades tiveram média de 0,03832 ms/tick em 120 amostras. A janela máxima de 256 × 256 apresentou 33,29370 ms para construir um campo em amostra separada. Medições e captura em [docs/validation/ECS-003](../../docs/validation/ECS-003/README.md).
- **Limites:** ADR-0006 registra hierarquia avaliada e adiada, revisão explícita, oito campos simultâneos e dispersão com sobreposição residual. Build de player, GPU timing e outras plataformas NOT_RUN.
- **Git:** main/b207bd7, uma revisão à frente de origin/main. Alterações ECS-003 locais, sem commit. PASS `git diff --check` e auditoria de GUIDs. Nenhum critério pendente. Aguardar autorização antes de iniciar outra tarefa.
