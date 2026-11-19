# ADR-0010 — Coleta e depósito

**Status:** aceita para ECS-005

Madeira (Tree) e pedra (Stone) são coletáveis. Ouro mantém o comportamento ambiental. A disponibilidade inicial deriva do catálogo e da configuração de economia, com 60 unidades de madeira e 80 de pedra na EconomyDemo. O estado mutável por ResourceKey guarda o total extraído. A simulação mantém esse registro fora da apresentação e durante o streaming.

`GatherWorker` é um componente ECS unmanaged com fase, alvo, célula de interação, tipo de carga e quantidade. `ResourceEconomySystem` roda no passo fixo após movimento e percorre trabalhadores ativos em ordem de entrada. Até três células de aproximação compartilham os campos da ECS-003. Uma transferência limita simultaneamente taxa, capacidade e restante. O depósito tem ID de sessão 1, célula navegável e estoque por proprietário e tipo. A carga entra no estoque na chegada. Se o recurso permanece disponível, o trabalhador retorna.

Move e Stop cancelam a tarefa e preservam carga. Ao trocar para um recurso de outro tipo com carga, o trabalhador entrega primeiro. Remoção por DestroyBatch contabiliza carga perdida separadamente. Esgotamento libera footprint, invalida os campos afetados e solicita a reconstrução da mesh do chunk residente. Um filtro usa o estado econômico também em chunks que voltam a carregar.

A demo tem um depósito compartilhado e estoque separado por proprietário. A economia é regional e em memória. Os botões de população e mudanças na definição procedural reiniciam a sessão de demonstração. Uma ordem para muitos trabalhadores divide até três posições adjacentes. Campos simultâneos seguem o limite de oito do ADR-0006 e a ordem pode aceitar um subconjunto quando houver pressão no cache.
