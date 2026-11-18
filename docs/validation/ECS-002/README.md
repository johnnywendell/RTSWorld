# ECS-002 — Evidências de validação

Data local: 2026-11-15 (America/Bahia). Git main/54ef57907c0e731f25adb0f8901e136ac8952326; sem commits. Ambiente: Unity 6000.6.9f1, LinuxEditor, Entities 6.6.0, Burst 2.0.0, URP 17.6.0; Intel i5-14600KF / NVIDIA RTX 4070 SUPER. Pacotes existentes preservados.

## Verificações

| Verificação | Status | Evidência |
| --- | --- | --- |
| Dependência ECS-001 | PASS | tarefa DONE, fontes presentes, baseline atual 181 EditMode / 7 PlayMode |
| Compilação real | PASS | scriptCompilationFailed=false; tipos novos confirmados nos assemblies após refresh all |
| EditMode final | PASS | 195/195, zero falhas/ignorados, 33,8572271 s; job de11f90b72504868a8cfe1d36f8b9509 |
| PlayMode | PASS | 9/9, zero falhas/ignorados, 3,5675784 s; job 831da261d18e46dfa1bc62a56b380016 |
| Cargas | PASS | 100/500/1000/5000 em ambos modos; dados CSV/JSON neste diretório |
| Cena real / apresentação | PASS | 5000 entidades e 5000 atualizadas; 6 submissões, 3 raízes, sem GO individual; demo-5000-framed.png |
| Unload visual | PASS | UnitDemoTests descarrega e destrói mesh real; população/ticks preservados |
| Pausa / retomada / teardown | PASS | testes e inspeção; zero Worlds RTSWorld após stop, cena limpa |
| Console final | PASS | zero erros/avisos; cinco logs informativos do Test Runner; Console não foi limpo pelo agente |
| Metadados / Git | PASS | metas presentes para todos assets novos; nenhum GUID duplicado em Assets; git diff --check |
| Build/player, outras plataformas, heap/VRAM, GPU timing | NOT_RUN | nenhuma inferência de determinismo/performance entre plataformas |

Os testes novos cobrem atributos por tipo, ownership, IDs sem reuso, spawn/destruição em lote, integridade, passo, intenção/orientação/velocidade/limites, pausa/retomada, replay com partições de frame diferentes, terreno v1 e ambiental v4 incluindo bordas negativas, ausência/suspensão de dados, ciclo de vida visual e carga. As regressões WORLD/câmera/streaming e teste Burst nativo existentes passaram.

## Custo de tick completo sem renderização

`UnitBenchmarkTests.ProfileCompletedFixedTicksWithoutRendering`: seed 7, terreno v1, passo 0,05 s, área [-64,64], população dividida entre tipos. Tick zero antes de 16 ticks de aquecimento e 120 amostras; cada amostra verifica incremento exato de um tick e inclui Advance + CompleteJobs. Sem apresentação. CSV ticks-N.csv guarda todas amostras; setup-N.txt separa preparação do snapshot/World e spawn. Não há limite obrigatório de tempo nem comparação entre máquinas.

| Unidades | Média ms | Mediana ms | p95 ms | Máximo ms |
| --- | --- | --- | --- | --- |
| 100 | 0.01134 | 0.01095 | 0.01300 | 0.02170 |
| 500 | 0.01843 | 0.01780 | 0.02200 | 0.02780 |
| 1000 | 0.02193 | 0.02040 | 0.03010 | 0.04260 |
| 5000 | 0.05286 | 0.05485 | 0.08590 | 0.12870 |

Preparação incluiu 16641 vértices e variou de 9,55 a 11,96 ms; spawn em lote de 0,39 a 3,80 ms para 500 a 5000 unidades; o primeiro caso de 100 teve 1,93 ms, incluindo aquecimento de código. Não interpretar a ordem dos primeiros casos como curva de escalabilidade.

## Frames e profiler

- `load-editor.json`: 15 frames de aquecimento + 60 observados por população no Test Runner, com renderização submetida mas sem a câmera da cena real. Inclui frames sem tick; seus FPS não representam o player nem o custo isolado de simulação.
- `scene-frame-5000.csv`: cena real em Play, câmera ortográfica enquadrada no terreno, 5000 entidades, 15 frames de aquecimento + 120 amostras, snapshot padrão 129×129. Média de frame 2,60764 ms (~383,49 FPS derivados), p95 3,33233 ms, máximo 3,80068 ms. Ticks observados 15→22; Advance+sync médio por frame 0,00552 ms, incluindo frames sem tick. Coleta por EditorApplication.update, sem throttling do Test Runner; resolução da Game View não foi fixada. Há interferência possível do Editor/MCP, portanto é uma observação local, não benchmark de player nem comparação controlada com Test Runner.
- `profiler-render.json`: captura real de contadores Unity Profiler em Play (51 draw calls, 27 SetPass, 186176 triângulos, 114428 vértices); valores globais do Editor, incluindo outras views/UI, não exclusivos de unidades. As seis submissões CPU da demo podem virar mais draw calls nativos. GPU timing retornou 0 e não é considerado válido.

## Reprodução

Abrir UnitsSimulationDemo, entrar em Play e usar botões de população/pausa/apresentação. HUD apresenta tempo por frame; para custo por tick rodar os quatro casos UnitBenchmarkTests no EditMode. Rodar toda suíte EditMode e PlayMode pelo Test Runner/MCP. Testes de profiling atualizam CSV/JSON no diretório de validação do projeto. A coleta de frames real foi feita com callback temporário EditorApplication.update, removido ao completar 120 amostras; câmera usada durante captura restaurada e nenhum helper ficou na cena.

`demo-5000-framed.png` foi lida de RenderTexture após frames normais e mostra os dois tipos sobre terreno. Não chamar Camera.Render isoladamente para validar Graphics.RenderMeshInstanced: as submissões pertencem ao frame normal. `demo-5000.png` está vazia por essa captura imediata da ferramenta e `demo-5000-rendered.png` mostra o enquadramento anterior, cortado; ambos preservados como diagnóstico e excluídos do aceite. O HUD IMGUI não faz parte da captura da câmera; seus valores foram inspecionados via propriedades em Play.

## Incidentes, decisões e limites

- Refresh scope scripts compilava apenas assets já importados; a execução intermediária de 181 testes foi baseline, não validação ECS-002. Refresh all importou os assets e os novos tipos foram confirmados antes das execuções de 191 e 195 testes.
- Aviso transitório MCP WebSocket durante reload; não persistiu no Console final. Inspeção World.All com LINQ falhou por coleção NoAlloc; refeita por foreach, zero Worlds próprios após stop.
- Câmera inicial apontava para Y=0 e cortava a área elevada. Pose final foi salva com foco na elevação central 71,0085754, orthographicSize=85.
- Identidade é estável somente na sessão; não é protocolo de persistência. Snapshot limitado, sem streaming lógico de terreno; ausência congela unidades. Sem seleção, navegação, combate, coleta, rede ou persistência; ECS-003 não iniciada.
- Nenhuma edição direta fora do projeto. O próprio Test Runner Unity escreveu seu relatório padrão de infraestrutura em ~/.config/unity3d/DefaultCompany/RTSWorld/TestResults.xml, conforme log MCP; evidências desta tarefa foram gravadas no projeto.
- ADR-0005 registra archetype equivalente a prefab, authoring sem Baking/SubScene e instancing sem pacote Entities Graphics. Alterações preexistentes, incluindo seed de RTSCameraDemo e ECS-001, foram preservadas.
