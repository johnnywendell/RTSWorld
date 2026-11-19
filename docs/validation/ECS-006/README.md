# Validação da ECS-006

## Reproduzir na Unity

1. Abra `Assets/_Project/Scenes/CombatDemo.unity` e entre em Play Mode. O cenário usa seed 7, terreno v1, ambiente v4 e janela lógica de 192 × 192 células.
2. Selecione um soldado laranja com clique ou arraste. Shift adiciona à seleção e Ctrl alterna. Clique com o botão direito em um soldado roxo visível. Observe a marca rosa sob o alvo, a aproximação, o indicador amarelo de ataque e a barra de vida, que fica vermelha ao sofrer dano. A unidade e sua indicação desaparecem na morte.
3. Pressione Espaço durante a aproximação e observe a parada. Clique no terreno para Move ou em outro inimigo para Attack. Selecione um aldeão verde e clique com o botão direito sobre uma árvore ou pedra para Gather. O painel de economia lista coleta, transporte e estoque.
4. Use WASD e scroll para deslocar e aproximar a câmera. Aproxime os grupos pela parte navegável perto da água e dos recursos. Mova a câmera para fora da região enquanto a batalha ocorre e volte para ver o resultado.
5. Os botões 100, 500, 1.000 e 5.000 reiniciam a população e o estado econômico. Em Test Runner, execute `CombatDemoTests.ProfilesIntegratedBattlesAtFourPopulations` em Play Mode para emitir um perfil com ordens recíprocas de grupo. Execute `UnitCombatTests.ProfilesSimultaneousLethalStrikesAgainstDistinctTargets` em Edit Mode para uma rodada de golpes letais simultâneos entre pares.

## Método de medição

- Play Mode mede contagem inicial, ordens ativas no início, maior número no alcance entre ticks observados e baixas. A câmera produz carregamento após o frame 20. A janela termina após 120 ticks ou 720 frames. FPS é a contagem de frames sobre tempo real. As métricas por frame cobrem preparação/submissão da apresentação, atualização total da sessão com espera pelos jobs e etapa do streaming. As métricas de combate registram o último tick executado em cada frame que avançou a simulação.
- Edit Mode usa duelos pré-posicionados com alvos distintos, uma ordem por unidade e um tick letal. Isola a escala de aquisição, ataque, redução de vida e destruição em lote.
- O tempo de perseguição mede o Flow Field e a decisão de aproximação. O job Burst de movimento está incluído no tempo total da sessão. A separação do tempo de execução desse job e temporização de GPU requerem captura específica.

## Resultado final

Unity 6000.6.9f1, Linux Editor, MCP.

| Verificação | Estado | Evidência |
| --- | --- | --- |
| Compilação e Console | PASS | Nova compilação e execução das suítes. Consulta durante a inspeção visual retornou 0 erros e 0 avisos. |
| Edit Mode completo | PASS | 246/246, zero falhas e ignorados, 33,965 s. Job MCP `4e3b2496a0a8427faf1141a96472fc3f`. |
| Play Mode completo | PASS | 15/15, zero falhas e ignorados, 14,129 s. Job MCP `8419962ce4ac4a308f7d3060ad885def`. |
| Facções, vida e recarga | PASS | Alvos aliados, aldeão atacante, longe, morto e destruído cobertos. Troca de alvo preservou recarga. Dois golpes no mesmo passo mataram ambos os combatentes. |
| Comandos, seleção e morte | PASS | Mouse virtual em CombatDemo selecionou 1 soldado, aceitou 1 Attack e reduziu a vida de 100 para 90. O alvo foi destruído, sua referência foi limpa e Move e Stop posteriores foram aceitos. O teste Play confirmou Stop durante a perseguição e remoção da seleção após morte do atacante. |
| Economia | PASS | Clique de contexto com 50 unidades selecionadas acionou Gather nos 10 aldeões e entregou 10 madeira. O teste Edit confirmou interrupção ao sofrer dano, conservação da carga em vida e contabilização em `LostCargo` na morte. |
| Navegação e streaming | PASS | Teste Play descarregou o chunk visual do alvo durante a perseguição e verificou dano, morte e células navegáveis. No Editor em Play, uma sessão com a mesma grade procedural desviou de árvore em (-133, 329), pedra em (-235, 261) e água em (-70, 279). Todos os atacantes permaneceram em células navegáveis e cada alvo foi eliminado. |
| Gargalo e sobreposição | PASS | No desvio de árvore com 32 soldados, até 32 compartilharam uma célula e houve um pico de 32 centros distintos após a partida. O teste Edit de passagem de uma célula confirmou desvio e múltiplos centros finais. A distribuição existente permite sobreposição visual. |
| Apresentação | PASS | As capturas mostram as duas facções, o alvo, o ataque e o dano. A demonstração usa modelos e indicadores instanciados. [Visão geral](combat-demo.png) · [Combate em detalhe](combat-close-up.png) |
| Mouse físico, build e GPU | NOT_RUN | A validação de input usa mouse virtual. O relatório de FrameTiming não forneceu uma amostra de GPU utilizável. O roteiro no início permite conferir os controles com mouse físico. |

A primeira rodada Edit revelou uma expectativa de contagem que incluía o tick de inicialização do grupo fixo. O teste passou a inicializar o grupo antes de cronometrar a cadência. A primeira rodada Play revelou que a consulta de apresentação precisava declarar os componentes de vida e combate. A consulta foi corrigida e as duas suítes foram reexecutadas. Os arquivos de referência de ECS-002 regravados pelos testes legados foram repostos com o conteúdo encontrado em HEAD, pois estavam limpos no início.

## Profiling

| Total inicial | Com ordens | Pico no alcance | Baixas | FPS no Editor | Sessão ms/frame | Visual ms/frame | Streaming pico ms |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 100 | 90 | 5 | 0 | 334,65 | 0,261 | 0,040 | 4,391 |
| 500 | 450 | 9 | 1 | 391,77 | 0,125 | 0,100 | 6,862 |
| 1.000 | 900 | 10 | 1 | 388,48 | 0,101 | 0,190 | 9,508 |
| 5.000 | 4.500 | 8 | 1 | 260,90 | 0,298 | 0,879 | 6,520 |

A quantidade de ticks observados variou de 40 a 58. As duas forças concentraram ordens em dois alvos. A morte de um deles encerrou a respectiva ordem de grupo. Em 5.000 unidades, a aquisição custou em média 0,955 ms/tick, a perseguição 1,911 ms/tick com pico de 16,043 ms, a etapa de ataque 0,261 ms/tick, o dano 0,0004 ms/tick e a remoção 0,046 ms/tick com pico de 2,527 ms. [Registros Play Mode](play-mode-profile.txt)

| Unidades em alcance e removidas no mesmo tick | Aquisição ms | Perseguição ms | Ataque ms | Dano ms | Morte ms | Tick total ms |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 100 | 0,197 | 0,085 | 0,122 | 0,040 | 0,029 | 0,483 |
| 500 | 0,358 | 0,395 | 0,164 | 0,197 | 0,121 | 1,240 |
| 1.000 | 0,708 | 0,794 | 0,325 | 0,394 | 0,241 | 2,468 |
| 5.000 | 3,538 | 3,965 | 1,612 | 1,973 | 1,211 | 12,313 |

O teste sintético iniciou cada par a meia célula de distância. A janela de medição contém um único tick letal por população e exclui a emissão das ordens. Serve para observar o custo do lote, sem reproduzir uma batalha distribuída sustentada em terreno procedural. [Registros Edit Mode](edit-mode-profile.txt)

Unity Profiler foi habilitado pelo MCP para inspeção de CPU após o primeiro golpe no cenário de 100 unidades, com a sessão congelada para a captura. FrameTiming retornou 2,262 ms de CPU e 1,179 ms de main thread nessa amostra. O perfil quantitativo de batalha está nas duas tabelas acima. O Profiler foi desligado ao final.
