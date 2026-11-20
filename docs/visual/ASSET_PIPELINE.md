# Pipeline de assets

## Modelos e importação

Para novos assets, exportar FBX de ferramentas DCC e manter fonte editável fora dos artefatos de build. Para texturas, PNG ou TGA conforme necessidade de alpha. Registrar origem, autor, licença e modificações no repositório antes de importar. A demonstração atual usa malhas procedurais próprias e não incorpora modelos ou imagens externos.

Usar Y para cima, +Z para frente e 1 m por unidade Unity. Pivô de personagem na planta dos pés. Pivô de árvore no centro do tronco junto ao terreno. Prédios têm pivô na base, no centro do footprint. Manter escala de importação unitária e confirmar dimensões no Editor. Nomear assets por categoria e variante, por exemplo `CHR_Villager_A_LOD0`, `BLD_Depot_A` e `ENV_Oak_Tall`. Manter GUIDs durante revisões.

## Materiais, LOD e animação

Compartilhar materiais URP por família. Preferir UVs de paleta ou atlas pequenos para variantes de cor. Usar os mesmos materiais nos LODs sempre que possível. Medir submeshes, triângulos, vértices e draw calls junto do custo de preenchimento. O protótipo usa faces e normals explícitas, textura 22 × 1 e um URP Lit. A mesh procedural é criada ao iniciar a demo e destruída ao sair.

Usar pose visual a partir dos dados ECS. Os frames Idle/Walk são malhas pré-calculadas do protótipo. O futuro rig deve alinhar raiz e pivô, com clips separados e transições exclusivamente de apresentação. Movimento da raiz visual não escreve no ECS. Um LOD distante pode conservar pose neutra, equipamentos reconhecíveis e silhueta reduzida.

## Integração

A projeção da apresentação lê pose, orientação, arquétipo, proprietário e atividade. O backend resolve malha, pose e grupo de submissão. A existência do visual é deduzida do snapshot atual. Morte, troca de sessão e Load refazem essa projeção. Vegetação de referência lê ResourceKey e residência de chunk. A disponibilidade é a do catálogo de simulação.

Futuras tarefas podem trocar a biblioteca procedural por malhas importadas ou testar animação nativa por GPU, conservando o contrato de snapshot e os benchmarks comparáveis.

## Continuidade dos protótipos no terreno

A aplicação coloca o pivô de construção na altura máxima amostrada no footprint. Geometria rígida acima desse apoio preserva a orientação vertical. A fundação de pedra preenche até o menor apoio e compartilha o material da biblioteca para cobrir desníveis. Para árvores e rochas, conservar pivô, material, proporções e transformação ao simplificar a malha. A base do tronco usa a altura triangulada do placement. A base facetada da rocha pode penetrar ligeiramente no solo.

## Biblioteca ambiental gerada em Unity

A VISUAL-002 gera modelos originais C# com material URP Lit compartilhado e atlas de cores. O gerador usa pivô na base, eixo Y para cima e variações vinculadas à chave global do placement. Ao trocar o LOD, mantenha transform, paleta e volumes identificáveis. Árvores, rochas, ouro e decorações compartilham a mesma submissão instanciada. A geração decorativa lê dados ambientais por chunk e não integra a persistência lógica.
