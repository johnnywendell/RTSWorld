# ADR-0009 — Ocupação de recursos ambientais

**Status:** aceita para INTEGRATION-002

A janela de navegação carrega o catálogo lógico dos chunks que a interceptam, com halo de duas células para footprint em bordas. ResourceKey deduplica placements. Tronco de árvore e base de pedra são círculos circunscritos às respectivas bases quadradas, com a mesma variação determinística do visual. A copa é aérea e não bloqueia o chão. Gold mantém o símbolo atravessável. A cena usa folga configurável de 0,22 célula lógica. A ocupação usa contagem de círculos que intersectam cada célula, para preservar bloqueio quando só um de dois vizinhos é removido.

A grade mantém custos de água e declividade separados da máscara. Mudanças de disponibilidade e raio reaplicam apenas o footprint do recurso. Cada campo de fluxo pode ser afetado quando a célula modificada pertence ao componente do destino ou toca um vizinho alcançável. A invalidação é conservadora dentro do componente e preserva campos desconectados. Jobs são sincronizados antes de publicar a alteração. Ordens em campos invalidados aguardam nova instrução. O próximo comando recalcula o campo sob demanda. Identidades são definidas pelo catálogo procedural. Adição de novas identidades em tempo de execução fica fora desta integração.

A aproximação enumera centros navegáveis dentro do alcance e do componente fornecido pelo chamador. Um campo compartilhado pode servir a vários trabalhadores. Ocupação e aproximação independem de mesh, collider e residência visual. A janela, os oito campos e a atualização fixa seguem o ADR-0006.
