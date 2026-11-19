# Validação ECS-003

Unity 6000.6.9f1 no Linux Editor. Cena interativa com URP, seed 7, passo fixo de 0,05 s e destinos (16,45) e (-16,45). A captura mostra a execução de 1.000 unidades com o diagnóstico ligado. O teste interativo adicional reconstruiu a população para 5.000 e avançou 1.600 passos, além do tick inicial. As 5.000 chegaram às células de destino e ficaram em estado Idle. Ao sair do Play, o Editor registrou zero Worlds RTSWorld Units.

[Captura da cena com diagnóstico](navigation-demo.png)

## Resultados finais

| Verificação | Estado | Resultado |
| --- | --- | --- |
| Compilação e Console no Editor | PASS | `scriptCompilationFailed=False`, zero erros, um aviso transitório de WebSocket MCP |
| Edit Mode | PASS | 204/204, zero falhas ou ignorados, 36,9886907 s |
| Play Mode | PASS | 10/10, zero falhas ou ignorados, 2,2888051 s |
| Cena e carregamento visual | PASS | A cena executou em Play. O teste descarregou o chunk visual e confirmou que as unidades navegavam e a população permanecia intacta. |
| Navegação máxima | PASS | 256 × 256 células, seed 7, rota de 385 células de (-112,-80) até (112,80), passagem de 11 células em X=0. Amostra via MCP com malha 11,04250 ms, A* 33,04500 ms e campo 33,29370 ms. |

| Unidades | Malha (ms) | A* (ms) | Campo (ms) | Média por tick (ms) |
| ---: | ---: | ---: | ---: | ---: |
| 8 | 2,60320 | 5,30300 | 7,46600 | 0,00684 |
| 100 | 2,53810 | 7,74870 | 10,97520 | 0,01102 |
| 500 | 2,53160 | 4,79150 | 7,44460 | 0,03152 |
| 5000 | 2,55600 | 6,07790 | 8,70450 | 0,03832 |

A primeira rodada também passou, com média de 0,19080 ms/tick a 5.000 unidades enquanto o Editor interativo recompilava. A repetição final registrou 0,03832 ms/tick. [Amostras finais em CSV](profiling.csv)

## Reprodução das suítes

A execução usou Unity em batch mode com `-nographics -runTests -testPlatform EditMode` e `PlayMode`. Os resultados completos ficam em `Logs/ECS-003-validation/results` na estação de validação. A cópia de projeto contém Assets, Packages, ProjectSettings e cache de pacotes local. O sistema de arquivos externo ficou somente leitura, e os sockets do cliente de licença foram montados para acesso ao serviço local. O diretório temporário, o cache UPM, XDG_CONFIG_HOME e XDG_CACHE_HOME apontaram para a área de validação. O XML do callback de performance foi confirmado dentro da área de validação.

Para reproduzir o profiling no Unity Test Runner, executar `RTSWorld.Tests.NavigationMovementTests.ProfilesTerrainPathFieldAndFixedMovement`. O teste imprime uma linha `NAV_PROFILE` por população no XML. Sem renderização, usa seed 7 e malha 128 × 128 com uma barreira em X=0, uma passagem central e A* de (-60,-30) até (40,30). As fases de malha, A* e campo são amostras individuais por cenário. O tempo de movimento é a média de 120 ticks após 16 ticks de aquecimento, com sincronização dos jobs em cada tick. A máquina usa Intel Core i5-14600KF. As medidas são de Editor local e variam com a carga da máquina.

A hierarquia de portais foi avaliada para mapas maiores. Nesta janela, A* e construção do campo ocorrem na preparação do comando. A atualização de cada unidade faz uma consulta ao campo compartilhado. Os limites de janela, cache e separação estão no [ADR-0006](../../decisions/ADR-0006-regional-unit-navigation.md).

Build de player, GPU timing e comparação entre plataformas ficaram fora da validação.
