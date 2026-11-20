# Validação ECS-010

Unity 6000.6.9f1, Linux Editor, via Unity MCP. Branch `main`, HEAD `c690c43`. Compilação PASS, Edit Mode 280/280 e Play Mode 21/21, sem testes ignorados. Console final com zero erros ou avisos. Editor fora de Play, com CombatDemo ativa ao final.

- Edit Mode verificou rotas e IDs estáveis, fase do relógio fixo, recarga e substituição repetidas do mesmo arquivo, versão incompatível, cabeçalho inválido, truncamento, digest inválido, alvo ausente, vida, recarga do ataque e retomada efetiva do combate.
- Play Mode abriu a PersistenceDemo real. A simulação produziu entregas, carga em trânsito e uma obra em andamento. O teste salvou, substituiu a sessão com 500 unidades, carregou duas vezes, conferiu o descarte dos Worlds anteriores e comparou estoque, cargo, IDs, progresso e catálogo. Também verificou ocupação, filtros após unload/reload, rejeição de arquivo truncado sem perder a sessão ativa e retomada de entrega e trabalho de construção.
- A primeira execução completa de Play Mode registrou FAIL no teste histórico `CombatDemoTests.ProfilesIntegratedBattlesAtFourPopulations`, sem combatentes no alcance dentro da janela de medição. A repetição integral com a mesma revisão passou 21/21. Um teste inicial de precisão revelou arredondamento no serializador original e passou após adoção de Newtonsoft.Json. Os arquivos históricos de profiling da ECS-002, regravados pelas suítes, foram repostos e comparados byte a byte.
- Player, I/O em outras plataformas e mouse físico NOT_RUN.

## Profiling

Reproduzir com `PersistenceDemoTests.MeasuresPersistenceStagesAcrossPopulations` em Play Mode. Uma observação por carga, em sessão pausada, na suíte completa aprovada. O caso compacto usa 48×48 células. A cena usa 192×192 células. As linhas usam a mesma definição procedural, com recortes diferentes. A gravação inclui flush e troca do arquivo. A leitura inclui verificação de SHA-256. Serialização também inclui codificação e hash.

| Unidades | Recursos | Captura (ms) | Serialização (ms) | Gravação (ms) | Leitura (ms) | Desserialização (ms) | Reconstrução (ms) | Arquivo (bytes) |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 100 | 628 | 0,180 | 4,813 | 2,034 | 1,610 | 13,244 | 7,790 | 144.644 |
| 1.000 | 3.767 | 1,687 | 50,495 | 2,560 | 8,344 | 115,450 | 87,552 | 1.241.450 |
| 5.000 | 3.767 | 10,105 | 212,625 | 2,961 | 32,538 | 436,580 | 96,104 | 5.012.969 |

| Unidades | Delta heap Mono na captura (bytes) | Delta heap Mono na reconstrução (bytes) | Delta memória alocada pelo Unity na reconstrução (bytes) |
| ---: | ---: | ---: | ---: |
| 100 | 159.744 | 1.134.592 | 4.953.364 |
| 1.000 | 831.488 | -3.276.800 | 7.429.700 |
| 5.000 | 2.146.304 | 9.560.064 | 8.659.764 |

Os deltas são leituras do processo no Editor, sujeitos ao GC e a outras alocações. O delta negativo reflete uma queda do heap observado durante a medição. Captura e reconstrução ocupam o thread principal pelos tempos da tabela. O rebuild visual e o descarte da sessão antiga acrescentam trabalho além da reconstrução medida. A reconstrução de 5.000 unidades pode produzir um frame perceptivelmente longo no carregamento. Serialização, gravação, leitura e desserialização rodam em segundo plano.
