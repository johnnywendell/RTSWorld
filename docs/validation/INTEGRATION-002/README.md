# Validação INTEGRATION-002

## Auditoria

EnvironmentGenerator seleciona Tree, Stone e Gold de um hash de seed e célula seca adequada. O ResourceKey usa seed, versão ambiental e X/Z globais. A origem de chunk usa divisão floor, inclusive em X/Z negativos. Offset determinístico de 0,25 a 0,75 e elevação triangular definem a posição. EnvironmentMeshBuilder desenha tronco e copa como pirâmides; Stone e Gold usam símbolos de geometria semelhante. ChunkManager libera a mesh e seus objetos visuais no unload. Os recursos não têm Collider, Rigidbody ou estado por GameObject. O catálogo reconstrói placements sem depender da câmera.

A sessão integrada anterior usava água e inclinação para classificar células e tinha suporte a máscara de obstáculos, sem passar um índice ambiental. A decisão inicial é ocupar troncos e pedras e deixar a copa e o ouro atravessáveis. A ocupação lógica considera a folga da unidade.

## Gate

- PASS compilação Unity 6000.6.9f1 e Console sem erros relacionados. Um aviso transitório de WebSocket MCP durante recarga. Dois testes focados também rodaram por invocação MCP. O terceiro escrevia via TestContext e foi verificado pelo runner NUnit.
- PASS Edit Mode 226/226 em 42,8771788 s e Play Mode 12/12 em 2,6136207 s, sem falhas ou ignorados. Suítes da cópia isolada em Logs/INTEGRATION-002-validation. A primeira compilação detectou referência à sobrecarga errada de uma asserção, corrigida antes das rodadas finais.
- PASS árvore, pedra, ouro, sobreposição de footprints, borda negativa, remoção, reativação, raio, posição de interação, cache em componentes desconectados, A* e passagem estreita. O teste ECS observou o desvio por uma abertura de uma célula e a chegada. A cena integrada continuou atravessando chunks durante unload/reload determinístico. Seu teste passou a usar a célula adjacente (-152,351), pois a anterior passou a estar ocupada.
- PASS inspeção MCP com 100/1.000/5.000 unidades. As ordens selecionaram 100/1.000/4.778 unidades visíveis e foram aceitas. Foram verificados centros em células navegáveis a cada 10 ticks durante 160 ticks por cenário. [Captura](scene.png)

## Medições e limites

Seed 7 e 3.767 registros no catálogo regional. Construção do índice de 33,11 a 44,40 ms, construção da grade de 16,20 a 16,40 ms e campo de 11,73 a 18,04 ms nas amostras gráficas. Alteração com invalidação seletiva levou 2,59440 ms em amostra de teste Edit Mode. Máscara e revisões usam dois vetores int de 192 × 192, 288 KiB de payload, além do catálogo e seus buckets. Sem medição de heap total, mouse físico ou player. [Perfil](profiling.csv)

A região usa margem conservadora. Mudança no componente provocada por um recurso invalida campos desse componente, mesmo que algumas rotas individuais não cruzassem a célula. Campos de outros componentes permanecem. Ordens em campos invalidados aguardam nova instrução. [ADR-0009](../../decisions/ADR-0009-environmental-obstacle-occupancy.md)
