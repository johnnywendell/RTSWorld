# TASK: ECS-008 — Buildings & Construction

**Arquivo:** `tasks/completed/ECS-008-buildings-construction.md`

**Status:** DONE  
**Prioridade:** Alta  
**Dependências:** ECS-005, ECS-007, INTEGRATION-002

## Objetivo

Implementar o primeiro sistema de construções do RTSWorld, permitindo posicionar estruturas, consumir recursos, utilizar aldeões construtores e integrar edificações à economia e à navegação.

## Contexto

A ECS-005 utiliza um depósito lógico sem representação visual de construção. Os trabalhadores entregam madeira e pedra em um ponto do mapa.

Esta tarefa deverá transformar esse conceito em uma estrutura real dentro da simulação.

Não implementar arquitetura gráfica definitiva ou sistemas complexos de produção.

## Requisitos funcionais

### 1. Definição de construções

Criar uma estrutura de dados extensível para tipos de construções.

Cada definição poderá incluir:

- Identificador do tipo.
- Nome.
- Dimensões.
- Área ocupada.
- Custos em recursos.
- Tempo ou trabalho necessário.
- Vida máxima.
- Categoria.
- Função econômica.
- Regras de posicionamento.

Implementar inicialmente:

**Depósito:** recebe madeira e pedra, substituindo o ponto lógico invisível.

**Centro principal:** estrutura inicial de referência para a facção, com representação simplificada.

**Construção genérica:** usada para testar o processo de construção.

Não implementar recrutamento, tecnologias ou produção militar nesta tarefa.

### 2. Posicionamento

Permitir selecionar uma construção e visualizar uma prévia antes de posicioná-la.

A prévia deverá indicar:

- Posição.
- Rotação, se suportada.
- Área ocupada.
- Posicionamento válido ou inválido.
- Conflitos com água.
- Declividade.
- Árvores.
- Rochas.
- Outras construções.
- Limites de navegação.

O posicionamento deverá utilizar dados lógicos do mundo, não apenas colisores visuais.

### 3. Estado da construção

Implementar estados:

- Planned.
- UnderConstruction.
- Completed.
- Destroyed ou equivalente.

Adaptar à arquitetura existente.

Uma construção incompleta não deve funcionar automaticamente como depósito completo.

### 4. Custos

Integrar com o estoque econômico ECS-005.

- Verificar disponibilidade de recursos.
- Impedir construção sem recursos suficientes.
- Debitar custos de forma consistente.
- Impedir consumo duplicado.
- Definir política de cancelamento e reembolso.
- Preservar integridade do estoque.

### 5. Trabalhadores construtores

Permitir selecionar aldeões e ordenar construção.

Fluxo:

1. Jogador posiciona a estrutura.
2. Sistema cria uma entidade de construção planejada.
3. Aldeão recebe ordem Build.
4. Navega até posição de interação.
5. Executa trabalho em passo fixo.
6. Progresso aumenta.
7. Construção é concluída.
8. Funcionalidade da estrutura é ativada.

Permitir múltiplos trabalhadores na mesma construção com regras claras de produtividade.

### 6. Navegação e ocupação

Integrar construções à INTEGRATION-002.

- Construções bloqueiam áreas apropriadas.
- Unidades não atravessam paredes ou volumes sólidos.
- Posições de interação permanecem acessíveis.
- Flow Fields afetados são invalidados.
- Remoção da construção libera ocupação.
- Não invalidar todo o mundo sem necessidade.

### 7. Depósitos reais

Integrar a entrega econômica ao depósito construído.

- Aldeões identificam depósitos disponíveis.
- Escolhem posições de entrega acessíveis.
- Entregam recursos.
- Atualizam estoque.
- Tratam depósito destruído ou inacessível.
- Preservam carga em trânsito conforme política definida.

Migrar o depósito lógico da EconomyDemo sem quebrar os testes existentes.

### 8. Construções e combate

Integrar com ECS-006 de forma limitada.

- Construções podem possuir vida.
- Definir se são alvos atacáveis.
- Permitir destruição quando suportado.
- Atualizar navegação após destruição.
- Tratar referências inválidas.

Não implementar armas defensivas, torres automáticas ou cercos nesta tarefa.

### 9. Apresentação visual

Criar modelos low-poly provisórios simples.

O depósito deve ser visualmente identificável.

Mostrar:

- Construção planejada.
- Construção em andamento.
- Progresso.
- Construção concluída.
- Vida, quando relevante.

Não implementar modelos definitivos ou efeitos complexos.

## Cena demonstrativa

Criar:

`Assets/_Project/Scenes/ConstructionDemo.unity`

A cena deverá permitir:

- Coletar madeira e pedra.
- Selecionar uma construção.
- Posicioná-la.
- Verificar custos.
- Ordenar aldeões construtores.
- Acompanhar progresso.
- Concluir um depósito.
- Entregar recursos ao depósito.
- Movimentar unidades ao redor das estruturas.

Preservar as cenas anteriores.

## Testes

Criar testes Edit Mode e Play Mode para:

- Definições de construções.
- Posicionamento válido.
- Posicionamento sobre água.
- Declividade excessiva.
- Colisão com obstáculos.
- Colisão com construções.
- Custos.
- Estoque insuficiente.
- Cancelamento.
- Reembolso.
- Construção por um aldeão.
- Construção por múltiplos aldeões.
- Progresso em passo fixo.
- Conclusão.
- Ativação de depósito.
- Entrega de recursos.
- Destruição.
- Atualização da navegação.
- Streaming.
- Consistência econômica.

## Desempenho

Medir:

- Validação de posicionamento.
- Atualização de construções.
- Atualização de progresso.
- Alterações de obstáculos.
- Invalidação de navegação.
- Interação com trabalhadores.
- Impacto sobre o FPS.

Evitar atualizações caras por construção a cada frame.

## Critérios de aceite

- [x] Definições de construções.
- [x] Posicionamento interativo.
- [x] Validação de terreno.
- [x] Custos integrados à economia.
- [x] Ordem Build.
- [x] Construção progressiva.
- [x] Múltiplos construtores.
- [x] Depósito visual funcional.
- [x] Entrega econômica integrada.
- [x] Obstáculos de navegação atualizados.
- [x] Estados de construção consistentes.
- [x] Integração básica com combate.
- [x] Streaming preservado.
- [x] Cena demonstrativa funcional.
- [x] Testes Edit Mode aprovados.
- [x] Testes Play Mode aprovados.
- [x] Compilação sem erros.
- [x] Console sem erros relacionados.
- [x] Profiling documentado.
- [x] Documentação e handoff atualizados.

## Execução e encerramento

Ao receber autorização:

1. Ler os documentos obrigatórios do projeto.
2. Verificar se ECS-007 está concluída.
3. Verificar o estado do Git.
4. Mover a tarefa para `tasks/in_progress/`.
5. Atualizar o planejamento.
6. Implementar por checkpoints.
7. Validar com Unity MCP.
8. Registrar evidências em `docs/validation/ECS-008/README.md`.
9. Atualizar arquitetura, estado atual e handoff.
10. Mover para `tasks/completed/` somente após validação.

Não criar commits sem autorização.

Não iniciar ECS-009 ou outras tarefas.

Em caso de bloqueio, registrar e interromper.
## Checkpoint inicial

- IN_PROGRESS. ECS-005, ECS-007 e INTEGRATION-002 registradas como DONE. Código de economia, grade e movimento presente. Unity MCP identificou RTSWorld 6000.6.9f1, CombatDemo fora de Play e zero erros no Console.
- Git main/c690c43, sincronizado com origin/main. Alterações locais da ECS-007 em simulação, apresentação, testes e documentação, mais backlog sem rastreamento.
- Plano: integrar ocupação à grade, implementar estado e trabalho fixo, migrar a entrega, adicionar apresentação e cena, validar com testes e profiling. Compilação e testes ECS-008 NOT_RUN.
- Próximo passo: implementar ocupação lógica e fluxo de construção.

## Checkpoint de simulação

- Implementados Building, definições, validação lógica, custo no posicionamento, reembolso integral antes do primeiro trabalho, dano e remoção da ocupação, ordem Build e progresso aditivo no passo fixo.
- Arquivos: Construction.cs, NavigationGrid.cs, UnitSession.cs e UnitOrders.cs. main/c690c43 com alterações locais da ECS-007 preservadas. git diff --check PASS. Compilação e testes NOT_RUN.
- Próximo passo: integrar depósitos físicos, apresentação e validação.

## Checkpoint de integração

- Depósitos físicos em EconomyDemo e ConstructionDemo, busca de células acessíveis e estoque por proprietário. Cena ConstructionDemo com seleção, prévia, Build e progresso. Testes ConstructionTests e ConstructionDemoTests adicionados.
- PASS Edit Mode focado 2/2 na primeira versão. Play Mode focado 1/2, EconomyDemo passou e o teste novo falhou ao buscar ChunkManager no GameObject do controlador. Busca corrigida via raiz da cena. Testes expandidos de água, declividade e ativação do depósito aguardam reexecução.
- main/c690c43, sem commits. Alterações da ECS-007 preservadas. Próximo passo: compilação, suíte completa, inspeção da cena e medições.

## Checkpoint de validação

- PASS 262/262 Edit Mode e 17/17 Play Mode, incluindo EconomyDemo e streaming de construções. PASS 4/4 Edit Mode focado com retomada de entrega após destruição. Segunda medição focada em Play Mode PASS, com aquecimento e 120 frames por fase.
- Unity MCP verificou por mouse virtual prévia válida, posicionamento, débito e conclusão de um depósito na ConstructionDemo. Estoque e escolha inicial do tipo foram preparados em memória para a inspeção. Coleta e entrega reais verificadas nos testes.
- Observada sobreposição de texto do HUD herdado no primeiro frame capturado. Removidos os controles de multidão do painel de construção e incluída a instrução para Build. Próximo passo: confirmar compilação, reexecutar regressão final, atualizar captura e concluir o handoff.
- main/c690c43, sem commits. Alterações locais da ECS-007 preservadas. Evidências em docs/validation/ECS-008.

## Checkpoint final

- DONE. Implementados os três tipos de construção, posicionamento com prévia, custo, cancelamento, Build no passo fixo, depósitos físicos e ocupação lógica. A sessão expõe dano em estruturas. Attack do ECS-006 seleciona unidades. ConstructionDemo validada em Play com clique virtual e conclusão.
- Arquivos da tarefa: Construction.cs, NavigationGrid.cs, ResourceEconomy.cs, UnitOrders.cs, UnitSession.cs, UnitMovementSystem.cs, RtsInteractionDemo.cs, ConstructionDemo.unity e testes ConstructionTests e ConstructionDemoTests, todos com metadados de Unity quando novos. Arquitetura, estado, handoff e evidências atualizados.
- PASS compilação, 262/262 Edit Mode e 17/17 Play Mode, sem ignorados. Uma rodada com ConstructionDemo ativa falhou no profiling de CombatDemo e a reexecução com CombatDemo ativa passou. Console final com zero erros e avisos. Editor em CombatDemo, fora de Play, zero Worlds de unidades.
- NOT_RUN mouse físico, build de player e GPU timing. Orientação fixa. [Validação e condições do profiling](../../docs/validation/ECS-008/README.md)
- Git main/c690c43, sem commits. ECS-007 continua com alterações locais preservadas. Arquivos históricos regravados pelos testes foram repostos. Próximo passo: revisão do diff local. Nenhuma tarefa posterior iniciada.
