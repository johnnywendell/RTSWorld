# ADR-0008 — Configuração comum do mundo e da simulação

**Status:** aceita para INTEGRATION-001

A cena integrada usa TerrainGenerator v1, EnvironmentGenerator v4 e a mesma configuração serializada que alimenta o ChunkManager. A câmera e o adaptador ECS resolvem WorldDefinition e EnvironmentDefinition a partir dessa fonte. Cada consumidor pode reconstruir amostras determinísticas. O streaming mantém somente meshes e contagens de recursos residentes e a sessão mantém uma janela regional de 192 × 192 células. Alterar seed, dimensão do chunk, nível do mar ou versão ambiental no Inspector em Play recria a sessão integrada e invalida seleção e ordens.

Navegação e oclusão usam a diagonal de triangulação existente. O movimento usa a superfície de terra. O picking usa o máximo entre terra e água interpoladas do snapshot regional e recorre à consulta WORLD fora da janela. Uma célula com profundidade de água positiva em qualquer vértice é bloqueada. A margem conservadora mantém as trajetórias fora do polígono molhado. Declividade usa a maior norma de gradiente da ECS-003, com limite lógico 2,25 no cenário. Diferenças de escala da apresentação ficam na conversão de posição.

O spawn seleciona células únicas e alcançáveis pelo campo calculado a partir da célula transitável mais próxima do centro configurado. Aldeões e soldados usam centros de distribuição distintos. Se a seed fornecer poucas células conectadas, a cena informa e gera apenas a população válida disponível.

Tree, Stone e Gold continuam como recursos atravessáveis. A classificação evita acoplar colisão a símbolos de mesh cujo tamanho é visual. O contrato `ProceduralTerrainContract.GetEnvironmentChunk` retorna o catálogo imutável por coordenada de chunk, independente da residência. A futura economia pode resolver um ResourceKey por célula global e usar a posição `Key + Offset`, tipo e altura. Identificadores são interpretados no contexto da definição completa. Quantidade extraída, ocupação e estado mutável permanecem para outra tarefa.
