# WORLD-004-FIX-001 — evidências reais

Validação em 2026-11-15, Unity6000.6.9f1/LinuxEditor. Baseline Git main/6a9966b. Nenhum commit nesta execução.

## Diagnóstico e correção
- `diagnostic-before.txt` e `diagnostic-after.txt` foram produzidos por .NET8 compilando os fontes reais de World. O harness está em `diagnostic-before.cs.txt`, com projeto/configuração offline ao lado. Antes usa os fontes do commit6a9966b; depois usa os fontes corrigidos. O projeto de exemplo contém o caminho absoluto deste workspace; ajustar ao reproduzir. Varredura limitada seed12345/X64..223/Z-192..-1, arestas orientadas+X/+Z: salto máximo de nível entre amostras molhada/seca19.37196→0.2104034. Não é máximo global para todas as seeds.
- O perfil antigo interrompia o nível local em raio1.2, mas restaurava terreno baixo até1.5; amostras molhadas podiam tocar vértices cujo nível voltava ao mar. A malha interpolava essa queda. O offset+0.015 afastava a água da interseção real e o terreno completo continuava quase coplanar sob a água nas margens.
- Perfil ambiental2: transição0.8..1.8, crista seca até2.3, restauração até3; patamares do rio cobrem a influência completa dos lagos. Caminho1 preservado; terreno v1 intocado. A superfície visível opaca é recortada em polígonos complementares de água/terra com os mesmos vértices de margem, sem offset.

## Testes
- `editmode-final.xml`: PASS103/103,0 ignorados,3.1347382s.
- `playmode-final.xml`: PASS3/3,0 ignorados,0.2203956s. Inclui streaming, troca de seed/mar,9 meshes estabilizadas e retorno ao baseline após disable.
- `regression-before.xml`: novas regressões contra gerador/builder originais somente na cópia isolada. FAIL5/6 e PASS1/6. Três casos detectam sobreposição projetada e dois detectam saltos artificiais de nível. Isso confirma sensibilidade ao defeito; o código final passa nesses mesmos casos.
- `editmode-intermediate-failed.xml` registra92/94 na primeira implementação; `editmode-intermediate-pass.xml` registra101/101 antes da ampliação de bordas X/Z e dos ajustes finais. Não são resultados finais.
- Asserções cobrem fechamento topológico das margens, cobertura projetada, winding/normais/índices, fronteiras molhadas X/Z não vazias, gradiente em3seeds, fixture ambiental1, determinismo, recursos, biomas, terreno v1 e lifecycle. As suítes anteriores permanecem incluídas.

## Ambiente e reprodução
- MCP localhost:8080 indisponível também fora do sandbox. Testes e capturas foram executados no próprio Unity, em cópia isolada `/tmp/rtswater-unity-validation`, preservando o Editor interativo do usuário. Assets e ProjectSettings foram copiados integralmente; `source-verification.json` registra158 hashes e zero diferenças entre os inputs da cópia e do workspace no momento da validação.
- O manifest temporário resolve59 pacotes já instalados via caminhos `file:` do PackageCache original. Nenhum pacote foi instalado/atualizado no projeto real; nenhum cache original foi editado. A cópia contém apenas utilitários adicionais de captura/leitura do Console, preservados aqui como `.cs.txt`.
- Rodar Unity com `-batchmode -nographics -projectPath <cópia> -runTests -testPlatform EditMode` e depois PlayMode, fornecendo `-testResults` e `-logFile`. Não executar dois processos sobre a mesma cópia simultaneamente.
- Para capturas, colocar `WaterValidationCapture.cs.txt` como `.cs` sob `Assets/Editor` somente na cópia; rodar sem `-nographics`, com `-executeMethod WaterValidationCapture.Run`. O utilitário abre EnvironmentDemo, estabiliza9chunks/fila0 e renderiza a câmera com URP SingleCameraRequest, no Editor e em Play. Não salva alterações na cena. Os PNGs são renders reais da cena, sem retoque ou composição.
- `metrics.txt` registra cada captura com versão2,seed12345,9chunks/fila0,shaderError=False e UniversalRenderPipeline. Editor/Play visualmente inspecionados em rio,aproximação da margem,lago,costa e floresta. Sem fissuras observadas nesses contornos; não é prova exaustiva de todas as seeds/ângulos.
- `console.txt` lê as contagens reais sem limpar Console: Editor0erros/1aviso e Play0erros/0avisos. `console-warning-investigation.txt` identifica o aviso de metadado `PunctualLightSampling.urtshader.meta` do pacote URP já existente. O utilitário corrigiu suas próprias chamadas obsoletas; a leitura final em outro processo (`console-detail.txt`) retorna0erros/0avisos/0logs. Isso não constitui correção do metadado do pacote. Nenhum erro de runtime registrado durante as capturas. Logs brutos do Editor não foram copiados porque incluem dados de autenticação da inicialização.

## Limitações e medidas
- Taludes altos e facetas low-poly continuam visíveis; não há erosão/hidrodinâmica. A mesh não emite fundo submerso sob água opaca, mas os dados Surface/BaseTerrain permanecem íntegros; apresentação subaquática/transparência não implementada.
- Medida local PlayMode batch sem GPU:48amostras do percurso8posições,N32,r1,max1,budget4ms,escalas2/1.2,seed12345,mar38,v2: mediana1.2488ms/P952.9830ms/máximo12.9819ms. Chunk atômico pode exceder4ms; não comparar diretamente com medições interativas anteriores nem inferir FPS/player.
- Editor interativo original/MCP:NOT_RUN. Build/player/outras plataformas/configurações máximas/heap/VRAM:NOT_RUN. Não há garantia de identidade numérica entre plataformas.
- WORLD-005 permanece intocada. Hashes do terreno v1 e WORLD-005 estão em `results.json`; ambos iguais ao baseline.
