# ADR-0012 — Persistência local da simulação

## Contexto

O mundo procedural pode ser regenerado por configuração, mas as alterações de economia, combate, ordens e construções pertencem à sessão. Save e Load precisam funcionar mesmo com chunks visuais descarregados.

## Decisão

- `UnitSession` captura um DTO em seu thread proprietário entre chamadas de `Advance`, após sincronizar os jobs. O snapshot mantém o tick, a fração pendente do passo fixo e o relógio de combate.
- `LogicalEntityId` identifica unidades e construções no mesmo espaço de IDs da sessão. Facções usam o inteiro de proprietário. Recursos usam seed, versão ambiental e célula global. O chunk é derivado da célula e do tamanho dos chunks.
- O envelope v1 contém assinatura, versão, comprimento, SHA-256 e JSON UTF-8. Campos da versão são obrigatórios. Versões desconhecidas, truncamento, corrupção e valores inválidos reportam erro. Novas versões exigirão um leitor explícito ou migração documentada. O hash detecta corrupção acidental.
- O serializador usa Newtonsoft.Json, disponível no projeto, para preservar valores fracionários. Serialização, verificação de integridade e I/O são executados em tarefas de fundo. O arquivo temporário, no mesmo diretório do destino, é sincronizado antes de renomear ou substituir o slot.
- A restauração cria uma sessão candidata e repõe ocupação, estoque e progresso antes de religar as ordens por ID. A geometria atual das construções tem orientação fixa e custos definidos pelo tipo. A restauração conserva o estoque já debitado e nunca cobra novamente pela obra.
- Campos de fluxo são reconstruídos a partir dos destinos. Índices de multidões e percepção voltam a ser produzidos no passo fixo. Attack ou Build sem alvo válido encerra a ordem correspondente. Gather sem recurso identificável fica inativo e preserva a carga. Trabalhadores em trânsito conservam a célula de entrega, validada pela economia contra os depósitos restaurados.

## Limites

A sessão candidata é criada no thread principal. O custo dessa etapa cresce com a área procedural e é medido na validação. Os índices transitórios voltam a ser construídos sob demanda, então o carregamento não promete replay bit a bit. O formato v1 cobre grades procedurais da simulação. Grades com predicados externos são recusadas na captura.
