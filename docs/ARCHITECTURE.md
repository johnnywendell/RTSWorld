# Architecture — Working Baseline

> Status: direção arquitetural; contrato de dados do terreno v1 implementado e testado em EditMode. Apresentação low-poly inicial validada em Editor/Play; demais sistemas e desempenho ainda não validados.

## Boundaries
1. **World domain:** seed, generator version, logical global coordinates, chunk identifiers, terrain/biome/resource data.
2. **Simulation:** units, commands, economy, combat, construction, navigation; independent of cameras and rendering.
3. **Presentation:** URP, low-poly meshes, scene streaming, camera, HUD and input; consumes simulation state.
4. **Networking:** authoritative server, validated commands, relevance/interest filtering and state replication.
5. **Persistence:** mutable chunk state, dynamic entities, snapshots and recovery; never rely on seed alone.

## Initial conventions
- Logical world plane uses X/Z; Y is elevation in the 3D view.
- Procedural output must be stable for a recorded seed **and generator version**; test determinism rather than assume cross-platform equivalence.
- Client visual chunks can unload without deleting authoritative world state.
- ECS/DOTS is the preferred direction for high-volume simulation, but package selection and implementation must be validated against the installed Unity version.
- Group navigation may combine hierarchical routing and flow fields; benchmark before committing to one algorithm.
- Network and persistence delta formats are distinct concerns.

## Proposed source organization (create only when needed)
`Assets/_Project/Core`, `World/Generation`, `World/Chunks`, `World/Biomes`, `Units`, `Buildings`, `Networking`, `Persistence`, `UI`, `Scenes`, `Tests`.

## Risks to resolve early
- Unity Entities/Netcode for Entities compatibility with installed editor.
- Headless server build/runtime on target environment.
- Numerical stability of world generation.
- Global coordinate precision for large worlds.
- Server CPU and bandwidth scaling with active units.

## Verification
Architecture changes require an ADR in `docs/decisions/` and an explicit record of approval if they materially change the agreed design.

## Terreno implementado — WORLD-001
- Assembly `Assets/_Project/World/RTSWorld.World.asmdef`: C# sem referências ao engine (`noEngineReferences=true`). Dados e geração em namespaces Chunks e Generation; sem câmera, cena ou estado de UnityEngine.Random.
- `WorldDefinition` imutável guarda Seed (int, inclusive negativo), GeneratorVersion (apenas 1 suportada) e CellsPerChunk (1..256, padrão 32). Todos são parte da definição reproduzível; versão desconhecida é rejeitada.
- `ChunkCoordinate` possui índices int X/Z; `GetSampleCoordinate` calcula long X/Z por `chunk * N + local`, com passo lógico unitário. N células possuem N+1 amostras por eixo, índices locais inclusivos 0..N. Limites int de chunk continuam válidos sem overflow na conversão para long. GlobalCoordinate é lógico, não uma posição float de Unity.
- `TerrainChunkData` guarda definição, coordenada e grade privada com cópia defensiva; indexador [x,z], X varia primeiro. `TerrainSample` imutável contém Elevation float, BiomeId e ResourceId. Na v1 os últimos campos são sempre Unassigned/None; classificação e distribuição futuras não foram implementadas.
- Gerador v1 stateless: hash unsigned de 64 bits com wraparound explícito, ruído de valor, interpolação quintic e quatro oitavas (períodos 64/32/16/8, pesos 1/0.5/0.25/0.125). Altura normalizada em [0,100]. Divisão floor trata coordenadas negativas. Toda avaliação usa coordenadas globais; bordas/cantos coincidentes têm os mesmos dados, independentemente da ordem de geração ou partição em chunks.
- O algoritmo, constantes e parâmetros fazem parte da versão. Mudanças no resultado exigem nova versão e manutenção do caminho antigo quando necessário. Identidade do chunk inclui a definição do mundo e sua coordenada, não apenas X/Z.
- Determinismo bit a bit verificado no Unity 6000.6.9f1/LinuxEditor pelos 29 testes da assembly RTSWorld.World.Tests. Interpolação usa double e saída float: não há promessa de identidade entre plataformas, runtimes ou Burst sem validar nesses alvos.
- Geração síncrona com alocação por chunk; não constitui streaming, persistência, simulação ECS ou otimização medida.

Exemplo sem objetos de cena:
```csharp
var definition = new RTSWorld.World.Generation.WorldDefinition(seed: 12345);
var generator = new RTSWorld.World.Generation.TerrainGenerator();
var chunk = generator.Generate(definition, new RTSWorld.World.Chunks.ChunkCoordinate(-1, 0));
float elevation = chunk[32, 0].Elevation; // amostra global (0,0), borda leste
```

## Apresentação do terreno — WORLD-002
- `Assets/_Project/Presentation` contém assembly com referência ao domínio; o domínio continua sem referências ao engine. Nenhum arquivo do gerador v1 foi alterado.
- `TerrainGeometry.Build(TerrainChunkData, horizontalScale, verticalScale)` aloca buffers finais de posições, normais e índices; não cria Mesh/GameObject nem gera ruído. Escalas finitas em [0.001,10000], X/Z horizontais e Y vertical, sem alterar dados procedurais.
- Cada célula possui dois triângulos com diagonal e winding constantes, seis vértices independentes e normais planas geométricas. Faces duplicam vértices intencionalmente para low-poly. Não há média local de normais na borda: uma partição em chunks produz as mesmas normais por face que uma grade contínua. Diferenças entre faces vizinhas são as facetas esperadas, sem política especial na fronteira do chunk.
- `TerrainMeshFactory.Create` adapta buffers para uma mesh, define UInt16/UInt32 conforme contagem e calcula bounds. O chamador é responsável por destruir a mesh. Uma mesh e um renderer por chunk; nenhuma entidade por célula.
- `TerrainDemo` é apenas um demonstrador fixo 2x2 em chunks (-1,-1), (0,-1), (-1,0), (0,0). Rebuild explícito, sem Update, cache/streaming ou simulação. Usa exclusivamente TerrainGenerator para obter dados. Coordenadas de apresentação locais ao root perto da origem; não implementa floating origin para coordenadas distantes.
- Objetos e meshes gerados são temporários DontSave e reconstruídos em OnEnable (Editor e Play) a partir da definição serializada. Em rebuild/disable, libera apenas objetos/meshes próprios; em Play desativa antes de destruir ao fim do frame. Um material compartilhado persistente, sem instanciar material por chunk. Dados CPU permanecem disponíveis para verificação; sem otimização/reuso de buffers ou desempenho prometido.
- Cena `Assets/_Project/Scenes/TerrainDemo.unity`: seed12345, N32, escala horizontal2/vertical1.2, quatro chunks adjacentes, câmera ortográfica de demonstração e luz direcional. Material `Assets/_Project/Materials/TerrainSage.mat` usa Universal Render Pipeline/Lit. Não é câmera RTS definitiva.
- Verificação: 44 testes EditMode (29 domínio +15 apresentação), renderização capturada e inspecionada no Editor/Play, duas seeds. Evidências em docs/validation/WORLD-002 e tarefa completed.

## Residência visual dinâmica — WORLD-003
- `World/Chunks/ChunkSelection` é lógica pura sem engine: posição lógica X/Z dividida pelo número de células com floor (negativos incluídos); janela quadrada de raio Chebyshev0..8. `Fill` reutiliza lista e ordena anéis próximos primeiro, sem duplicatas e sem overflow nos extremos int (recorta a janela).
- `Presentation/Streaming/ChunkManager` converte observador via InverseTransformPoint e escala horizontal; seleciona somente quando muda centro ou raio. Dicionário ativo e fila de faltantes mantêm sobreposição, cancelam requests antigos e removem referências aos descarregados. Sem cache ilimitado, geração duplicada ou objetos por célula.
- Mudanças de seed/N/escalas/material invalidam a apresentação; raio altera só o conjunto necessário. Dados vêm exclusivamente de TerrainGenerator e são consumidos por TerrainGeometry/TerrainMeshFactory; gerador v1, seus contratos e TerrainDemo preservados. TerrainChunkData e buffers geométricos não ficam retidos pelo manager após upload; uma mesh e um objeto por chunk ativo, material compartilhado.
- `Step` gera até MaxChunksPerFrame (1..8), verificando GenerationBudgetMilliseconds(0.1..50ms) entre chunks. Um chunk é atômico e o primeiro progride mesmo se ultrapassar orçamento. Seleção/unload também são síncronos; não é teto rígido de frame. Contadores e tempos incluem gerenciamento, geração e upload. Sem processamento assíncrono, pooling, Jobs ou LOD antecipados.
- Editor usa EditorApplication.update registrado/desregistrado no ciclo do componente; Play usa Update, sem tick duplo. OnDisable remove callback e libera somente recursos próprios. Em Play, desativa objeto e retira referência do MeshFilter antes de Destroy diferido até fim do frame; em Editor destrói imediatamente. Coleções reutilizam capacidade limitada, não referências a chunks antigos.
- Manager controla residência visual, não autoridade/estado mutável. Unload não representa remoção de entidades da simulação. Ainda não há estado persistente ou simulação a preservar; revisitas reconstroem terreno base determinístico. Seleção pura e upload separado permitem introduzir scheduler/async mais tarde sem duplicar ruído.
- Demo independente `Assets/_Project/Scenes/StreamingDemo.unity`: observador editável, câmera com offset fixo apenas para teste, controles IMGUI X/Z/seed/raio/contadores. TerrainDemo continua disponível. Coordenadas visuais são float próximas à origem; floating origin e precisão distante ainda não implementados. Buracos temporários enquanto fila está pendente são carregamento incremental esperado.
- Evidência:64 EditMode (44 anteriores+20 novos) e2 PlayMode, percurso12 posições com70 gerações e residência9 meshes após estabilizar; liberação retorna ao baseline. Medidas locais do Editor em docs/validation/WORLD-003/release-test-results.json, sem promessa de player/FPS ou de configurações máximas.

## Camada ambiental — WORLD-004
- Decisão em docs/decisions/ADR-0002-environment-layer.md. World/Environment separa EnvironmentDefinition(version1,SeaLevel), EnvironmentChunkData, campos climáticos, biomas, hidrologia analítica e ResourcePlacement/ResourceKey. BaseTerrain intacto, Surface derivada; TerrainSample e enums v1 não alterados.
- TerrainGenerator.SampleElevation é API aditiva chamando Elevation v1 existente. Generate e constantes não mudaram; fixtures e testes anteriores passam. Todos os inputs de reprodução incluem definições completas, não apenas seed isolada.
- Campos globais de temperatura/umidade e resfriamento por altura; dez biomas estáticos. Oceanos abaixo do mar, lagos/bacias e rios descendentes globais dentro de regiões256x256, cruzando chunks sem depender de vizinhos carregados. Canais/bermas esculpem apenas Surface. Sem erosão, confluências ou simulação hidrodinâmica.
- Recursos em células half-open com IDs estruturais e jitter interno; densidade por bioma e restrições de água/declive. Altura respeita triangulação. Dados imutáveis sem relação com GameObjects; nenhuma coleta/persistência implementada.
- EnvironmentMeshBuilder reutiliza TerrainGeometry(Surface), adiciona cores, recorta água contra profundidade e agrupa árvores/pedra/ouro. Uma mesh e renderer por chunk/material compartilhado, sem objeto por recurso. Shader Environment Colors URP com iluminação simples, água opaca e sem sombras/reflexos dedicados.
- ChunkManager tem EnvironmentPresentation opcional e invalidação por revisão/material/configuração. Path sem camada preserva demos/testes anteriores. Ownership/unload gerencia a única mesh completa; nenhuma camada ambiental de dados permanece retida pelo manager após upload. Cache de bacias do gerador limitado a9 metadados.
- EnvironmentDemo.unity preserva TerrainDemo/StreamingDemo. Landmarks para seed12345/mar38 demonstram biomas/água/recursos; controles de streaming anteriores reutilizados, sem implementar WORLD-005.
- Validação final:94 EditMode e3 PlayMode, capturas reais Editor/Play, shader sem erros. Percurso ambiental N32/r1/max1/budget4ms teve Step mediana1.0838ms/P954.1474ms/máximo7.1368ms no Editor validado. Não é teto rígido nem garantia de FPS/player; ver docs/validation/WORLD-004/release-test-results.json e tarefa.

## Correção de margens — WORLD-004-FIX-001
- Terreno/ruído v1 permanece intacto. `EnvironmentDefinition` aceita versões1 e2; na conclusão da FIX-001 a atual era2 e a cena EnvironmentDemo registrava-a explicitamente. Para reproduzir os dados ambientais anteriores, fornecer `version: 1`; seed isolada não identifica a versão. Os IDs de recursos continuam incluindo a versão ambiental.
- Ambiente v2 alarga a transição interna da margem de raio normalizado0.8 até1.8. A crista seca permanece até2.3 e o terreno base é restaurado suavemente até3; o corte do nível local ocorre sobre terreno seco, evitando interpolar diretamente lago/rio até o mar em uma célula molhada. O raio nominal do canal é4; a faixa seca tem ao menos2 unidades lógicas. A influência dos lagos fica contida nas regiões256x256 existentes. O rio mantém patamar de nível durante a influência completa dos lagos antes de descer.
- A mesh de apresentação representa a superfície visível opaca: cada triângulo é recortado em terreno seco e água pelo mesmo campo de profundidade, diagonal e interseção canônica. Ambos compartilham posições de margem sem offset. Faces submersas não são emitidas nessa mesh; `BaseTerrain`/`Surface` e alturas dos dados continuam disponíveis e não dependem da câmera. Mergulho/transparência exigirão outra apresentação, fora do escopo atual.
- Uma mesh/renderer/material compartilhado por chunk e ownership do streaming preservados. `EnvironmentPresentation` guarda a versão e invalida a apresentação por revisão ao mudá-la. Nenhuma dependência nova ou mudança de fronteira arquitetural.
- Taludes altos e facetas low-poly permanecem possíveis em terrenos inclinados; não há erosão/hidrodinâmica nem garantia de perfil de vale realista para todas as seeds. Evidências e testes da correção ficam em `docs/validation/WORLD-004-FIX-001` e na tarefa correspondente.

## Continuidade do relevo hidrológico — WORLD-004-FIX-002
- Na conclusão da FIX-002,a versão ambiental atual era3;dados1/2continuam disponíveis sem reinterpretar suas saídas. A cena EnvironmentDemo seleciona3explicitamente. O gerador de terreno v1 continua intacto. Os registros anteriores de aceite não cobriam a parede reproduzida na seed7/v2.
- V3 usa campos analíticos contínuos:superfície derivada=min(terreno base,envelopes de lago/rio). Cada envelope parte de nível−2.5num núcleo plano e cresce com grade1.25fora do núcleo. Assim a margem encontra o relevo original a uma distância proporcional à profundidade do corte;nenhuma berma eleva terreno ou desfaz outro corte. Não é filtro/suavização do ruído base.
- O trajeto meandrante do rio usa projeção contínua com pontas arredondadas,sem corte booleano emt=0/1. Lagos receptores ao nível do mar também contribuem com o envelope. A composição elimina o booleano insideLake da v3e a aplicação sequencial que reerguia margens.
- A água é o máximo de potenciais contínuos e do mar:nível constante no núcleo do corpo;fora de um pequeno apron o potencial decai com grade1até o mar. Isso permite conexão a áreas naturalmente baixas sem construir barragens de terreno. O rio mantém sua variação longitudinal. A borda externa pode ser inclinada quando não contida pelo relevo;é modelo analítico estático,sem equilíbrio hidrostático,drenagem continental ou simulação física.
- Influências compactas são avaliadas pelas coordenadas globais em até3x3bacias regionais vizinhas,com broadphase espacial e cache de metadados limitado às9entradas existentes. O suporte máximo é122unidades(<256),derivado do intervalo de altura[0,100]. Não há dependência de chunks carregados nem uma passagem de suavização local que crie costuras.
- As grades máximas adicionais são limitadas:envelope<2.25e potencial de água<2por unidade lógica,incluindo variação do nível e derivada do meandro. Min/max preservam esses limites;declives já presentes no terreno base podem legitimamente excedê-los. Escalas de apresentação mudam o ângulo visual,sem alterar os dados. Estes limites são verificados em múltiplas seeds/mar e fronteiras regionais.
- O recorte visual complementar da FIX-001 permanece byte a byte. Uma tangência da água a um único vértice de fronteira pode existir só em um chunk,sem segmento de água no vizinho seco. Regressões comparam segmentos de comprimento positivo e a superfície completa,em vez de exigir o mesmo conjunto de vértices marcados como água em contatos de área zero.

## Câmera RTS — WORLD-005
- Decisão em ADR-0003. `Presentation/Cameras` contém assembly `RTSWorld.Camera`,com referências a World,Presentation e Unity.InputSystem já instalado. Nenhum pacote ou sistema de entrada legado adicional.
- RtsCameraInput traduz teclado/mouse em frame imutável e respeita foco da aplicação,bloqueios de UI e exclusão de painel. RtsCameraMotion integra foco lógico double,velocidade,zoom e yaw sem conhecer dispositivos/cena/geração. RtsCameraController aplica projeção ortográfica e pose;CameraStreamingObserver adapta foco/enquadramento ao observador do ChunkManager.
- Velocidade em unidades lógicas/segundo real,aceleração/desaceleração como taxas de resposta exponencial. Integração exata para comando constante;arraste usa pixels uma vez,sem multiplicar por deltaTime;zoom suavizado/limitado e Q/E opcional. Inclinação20..80graus. InputReader é substituível e possui bloqueios de teclado/ponteiro para UI/seleção futura.
- WorldSurfaceQuery é adaptador de apresentação:same diagonal dos triângulos,max de terreno/água,sampling global sem carregar chunks. Cache de configuração reage a seed/dimensão/camada/revisão. ChunkManager ganhou apenas getters de escala vertical,dimensão e camada;sem alterar seleção/geração/ownership.
- O limite superior101×escala vertical é conservador para as versões atuais do terreno/ambiente. A distância da câmera preserva foco/ângulo/zoom até todo near plane ficar acima desse limite+folga. Far plane calculado para incluir o solo mínimo no canto superior da vista,mesmo com foco alto e inclinação baixa. Não depende de colisores ou de chunks já gerados.
- Ponte executa após controlador e antes do manager. Raio acompanha aspecto,zoom,yaw,inclinação e faixa de altura,dentro do máximo8existente. Foco estável não causa regeneração;mudanças de enquadramento podem legitimamente alterar a residência visual.
- Foco double e guarda de navegação configurável(default±32768lógicos),com conversão float apenas na apresentação. Frame vertical com translação,yaw e escala uniforme positiva;sem floating origin. Não confundir limite visual com limite do mundo/autoridade. Configurações que excedem raio8sinalizamCoverageLimited;demo normal permanece dentro do limite.
- Nova RTSCameraDemo.unity contém camada ambiental3,consulta,rig,foco e painel de controles. As três cenas anteriores permanecem disponíveis sem mudanças. Controle da câmera não altera a versão ou o estado do mundo.

## Hidrologia contida — WORLD-004-FIX-003

- Versão ambiental atual4. `TerrainGenerator` v1 e versões ambientais1/2/3 preservadas;comparação de fingerprints inclui amostras,clima,biomas e recursos. As duas cenas ambientais selecionam4explicitamente. Seeds sem versão ambiental não identificam a mesma saída.
- Causa comprovada:na v3,`ApplyField` mistura nível e distância transversal(`level-max(0,distance-wetRadius)`),criando água inclinada onde o relevo não contém o nível escolhido. Na v2,bermas de`ApplyContinuousBasin`,corte booleano de pontas e troca do campo local para o mar ainda produzem paredes/triângulos de água. O recorte de mesh interpola fielmente esses dados;índices/normais não são a causa primária.
- `ContainedHydrology` implementa exclusivamente v4 no domínio World,independente da câmera/streaming. Cada região256tem lagos constantes e um trajeto meandrante. A capacidade dos lagos vem do mínimo do terreno original numa faixa anular amostrada em coordenadas globais;nenhuma berma eleva terreno. O leito é mínimo do terreno original e envelopes de núcleo plano,profundidade2.5 e grade1.25.
- O perfil de rio usa estações com espaçamento≤1intervalo lógico. Vértices das margens limitam os dois nós da interpolação;um envelope monotônico para jusante e Lipschitz para montante limita a grade a0.25unidade de elevação/intervalo lógico. Janelas planas nas duas pontas incluem os lagos e suas faixas secas;níveis dos lagos são ajustados ao perfil para não introduzir degraus. `RiverReach.LevelAt` interpola o perfil v4;sem perfil usa exatamente a fórmula legada.
- A água de um lago é constante;água do rio depende da estação longitudinal,sem taper transversal. Fora do domínio usa o mar;essa troca ocorre num buffer seco de4intervalos,maior que2diagonais de célula. Pode haver salto num`WaterLevel` de amostra seca,mas nenhum triângulo molhado pode interpolar domínios incompatíveis. Testes verificam o triângulo fonte inteiro,além da mesh recortada. Oceanos permanecem no mar;um canal ao nível do mar é classificado Ocean.
- Unidades:grades2.5/1.25/0.25são do domínio lógico,não metros presumidos. ParaH2/V1.2,a grade longitudinal máxima corresponde a8.53°. Na reconstrução triangular,o limite conservador é√2×0.25. A tolerância de altura em testes é1e-4lógico,derivada de8erros relativos float32 a100;testes de mesh escalam a tolerância porV. Escalas extremas podem exagerar visualmente declives legítimos;nenhum clamp do terreno base é aplicado.
- Domínios/capacidades/perfis são globais e particionamento de chunks não muda resultados. Cache limitado9metadados regionais;`EnvironmentPresentation` reutiliza gerador por seed/versão/N/mar,sem reter dados de chunks. Manager continua dono de uma mesh/renderer porchunk,com o mesmo unload/rebuild. Não há novo pacote,rede,simulação física ou dependência de estado visível.
- Instrumentação opcional:`EnvironmentGeometryView` separa Combined/OriginalTerrain/EnvironmentalTerrain/WaterOnly e invalida apresentação por revisão. `EnvironmentGeometryDiagnostics.Capture` registra coordenadas globais/locais,amostras,índices,vértices,normais,áreas e grades sob demanda. `HydrologyDiagnosticSession` é ferramenta exclusivamente Editor com replay porChunkManager,checagem de residência,capturas URP e limpeza;não altera a câmera ou o observador da cena original.
- Migração:selecionar explicitamente versão4 e reconstruir chunks ambientais. `ResourceKey.Version` muda,assim como posições/biomas condicionados à hidrologia;não reinterpretar IDs1/2/3 como4. Não há persistência implementada a migrar;uma futura persistência deverá reter a definição completa ou migrar entidades explicitamente. Legados continuam disponíveis para reprodução.
- Limites:modelo analítico estático,não drenagem continental/hidrodinâmica. Promontórios/ilhas do relevo original e facetas low-poly legítimas permanecem;nenhuma suavização global. Validação visual é finita,com seeds/locais e condições registradas em docs/validation/WORLD-004-FIX-003.
