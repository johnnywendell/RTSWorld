# Architecture — Working Baseline

> Status: direção arquitetural; contrato de dados do terreno v1 implementado e testado em EditMode. Demais sistemas e desempenho ainda não validados.

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
