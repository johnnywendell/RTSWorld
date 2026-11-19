# Validação ECS-005

## Como jogar

Abra `Assets/_Project/Scenes/EconomyDemo.unity` e entre em Play. Selecione aldeões verdes. Clique com o botão direito sobre uma árvore ou pedra para coletar, ou sobre chão navegável para mover. O cubo ciano marca o depósito. Espaço interrompe o trabalho e mantém a carga na unidade. O painel exibe estoque, entregas e trabalhadores por fase. Os botões de população reiniciam o cenário.

[Estoque entregue](economy.png) · [Coleta em andamento](economy-working.png)

## Resultados

| Verificação | Estado | Evidência |
| --- | --- | --- |
| Compilação e Console | PASS | Unity 6000.6.9f1, sem falha. Console final com zero erros e avisos |
| Edit Mode | PASS | 233/233 em 41,0308505 s, zero falhas ou ignorados |
| Play Mode | PASS | 13/13 em 3,0377123 s, zero falhas ou ignorados |
| Conservação | PASS | Casos de 1, 10, 50 e 100 trabalhadores. Restante + carga + estoque igual à quantidade inicial a cada tick amostrado. Capacidade respeitada, entrega em múltiplos ciclos, saldo final e footprint liberado |
| Comandos e falhas | PASS | Stop, Move substituindo Gather ativo, carga preservada e perda contabilizada na destruição. Soldado rejeitado. Recurso desconectado e depósito bloqueado rejeitados antes de mudar ordem |
| Streaming e apresentação | PASS | Coleta parcial, descarregamento visual, entrega fora da câmera, reconstrução com um recurso a menos e menor contagem de vértices. Quantidade ficou zero e bloqueio liberado. Reinício explícito restaurou cenário |
| Input e HUD | PASS | Botão direito via Input System virtual sobre árvore e pedra. Até 50 aldeões ativos na seleção mista, 60 madeira e 80 pedra entregues e HUD inspecionado |
| Mouse físico, build/player, outras plataformas e GPU | NOT_RUN | Validação gráfica usou Linux Editor e input virtual |

O Unity MCP verificou a compilação, invocou três testes focados, executou EconomyDemo e capturou as imagens. As suítes completas foram executadas na cópia isolada em `Logs/ECS-005-validation`, com a saída extra do callback de performance mantida dentro do projeto. A tentativa intermediária de iniciar Edit Mode antes de Play Mode liberar a cópia retornou erro de lock. A repetição após a saída do processo foi aprovada.

## Profiling

Os testes Edit Mode usam seed 7, janela 48 × 48, um alvo procedural sintético, taxa 10 por segundo e quantidade total 21 ou 23 conforme o tipo. O tempo do harness inclui asserções, avanço fixo e sincronização. O pico do tick econômico mede o laço de trabalhadores, inclusive transições que possam solicitar campos.

| Trabalhadores | Ticks | Harness (ms) | Pico econômico (ms/tick) |
| ---: | ---: | ---: | ---: |
| 1 | 522 | 10,1937 | 1,56200 |
| 10 | 247 | 6,6042 | 1,03170 |
| 50 | 191 | 18,5451 | 1,08760 |
| 100 | 131 | 21,5297 | 1,11570 |

Na EconomyDemo com 50 trabalhadores, a média dos ticks econômicos ativos foi 0,20420 ms para madeira e 0,09130 ms para pedra, com picos de 13,57290 e 11,38490 ms. Dois grupos simultâneos de 25 entregaram os dois estoques em 288 passos. Na amostra, preparar cada Gather custou 41,01350 e 33,39430 ms, incluindo construção e aplicação de campos. Os valores usam Intel Core i5-14600KF no Editor e incluem picos de preparação. Coleta, entrega e consulta foram cronometradas no mesmo tick econômico. O custo de atualização do HUD não foi temporizado isoladamente, e ele atualiza textos a cada 0,25 s. [Medições na cena](scene-profile.txt)

## Limites

Economia em memória na janela regional, com depósito único e até oito campos simultâneos. Os recursos esgotados permanecem ausentes após recarregar a mesh. Trocar seed ou população reinicia deliberadamente a sessão. Ouro continua decorativo. As aproximações compartilham até três células e admitem sobreposição em grupos grandes. Não há persistência externa, regeneração ou contabilidade de carga removida fora da API DestroyBatch. [ADR-0010](../../decisions/ADR-0010-gathering-and-storage.md)
