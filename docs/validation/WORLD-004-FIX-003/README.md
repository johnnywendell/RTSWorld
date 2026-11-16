# WORLD-004-FIX-003 — diagnóstico e validação

## Causa comprovada

A v3 confunde o nível do corpo de água com um potencial espacial. `EnvironmentGenerator.ApplyHydrology` calcula a distância transversal ao trajeto meandrante; `ApplyField` subtrai essa distância do nível fora do núcleo/apron. Para rios: `water=max(sea, profileLevel-max(0,distance-6))`. Quando o terreno original está abaixo desse potencial, a superfície inclinada é renderizada como água. O máximo entre campos também introduz mudanças de normal em faixas. O mesh builder não inventa essas alturas.

Na seed7/ambiente3/mar38, em(-153,357): original45.13231, ambiental45.13231, água45.68278, perfil46.6090781134, distância transversal usada pelo algoritmo−6.9262950198. A fórmula produz45.6827830936. A derivada na seção de estação constante é0.9999995293. Em(-150,360), produz44.8182712876 e derivada1.0000004927. Logo a inclinação lateral não é a declividade longitudinal legítima do rio.

`directional-derivatives-complete.csv` contém27amostras das versões2/3/4: alturas original/ambiental/água, classificação, distância usada pelo algoritmo, distância euclidiana ao ponto mais próximo do eixo, vetores longitudinal/normal ortogonais e derivadas centrais dos três campos. A seção de estação constante também é registrada; ela não é exatamente perpendicular à tangente do meandro. Em(-153,357), a v3 tem derivada longitudinal−0.09496751 e normal1.01019384; a distância euclidiana é6.9064216065. Unidades lógicas: derivada=unidade de elevação por intervalo de amostragem. `DirectionalDerivativeCapture.cs.txt` reproduz a coleta via MCP execute_code.

Na v2, `ApplyContinuousBasin` usa `max(original,level+0.8)` para construir bermas e restaura o terreno por thresholds; `Evaluate` interrompe o rio emt=0/1. Seed7(64,59)→(64,60):49.83183→35.50141,uma diferença superior a14. A troca do campo local de água para o mar também pode ser interpolada num triângulo parcialmente molhado,criando rampas de água. Os testes antigos não cobriam essa condição.

Geometria da mesma região(-5,11),H2/V1.2,WaterOnly:

| Versão | Triângulos | Maior inclinação | Índices inválidos/degenerados/invertidos |
| --- | ---: | ---: | --- |
| 2 | 1249 | 69.06777° | 0/0/0 |
| 3 | 1443 | 31.34414° | 0/0/0 |
| 4 | 1243 | 8.53290° | 0/0/0 |

Exemplos localizáveis nos JSONs: v2tri759,célula(24,21);v3tri42,célula(6,4);v4tri40,célula(4,5). Cada registro inclui vértices/normais/áreas/coordenadas da célula. A ausência de índices inválidos não equivale à ausência de defeito: os valores de altura/domínios eram incorretos para uma superfície de água.

## Correção e versionamento

`ContainedHydrology` é a implementação exclusiva da versão ambiental4. Lagos têm nível constante; rios têm perfil monotônico por estações com espaçamento≤1intervalo lógico, limitado pelas alturas originais das margens e por grade longitudinal≤0.25. O perfil é constante por seção transversal,sem taper lateral. Janelas planas conectam lagos e rios no mesmo nível. Prefix minimum/backward Lipschitz envelope respeitam limites de capacidade sem elevar bermas.

O terreno derivado continua sendo mínimo entre o original e envelopes de leito: profundidade2.5,grade de margem1.25. Não se suaviza o gerador original. A mudança entre domínio local de água e mar ocorre numa faixa seca de4intervalos,maior que2diagonais de célula. Pode haver salto no atributo WaterLevel de vértices secos;ele não pode chegar a nenhum triângulo fonte molhado. As regressões verificam isso explicitamente. Não há offset vertical da água.

A grade0.25 é limite de projeto do rio estático,sem cachoeiras: emH2/V1.2 equivale a8.53°. A reconstrução triangular tem limite conservador√2×0.25. Tolerâncias de altura1e-4lógico e de mesh8×epsilonFloat32×100×V cobrem arredondamento/interpolação; não são limites escolhidos para tolerar paredes. Escalas extremas exageram ângulos físicos sem alterar o domínio.

O gerador v1 e versões ambientais1/2/3 foram preservados. `legacy-fingerprint-comparison.json` compara12chunks×3seeds com o snapshot anterior,incluindo recursos/clima/biomas. Fingerprints .NET e Unity não são prometidos iguais entre runtimes; comparação é antes/depois no mesmo runtime. Os golden tests Unity de versões1/2 e de terreno v1 permanecem.

As cenas EnvironmentDemo e RTSCameraDemo selecionam4explicitamente. Migração requer regenerar a camada ambiental e manter versão em ResourceKey;não reinterpretar IDs legados como4. Não há persistência existente a migrar. Câmera RTS,shader,material,pacotes e ProjectSettings não foram modificados por esta correção.

`EnvironmentPresentation` reutiliza o gerador por definição com cache limitado9metadados regionais,sem guardar chunks ambientais. ChunkManager mantém o mesmo ownership de mesh,renderer,upload,unload e reconstrução.

## Reprodução visual e coordenadas

Todas as capturas válidas usam Unity6000.6.9f1/URP real,acionado via MCP HTTP. `HydrologyDiagnosticSession.Capture` cria um replay temporário no Editor com os componentes de produção EnvironmentPresentation/ChunkManager,carrega25chunks,confirma fila0 e a mesh central antes de renderizar. A câmera captura somente essa residência isolada por layer31;ao terminar,todos os objetos/meshes temporários são liberados. Não salva mudanças na câmera/observador da cena original. Não é imagem sintética nem teste manual humano.

Cada cenário tem Combined/OriginalTerrain/EnvironmentalTerrain/WaterOnly no mesmo enquadramento. `*-capture.txt` registra cena,Play,seed,versão,mar,escalas,pose,centro e todas as coordenadas residentes. JSONs por camada permitem localizar células e triângulos. A captura inicial vazia `before-historical-pose-v3.png` é INVÁLIDA para reprodução/aceite: os chunks não estavam residentes naquele checkpoint;foi preservada como registro da limitação anterior.

Cenário A:seed7,N32,H2,V1.2,raio2,mar38. `env-before-v[23]-2-1-*` e `env-editor-v4-2-1-*` mostram a parede/faixas antes e depois. `env-play-*` repete com EnvironmentDemo emPlay. O corte abrupto do fim do rio é substituído por leito contínuo até o lago receptor;permanecem as montanhas originais.

Cenário B:pose mundial(-301.94,153.09,673.45),pitch55/yaw45. Zoom histórico não foi fornecido;ortho48 é enquadramento explícito de replay a partir do zoom inicial24×H2,não um valor histórico inventado. O arquivo da cena no baseline inicial tinha raiz(0,0,0);`scene-frame-baseline-comparison.json` prova a reconstrução porSHA256. Nessa configuração,o defeito é reproduzido à esquerda do raio central,no chunk(-5,11). O raio central intercepta terreno emlogical(-127.98717,359.70783),chunk(-4,11);isso é inferência geométrica, não o foco histórico do controlador.

A raiz atual da RTSCameraDemo está emX=−5807 e foi preservada. O observador mundial0 corresponde então a um centro lógico próximo de(90,0),explicando a ausência de residência na região negativa nas capturas iniciais. A mesma pose histórica com esse frame aponta para logical(2779.14030,363.33530),chunk(86,11),oceano no mar38. Essa região também foi capturada emv2/v3/v4,sem fingir que se trata do mesmo local lógico. `camera-ray-intersections.jsonl` registra ambos os frames. Não atribuir o frame ou zoom atuais à captura histórica sem evidência.

Reprodução por MCP execute_code,com a cena desejada aberta:

```csharp
return RTSWorld.Presentation.Editor.HydrologyDiagnosticSession.Capture(4, 2, 1, "manual-A");
return RTSWorld.Presentation.Editor.HydrologyDiagnosticSession.Capture(4, -5, 11, "manual-B-baseline");
return RTSWorld.Presentation.Editor.HydrologyDiagnosticSession.Capture(4, 86, 11, "manual-B-current", 7, 38, 2, 1.2f, -5807);
```

Executar uma chamada de cada vez;todas descartam o replay no fim. Para comparar legados,substituir4por2ou3. Para uso normal,basta abrir as cenas e entrar emPlay;Inspector→EnvironmentPresentation→GeometryView seleciona a camada e força reconstrução. Desativar o componente revela o terreno original,mas não prova qual camada causou um defeito.

Variações capturadas:seed7/mar0/H0.5/V3,seed12345/mar38/H4/V0.75/chunk(3,-5),seed67890/mar55/H2/V1.2/chunk(5,5). A primeira exagera as montanhas porV/H=6;comparar OriginalTerrain para distinguir relevo legítimo de paredes introduzidas. Na seed7,o pequeno afloramento próximo de(82,69)já tinha altura original38.46340,ambiental38.43061,água38;`natural-island-check.txt` demonstra que não é berma elevada. Não remover picos legítimos por suavização indiscriminada.

## Testes e limites da validação

Resultados completos ficam em editmode-final.json e playmode-final.json. A execução inválida causada pela troca prematura paraPlay é documentada em validation-sequence-incident.txt; não conta como PASS. Rodadas iniciais com falhas foram preservadas. O antigo teste de potencial contínuo agora fixa explicitamente v3;novos testes v4 verificam domínio seco/triângulos molhados em vez de alargar os limites do legado.

Regressões incluem determinismo,recursos,biomas,base inalterada,coordenadas positivas/negativas,fronteiras de chunks/regiões,perfil longitudinal,seção transversal,lagos/oceanos,índices,orientação/degeneração,meshes em várias escalas e reconstrução exata após unload/reload no chunk(-5,11). O cenário atual(86,11)também tem casos explícitos. A câmera consulta versões1/2/3/4.

Medição local do teste Play de8waypoints,N32/raio1/max1chunk/frame/budget4ms/ambiente4:48amostras,mediana1.3669ms,p953.0432ms,máximo4.5793ms para Step. O chunk continua atômico e pode exceder o orçamento. Isso não é promessa deFPS,benchmark de player ou medição de heap/VRAM.

Modelo estático analítico,sem drenagem continental ou hidrodinâmica. Facetas low-poly,promontórios e ilhas legítimas permanecem. Testes e inspeção cobrem seeds/locais registrados,não todas as coordenadas possíveis. Build/player e outras plataformas:NOT_RUN. Esses limites não substituem os critérios obrigatórios de geometria,streaming,Console e inspeção nas duas cenas.


## Resultado final

| Verificação | Estado | Evidência |
| --- | --- | --- |
| Compilação Unity 6 / URP | PASS | Ferramentas executadas no Editor; shaderError=False |
| EditMode completo | PASS | 172/172, 0 falhas/ignorados, 35.9704167 s |
| PlayMode completo | PASS | 6/6, 0 falhas/ignorados, 1.8620796 s |
| EnvironmentDemo em Editor/Play | PASS | Cenários A/B, camadas separadas, residência verificada |
| RTSCameraDemo em Editor/Play | PASS | A/B, frame inicial e frame atual X=−5807 |
| Variações de seed/mar/escalas | PASS | Capturas env-variant-* e regressões |
| Console das cenas em Play e Editor final | PASS | Zero erros/avisos nos três JSONs de Console |
| Limpeza e preservação | PASS | 25 meshes/25 chunks, fila vazia, nenhum replay; 38 protegidos idênticos |
| Build/player e outras plataformas | NOT_RUN | Sem alegação de validação |

Foram produzidos vinte conjuntos de replay com quatro camadas (80 imagens válidas), duas capturas das câmeras originais e uma captura inicial vazia, preservada como inválida. A auditoria final verifica todos os conjuntos com 25 chunks residentes/fila vazia, os testes finais, os GUIDs, os arquivos protegidos e os quatorze relatórios WaterOnly da v4. O Editor foi deixado na RTSCameraDemo, fora de Play, sem alterações de cena pendentes e com a translação X=−5807 preservada. `final-audit.json` registra PASS.

A rodada interrompida por erro de sequência do agente foi descartada; a rodada final de 172 casos terminou antes da entrada em Play para inspeção. Nenhum resultado intermediário ou captura vazia foi utilizado como aceite. Não foram criados commits nem iniciadas outras tarefas.
