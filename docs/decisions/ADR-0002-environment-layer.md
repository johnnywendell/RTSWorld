# ADR-0002 — Camada ambiental versionada sobre o terreno v1

**Status:** aceita no escopo autorizado da WORLD-004
**Data:** 2026-11-12

## Contexto
WORLD-004 exige clima, biomas, água e recursos preservando o gerador v1 e o streaming já existente. A autorização permite camadas adicionais versionadas; não há reversão da arquitetura definida em ADR-0001.

## Decisão
- Manter Generate/Elevation v1 e suas fixtures. Expor SampleElevation como consulta aditiva do mesmo cálculo, sem copiar o ruído base.
- EnvironmentDefinition versão1 e SeaLevel, junto a WorldDefinition, definem uma camada imutável separada: BaseTerrain permanece intacto; Surface é a topografia derivada de bacias/canais. EnvironmentSample guarda temperatura, umidade, bioma, nível/tipo de água. Não repurposear os enums de reserva de TerrainSample v1.
- Clima por campos globais com canais saltados independentes; temperatura sofre resfriamento por elevação. Classificação é estática e determinística no ambiente validado.
- Hidrologia inicial analítica: duas bacias por região global256x256, níveis derivados de mínimos amostrados no entorno e limitados pelo mar. Rio com meandro conecta o nível mais alto ao mais baixo, com patamares nas entradas dos lagos. Escavação e bermas existem apenas na camada adicional. Não elevar bermas de rio dentro de lago já submerso.
- Oceanos são a máscara de submersão no nível do mar, não uma análise mundial de conectividade. Lagos/rios terminam em bacias ou região ao nível do mar; não é uma rede hidrológica continental, não há confluências nem simulação física.
- Um candidato de recurso por célula lógica half-open, jitter interno e identidade estrutural (seed, versão ambiental, X/Z), sem colisão de hash de identidade. Probabilidades por bioma/terreno e exclusão de água/declives. A posição vertical usa a mesma diagonal triangulada do terreno.
- Renderizar terreno colorido, água recortada nas margens e recursos low-poly em uma mesh por chunk, com material compartilhado de cores por vértice URP. Água opaca, sem reflexão/refração; geometria simples, sem animação, colisores ou interação.
- Integrar ao ChunkManager por referência opcional EnvironmentPresentation; ausência mantém caminho anterior. Alterações de configuração/revisão invalidam os chunks; budget e ownership permanecem os da WORLD-003. Sem cache ilimitado, objeto por árvore, dependências novas ou sistemas de gameplay.

## Consequências e limites
- Compatibilidade v1 testada; cenas antigas e contratos preservados. O domínio continua sem UnityEngine; apresentação não define estado autoritativo.
- Canais podem formar cânions e lagos ter margens esculpidas. É topografia procedural inicial coerente com sua superfície de água, sem prometer erosão realista ou drenagem global.
- Identidade reproduzível exige a definição completa (seed, versões, dimensão e nível do mar); futuras mudanças de saída devem evoluir a versão ambiental. Identidade entre plataformas ainda não garantida por uso de floating point/Math.Sin/Math.Cos.
- Agrupamento reduz objetos/draws, mas duplica vértices para low-poly; o custo de um chunk é síncrono e pode exceder orçamento. Configurações máximas/player/heap/VRAM não benchmarkados. Recurso lógico não equivale a estoque coletável ou persistido.
