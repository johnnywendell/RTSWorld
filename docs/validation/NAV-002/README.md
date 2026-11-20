# Validação NAV-002

## Linha de base na revisão validada da NAV-001

A suíte `CrowdMovementTests` passou 8/8 via MCP antes da primeira mudança da NAV-002. Dois soldados cruzaram uma fronteira negativa e a sobreposição acumulada caiu de 1,4 para zero com avoidance. No ensaio de dois destinos compartilhados, com avoidance ligado, 6/7/8/6 unidades chegaram das populações de 100/500/1.000/5.000. Ao final, 49/236/438/2.173 estavam marcadas como congestionadas. As chegadas não expressam deadlocks recuperáveis porque cada destino tem uma única célula e o cenário comprime toda a população nesses dois pontos. As médias de tick com avoidance foram 0,0303/0,0483/0,0934/0,2794 ms.

Outro ensaio via MCP isolou a política de rota com avoidance desligado. Seed 7, grade (−16, −16) a (16, 16), 9 soldados com origem do lote (−7,5; −0,5), espaçamento de spawn 1, ordem Move para (5, 0), espaçamento final 2 e 300 ticks de 0,05 s. Todas as unidades chegaram. A unidade de índice 1 foi para (3, −2), visitando primeiro a âncora. A trajetória somou 15,628 unidades lógicas e a rota A* ponderada de referência tinha 11 transições cardinais. A unidade de índice 8 foi para (3, 0), percorreu 12,849 unidades e a referência tinha 10 transições. A referência é medida em deslocamentos cardinais de célula, e a trajetória inclui a posição inicial exata e segmentos contínuos.

## Entrada antecipada no trecho final da formação

Ao entrar em uma célula, a unidade pode ingressar no trecho final se a célula já pertence ao buffer de seus waypoints. O movimento continua desse ponto no sentido da posição final. Cada célula visitada consecutivamente é consultada uma vez. O atlas, a formação, o espaçamento e as restrições de segmento são os mesmos. O índice usado para repetir a consulta é transitório. Após Load, o passo da formação salvo é mantido e a consulta pode ser recalculada para a célula atual.

Repetição via MCP do ensaio anterior. Todas as unidades chegaram às mesmas nove posições em 300 ticks. A unidade de índice 1 reduziu a trajetória de 15,628 para 11,000 unidades e não cruzou mais a âncora. A unidade de índice 8 passou de 12,849 para 9,651; a de índice 6, de 16,405 para 13,395. Nos demais casos, a trajetória foi a mesma. O cenário isola a qualidade da rota do efeito do steering.

## Fluxo oposto na faixa da NAV-001

Ensaio MCP com a mesma árvore e rocha da NAV-001, somente a linha Z=0 disponível e bolsões nas células (−2, 1) e (2, 1). Dois soldados com avoidance ativo, nas posições (−1,5; 0,5) e (2,5; 0,5), receberam Move para trocar de lado. Após 200 ticks de 0,05 s, ambos chegaram. No interior da faixa, houve sete ticks de presença simultânea e quatro abaixo da separação mínima de 0,60. A distância mínima entre centros foi 0,100. A travessia é segura em relação aos obstáculos estáticos e a concorrência entre unidades ainda exige controle de entrada e prioridade.

## Verificação da primeira mudança

- **PASS** Edit Mode 291/291 em 36,594 s, incluindo a regressão do desvio na formação. Play Mode 22/22 em 31,862 s.
- **PASS** compilação, inspeção MCP, Console sem erros ou avisos. Editor na NavigationDiagnosticsDemo fora de Play.
- **NOT_RUN** matriz completa de recuperação, cena CrowdRecoveryDemo e comparação de profiling da recuperação. A tarefa permanece em andamento.

## Recuperação lateral

O job mantém o débito de avanço pela projeção do deslocamento real sobre a intenção. Com recuperação ligada, progresso ou ausência de vizinhos reduzem o débito em dois ticks. Uma rota com vizinhos e débito de 12 ticks pode tentar um desvio de oito ticks. Depois espera 24 ticks antes de mudar de lado ou tentar novamente. Cada candidato respeita a velocidade máxima e é validado pelo mesmo teste de segmento da navegação. A tentativa mantém o campo e a ordem. As pernas de interação, a célula final do campo e as faixas restritas ficam fora da manobra. A direção lateral alternava a cada oito ticks de congestão antes desta alteração. O estado de tentativa é transitório e reinicializado na atribuição de rota ou na restauração dos componentes.

A regressão usa um soldado com raio 0,49 e quatro vizinhos parados numa faixa de três células de largura. Ela exige chegada com recuperação, permanência em rota após 220 ticks sem recuperação, observação de congestionamento, cooldown e poses finitas dentro da grade. A posição dos vizinhos parados e o limite de velocidade também entram nas asserções.

A CrowdRecoveryDemo reutiliza a seed, o streaming, o combate, a economia e os diagnósticos da NavigationDiagnosticsDemo. O painel controla Fluxos opostos, Convergir, a recuperação lateral e as quatro populações. A cena ainda não contém os 12 roteiros dedicados do aceite.

## Perfil pareado inicial

1200 ticks por condição após 16 de aquecimento, mesmos mapas, unidades, ordens e avoidance ligado. A recuperação é a variável do par. O benchmark concentra os grupos em duas células e não mede vazão em um destino com espaço suficiente. Bytes gerenciados por tick na thread chamadora foram zero. O débito de avanço tem histerese com recuperação, portanto os totais de congestionamento não são diretamente comparáveis.

| Unidades | Tick sem → com (ms) | Chegadas sem → com | Unidades sobrepostas em média, sem → com | Tentativas |
| ---: | ---: | ---: | ---: | ---: |
| 100 | 0,0294 → 0,0261 | 6 → 12 | 62,0 → 62,1 | 2.257 |
| 500 | 0,0476 → 0,0474 | 7 → 13 | 367,5 → 340,2 | 15.544 |
| 1.000 | 0,0793 → 0,0753 | 8 → 15 | 817,5 → 777,0 | 34.678 |
| 5.000 | 0,2983 → 0,3008 | 6 → 24 | 4.628,3 → 4.474,6 | 185.897 |

O aumento de chegadas neste ensaio não implica recuperação geral. Com 5.000 unidades, o total de unidades com velocidade abaixo do limite de estagnação no fim passou de 13 para 23. O teste de primeira chegada também piorou nas cargas 100 e 500. O perfil da cena com recuperação e a estratégia de fluxos opostos na faixa de uma unidade ainda exigem validação específica.


## Ensaio integrado da CrowdRecoveryDemo

Amostras MCP com Fluxos opostos, 16 ticks de aquecimento e 240 ticks medidos sem renderização no trecho cronometrado. As oito ordens foram aceitas. Em 5.000 unidades, o tick médio foi 0,2347 ms com recuperação desligada e 0,2344 ms ligada. As chegadas nessa janela passaram de 22 para 34. Com 100 unidades, foram 3 sem recuperação e 2 com recuperação na mesma janela. As amostras do Editor são únicas por condição e não estabelecem melhora uniforme. [CSV de simulação](profiling-scene-fixed-tick.csv) · [Captura em Play](crowd-recovery-demo.png)


A regressão do corredor foi repetida por MCP com a mesma geometria. Em 220 ticks sem recuperação, a unidade percorreu 18,162 unidades e continuou em rota, sem chegar. Com a recuperação, chegou no tick 161, após quatro tentativas e 13,510 unidades percorridas. As inversões de deslocamento lateral acima de 0,005 unidade por tick passaram de 47 para 19 até o final de cada execução. Nenhum caso ficou completamente imóvel. A estagnação estava no avanço pela rota.

Play Mode mediu 30 frames após 12 de aquecimento em cada configuração da cena. O FPS sem → com recuperação foi 185,0 → 243,4 em 100 unidades, 227,0 → 206,3 em 500, 182,0 → 143,0 em 1.000, e 152,4 → 164,1 em 5.000. A alocação gerenciada ficou entre 610.075 e 610.442 bytes por frame. O frame inclui Editor, renderização, UI e o executor de teste. Variações do Editor afetam a amostra curta. Memória nativa e timings de player/GPU ficaram fora da medição. [CSV de frames](profiling-scene-frame.csv)

## Verificação anterior à arbitragem

- **PASS** compilação, Edit Mode 293/293 (37,868 s), Play Mode 23/23 (36,915 s com ensaio de frames), Console sem erros ou avisos. A suíte Play inclui streaming, combate, economia, construções, IA e persistência.
- **PASS** regressão pareada de recuperação no corredor, profiling de 100 a 5.000 unidades e CrowdRecoveryDemo aberta em Play com controles de recuperação e fluxo. Editor ao final em CrowdRecoveryDemo, fora de Play e cena limpa.
- **NOT_RUN** arbitragem de faixas de uma unidade, replanejamento, matriz completa dos 12 roteiros da demo e suas regressões dedicadas. O protocolo atual só usa desvio lateral em células com espaço. Ainda falta medir o avanço acumulado e distinguir de forma explícita deadlock persistente de espera em passagem ocupada.
- Estado da tarefa permanece IN_PROGRESS. Não houve commits ou início de outra tarefa.

## Arbitragem da faixa restrita

A leitura da vizinhança usa a posição, o raio, o ID lógico e o destino válido no atlas da revisão corrente. Na entrada de um trecho restrito reto, a unidade procura fluxo oposto nas células do trecho e nos dois pontos seguintes. Quem já entrou tem precedência. Na disputa entre duas entradas, o menor ID tem prioridade. A unidade que cede usa um bolsão lateral transitável e espera o outro atravessar e liberar a boca. O afastamento usa a mesma validação geométrica da NAV-001. O deslocamento perto da faixa também verifica o segmento contra os vizinhos da amostra fixa. A inspeção é limitada a oito células de faixa e 32 vizinhos por célula. Exceder esses limites impede novas entradas quando a precedência não pôde ser verificada. Faixas longas, ausência de bolsão, saídas ocupadas e demanda contínua podem manter a espera. O desempate por ID não oferece garantia de justiça sob demanda contínua.

Nos dois testes com bolsões, o menor afastamento medido foi 1,000 com prioridade em ambos os sentidos. Os dois soldados chegaram. Sem bolsões, a separação mínima foi 4,000 e o estado permaneceu WaitingForPassage. O ensaio anterior com destinos nas próprias bocas media 0,100 de separação e sobreposição em quatro ticks. O teste atual coloca os destinos uma célula além da boca para que haja espaço de saída. Essa diferença deve ser considerada na comparação.

A recuperação acumula distância efetiva, débito de segundos sem avanço e tempo de espera pela passagem. Espera pela sua vez não incrementa o débito de deadlock. Um débito de 1,2 s sob interferência de vizinhos pode sinalizar Deadlocked. Bloqueio estático, campo inválido e terreno indisponível são estados distintos. A manobra mantém o Flow Field e o destino, tem cooldown e não adiciona A* por unidade. O caso de regressão de recuperação confirma uma única construção de campo ao longo de todas as tentativas. Replanejar o mesmo campo com o grafo inalterado não resolveria a precedência do fluxo. O progresso e a reserva do bolsão são transitórios. Uma sessão restaurada reconstrói campos e reavalia ocupação da passagem no primeiro tick.

Ordens coletivas reservam posições finais em células inteiras, deixando as faixas para trânsito. Uma ordem individual pode terminar na posição solicitada. Aproximações de recurso e entrega preferem células inteiras. Uma faixa continua elegível se a interação não tiver alternativa alcançável.

## Roteiros na CrowdRecoveryDemo

Seletor anterior/próximo e Executar no painel. Os exercícios de corredor usam uma região seca da seed configurada e uma máscara de cenário. No gargalo, os círculos de árvore e rocha são registrados no catálogo lógico e visíveis nos diagnósticos. Floresta, rochas, água, combate, economia e fronteiras usam o mundo procedural da cena.

| Cenário | Resultado da inspeção MCP, 800 ticks |
| --- | --- |
| Duas unidades se cruzando | 2/2 chegadas |
| Grupos em direções opostas | 12/12 chegadas |
| Gargalo estreito | 2/2 chegadas, espera registrada |
| Corredor largo | 8/8 chegadas |
| Travessia de floresta | 5/5 chegadas junto ao recurso |
| Passagem próxima a rochas | 5/5 chegadas |
| Contorno de água | 5/5 chegadas com a linha direta cruzando células molhadas |
| Soldados atacando | 1 alvo derrotado |
| Aldeões e depósito | 60 unidades entregues |
| Congestionamento inicial | 1/1 viajante chegou com quatro vizinhos parados |
| Destino inalcançável | Ordem rejeitada e zero chegadas |
| Travessia de chunks | 5/5 chegadas |

Os presets usam populações fixas para produzir o caso selecionado. Os botões de 100 a 5.000 unidades reconstroem a população integrada para os ensaios de carga.

### Comparação da arbitragem com metas idênticas

Uma repetição via MCP manteve a seed, os bolsões, os dois soldados e os destinos uma célula além das bocas. Com a recuperação desligada, a separação mínima foi 0,100, houve quatro ticks abaixo dos 0,60 exigidos e as duas ordens terminaram no tick 33. Com a arbitragem ligada, a separação mínima foi 1,000, houve zero ticks de sobreposição e as duas ordens terminaram no tick 76. O tempo adicional corresponde à passagem sequencial. As prioridades invertidas e a ausência de bolsões também são cobertas em Edit Mode.

Na rodada final de Edit Mode, o benchmark sintético manteve 0 bytes gerenciados por tick na thread chamadora. Em 5.000 unidades, o índice espacial ficou em 0,0563 → 0,0574 ms e o job de movimento, que inclui evitação e recuperação, em 0,2564 → 0,2442 ms. O total foi 0,3186 → 0,3074 ms. A recuperação não é um job separado. As quatro cargas e os tempos de campo estão no [perfil sintético final](profiling-final-synthetic.csv). Os dados dos roteiros inspecionados estão em [scenarios.csv](scenarios.csv).


## Aceite final

- **PASS** Unity 6000.6.9f1, compilação e Console. Edit Mode 296/296 em 38,112 s. Play Mode 24/24 em 39,363 s. A primeira rodada de Play após adicionar os roteiros detectou a ausência de LogicalRegion na consulta do próprio teste. A consulta foi corrigida e a suíte completa passou na repetição.
- **PASS** 12/12 roteiros executados via MCP e validados pelo teste de Play. Inclui saídas dos corredores, tempo de espera, morte do alvo, entrega real, rejeição explícita e mudança de coordenada de chunk. O teste de streaming descarregou e recarregou meshes sem remover a população lógica.
- **PASS** regressões das tarefas ECS-003 a ECS-010 nas duas suítes, incluindo Stop, alvos móveis, combate, recursos, construções, IA, continuidade após Load e retomada visual após unload. O cooldown do desvio e sua independência de novas construções de campos foram verificados no ensaio de estagnação.
- **PASS** perfil em 100, 500, 1.000 e 5.000. No maior ensaio integrado, o tick foi 0,2386 → 0,2291 ms e a contagem instantânea de Deadlocked no fim foi 2.709 → 1.719. As chegadas passaram de 22 para 11 na janela de 240 ticks. A passagem sequencial troca vazão imediata por separação. A medição começou após 16 ticks de aquecimento e as condições usam as mesmas ordens. A espera é contabilizada à parte. Nenhuma das oito janelas alocou bytes gerenciados na thread que chamou o tick. [CSV integrado](profiling-final-integrated.csv)
- **PASS** perfil renderizado nas quatro cargas. Em 5.000 unidades foram 163,0 → 161,3 FPS no Editor numa amostra de 30 frames após 12 de aquecimento. Inclui Editor, UI, renderização e teste. Memória gerenciada por frame e restantes populações em [CSV de frames](profiling-final-frames.csv). Player, GPU timing e pico de heap nativo não foram medidos.
- **PASS** Editor em CrowdRecoveryDemo fora de Play, cena limpa e Console sem erros ou avisos. [Captura da cena com seletor](crowd-recovery-scenarios.png)

O desvio de formação caiu de 15,628 para 11,000 unidades no caso que tinha 11 transições A* de referência. Na regressão lateral, chegou no tick 161 em quatro tentativas, com 19 inversões laterais, em comparação com 47 inversões e ausência de chegada até o tick 220 no controle. O ensaio do gargalo confirmou chegada segura em ambos os sentidos com bolsões e espera quando o corredor não tem espaço para ceder. Vão ocupado por unidades inativas, ausência de bolsões e superlotação continuam impondo limites ao progresso. Os resultados se aplicam aos casos e às cargas medidos. NAV-002 concluída.
