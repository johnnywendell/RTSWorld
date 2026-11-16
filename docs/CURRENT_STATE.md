# Current State

**Atualizado:** 2026-11-15 (America/Bahia)
**Estado:** WORLD-004-FIX-003 DONE. Nenhuma tarefa em execução.

## Projeto verificado

- WORLD-001 a WORLD-005 concluídas: terreno determinístico v1, renderização low-poly, streaming, biomas/água/recursos e câmera RTS. World e Presentation mantêm suas fronteiras; esta correção não acrescentou rede, persistência ou unidades.
- A versão ambiental atual é **4**, selecionada explicitamente em EnvironmentDemo e RTSCameraDemo. O terreno v1 e as versões ambientais 1, 2 e 3 foram preservados. A definição reproduzível inclui seed e versão; ResourceKey também inclui a versão ambiental.
- A FIX-003 comprovou dois problemas anteriores: o potencial lateral de água da v3 produzia superfícies inclinadas; bermas e cortes de campos da v2 produziam paredes e faixas de terreno. A v4 usa lagos constantes e rios nivelados por seção transversal, com perfil longitudinal monotônico limitado a 0,25 e à capacidade das margens. A troca de domínios ocorre em faixa seca; o leito somente corta o relevo. Não há suavização global nem offset de água.
- Permanece uma mesh/renderer por chunk, com o mesmo ownership de streaming. A apresentação reutiliza o gerador por definição, com cache de nove metadados regionais e sem reter chunks ambientais. Instrumentação opcional separa camadas e exporta amostras/triângulos; o replay do Editor verifica residência antes de capturar.
- A câmera RTS mantém InputSystem, WASD/MMB/scroll/QE e bordas opcionais. Foco lógico double, consulta de superfície triangulada independente dos chunks visíveis e ponte de streaming. Seu código permaneceu intacto na FIX-003; a consulta foi testada nas versões ambientais 1 a 4.

## Validação atual — FIX-003

- **PASS:** Unity 6000.6.9f1/LinuxEditor/URP via MCP; 172/172 EditMode e 6/6 PlayMode, sem falhas ou ignorados. Inclui regressões anteriores, perfis/margens/lagos, índices/normais/degeneração, coordenadas positivas e negativas, fronteiras e reconstrução após unload.
- **PASS:** inspeção visual real nas duas cenas, em Editor e Play. Vinte replays com quatro camadas e 25 chunks residentes/fila vazia, três seeds, múltiplos níveis do mar/escalas e frames X=0 e X=−5807. As 83 imagens incluem uma captura inicial inválida, preservada e excluída do aceite. Na região B, a maior inclinação de WaterOnly passou de 69,07° na v2 e 31,34° na v3 para 8,53° na v4, com H=2 e V=1,2.
- Console final do Editor e das duas cenas em Play: zero erros/avisos. Shader sem erro, nenhum replay temporário restante. Estado final: 25 meshes para 25 chunks, fila vazia, cena limpa e Play desligado. Auditoria dos 38 arquivos protegidos e dos GUIDs: PASS.
- Relatório, dados de 27 amostras, triângulos, reprodução, migração e incidentes: `docs/validation/WORLD-004-FIX-003/README.md`. Tarefa concluída em `tasks/completed/WORLD-004-FIX-003-hydrology-terrain-geometry.md`.

## Limites e continuidade

- Hidrologia analítica estática, sem hidrodinâmica ou drenagem continental. Relevo, ilhas e facetas legítimas permanecem; escalas extremas podem exagerar declives. A validação não cobre todas as seeds/coordenadas. Build/player, outras plataformas e medições de heap/VRAM: NOT_RUN.
- Geração atômica de chunks e carregamento transitório permanecem. Medição local de Step em Play, oito waypoints, raio 1, máximo de um chunk/frame e orçamento de 4 ms: mediana 1,3669 ms, p95 3,0432 ms, máximo 4,5793 ms. Não é uma medição de FPS; um chunk pode exceder o orçamento.
- Câmera sem floating origin, guarda lógica padrão ±32768 e frame vertical com escala uniforme; cobertura limitada ao raio 8. Segurança considera terreno/água, não colisores dos recursos.
- A raiz atual da RTSCameraDemo em X=−5807 foi preservada. O baseline inicial tinha X=0, comprovado por hash. A mesma pose mundial aponta para regiões lógicas diferentes nesses frames; zoom e foco históricos não foram inventados.
- Git main/6a9966b, sem commits. Alterações preexistentes da FIX-001, FIX-002 e WORLD-005 preservadas. Aguardar revisão do usuário; não iniciar outra tarefa automaticamente.
