# ECS-001 — Evidências de validação

Ambiente: Unity 6000.6.9f1, LinuxEditor, URP 17.6.0; branch main, base 54ef579. Nenhum commit criado.

| Verificação | Resultado | Evidência |
| --- | --- | --- |
| Dependências WORLD | PASS | WORLD-001..005 e FIX-001..003 em completed/DONE; fontes protegidas sem diff |
| Package Manager | PASS | Entities 6.6.0, Collections 6.6.0, Burst 2.0.0; Mathematics/Jobs módulos do Editor; URP preservado |
| Baseline EditMode | PASS | 172/172, 35,2205676 s; job 721bfa8f14d04e2e94087f911247d154 |
| Baseline PlayMode | PASS | 6/6, 1,8035805 s; job 505dd5ea242647f18beecc7d2129a8de |
| Compilação final | PASS | isCompiling=false; scriptCompilationFailed=false; tipos carregados via MCP |
| EditMode final | PASS | 181/181, 33,7417546 s; job e66ef87ba8c3451eaf12c9786c4bfd80 |
| PlayMode final | PASS | 7/7, 2,023978 s; job 22cbecf82899436a90a5753dd5910cdf |
| Burst nativo | PASS | ScheduledJobActuallyExecutesBurstNativeCode usa BurstDiscard para distinguir execução nativa de fallback managed |
| Cena em Play | PASS | 256 entidades; passo 0,05 s; snapshots ticks 196/427, updates iguais aos ticks; X 1,800000146→13,350000318 |
| Apresentação mínima | PASS | demo-play.png inspecionado: HUD Entities 256, Tick 203, Fixed 0,050s |
| Liberação | PASS | Ao sair de Play: zero Worlds RTSWorld Foundation; cena limpa, fora de Play |
| Console final | PASS | Zero entradas, sem Console Clear pelo agente |
| Metas/assemblies | PASS | Sem GUID duplicado em Assets; metas da fundação presentes; teste de fronteiras de assemblies aprovado |
| Fontes WORLD/apresentação | PASS | git diff vazio em Assets/_Project/World e Assets/_Project/Presentation |
| Formatação Git | PASS | git diff --check sem erros |
| Build/player/outras plataformas/performance | NOT_RUN | Fora desta validação; sem alegação de throughput ou determinismo entre plataformas |

Resultados retornados pelo MCP e snapshots: [mcp-results.json](mcp-results.json). Os totais finais incluem integralmente a suíte anterior. As nove verificações EditMode novas cobrem criação/destruição, componentes, identidade/inputs, isolamento, passo fixo, movimento/regiões, replay por partição de frame/ordem de inserção, amostras do terreno, Burst e referências de assemblies (casos parametrizados incluídos no total). PlayMode acrescenta ciclo de ativação/desativação e liberação do World da demo.

## Reprodução

1. Abrir Assets/_Project/Scenes/ECSFoundationDemo.unity e entrar em Play. HUD mostra quantidade, ticks e passo.
2. Inspecionar World RTSWorld Foundation em Entities Hierarchy/Systems; LogicalPose muda X a 1 unidade lógica/s e EntitySimulationState conta updates. A consulta MCP pode obter FoundationDemo.Session, completar jobs e ler EntityManager.
3. O tick inicial de FixedRateCatchUpManager acontece em t=0. Em teste com passo 0,125: Advance(0) → tick 1; dois frames de 0,0625 → tick 2.
4. Rodar todas as suítes EditMode e PlayMode sequencialmente, sem iniciar Play enquanto EditMode executa. Burst deve estar habilitado para o teste de execução nativa.
5. Sair de Play libera o World próprio. A cena não depende de streaming, câmera RTS ou meshes.

## Incidentes e limites

- Uma desconexão transitória MCP durante polling do baseline EditMode foi recuperada; resultado do mesmo job confirmado. Avisos WebSocket durante testes/reload pertencem à infraestrutura.
- A primeira compilação do assembly PlayMode falhou com DC0061 por referência Unity.Collections ausente; corrigida antes das suítes finais. Compilação final PASS.
- A seed serializada de RTSCameraDemo mudou de 7 para 123 durante o baseline. Origem não confirmada; alteração preservada sem sobrescrever a cena ou assumir autoria. Raiz/transforms e fontes WORLD não foram alterados pela implementação ECS. Revisar essa alteração separadamente.
- Demo tem somente HUD; não renderiza unidades. Elevação ambiental é amostrada no spawn, sem navegação/seguimento do relevo. Posição global double e orientação quaternion são independentes de escala visual.
- Identidade é local à sessão; sem persistência/rede. Jobs pressupõem movimento finito dentro do domínio int de chunks. Não há contrato de determinismo entre plataformas.
- Sem benchmark de centenas/milhares; as 256 entidades são demonstração funcional, não medição de capacidade.
