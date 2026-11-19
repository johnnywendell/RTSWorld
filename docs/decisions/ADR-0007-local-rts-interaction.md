# ADR-0007 — Seleção local e ordens RTS

**Status:** aceita para ECS-004

A seleção pertence ao jogador local e usa os IDs lógicos de sessão. O apresentador gera uma lista compacta a partir da consulta ECS usada para renderizar as unidades. O clique escolhe o primeiro volume na direção do raio, limitado pela superfície procedural e por colisores. O retângulo testa o centro projetado de unidades elegíveis e a oclusão do terreno e de colisores na confirmação. A CPU só repete picking quando o usuário confirma a seleção. A caixa de seleção, cores e marcadores são locais e independentes do estado da unidade. Geometria adicional precisa de colisor para atuar como oclusor.

Move e Stop são valores explícitos aceitos pelo UnitSession para unidades atuais do proprietário. Move valida lote, célula, conectividade e espaço antes de substituir a intenção. O destino inválido preserva a ordem anterior. A sessão continua usando o atlas de até oito campos da ECS-003. Cada comando de grupo usa um campo para o destino coletivo. As posições finais são centros distintos da grade no componente alcançável, enumerados em anéis com espaçamento inteiro. O percurso final reverte os predecessores do mesmo campo em um buffer por unidade. O custo desse trecho é proporcional à soma dos percursos locais. Unidades desaceleram apenas pelo limite de distância de cada passo, sem resolução de colisões ou garantia de vazão no destino coletivo.

Espaço aciona Stop para preservar S da câmera. O painel IMGUI compartilha a área excluída do ponteiro com a câmera. O cenário usa terreno v1 e streaming WORLD-003. Testes separados ativam a classificação ambiental para verificar rejeição de água.

Esta decisão preserva a fronteira da futura autoridade. O input e a seleção conhecem a câmera. O comando conhece o proprietário, destinos lógicos e handles da sessão. Um servidor futuro pode resolver identidades e limpar lotes sem receber coordenadas de tela.
