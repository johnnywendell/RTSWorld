# Validação ECS-004

## Reprodução

1. Abra `Assets/_Project/Scenes/RTSInteractionDemo.unity` e entre em Play.
2. Clique em um soldado laranja e use Shift para adicionar um aldeão verde. Ctrl alterna uma unidade. Arraste fora do painel para selecionar um grupo. Clique no terreno vazio para limpar.
3. Clique com o botão direito em um ponto do terreno dentro da área jogável para mover. Clique em outro ponto para trocar a ordem. Use Espaço para parar e preservar a seleção.
4. Use WASD ou o botão do meio para mover a câmera, scroll para zoom e Q/E para rotacionar. A opção Bordas ativa movimento pelo perímetro. Use Diagnóstico para ver contagens e tempos, e os botões de população para repetir com até 5.000 unidades.

[Captura da cena em Play](rts-interaction.png)

## Resultados

- **PASS** compilação no Unity 6000.6.9f1, Console final sem erros ou avisos, cena limpa, Play desligado e zero Worlds de unidades após sair. Os primeiros erros de compilação foram corrigidos e verificados em nova execução.
- **PASS** Edit Mode 221/221 em 40,9610971 s e Play Mode 11/11 em 2,3597547 s. Zero falhas ou ignorados. O teste Play executou a cena nova, selecionou após zoom e rotação, moveu e parou uma unidade, descarregou uma mesh e confirmou que a unidade continuou em movimento. Depois destruiu a entidade e confirmou que sua seleção foi removida.
- **PASS** Unity MCP invocou diretamente quatro testes focados e conduziu a sessão gráfica. Estados de dispositivos virtuais do Input System dispararam os mesmos controles da cena. Clique = 1. Shift + clique = 2. Ctrl + clique = 1. Arraste e Shift + arraste = 100. Botão direito enviou Move. Espaço cancelou a ordem de 100 unidades e manteve a seleção. A opção Bordas devolveu pan (1,0) no lado direito.
- **PASS** 100 unidades chegaram paradas a 100 posições diferentes. Uma nova ordem moveu o grupo, e Espaço zerou as velocidades após o passo fixo.
- **NOT_RUN** interação com mouse físico, build de player, medição de GPU e outras plataformas. Os controles físicos podem ser conferidos com o roteiro acima.

As suítes completas rodaram em uma cópia isolada dentro de `Logs/ECS-004-validation`, mantendo cache e a saída adicional do callback de performance dentro do projeto. O MCP foi usado na instância gráfica para compilação, Console, testes focados, inspeção e input virtual. O Test Runner normal da instância gráfica aponta para um diretório de resultados fora do projeto.

## Profiling

| Unidades | Projeção (ms) | Seleção na cena com oclusão (ms) | Campo frio (ms) | Campo existente (ms) | Distribuição (ms) | Aplicação (ms) |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 100 | 0,02417 | 0,53320 | 9,11300 | 0,01580 | 0,00330 | 0,07480 |
| 500 | 0,10207 | 2,61664 | 15,97140 | 0,06510 | 0,02350 | 0,51690 |
| 1000 | 0,20914 | 5,38110 | 8,61880 | 0,14310 | 0,03450 | 1,36020 |
| 5000 | 1,06989 | 26,64080 | 9,74350 | 0,72140 | 0,18070 | 12,83280 |

A projeção sem oclusão mede 20 retângulos após aquecimento em Edit Mode. A coluna de seleção na cena mede cinco retângulos sobre toda a área da câmera após um aquecimento, zoom máximo, 2560 × 1440, oclusão real e câmera ortográfica, via Unity MCP. Os quatro cenários selecionaram toda a população e aceitaram a ordem. A leitura do snapshot reduziu a média de 116,22 ms para 26,64 ms na seleção de 5.000 unidades neste cenário. Esse custo ocorre ao confirmar o retângulo. O campo e a aplicação são medidos em Edit Mode com seed 7, janela 128 × 128 e destino (20,20). Tempos de CPU locais de Editor, sem meta de FPS. [Amostras em CSV](profiling.csv)

A formação concentra o tráfego no destino coletivo antes de espalhar o grupo. Objetos de cena precisam de colisor para ocluir a seleção. A demo usa terreno v1. Destino sobre água foi validado separadamente em teste ambiental.
