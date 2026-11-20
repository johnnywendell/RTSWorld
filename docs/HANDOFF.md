# Current Handoff

**Tarefa:** VISUAL-002 — DONE
**Arquivo:** tasks/completed/VISUAL-002-environment-art-biome-overhaul.md
**Escritor ativo:** nenhum

- Biblioteca Unity própria, 30 variantes e terreno/água por bioma. Recursos, terreno lógico, navegação e persistência preservados. VisualStyleDemo e EnvironmentBenchmarkDemo usam o mesmo streaming e instancing.
- PASS compilação, 302/302 Edit Mode e 28/28 Play Mode. Console final sem erros e avisos, VisualStyleDemo ativa, cena limpa e Play desligado.
- Editor e Linux Player Development executaram 17 cenários cada. Em 5.000 unidades, densidade média com sombras, CPU de apresentação 1,909 ms no Editor e 1,437 ms no Player. GPU Frame Time e batches totais NOT_RUN. [Relatório, capturas e CSVs](validation/VISUAL-002/README.md)
- Git em main/c690c43, alinhada com origin/main. Saídas automáticas do build e dos testes históricos foram repostas. Alterações locais anteriores preservadas, sem commits. VISUAL-003 sem início.

## Checkpoint anterior — VISUAL-001A

**Tarefa:** VISUAL-001A — DONE
**Arquivo:** tasks/completed/VISUAL-001A-terrain-alignment-streaming-lod.md
**Escritor ativo:** nenhum

- Fundação nivelada amostra os extremos de todo o footprint. Vegetação e rochas usam os placements residentes em três LODs com histerese. A câmera retém a última vista coberta enquanto prepara a próxima.
- PASS 300/300 Edit Mode, 27/27 Play Mode e compilação. Console final com zero erros e avisos. VisualStyleDemo selecionada, cena limpa e Play desligado.
- Instancing padrão com 5.000 unidades em 1,928 ms a 2560 × 1440. Triângulos +22% em relação à VISUAL-001. Salto da câmera pronto em 127,2 ms, com pico de Step de 7,164 ms. Player, total de batches e GPU Frame Time NOT_RUN. [Relatório, CSV e capturas](validation/VISUAL-001A/README.md)
- Git main/c690c43 alinhada com origin/main. Alterações anteriores preservadas. Relatórios ECS-002 regenerados pelos testes foram repostos. Nenhum commit ou início de VISUAL-002.

## Checkpoint anterior — VISUAL-001

**Tarefa:** VISUAL-001 — DONE
**Arquivo:** tasks/completed/VISUAL-001-art-direction-rendering-architecture.md
**Escritor ativo:** nenhum

- VisualStyleDemo jogável com protótipos medievais e Idle/Walk. Instancing é o backend principal, com MeshRenderer e blocos como comparações. Navegação e autoridade permanecem no ECS.
- PASS 296/296 Edit Mode, 26/26 Play Mode e compilação. Console sem erros e avisos. Editor na VisualStyleDemo, cena limpa e Play desligado.
- Em 5.000 unidades, instancing 2,659 ms e 111 draw calls, MeshRenderer 4,177 ms e cerca de 3.008. Culling, LOD e sombras ligados levaram o instancing a 1,960 ms com 1.485 visíveis. Entities Graphics, GPU Frame Time e batches totais NOT_RUN. [Relatório, CSV e capturas](validation/VISUAL-001/README.md)
- Git main/c690c43 sincronizado com origin/main. Alterações locais anteriores e da VISUAL-001 preservadas, sem commits. Relatórios ECS-002 regravados pelos testes foram repostos.
- Próximo passo disponível é a revisão da cena e do diff local. Nenhuma tarefa posterior iniciada.

## Checkpoint anterior — NAV-002

**Tarefa:** NAV-002 — DONE
**Arquivo:** tasks/completed/NAV-002-crowd-deadlock-path-quality.md
**Escritor ativo:** nenhum

- Recuperação lateral, entrada antecipada no trecho final de formação e precedência em faixas restritas. Com bolsões e destinos idênticos, a sobreposição foi de quatro para zero ticks. Sem espaço para ceder a espera é explícita. Saídas ocupadas, corredores longos e superlotação continuam limitando o avanço.
- 12 roteiros da CrowdRecoveryDemo verificados em Play. PASS 296/296 Edit Mode, 24/24 Play Mode, compilação e Console sem erros ou avisos. Editor em CrowdRecoveryDemo fora de Play, cena limpa.
- Profiling pareado em 100, 500, 1.000 e 5.000. No ensaio integrado de 5.000, tick médio 0,2386 → 0,2291 ms, chegadas 22 → 11 na janela medida. [Resultados, limites e capturas](validation/NAV-002/README.md)
- Git main/c690c43, alterações anteriores preservadas, sem commits. Nenhuma tarefa posterior iniciada.

## Checkpoint anterior — ECS-010

**Tarefa:** ECS-010 — DONE
**Arquivo:** tasks/completed/ECS-010-simulation-persistence-save-load.md
**Escritor ativo:** nenhum

- PersistenceDemo salva uma sessão lógica versionada e troca o World somente depois de construir o candidato. Ordens, economia, combate, IA e obras são restaurados por IDs estáveis e o streaming visual é reconstruído.
- PASS compilação, 280/280 Edit Mode e 21/21 Play Mode. A primeira rodada completa de Play Mode falhou no profiling histórico do combate e a repetição integral passou. Console sem erros ou avisos, Editor fora de Play em CombatDemo.
- [Validação ECS-010](validation/ECS-010/README.md). Em 5.000 unidades, a captura ocupou 10,105 ms e a reconstrução 96,104 ms no thread principal. Player e mouse físico NOT_RUN.
- Git main/c690c43 sincronizado com origin/main. Alterações anteriores preservadas, sem commits. Próximo passo da ECS-010 é a revisão do diff local. Nenhuma tarefa posterior iniciada.

## Checkpoint anterior — ECS-009

**Tarefa:** ECS-009 — DONE
**Arquivo:** tasks/completed/ECS-009-unit-ai-autonomous-behaviors.md
**Escritor ativo:** nenhum

- UnitAIDemo usa trabalhadores autônomos, depósitos físicos, obra inicial e guardas. A decisão escalonada é da sessão ECS. Ordens explícitas têm prioridade. Stop exige reativação explícita ou nova ordem válida.
- PASS compilação, 273/273 Edit Mode e 19/19 Play Mode. Inspeção MCP registrou 15 entregas, uma obra concluída e Stop persistente em 50 unidades. Editor em CombatDemo, fora de Play, Console sem erros ou avisos.
- [Validação e profiling](validation/ECS-009/README.md). Em 5.000 unidades, 2.500 aldeões ativos registraram 33 entregas em 12 s simulados. O tick sintético médio foi de 1,571 ms. Mouse físico, build de player, GPU timing e bytes nativos NOT_RUN.
- Git main/c690c43 sincronizado com origin/main. Alterações anteriores e da ECS-009 preservadas, sem commits. Relatórios históricos repostos. Próximo passo desta tarefa: revisão do diff local. Nenhuma tarefa posterior iniciada.

## Checkpoint anterior — ECS-008

**Tarefa:** ECS-008 — DONE
**Arquivo:** tasks/completed/ECS-008-buildings-construction.md
**Escritor ativo:** nenhum

- ConstructionDemo e EconomyDemo usam ocupação lógica e depósitos físicos. Build progride no passo fixo com produtividade aditiva. Cancelamento antes do trabalho reembolsa, destruição libera células e entregadores preservam carga para retomada.
- PASS Unity 6000.6.9f1, compilação, 262/262 Edit Mode, 17/17 Play Mode e posicionamento via mouse virtual. Uma rodada de Play Mode com ConstructionDemo ativa falhou no teste de profiling de CombatDemo, que passou na nova execução após restaurar CombatDemo. Console final com zero erros e avisos. Editor fora de Play e zero Worlds de unidades.
- [Validação, captura e medições](validation/ECS-008/README.md). Attack segue restrito a unidades. Mouse físico, build de player e GPU timing NOT_RUN.
- Git main/c690c43 sincronizado com origin/main, sem commits. Alterações locais da ECS-007 preservadas e relatórios antigos regravados pelos testes restaurados. Próximo passo desta tarefa: revisão do diff local. Nenhuma tarefa posterior iniciada.

## Checkpoint anterior — ECS-007

**Tarefa:** ECS-007 — DONE
**Arquivo:** tasks/completed/ECS-007-local-avoidance-crowd-movement.md
**Escritor ativo:** nenhum

- Índice espacial e steering Burst no passo fixo, raios configuráveis, movimento e recuperação local, formação preservada, integração com Attack e Gather, metrificação do atlas e cena CrowdMovementDemo. As sessões antigas mantêm crowd desligado.
- PASS Unity 6000.6.9f1, 258/258 Edit Mode e 16/16 Play Mode. Inspeção MCP identificou rota com nove células estreitas e execução real de fluxos opostos. Play Mode verificou unload visual com população lógica preservada. Console com zero erros e um aviso de WebSocket do MCP. Editor em CombatDemo, Play desligado, zero Worlds de unidades.
- [Medições e reprodução](validation/ECS-007/README.md). Em 5.000 unidades e dois destinos de uma célula, sobreposição média caiu 7,3% e o tick com crowd levou 0,3059 ms. Concentração extrema mantém congestionamento. Build de player, mouse físico, GPU timing e memória nativa NOT_RUN.
- Git main/c690c43 sincronizado com origin/main. Alterações locais da ECS-007, sem commits. Artefatos antigos regravados durante os testes foram repostos. ECS-008 a ECS-010 preservadas em pending.
- Próximo passo da ECS-007: revisão da implementação e dos resultados no diff local. Nenhuma tarefa posterior iniciada.
