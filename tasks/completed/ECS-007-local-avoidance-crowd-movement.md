# TASK: ECS-007 — Local Avoidance & Crowd Movement

**Arquivo:** `tasks/completed/ECS-007-local-avoidance-crowd-movement.md`

**Status:** DONE  
**Prioridade:** Alta  
**Dependências:** ECS-003, ECS-004, INTEGRATION-002, ECS-006

## Objetivo

Implementar movimentação dinâmica de multidões para impedir sobreposição excessiva entre unidades, reduzir congestionamentos e melhorar o posicionamento durante deslocamentos e combates, preservando a arquitetura DOTS/ECS, Jobs, Burst e Flow Fields existentes.

## Contexto

A ECS-006 identificou que grupos de unidades podem se sobrepor em gargalos e durante ataques. A navegação atual já considera água, declividade e obstáculos ambientais estáticos, mas ainda necessita de tratamento local para obstáculos móveis.

Com 5.000 unidades, o sistema deve continuar escalável.

## Requisitos funcionais

### 1. Índice espacial de unidades

Implementar ou reutilizar uma estrutura espacial eficiente, preferencialmente compatível com NativeCollections e Jobs.

- Consultas por proximidade.
- Divisão espacial por células ou buckets.
- Atualização eficiente de posições.
- Suporte a unidades em movimento e paradas.
- Sem busca global O(N²).
- Suporte a coordenadas negativas e fronteiras de chunks.

### 2. Separação entre unidades

Cada unidade deverá possuir raio lógico de ocupação configurável.

Implementar forças ou correções locais de separação que:

- Reduzam sobreposição.
- Respeitem raios de unidades.
- Evitem oscilações.
- Não produzam deslocamentos excessivos.
- Respeitem obstáculos ambientais.
- Preservem destinos e comandos.

Não utilizar Rigidbody individual como solução padrão.

### 3. Evitação dinâmica

Integrar um algoritmo de evitação local compatível com o movimento ECS existente.

Investigar técnicas como steering, velocity obstacles ou abordagens simplificadas de avoidance.

Escolher a solução com base em custo computacional, previsibilidade e integração real com o projeto.

Não implementar um segundo sistema global de pathfinding.

### 4. Congestionamento

Tratar:

- Passagens estreitas.
- Grupos em direções opostas.
- Unidades paradas bloqueando passagem.
- Concentração em destinos.
- Grupos de centenas de unidades.
- Situações temporárias sem caminho local livre.

Implementar mecanismos de recuperação para reduzir travamentos permanentes, sem garantir artificialmente passagem por obstáculos impossíveis.

### 5. Formação e distribuição de destinos

Melhorar a distribuição local das unidades em comandos de grupo.

Evitar que todas tentem ocupar o mesmo ponto.

Preservar os destinos distribuídos existentes na ECS-004.

### 6. Integração com combate

Durante Attack:

- Distribuir soldados ao redor do alvo.
- Respeitar alcance de ataque.
- Evitar empilhamento excessivo.
- Preservar cadência e dano.
- Permitir aproximação de múltiplos atacantes.
- Tratar alvos móveis.
- Tratar mortes e liberação de espaço.

### 7. Integração com economia

Durante Gather:

- Permitir múltiplos trabalhadores próximos ao recurso.
- Reduzir congestionamento em árvores, rochas e depósitos.
- Preservar posições de interação.
- Não duplicar coleta ou entrega.
- Preservar inventários e ordens.

### 8. Investigação do atlas de Flow Fields

A ECS-006 registrou disputa por oito campos do atlas.

Investigar:

- Taxa de acertos e substituições.
- Custo de reconstrução.
- Comportamento com muitos destinos.
- Impacto sobre perseguições.
- Possibilidade de cache regional mais eficiente.

Não alterar arbitrariamente a capacidade do atlas.

Implementar ajustes apenas quando justificados por medições. Caso uma reformulação maior seja necessária, registrar uma tarefa futura.

## Requisitos técnicos

- Manter movimento em passo fixo.
- Preservar Jobs/Burst quando aplicável.
- Evitar alocações gerenciadas por unidade a cada frame.
- Evitar O(N²).
- Não transformar unidades em obstáculos permanentes da navegação global.
- Preservar determinismo na medida suportada pela arquitetura atual.
- Preservar streaming e estados ECS.
- Evitar dependência dos GameObjects visuais.

## Demonstração

Criar:

`Assets/_Project/Scenes/CrowdMovementDemo.unity`

A cena deverá demonstrar:

- 100 unidades em deslocamento.
- 500 unidades.
- 1.000 unidades.
- 5.000 unidades.
- Passagens estreitas.
- Fluxos opostos.
- Grupos convergindo para destinos.
- Ataques com múltiplos soldados.
- Trabalhadores coletando recursos.

Adicionar diagnósticos opcionais para visualizar raios, vizinhos, velocidades e congestionamentos.

## Testes

Criar testes Edit Mode e Play Mode para:

- Separação de duas unidades.
- Separação de grupos.
- Unidades com raios diferentes.
- Fluxos opostos.
- Gargalos.
- Destinos compartilhados.
- Unidades paradas.
- Alvos móveis.
- Combate corpo a corpo.
- Coleta simultânea.
- Água e obstáculos.
- Streaming.
- Coordenadas negativas.
- Ausência de NaN e velocidades inválidas.
- Ausência de regressões nos comandos.

## Desempenho

Medir com 100, 500, 1.000 e 5.000 unidades:

- FPS.
- Tempo de atualização espacial.
- Tempo de avoidance.
- Tempo de navegação.
- Custo de Jobs.
- Alocações.
- Contagem de sobreposições.
- Unidades congestionadas.
- Tempo para alcançar destinos.

Comparar com a implementação anterior quando possível.

Registrar condições reais do benchmark.

## Critérios de aceite

- [x] Índice espacial funcional.
- [x] Separação entre unidades funcional.
- [x] Evitação dinâmica funcional.
- [x] Redução mensurável de sobreposição.
- [x] Tratamento de congestionamentos.
- [x] Distribuição de destinos preservada.
- [x] Integração com combate.
- [x] Integração com economia.
- [x] Navegação global preservada.
- [x] Streaming preservado.
- [x] Investigação do atlas documentada.
- [x] Cena demonstrativa funcional.
- [x] Testes Edit Mode aprovados.
- [x] Testes Play Mode aprovados.
- [x] Compilação sem erros.
- [x] Console sem erros relacionados.
- [x] Profiling documentado.
- [x] Documentação e handoff atualizados.

## Execução e encerramento

Ao receber autorização para executar esta tarefa:

1. Ler `AGENTS.md`, documentos de arquitetura, estado atual, handoff e ADRs.
2. Verificar as dependências.
3. Verificar `git status`.
4. Mover este arquivo de `tasks/pending/` para `tasks/in_progress/`.
5. Atualizar `tasks/ACTIVE.md`.
6. Implementar em checkpoints.
7. Validar via Unity MCP.
8. Registrar resultados em `docs/validation/ECS-007/README.md`.
9. Atualizar documentação e handoff.
10. Mover para `tasks/completed/` somente após cumprir os critérios obrigatórios.

Não criar commits sem autorização.

Não iniciar ECS-008 nem qualquer outra tarefa.

Se houver bloqueio, manter IN_PROGRESS, registrar o motivo e interromper.

## Checkpoint inicial — 2026-11-19

**Status atual:** IN_PROGRESS

- Dependências ECS-003, ECS-004, INTEGRATION-002 e ECS-006 registradas como DONE. Inspecionados arquitetura, ADRs e job de movimento. O atlas, a formação e a ordem fixa existentes são o ponto de integração.
- Git main/c690c43 sincronizado com origin/main, sem alterações rastreadas na entrada. ECS-007 a ECS-010 estavam sem rastreamento. Apenas ECS-007 foi movida para execução. O handoff anterior registrava um HEAD histórico.
- Unity MCP confirmou a raiz RTSWorld, Unity 6000.6.9f1, CombatDemo fora de Play e zero erros/avisos no Console. Compilação e testes da ECS-007 NOT_RUN.
- Plano: índice espacial e steering no passo fixo, validação de deslocamento na grade existente, integração com combate e economia, métricas do atlas, cena e testes.
- Próximo passo: inspecionar os pontos de configuração, publicação das células e atualização dos consumidores antes de editar a simulação.

## Checkpoint de implementação

- Implementados índice espacial nativo, steering Burst, raios por definição, consulta rasterizada de terreno, chegada econômica por célula e contadores do atlas. A opção de sessão permite medir com e sem crowd e mantém compatibilidade com cenas anteriores. CrowdMovementDemo habilita a opção e reaproveita combate, coleta e streaming.
- Alterações em UnitComponents, UnitSession, UnitMovementSystem, UnitOrders, UnitCombat, ResourceEconomy, RtsInteractionDemo e na nova cena CrowdMovementDemo. main/c690c43, sem commits, backlog restante preservado.
- Unity MCP solicitou compilação da primeira versão da simulação. Console retornou um aviso de WebSocket do MCP e nenhum erro C#. Validação da cena, testes e profiling NOT_RUN.
- Próximo passo: compilar a integração e rodar cenários com a evitação habilitada.

## Checkpoint de integração

- Primeira validação via Unity MCP: PASS 3/3 Edit Mode específicos e 1/1 Play Mode da cena. A suite Edit Mode inicial encontrou duas falhas de entrega com crowd habilitado (10 e 50 trabalhadores). A aproximação agora usa a área de 1,25 célula em torno da posição de interação, com linha navegável, e limita o desvio do trabalhador à rota do campo. Reexecução da regressão de economia PASS 9/9, incluindo as duas cargas.
- Diagnóstico de cena adicionado para raios, velocidade, vizinhança e congestionamento. Perfis sintético pareado e Play Mode adicionados aos testes. Resultados definitivos, streaming em Play e suítes completas pendentes.
- Git main/c690c43, sem commits. Arquivos relevantes listados no checkpoint anterior, mais CrowdMovementTests, CrowdMovementDemoTests e ResourceEconomyTests.
- Próximo passo: confirmar a compilação e repetir Edit Mode e Play Mode após os ajustes, incluindo profiling e Console.


## Checkpoint final — 2026-11-19

- Implementação concluída e verificada. Índice nativo, steering Burst, raios, recuperação local, formação, perseguição, interação econômica e diagnóstico da cena concluídos. Capacidade do atlas mantida em oito.
- Arquivos de simulação alterados: UnitComponents, UnitMovementSystem, UnitSession, UnitOrders, UnitCombat e ResourceEconomy. Apresentação: RtsInteractionDemo e CrowdMovementDemo com meta. Validação: CrowdMovementTests, CrowdMovementDemoTests e extensões aos testes de economia e ordens, além do relatório ECS-007.
- PASS compilação, 258/258 Edit Mode, 16/16 Play Mode, inspeção direta Unity MCP com fluxo em gargalo, streaming e profiling nas quatro cargas. Console final com zero erros e um aviso do WebSocket do MCP. O Editor voltou à CombatDemo, fora de Play e com zero Worlds de unidades.
- Limite verificado: dois destinos de uma célula para 5.000 agentes geraram 2.173 congestionados ao fim de 60 s de amostra. A sobreposição média caiu de 4.994,1 para 4.628,3 unidades. Formação mantém destinos distintos. Mouse físico, build de player, GPU timing e memória nativa NOT_RUN. [Relatório de validação](../../docs/validation/ECS-007/README.md)
- main/c690c43 sincronizado com origin/main, sem commits. Código, cena, testes e documentação da ECS-007 permanecem como alterações locais. Evidências antigas regravadas pelos testes ECS-002 foram repostas à versão da entrada. ECS-008 a ECS-010 continuam pendentes e intactas.
- Próximo passo da ECS-007: revisão das alterações locais e das medições. Nenhuma outra tarefa iniciada.
