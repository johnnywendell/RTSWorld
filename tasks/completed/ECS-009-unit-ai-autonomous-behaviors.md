# TASK: ECS-009 — Unit AI & Autonomous Behaviors

**Arquivo:** `tasks/completed/ECS-009-unit-ai-autonomous-behaviors.md`

**Status:** DONE  
**Prioridade:** Alta  
**Dependências:** ECS-005, ECS-006, ECS-007, ECS-008

## Objetivo

Implementar comportamentos autônomos básicos para unidades, permitindo que aldeões e soldados executem tarefas e reajam ao ambiente sem exigir comandos manuais constantes.

O sistema deverá ser compatível com milhares de unidades e respeitar as ordens explícitas do jogador.

## Contexto

Atualmente, as unidades dependem principalmente de comandos diretos:

- Move.
- Stop.
- Gather.
- Attack.
- Build, após ECS-008.

Precisamos introduzir uma camada de decisão para comportamentos automáticos.

Não implementar inteligência artificial estratégica completa nesta tarefa.

## Requisitos funcionais

### 1. Arquitetura de decisões

Projetar uma arquitetura ECS para comportamentos autônomos.

Avaliar máquinas de estados, utility AI simplificada ou outra abordagem adequada.

Separar:

- Percepção.
- Avaliação.
- Decisão.
- Execução.
- Navegação.
- Apresentação.

Evitar MonoBehaviour individual como controlador principal de IA.

### 2. Prioridade de comandos

Definir uma política clara:

**Comandos explícitos do jogador têm prioridade sobre comportamentos automáticos**, salvo situações excepcionais documentadas.

O sistema deve distinguir:

- Ordem manual.
- Comportamento automático.
- Estado de interrupção.
- Retomada de tarefa.
- Falha de execução.

Stop deve possuir semântica previsível, sem reativação imediata de uma ação automática cancelada.

### 3. Aldeões

Implementar comportamentos econômicos básicos, configuráveis:

- Procurar trabalho quando ociosos.
- Identificar recursos próximos.
- Escolher recursos acessíveis.
- Coletar.
- Transportar.
- Entregar.
- Retornar ao trabalho.
- Procurar outro recurso quando o anterior se esgotar.
- Auxiliar em construções quando explicitamente designados ou quando uma política automática habilitada permitir.

Não criar economia automática global complexa.

### 4. Soldados

Implementar comportamentos militares básicos:

- Idle.
- Guard.
- Attack.
- Chase.
- Return.
- HoldPosition.

Permitir reação a inimigos próximos quando o modo de comportamento permitir.

Não fazer soldados abandonarem indiscriminadamente suas posições.

### 5. Percepção

Criar consultas espaciais eficientes para:

- Inimigos próximos.
- Recursos disponíveis.
- Construções que necessitam de trabalhadores.
- Depósitos.
- Ameaças locais.

Reutilizar índices espaciais existentes.

Não realizar varreduras globais por unidade a cada frame.

### 6. Retorno e retomada

Implementar regras de retomada:

- Soldado persegue alvo dentro de limite configurável.
- Se perder o alvo, retorna à posição ou tarefa anterior.
- Aldeão procura recurso alternativo quando necessário.
- Ordens canceladas não são retomadas indevidamente.
- Morte ou remoção de alvos não deixa estados inválidos.

### 7. Distribuição de trabalho

Implementar uma versão simples de distribuição de tarefas para trabalhadores ociosos.

Evitar que todos escolham o mesmo recurso quando existem alternativas adequadas.

Considerar:

- Distância.
- Acessibilidade.
- Disponibilidade.
- Capacidade de interação.
- Quantidade restante.
- Tarefas em andamento.

Não implementar planejamento econômico estratégico nesta tarefa.

### 8. Integração com construções

Permitir que trabalhadores designados:

- Identifiquem construções pendentes.
- Naveguem até elas.
- Executem trabalho.
- Retomem tarefas anteriores conforme política definida.

Não implementar planejamento automático de novas construções.

### 9. Integração com combate

Garantir que:

- Ataques automáticos respeitem facções.
- Unidades não ataquem aliados.
- Soldados respeitem limites de perseguição.
- Alvos mortos sejam descartados.
- Comandos manuais continuem prioritários.
- Economia permaneça consistente.

### 10. Escalabilidade

Atualizar decisões em frequências adequadas.

Não é necessário executar toda a lógica de percepção e decisão em todos os frames.

Avaliar:

- Atualizações escalonadas.
- Frequência configurável.
- Jobs/Burst.
- Cache de decisões.
- Eventos de mudança de estado.
- Consultas espaciais.

Evitar comportamento O(N²).

## Cena demonstrativa

Criar:

`Assets/_Project/Scenes/UnitAIDemo.unity`

A cena deverá demonstrar:

- Aldeões ociosos.
- Recursos disponíveis.
- Depósitos.
- Construções pendentes.
- Soldados em guarda.
- Inimigos próximos.
- Reação a ameaças.
- Retomada de tarefas.
- Intervenção manual do jogador.

Adicionar indicadores opcionais de estado de IA.

## Testes

Criar testes para:

- Prioridade de comandos.
- Stop.
- Trabalhador ocioso.
- Escolha de recurso.
- Recurso esgotado.
- Recurso inacessível.
- Depósito indisponível.
- Construção pendente.
- Trabalho concluído.
- Soldado em guarda.
- Detecção de inimigo.
- Limite de perseguição.
- Retorno.
- Alvo morto.
- Interrupção manual.
- Retomada.
- Streaming.
- Ausência de ciclos infinitos de decisão.
- Escalabilidade.

## Desempenho

Medir com 100, 500, 1.000 e 5.000 unidades, distinguindo unidades com IA ativa de unidades apenas presentes.

Registrar:

- Tempo de percepção.
- Tempo de decisão.
- Tempo de execução.
- Consultas espaciais.
- Alocações.
- FPS.
- Frequência de reavaliações.
- Taxa de tarefas concluídas.

## Critérios de aceite

- [x] Arquitetura de IA documentada.
- [x] Prioridade de comandos definida.
- [x] IA básica de aldeões.
- [x] IA básica de soldados.
- [x] Percepção espacial eficiente.
- [x] Retomada de tarefas.
- [x] Tratamento de falhas.
- [x] Integração com economia.
- [x] Integração com construções.
- [x] Integração com combate.
- [x] Navegação preservada.
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

1. Ler os documentos obrigatórios.
2. Verificar se ECS-008 está concluída.
3. Verificar `git status`.
4. Mover para `tasks/in_progress/`.
5. Registrar a tarefa ativa.
6. Implementar por checkpoints.
7. Validar via Unity MCP.
8. Registrar evidências em `docs/validation/ECS-009/README.md`.
9. Atualizar documentação e handoff.
10. Mover para `tasks/completed/` somente após validação.

Não criar commits sem autorização.

Não iniciar ECS-010 nem outras tarefas.

Em caso de bloqueio, registrar e interromper.
## Checkpoint inicial

- Git main/c690c43 sincronizado com origin/main. Árvore já modificada pelas ECS-007 e ECS-008, incluindo código, cenas, testes e documentação. Preservar esse trabalho.
- ECS-005 a ECS-008 constam como DONE. Confirmados no código Gather, Attack, crowd e Build com depósitos físicos. Console inicial via MCP sem erros ou avisos.
- Plano: estado de autonomia por entidade; decisões escalonadas no passo fixo; consultas locais em coordenadas lógicas; execução pelos sistemas existentes; cena, regressões e profiling.
- Implementação parcial: nenhuma. Compilação, testes e profiling da ECS-009: NOT_RUN.
- Próximo passo: integrar estado e precedência das ordens aos pontos de entrada existentes.

## Checkpoint de integração

- Camada ECS com adesão por entidade, prioridade manual, Stop persistente, trabalho econômico, construção opcional e guarda com retorno. Integração com a cena UnitAIDemo. Entrega e construção usam os executores existentes.
- Arquivos desta etapa: UnitAutonomy.cs, UnitSession.cs, UnitOrders.cs, UnitCombat.cs, ResourceEconomy.cs, Construction.cs, EnvironmentalObstacles.cs, RtsInteractionDemo.cs, UnitAIDemo.unity, UnitAutonomyTests.cs e UnitAIDemoTests.cs, com metadados Unity.
- PASS na primeira versão: 10/10 Edit Mode focado e 1/1 Play Mode focado. A segunda versão inclui fallback de recurso inacessível, índices escalonados, cenário de profiling renderizado e contadores de conclusão. Console sem erros ou avisos após refresh.
- Suíte Edit Mode completa em execução. Play Mode completo e medições da segunda versão aguardam execução. Git main/c690c43, alterações locais anteriores e da ECS-009, sem commits.
- Próximo passo: verificar suítes, confirmar medições nas quatro populações e inspecionar a cena no Unity.


## Checkpoint final

- Implementação concluída em UnitAutonomy e UnitAIDemo. Autoridade de decisão e execução pertence à sessão. Guard e HoldPosition respeitam facções e âncora. Stop suspende autonomia e preserva carga. Trabalhadores usam recursos, depósitos e obras existentes e voltam à distribuição local após falha ou término.
- PASS compilação no Unity 6000.6.9f1, 273/273 Edit Mode e 19/19 Play Mode. PASS inspeção MCP da cena, com 15 entregas, uma obra, reação em combate e 50 unidades em Stop persistente. Console final sem erros ou avisos.
- PASS profiling em 100, 500, 1.000 e 5.000 unidades. No ensaio sintético com 2.500 aldeões autônomos entre 5.000 presentes, 1,571 ms por tick e 33 entregas em 12 s simulados. No ensaio renderizado de 5.000 unidades, 225,23 FPS com IA em metade da população e 241,86 FPS com IA desligada. [Resultados e limites](../../docs/validation/ECS-009/README.md)
- Arquivos: UnitAutonomy.cs, UnitSession.cs, UnitOrders.cs, UnitCombat.cs, ResourceEconomy.cs, Construction.cs, EnvironmentalObstacles.cs, RtsInteractionDemo.cs, UnitAIDemo.unity, UnitAutonomyTests.cs e UnitAIDemoTests.cs, incluindo metadados novos. Arquitetura, estado, evidências, tarefa ativa e handoff atualizados.
- Limites conhecidos: representante por célula e facção no índice militar, oito campos simultâneos e concorrência por acessos. Mouse físico, build de player, GPU timing e bytes nativos NOT_RUN.
- Git main/c690c43 sincronizado com origin/main. Alterações locais da ECS-007, ECS-008 e ECS-009, sem commits. Relatórios históricos restaurados. Editor fora de Play, com CombatDemo restaurada.
- Próximo passo desta tarefa: revisão do diff local da ECS-009. Nenhuma tarefa posterior iniciada.
