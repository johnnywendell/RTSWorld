# ADR-0006 — Navegação regional de unidades

**Status:** aceita para ECS-003  
**Data:** 2026-11-18

## Contexto

O snapshot de terreno da ECS-002 cobre até 256 células por eixo. A simulação precisa atravessar bordas de chunks e servir muitos comandos compartilhados no mesmo passo fixo. O streaming visual pode descarregar meshes enquanto as unidades continuam ativas.

## Decisão

- A janela de navegação é uma revisão imutável indexada por células globais inteiras. Região ausente, célula bloqueada e célula transitável têm estados distintos. O chamador controla disponibilidade lógica por chunk e publica uma nova revisão após alteração. A vizinhança cardinal atravessa bordas sem tratamento especial e evita cortar cantos. A classificação usa a maior declividade entre os dois triângulos do contrato de terreno, água ambiental nos vértices e uma máscara opcional de obstáculos por célula. O custo de entrada é `1 + min(254, ceil(10 × declividade))`.
- A* regional usa Manhattan com custo unitário admissível e desempate por índice. O campo compartilhado usa Dijkstra reverso e armazena a próxima célula para cada posição alcançável. A construção custa O(N log N) em tempo e O(N) em memória. A busca é acionada por comando, e cada unidade consulta o campo em O(1) por tick.
- O `UnitSession` publica até oito campos simultâneos em um atlas nativo. Um campo sem referências pode ser reaproveitado após sincronizar os jobs. Trocar a janela invalida os comandos anteriores. Unidades em movimento aguardam um novo comando. O cache gerenciado disponível para outros consumidores usa FIFO por destino e invalidação por instância da janela.
- No passo fixo, o movimento Burst limita a distância pelo atributo de velocidade e usa a elevação do snapshot. Cada unidade mira uma posição interna estável na próxima célula. Dezesseis posições internas reduzem sobreposição e ajudam a distribuir o tráfego em passagens. Grupos maiores podem compartilhar posições. A solução admite sobreposição e não resolve colisões entre unidades nem garante vazão em um gargalo.
- Adiamos a hierarquia de portais. O maior mapa inicial tem 65.536 células e os campos são compartilhados. Um grafo de portais aceleraria busca entre várias janelas, mas exigiria resumos de conectividade e invalidação por revisão, além de busca local em cada corredor. Reavaliar quando houver destinos fora da janela ou quando o tempo de construção de campos por comando pesar para o servidor. O relatório ECS-003 registra as medições para essa decisão.

## Limites

Células globais usam inteiros e posição interna usa double. A ordem do algoritmo é estável no mesmo runtime com o mesmo snapshot e comandos. Terreno, inclinação e posição usam ponto flutuante, sem garantia de identidade bit a bit entre plataformas. A janela cobre uma área finita e usa o snapshot lógico da sessão. Células além da janela, perfis de tamanho de unidade, ocupação dinâmica, replanejamento automático e rotas inter-regionais de longo alcance ficam para evolução.
