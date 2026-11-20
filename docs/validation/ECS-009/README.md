# Validação da ECS-009

Unity 6000.6.9f1, Linux Editor, URP. Sessão local com passo de 0,05 s e decisões a cada 12 ticks por unidade. Os ensaios rodam pelo Unity Test Framework via Unity MCP.

## Reprodução

- Edit Mode, classe `RTSWorld.Tests.UnitAutonomyTests`. Os testes verificam precedência, Stop, reativação, distribuição, esgotamento, inacessibilidade, carga sem depósito, conclusão de obra, retomada, guarda, facção, ida e volta, baixa e HoldPosition. O ensaio de escala usa um terreno sintético com duas fontes, metade de aldeões e metade de soldados passivos. Compara 0% e 50% de unidades autônomas. Aquece 24 ticks e mede 240 ticks completos.
- Play Mode, classe `RTSWorld.Tests.UnitAIDemoTests`. O primeiro teste remove o chunk visual de uma construção planejada e verifica que a sessão prossegue com autonomia ativa. O segundo reconstrói UnitAIDemo em cada população, remove os componentes de autonomia na fase de controle e usa a configuração da cena na fase ativa. Aquece 40 frames e mede 120 por fase com `Time.unscaledDeltaTime`. O FPS inclui Editor, streaming, renderização e custo de teste.
- Na cena `Assets/_Project/Scenes/UnitAIDemo.unity`, aldeões podem trabalhar por conta própria e até dois assumem cada obra. Soldados aliados guardam suas âncoras contra os inimigos próximos. Selecione e dê uma ordem com o botão direito para intervir. Espaço cancela e o botão **Retomar autonomia das selecionadas** restaura o modo. O cenário começa com um depósito físico e recursos de preparação para colocar uma obra pendente.

## Interpretação das medições

As colunas de CPU registram a soma dos tempos amostrados. Percepção inclui a atualização dos índices e das reservas. Decisão inclui a pesquisa local e o ranking. Execução mede o despacho de ordens automáticas. O total do tick inclui o restante da camada de decisão e os executores de movimento, economia, construção e combate. Consultas contabilizam buckets visitados pela autonomia. Alocações contabilizam bytes gerenciados na thread principal na janela, após aquecimento. As queries ECS ainda usam memória nativa temporária.

Cada entrega ao depósito e cada construção finalizada conta como uma conclusão econômica. Taxas do ensaio sintético usam segundos simulados. As janelas de FPS são curtas e podem observar zero conclusões mesmo durante atividade. O limite de oito campos de fluxo da sessão e a disputa por pontos de acesso seguem relevantes em grupos densos. A amostragem militar guarda um representante por célula e facção e pode escolher outro inimigo alcançável depois de uma nova atualização do índice.

Build de player, GPU timing, mouse físico e medição de bytes nativos: NOT_RUN.

## Edit Mode

PASS 273/273 em 38,164 s. Onze casos da ECS-009. Cada linha mede 240 ticks, equivalentes a 12 s simulados. Tempos em milissegundos.

| Presentes | IA ativa | Tick total | Percepção | Decisão | Execução | Reavaliações | Consultas | Bytes GC | Entregas | Entregas/s |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 100 | 0 | 1.8116 | 0.0000 | 0.0000 | 0.0000 | 0 | 0 | 0 | 0 | 0.00 |
| 100 | 50 | 13.3343 | 3.5335 | 0.8387 | 0.1244 | 1000 | 30723 | 0 | 20 | 1.67 |
| 500 | 0 | 2.5674 | 0.0000 | 0.0000 | 0.0000 | 0 | 0 | 0 | 0 | 0.00 |
| 500 | 250 | 48.3172 | 16.3126 | 5.7647 | 2.0179 | 5000 | 193942 | 0 | 26 | 2.17 |
| 1000 | 0 | 4.7309 | 0.0000 | 0.0000 | 0.0000 | 0 | 0 | 0 | 0 | 0.00 |
| 1000 | 500 | 89.1055 | 31.8269 | 10.9368 | 8.0742 | 10000 | 395087 | 0 | 31 | 2.58 |
| 5000 | 0 | 5.0583 | 0.0000 | 0.0000 | 0.0000 | 0 | 0 | 0 | 0 | 0.00 |
| 5000 | 2500 | 377.1284 | 155.4376 | 57.6329 | 9.5215 | 50000 | 2081961 | 0 | 33 | 2.75 |

Com 2.500 aldeões autônomos e 2.500 soldados passivos, o tick completo levou 1,571 ms em média. A frequência medida foi de 20 reavaliações por unidade ativa em 12 s simulados. Duas fontes sustentaram 33 entregas na janela de maior população.

## Play Mode

PASS 19/19 em 24,295 s. O teste de streaming descarregou a mesh do chunk da obra com 50 unidades autônomas entre as 100 presentes. A obra permaneceu na sessão e foram registradas nove atribuições.

| Presentes | IA ativa | FPS | Ticks | Atribuições | Conclusões | Janela (s) |
|---:|---:|---:|---:|---:|---:|---:|
| 100 | 0 | 330.67 | 7 | 0 | 0 | 0.3629 |
| 100 | 50 | 338.63 | 7 | 0 | 0 | 0.3544 |
| 500 | 0 | 340.88 | 7 | 0 | 0 | 0.3520 |
| 500 | 250 | 303.93 | 8 | 1 | 0 | 0.3948 |
| 1000 | 0 | 323.16 | 8 | 0 | 0 | 0.3713 |
| 1000 | 500 | 193.46 | 13 | 2 | 0 | 0.6203 |
| 5000 | 0 | 241.86 | 10 | 0 | 0 | 0.4962 |
| 5000 | 2500 | 225.23 | 11 | 0 | 0 | 0.5328 |

## Inspeção da cena via Unity MCP

PASS compilação e execução da UnitAIDemo. No tick 415, a sessão tinha 96 unidades, 14 atribuições, 15 entregas, uma obra concluída e duas unidades engajadas. Foram observadas guarda, perseguição, retorno, trabalho e bloqueio. A seleção por retângulo abrangeu 50 unidades e a chamada ao controlador da cena aceitou Stop para todas. No tick 573, as 50 continuavam em Stopped e o combate tinha zero unidades engajadas. A inspeção executou chamadas ao controlador com CodeDOM. O backend Roslyn estava indisponível.

Console final sem erros ou avisos. Editor fora de Play, com CombatDemo restaurada. Os arquivos históricos de profiling regravados pelas suítes foram repostos byte a byte.
