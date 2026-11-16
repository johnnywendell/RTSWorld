# Current Handoff

**Última tarefa:** WORLD-004-FIX-003 — DONE
**Arquivo:** tasks/completed/WORLD-004-FIX-003-hydrology-terrain-geometry.md
**Escritor ativo:** nenhum
**Checkpoint:** 2026-11-15 (America/Bahia)

## Concluído e verificado

- Causa comprovada antes da correção: ApplyField v3 reduz o nível da água com a distância transversal. A derivada é aproximadamente 1 em (-153,357), criando água lateralmente inclinada. Na v2, bermas e interrupções dos campos produzem paredes no terreno e na água. OriginalTerrain, EnvironmentalTerrain e WaterOnly foram comparados com 25 chunks residentes e fila vazia.
- Nova versão ambiental 4: ContainedHydrology separa níveis de água e forma do leito. Lagos e seções transversais dos rios são nivelados; o perfil longitudinal é monotônico, limitado a 0,25 e pela capacidade das margens. Buffer seco de quatro intervalos separa domínios. O leito usa envelopes mínimos, sem bermas ou suavização global.
- Terreno v1 e versões ambientais 1/2/3 preservados por fixtures e fingerprints. EnvironmentPresentation reutiliza o gerador com cache de nove metadados; ChunkManager mantém ownership/upload/unload. Nenhuma alteração no código da câmera, shader, material, pacotes ou ProjectSettings.
- Instrumentos opcionais: EnvironmentGeometryView, EnvironmentGeometryDiagnostics e Editor/HydrologyDiagnosticSession. As duas demos selecionam explicitamente v4; regeneração e ResourceKey devem manter a versão ambiental.
- **PASS:** 172/172 EditMode em 35,9704167 s e 6/6 PlayMode em 1,8620796 s, sem falhas/ignorados, via Unity MCP. Inclui fronteiras, coordenadas positivas/negativas, perfis/seções, meshes e reconstrução exata de (-5,11).
- **PASS:** visual nas duas cenas, em Editor/Play; vinte replays com quatro camadas e duas capturas das câmeras originais. Console final sem erros/avisos; shader sem erro. Auditoria PASS: 38 arquivos protegidos preservados, GUIDs únicos, 14 relatórios de água v4 sem índices inválidos, degenerações ou faces invertidas. Estado final: 25 meshes/25 chunks, fila vazia, cena limpa, fora de Play e nenhum replay restante.

## Reprodução e coordenadas

- `docs/validation/WORLD-004-FIX-003/README.md` contém diagnóstico, limites, migração e chamadas MCP. CSV de 27 amostras registra alturas dos três campos, distância euclidiana, direções e derivadas. JSONs localizam cada triângulo.
- Seed 7, N=32, H=2, V=1,2, raio=2, mar=38: cenário A com centro (2,1); cenário B no frame inicial X=0 com centro (-5,11), pose mundial (-301.94,153.09,673.45), rotação 55°/45°. Ortho 48 é enquadramento explícito de replay; zoom/foco históricos não foram registrados.
- A raiz atual da RTSCameraDemo em X=−5807 foi preservada. Hash comprova X=0 no baseline inicial. A mesma pose mundial agora aponta para o chunk (86,11), também validado. Não sobrescrever essa translação nem confundir coordenadas mundiais e lógicas. Rayhits e comparação de hashes estão registrados. Editor deixado nessa cena, fora de Play.

## Incidentes e limites

- As 83 imagens incluem uma captura inicial vazia, explicitamente inválida para reprodução. Uma rodada EditMode foi interrompida por solicitação prematura de Play do agente; foi invalidada e repetida corretamente até 172/172 PASS. Ver `validation-sequence-incident.txt`. Não contar essa rodada como sucesso. O agente não usou Console Clear.
- Modelo estático, sem hidrodinâmica/drenagem global. Facetas e afloramentos naturais foram preservados. Um pequeno afloramento em A foi medido como terreno original acima do mar, não uma berma elevada. Validação finita nas seeds/locais registrados. Build/player e outras plataformas: NOT_RUN.
- Medição limitada de Step em Play, oito waypoints, raio 1, máximo de um chunk/frame e orçamento de 4 ms: mediana 1,3669 ms, p95 3,0432 ms e máximo 4,5793 ms. Não é FPS; o chunk permanece atômico.

## Git e próxima ação

- main/6a9966b0168d3ba553c97ed7f7c8a2818f5bbca4, sem commits. Working tree anterior da FIX-001/FIX-002/WORLD-005 preservada; baseline-git-status.txt e baseline-hashes.json registram a entrada. Relatórios concluídos anteriores intactos.
- Não há implementação ou critério obrigatório pendente. Próxima ação: revisão do usuário. Nenhum commit ou outra tarefa autorizado.
- MCP HTTP em localhost:8080; última sessão 22d7c9a2d2074a8786abc4191c4b6386, sujeita a expiração. Reinicializar se necessário; schemas em /tmp/rtsworld-mcp-tools.json. Não publicar logs integrais de inicialização, que podem conter autenticação.
