# Current Handoff

**Tarefa:** ECS-003 — BLOCKED na auditoria
**Arquivo:** tasks/in_progress/ECS-003-navigation-movement.md
**Escritor ativo:** nenhum — interrompido conforme instrução do usuário
**Checkpoint:** 2026-11-15 (America/Bahia)

## Sessão ECS-003

- Git main/54ef57907c0e731f25adb0f8901e136ac8952326; nenhum commit. Alterações preexistentes ECS-001/ECS-002/WORLD preservadas; nenhuma implementação ECS-003. Documentos de continuidade atualizados e tarefa movida para in_progress/BLOCKED.
- Inicialização e fontes fundamentais revisadas. Dependências ECS-001/ECS-002 DONE; World isolado, snapshot lógico e movimento fixo confirmados nas fontes. Unity conectado ao projeto correto, UnitsSimulationDemo limpa, Play desligado, sem compilação/falha.
- PASS baseline EditMode: 195/195, zero falhas/ignorados, 33,7525616 s, job a626e4ca73e848df9c5cc0d6973d6b90. Desconexão transitória do poll recuperada. Console antes da suíte vazio de erros/avisos. Implementação/compilação nova, PlayMode ECS-003 e profiling ECS-003 NOT_RUN.
- Bloqueio: usuário proibiu alterações fora do projeto. Após acionar o baseline, auditoria do pacote performance revelou RunFinished escrevendo incondicionalmente Application.persistentDataPath/TestResults.xml (diretório confirmado pelo Editor fora do projeto). Também existe fallback/cópia de resultados de performance ao caminho padrão. Não basta configurar outro diretório de resultados. Ver fonte/evidência exata no checkpoint da tarefa. Nenhum arquivo externo foi editado manualmente ou removido; não repetir testes nessa configuração.
- Menor próxima ação: resolver configuração/runner de validação compatível com a restrição antes de retomar auditoria/implementação. Não houve contorno, remoção de callbacks, mudança de pacote/cache nem solicitação de permissão ampliada. Parada exigida pelo usuário ao encontrar bloqueio; não iniciar próxima tarefa.
- Arquivos editados nesta sessão: tarefa ECS-003, ACTIVE, BACKLOG, CURRENT_STATE e este HANDOFF. A suíte inclui benchmarks preexistentes que podem regravar evidências ECS-002; preservar sem restauração às cegas. BACKLOG já continha linhas malformadas NET-001/UNITS-001, preservadas por não pertencerem ao escopo.

## Referência anterior preservada — ECS-002 DONE

## Concluído e verificado

- UnitSession reutiliza componentes/contrato ECS-001 num World RTSWorld Units; archetype e spawn/destruição em lote, IDs monotônicos por sessão, tipos aldeão/soldado, atributos configuráveis, proprietário, intenção e atividade. FoundationSession/MovementSystem anteriores não foram alterados nem registrados no World de unidades.
- UnitMovementSystem agenda IJobEntity/Burst em fixed group, limita velocidade/área, orienta, atualiza pose/região/estado e interpola snapshot de terreno. Pausa descarta tempo recebido; retomada preserva fração anterior. Dados indisponíveis congelam pose, preservam intenção e marcam WaitingForTerrain. Snapshot é lógico e independente de chunks visuais.
- UnitsSimulationDemo com authoring serializável, terreno v1/seed 7, caixas verdes/vermelhas instanciadas, HUD e botões 100/500/1000/5000/pausa/apresentação. Apresentação em assembly separado, sem pacote novo ou GameObject individual por unidade.
- PASS: compilação real; 195/195 EditMode (33,8572271 s), 9/9 PlayMode (3,5675784 s); cargas até 5000, replay/pausa/terreno v1 e ambiente v4, unload real de mesh sem perda lógica, liberação de recursos. Metas presentes, GUIDs únicos, git diff --check.
- Editor final: UnitsSimulationDemo limpa, Play desligado, sem compilação/falha, zero Worlds RTSWorld; Console sem erros/avisos, somente logs informativos do Test Runner. Console não apagado pelo agente.

## Evidências, decisões e limites

- docs/validation/ECS-002/README.md, mcp-results.json, load-editor.json, ticks-N.csv/setup-N.txt, scene-frame-5000.csv, profiler-render.json e demo-5000-framed.png. Esta última é a captura válida; demo-5000.png vazia e demo-5000-rendered.png cortada são diagnósticos preservados e excluídos do aceite.
- Tick completo sem renderização: 16 ticks de aquecimento + 120 amostras/população; médias para 100/500/1000/5000 = 0,01134/0,01843/0,02193/0,05286 ms. Cena real 5000: 120 frames, média 2,60764 ms, p95 3,33233 ms. Frames sem tick e interferência Editor documentados; seis submissões CPU não significam seis draw calls GPU.
- ADR-0005 registra archetype como equivalente de prefab, authoring sem Baking/SubScene e RenderMeshInstanced sem Entities Graphics. Snapshot cobre área limitada até 256 células/eixo, dentro de ±1000000, não streaming mundial. Identidade somente por sessão; sem garantias entre plataformas. Build/player/outras plataformas/heap/VRAM/GPU timing NOT_RUN.
- Refresh scope scripts inicialmente não importava assets; corrigido com refresh all e tipos confirmados. Inspeção World.All via LINQ substituída por foreach; aviso MCP WebSocket transitório recuperado. Sem bloqueio ou critério parcial.

Console final: dois avisos MCP WebSocket da desconexão transitória; conexão recuperada. Nenhum erro retornado pela consulta final. `git diff --check`: PASS após correção de linha vazia final no handoff.
