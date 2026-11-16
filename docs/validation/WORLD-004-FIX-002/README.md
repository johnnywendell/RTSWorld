# WORLD-004-FIX-002 — diagnóstico e validação

Execução em 2026-11-15. Unity 6000.6.9f1/LinuxEditor, URP. Git main/6a9966b; sem commits.

## Resultado da seed 7

Cenário: N32, escala horizontal2/vertical1.2, raio2, mar38, observador inicialmente(0,0), 25chunks estabilizados. A reprodução anterior usa ambiente2; a correção usa ambiente3 explicitamente. A versão2 continua disponível para reproduzir o defeito, sem alteração silenciosa dos seus dados.

| Medida | Ambiente2 | Ambiente3 |
| --- | ---: | ---: |
| Maior gradiente de Surface na janela lógica[-64,96]² |14.330421 |2.861179 |
| Maior gradiente do terreno base na mesma janela |2.861179 |2.861179 |
| Arestas com gradiente>4 induzidas pelo ambiente |149 |0 |
| Amostras elevadas artificialmente pelo ambiente |82 |0 |
| Triângulos de superfície com normalY<0.25 nos25chunks |44 |0 |
| Menor normalY de superfície nos25chunks |0.08298275 |0.497627616 |
| Triângulos degenerados nos25chunks |0 |0 |
| Área projetada total das superfícies |102400 |102400 |

Os gradientes foram obtidos por harness .NET8 compilando os fontes reais do domínio. Os triângulos/normais foram medidos nas meshes reais do Unity. As janelas e limites estão explicitados; não são máximos globais sobre todas as seeds.

## Causa e correção

- Na aresta(64,59)→(64,60), a base varia49.83183→49.20860, mas Surface2 varia49.83183→35.50141. O rio interrompia uma escavação profunda no plano t=1; o receptor no nível do mar era ignorado pelo operador de lago. A transição abrupta já existia nos dados antes da triangulação.
- A largura da deformação era fixa e independente da diferença de altura. A berma podia elevar terreno baixo sobre água; o booleano insideLake e a aplicação sequencial trocavam operadores na fronteira e podiam reerguer cortes anteriores. Não foi encontrado índice ou winding incorreto como causa da parede.
- V3 compõe envelopes contínuos de escavação por min, sem elevar terreno. A grade da margem é1.25, portanto a transição encontra a base a uma distância proporcional à profundidade. O rio tem pontas arredondadas e conecta também receptores no nível do mar. Não há filtro de suavização indiscriminado da base.
- Potenciais de água contínuos são compostos por max com o mar: núcleo plano e decaimento externo limitado. Influências de bacias vizinhas são avaliadas globalmente, com suporte compacto e cache de metadados limitado. Assim os limites regionais/chunks não cortam a deformação.
- O builder final é byte a byte igual à entrada da FIX-002. Um experimento de snap numérico foi removido porque não era a causa. A discrepância13/14vértices de água era uma tangência de profundidade exatamente0 em(160,-72), não uma fissura: o vizinho seco não precisa emitir um triângulo de água num contato isolado. O teste agora compara segmentos positivos compartilhados; a superfície completa também é verificada.

## Evidências

- `seed7-before.json` / `seed7-after.json`: amostras base/derivadas, gradientes, contagem de cortes/elevações e piores arestas. Fontes/configuração do harness ao lado como `.cs.txt`/`.csproj.txt`; caminhos absolutos devem ser ajustados ao reproduzir.
- `gradient-matrix.json`:32comparações de quatro seeds(7,12345,67890,-1), quatro mares(0,38,55,100) e versões2/3 na janela do cenário. Emv3 nenhuma elevação artificial ou aresta nova>4 nessas comparações.
- `geometry-comparison.txt`: instrumentação nativa de25meshes por versão, normais, degeneração, cobertura e contato tangencial. `Fix002GeometryReport.cs.txt` permite repetir no próprio Unity.
- `legacy-fingerprints-before/after.json`: .NET8, versões1/2 idênticas antes/depois. `legacy-unity-before/after.txt`: Unity/Mono, também idênticas antes/depois. Cada fingerprint cobre12chunks,3seeds e todos os campos/recursos. Os hashes diferem entre runtimes; não se promete identidade entre runtimes/plataformas. As fixtures automatizadas usam o baseline do próprio Unity, obtido executando o gerador anterior preservado.
- `editmode-final.xml`: PASS127/127, zero falhas/ignorados,31.7535708s. Inclui16casos de varredura de gradiente em[-32,288]², cobrindo fronteiras regionais, três arestas por célula, sem clamp das montanhas base; continuidade, determinismo, biomas/recursos, legados e25meshes da seed7.
- `playmode-final.xml`: PASS4/4, zero falhas/ignorados,0.3869893s. Inclui carregar25chunks, sair/revisitar com vértices idênticos e liberar até retornar ao baseline; regressões anteriores também executadas.
- `regression-v2-failed.xml`: os dois novos testes centrais executados selecionando explicitamente versão2 somente na cópia falham nos defeitos originais(degrau14.330421 e normalY0.083695). Os mesmos testes passam na suíte final emv3.
- `editmode-initial-failed.xml`(122/127) e `editmode-second-failed.xml`(126/127) registram as investigações, não o resultado final. Os testes estritos de nível da FIX-001 mantêm seu limiar original selecionando versão2; a versão3 possui testes de grade normalizada por distância, incluindo a diagonal√2.
- `before/`:8renders reais Editor/Play, versão2,seed7. `after/`:18renders reais Editor/Play, versão3,9enquadramentos. O mesmo enquadramento Editor/Play produziu PNGs idênticos por SHA256. Nenhuma imagem foi gerada artificialmente, retocada ou composta.
- `after/metrics.txt`:25chunks/fila0/shaderError=False em todos os renders. Foram inspecionados origem,lago,margem aproximada e rio da seed7; lago/rio12345; seed67890/mar55; seed−1/mar0; seed7/mar100. As montanhas/facetas legítimas permanecem. Não se afirma inspeção exaustiva de todas as seeds ou ângulos.
- `after/console.txt`: contagem real, sem limpar: Editor0erros/1aviso, Play0erros/0avisos. O aviso de metadado do pacote URP está em `console-warning.txt`; o pacote/cache original não foi modificado. Nenhum erro de runtime foi registrado nas capturas.

## Reprodução

1. Reabra `Assets/_Project/Scenes/EnvironmentDemo.unity` e selecione seed7,N32,h2/v1.2,r2,mar38 e **Environment Version3**. Aguarde25chunks/fila0. A versão2 preserva a falha original por compatibilidade. Mudanças feitas durante Play podem ser temporárias; confirme a versão no Inspector ao repetir.
2. Observe a região próxima a(64,60), usando coordenadas lógicas; a posição visual é multiplicada pela escala horizontal. Os landmarks existentes são referências para seed12345, não um mapa de biomas da seed7.
3. MCP localhost:8080 estava indisponível dentro e fora do sandbox. As validações foram executadas no Unity real em uma cópia isolada `/tmp/rtswater-unity-validation`. `validated-inputs.json` registra160inputs Assets/ProjectSettings iguais por hash ao workspace. O manifest temporário resolve os mesmos59pacotes locais já instalados por caminhos `file:`; nenhum pacote novo foi instalado.
4. Para repetir EditMode/PlayMode nessa cópia, execute Unity com `-batchmode -nographics -projectPath <cópia> -runTests -testPlatform EditMode`(ou PlayMode), fornecendo `-testResults` e `-logFile`. Não execute dois Unity sobre a mesma cópia simultaneamente.
5. Os utilitários `.cs.txt` devem ser copiados como `.cs` sob `Assets/Editor` somente na cópia. `WaterValidationCapture.Run`, sem `-nographics`, renderiza a cena real via URP SingleCameraRequest, estabiliza25chunks e captura Editor e Play sem salvar a cena. `Fix002GeometryReport.Run` mede a geometria; `Fingerprints` coleta dados1/2. Os utilitários não integram o runtime do jogo.

## Limitações e custo

- A hidrologia continua analítica, sem drenagem continental, erosão ou equilíbrio hidrostático. A superfície de água externa pode inclinar-se onde o relevo não contém o nível do núcleo; as transições têm grade limitada. Água opaca, recursos simples, sem gameplay novo.
- Montanhas e facetas do terreno base não foram globalmente suavizadas. Declives legítimos podem continuar existindo. Os artefatos artificiais reproduzidos da seed7 foram removidos emv3; versões antigas continuam reproduzíveis.
- Medida local batch semGPU:48amostras do percurso8posições,N32,r1,max1,budget4ms,seed12345/mar38/v3,escalas2/1.2: mediana1.2670ms/P952.9459ms/máximo11.5468ms. Nove meshes estáveis e retorno ao baseline. O novo teste r2 verifica25meshes, mas não constitui benchmark deFPS. Chunk atômico pode exceder4ms.
- Editor interativo original/MCP:NOT_RUN. Build/player,outras plataformas/configuração máxima/heap/VRAM:NOT_RUN. As verificações Editor/Play acima são do Unity real com os mesmos fontes em cópia isolada.
- Logs integrais do Editor não foram publicados: a inicialização contém informações de autenticação. Foram preservados XMLs, contagens e o aviso relevante.
