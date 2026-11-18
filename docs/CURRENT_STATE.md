# Current State

**Atualizado:** 2026-11-15 (America/Bahia)
**Estado:** ECS-002 DONE. ECS-003 BLOCKED na auditoria; nenhuma implementação de navegação.

## ECS-003 — bloqueio de validação (2026-11-15)

- Dependências e fontes ECS-001/ECS-002 verificadas; baseline atual PASS 195/195 EditMode, compilação anterior sem falha. Unity correto, UnitsSimulationDemo limpa e Play desligado.
- Pacote de performance registra callback que escreve TestResults.xml em persistentDataPath fora do projeto. Baseline foi acionado antes da identificação desse efeito. Restrição explícita do usuário impede repetir; execução interrompida conforme solicitado, sem alterar pacotes/cache ou pedir permissões ampliadas.
- ECS-003 BLOCKED em tasks/in_progress; implementação, PlayMode e profiling ECS-003 NOT_RUN. Git main/54ef579, sem commits, alterações anteriores preservadas. Ver tarefa/handoff para evidência e próxima ação; nenhuma tarefa posterior iniciada.

## Unidades ECS verificadas — 2026-11-15

- ECS-002 concluída: aldeões/soldados, definições configuráveis, ownership/IDs por sessão, spawn/destruição em lote, movimento fixo Jobs/Burst com limites/orientação, pausa/retomada e elevação triangular por snapshot do contrato ECS-001.
- Simulação independente da apresentação/residência visual. Sem dados de terreno, congela pose; unload real testado sem perda de unidades. Snapshot limitado a área inteira de até 256 células/eixo, sem navegação, coleta, combate, rede ou persistência.
- UnitsSimulationDemo usa malha compartilhada e RenderMeshInstanced, tipos distintos, controles de carga/pausa/apresentação e HUD; sem GameObject individual por unidade nem pacote adicional. Fundação anterior preservada.
- PASS atual: compilação, 195/195 EditMode, 9/9 PlayMode, cargas 100/500/1000/5000, cena real em Play e teardown. Console sem erros/avisos; Play desligado, cena limpa, zero Worlds próprios.
- Profiling local Editor: média por tick completo para 5000 = 0,05286 ms (120 amostras, 16 ticks de aquecimento); cena real 5000 = 2,60764 ms/frame médio em 120 amostras. Condições, CSVs e limites em docs/validation/ECS-002/README.md; não são metas de player. Build/player/outras plataformas/heap/VRAM/GPU timing NOT_RUN.
- Decisão ADR-0005 e arquitetura atualizadas. Git main/54ef579, sem commits, alterações preexistentes preservadas (incluindo RTSCameraDemo seed 7→123). Nenhuma próxima tarefa iniciada.

## Fundação ECS verificada — 2026-11-15

- Entities 6.6.0; Burst 2.0.0 e Collections 6.6.0 existentes preservados; Mathematics/Jobs módulos do Unity 6000.6.9f1. URP 17.6.0 preservado.
- Simulation tem World isolado, componentes unmanaged com pose global double3/quaternion, passo fixo padrão 0,05 s, movimento IJobEntity/Burst e contrato managed com terreno versionado. Nenhuma migração WORLD ou dependência da apresentação.
- ECSFoundationDemo inicializa 256 entidades com HUD de quantidade/ticks/passo. Elevação somente no spawn; sem navegação, rede, persistência ou benchmark.
- PASS nesta sessão: 181/181 EditMode e 7/7 PlayMode; compilação, Burst nativo, inspeção da cena em Play e liberação do World. Console final vazio; cena limpa, Play desligado. Build/player/outras plataformas/performance NOT_RUN.
- Evidências: docs/validation/ECS-001/README.md; arquitetura/limites em ADR-0004 e docs/ARCHITECTURE.md.
- Git real: main/54ef57907c0e731f25adb0f8901e136ac8952326, sem commits. tasks/pending preexistente preservado. RTSCameraDemo apresentou seed 7→123 durante baseline, origem não confirmada, preservada para revisão. Nenhuma próxima tarefa iniciada.

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
- Referência histórica da FIX-003: main/6a9966b. Na entrada ECS-001, HEAD já era 54ef579 e os trabalhos WORLD estavam incorporados; consultar o estado Git atual acima.
