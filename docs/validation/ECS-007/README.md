# Validação ECS-007

## Reprodução

- Unity 6000.6.9f1, Linux Editor, Intel Core i5-14600KF. Rodar Edit Mode e Play Mode pelo Unity MCP.
- Em `Assets/_Project/Scenes/CrowdMovementDemo.unity`, entrar em Play e escolher 100, 500, 1.000 ou 5.000 unidades. Os botões **Fluxos opostos** e **Convergir** enviam ordens às duas facções. A seleção e o clique direito mantêm Move, Gather e Attack. Espaço executa Stop.
- Ativar **Diagnóstico** e gizmos no Editor para ver raio, vetor de velocidade, vizinhos amostrados por intensidade de cor e congestionamento em vermelho, limitados às primeiras 100 unidades. O painel mostra os acessos e recusas no atlas.

## Escolha do steering

A solução combina separação e previsão simplificada de colisão pela velocidade relativa. O raio de busca e a janela de previsão são fixos, com limite de 24 amostras por célula. Isso permite aplicar correções locais em um job Burst sobre um snapshot imutável por tick. Uma solução de velocity obstacles com restrições recíprocas exigiria resolver um conjunto de restrições por unidade e integrar raios e máscaras ambientais ao solver. O steering tem custo mais previsível nesta integração e aceita sobreposição residual quando o espaço disponível é insuficiente. Os desempates locais usam ID lógico. A validade da repetibilidade fica restrita ao mesmo runtime, ordem de comandos e estado de origem.

## Metodologia de desempenho

- Edit Mode com duas populações de soldados em sentidos opostos, seed 7, grade 96×96, 0,05 s por tick, distância de spawn 0,66 unidade e dois destinos compartilhados. Compara exatamente o mesmo cenário com o steering desligado e ligado.
- Mede 1.200 ticks após 16 de aquecimento. As etapas são sincronizadas separadamente para medir o índice e o job de movimento. Os tempos incluem agendamento e espera. A coluna de movimento inclui aplicação da navegação, steering, pose e elevação. O custo de reconstrução dos dois campos é medido à parte. Os bytes por tick cobrem alocação gerenciada na thread chamadora.
- A cada 20 ticks, fora da região cronometrada, conta unidades com pelo menos um vizinho a menos de 0,60 unidade. Unidades lentas fora dos destinos têm velocidade abaixo de 0,10. Congestionadas atingiram oito ticks seguidos com avanço insuficiente. A chegada conta unidades que encerraram o movimento na célula do objetivo. A primeira chegada está quantizada pelo intervalo de amostragem. Este cenário comprime cada população em uma única célula. Move com formação usa destinos individuais e tem ensaio próprio.
- Play Mode mede FPS médio e alocação gerenciada por frame via ProfilerRecorder, com 30 frames após 12 de aquecimento para cada população. Inclui Editor, renderização e harness de teste. Sem build de player, GPU timing ou medição de pico de memória nativa.

## Resultado sintético pareado

Os pares abaixo estão na ordem **base → crowd**. O custo adicional do job inclui o steering e alterações no tráfego, portanto a diferença é uma estimativa do custo de evitação nessa carga.

| Unidades | Tick ms | Índice ms com crowd | Job de movimento ms | Unidades sobrepostas, média | Primeira chegada, s | Chegadas ao fim | Congestionadas ao fim com crowd |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 100 | 0,0098 → 0,0299 | 0,0052 | 0,0064 → 0,0212 | 99,8 → 62,0 | 13,8 → 14,8 | 100 → 6 | 49 |
| 500 | 0,0167 → 0,0485 | 0,0087 | 0,0121 → 0,0360 | 499,1 → 367,5 | 13,8 → 15,8 | 500 → 7 | 236 |
| 1.000 | 0,0184 → 0,0783 | 0,0129 | 0,0139 → 0,0608 | 998,4 → 817,5 | 13,8 → 21,8 | 1.000 → 8 | 438 |
| 5.000 | 0,0350 → 0,3059 | 0,0526 | 0,0291 → 0,2470 | 4.994,1 → 4.628,3 | 7,8 → 31,8 | 5.000 → 6 | 2.173 |

Todas as cargas registraram 0 bytes gerenciados por tick na thread chamadora em ambas as configurações. Com crowd, ao final havia 1, 2, 2 e 13 unidades abaixo de 0,10 de velocidade fora dos destinos. O ensaio de duas unidades mediu profundidade de sobreposição acumulada de 1,40 para 0,00 unidade lógica × tick e confirmou que ambas cruzaram. A concentração de milhares em duas células expõe o limite do steering. As ordens Move com formação conservaram 9 posições individuais espaçadas em pelo menos 2 unidades após o gargalo, com crowd ligado e desligado.

## Cena em Play Mode

| Unidades | FPS médio do Editor | Bytes gerenciados por frame |
| ---: | ---: | ---: |
| 100 | 209,5 | 610.076 |
| 500 | 236,9 | 609.691 |
| 1.000 | 204,4 | 609.988 |
| 5.000 | 179,6 | 609.806 |

Os dados de FPS e alocação cobrem o frame inteiro. Variação entre cargas inclui renderização, Editor e harness. A inspeção direta com Unity MCP na seed da cena confirmou os dois destinos `(-173, 351)` e `(-134, 351)` e uma rota de 244 células, nove bloqueadas lateralmente em ambos os lados do eixo de saída. Após iniciar a ordem, o snapshot no tick 399 tinha 98 de 100 unidades em movimento e dois campos construídos. O Play Mode verificou descarte real de mesh com a população lógica preservada.

## Atlas de Flow Fields

- Na janela sintética 16×16, o teste solicitou oito destinos referenciados, tentou um nono, reutilizou um campo ativo e liberou um slot. Foram 11 consultas, 1 acerto, 10 faltas, 1 recusa por capacidade e 1 substituição. Nove construções somaram 3,3861 ms. As taxas descrevem o roteiro de saturação.
- Na janela 96×96 do ensaio, os dois campos custaram juntos entre 8,33 e 15,34 ms nas execuções pareadas. A dimensão da grade e a região alcançável interferem no custo. Na cena procedural, os fluxos opostos compartilharam dois campos entre as facções.
- Na perseguição móvel, quatro atacantes reutilizaram campos, reconstruíram conforme o alvo mudou de célula e mais de um entrou em alcance. Em combate, a capacidade só é recusada quando os campos permanecem referenciados. A taxa de construção continua limitada a uma solicitação nova por tick de combate. Muitos alvos móveis espalhados podem atrasar rotas.
- Um cache regional ou hierárquico poderia reutilizar trabalho parcial entre destinos de uma região. Ele precisaria preservar custos, cruzamento de fronteiras e invalidação pela revisão ambiental. Os testes confirmam o caso de disputa por slots e o benefício do compartilhamento. A capacidade permaneceu em oito e o algoritmo do atlas foi preservado.

## Verificação funcional

- **PASS** compilação Unity e Console sem erros ou avisos ao final da inspeção direta. A leitura final após a suíte retornou um aviso do WebSocket do MCP e zero erros ou avisos atribuídos à tarefa.
- **PASS** 258/258 Edit Mode e 16/16 Play Mode nas suítes completas. Os testes específicos cobrem raios distintos, coordenadas negativas, fluxo oposto, separação, formação, Stop, gargalo, água, alvo móvel, capacidade do atlas, economia e streaming.
- **PASS** entregas completas com crowd para 10 aldeões coletando madeira e 50 coletando pedra, com conservação verificada a cada tick.
- **NOT_RUN** mouse físico, build de player, GPU timing e perfil de memória nativa. A inspeção da cena usou ordens por Unity MCP e o teste Play Mode.
