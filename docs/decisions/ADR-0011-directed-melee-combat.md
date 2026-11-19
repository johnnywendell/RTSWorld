# ADR-0011 — Combate corpo a corpo direcionado

**Status:** adotada para ECS-006

## Decisão

- `UnitIdentity.Owner` identifica a facção nesta etapa. Inteiros distintos são hostis. A facção 0 pertence ao jogador local. Soldados com dano e alcance positivos podem receber Attack. Aldeões podem ser alvos. Sem diplomacia, defesa automática ou troca automática de alvo.
- A apresentação resolve o clique sobre inimigo com o raio existente e oclusão do terreno. Attack tem prioridade sobre Gather e Move. Em seleção mista, Attack usa apenas soldados e Gather usa apenas aldeões. Um clique hostil com seleção incompatível produz feedback e preserva ordens existentes. Sem inimigo selecionável, vale o despacho de recurso ou terreno da ECS-005. Espaço para todas as unidades selecionadas.
- A sessão resolve o handle versionado em tempo constante. A cada tick, os atacantes ativos compartilham um snapshot por alvo distinto. Alcance usa distância no plano XZ e testa as células atravessadas do segmento, inclusive vizinhos cardinais em cruzamento exato de canto. Água, declividade e recursos bloqueiam aproximação e golpes através da grade de navegação existente.
- A perseguição usa a célula lógica do alvo como destino coletivo. O atlas regional de oito campos continua compartilhado com Move e Gather. A sessão admite uma tentativa de obter um campo novo por tick de combate. Enquanto persegue, um atacante revisita a célula do alvo em até aproximadamente 0,35 s de simulação por tentativa bem-sucedida. Campo indisponível ou rota bloqueada gera nova tentativa com intervalo de cerca de 0,5 s. A entrada ou saída do alcance é examinada a cada tick. Ocupação de soldados permanece fora da grade. O movimento existente distribui centros em 16 offsets estáveis por célula.
- O combate roda antes de movimento e coleta no grupo fixo. Reúne os golpes válidos com base nas posições do início do tick, aplica a soma por alvo, limita vida a zero e destrói as baixas no mesmo passo. Um soldado morto também acerta se já tiver um golpe válido naquele tick. A recarga pertence ao atacante e persiste após Stop, Move ou troca de alvo. Cada atacante gera no máximo um golpe por tick.
- Dano interrompe coleta e entrega. A carga fica com o aldeão enquanto vivo. A morte usa `DestroyBatch` e contabiliza a carga em `LostCargo`, preservando estoque e saldo da fonte. Referências de combate são invalidadas no lote de remoção, e os trabalhadores removidos saem da lista econômica na atualização do mesmo passo.
- O HUD e as malhas instanciadas consomem os componentes sem alterar o estado da batalha. A morte elimina a malha e o próximo snapshot de apresentação limpa a seleção.

## Limites

O destino compartilhado pode concentrar atacantes numa passagem ou nos arredores do alvo. Os offsets reduzem coincidência exata em pequenos grupos e admitem sobreposição em grandes grupos. Muitos alvos em células distintas podem disputar os oito campos e aumentar o tempo até obter uma rota. Os componentes são unmanaged. A coordenação de combate roda na thread principal, e o movimento continua no job Burst existente. Determinismo validado no mesmo runtime cobre os cenários testados. Referências a outras plataformas dependem de validação própria.
