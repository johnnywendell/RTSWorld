# Direção artística

**Stylized Medieval RTS — Semi-Realistic Low-Poly.** A leitura parte de silhuetas simples e materiais naturais. O aldeão tem linho, avental e machado. O soldado tem capacete, armadura, lança e escudo. Tecidos azul-petróleo e vermelho-telha distinguem as duas paletas de facção. O telhado de ardósia identifica o depósito. Telhas, chaminé e bandeira identificam o centro principal. Copas facetadas sobre troncos visíveis dão escala. Coníferas e carvalhos compõem o bosque.

## Escala

Uma célula lógica da cena equivale a dois metros Unity em XZ. A altura visual de um humanoide fica perto de 1,68 m sem equipamento elevado. O tronco e os ombros medem cerca de 0,5 a 0,7 m de largura, conforme pose. A haste do equipamento militar ultrapassa a mão e o escudo amplia a silhueta lateral. Os raios lógicos continuam 0,22 para aldeão e 0,30 para soldado, equivalentes a 0,44 e 0,60 m em XZ. Os limites visuais incluem ferramentas e escudos. Acertar e selecionar continuam usando as regras do projeto.

O depósito ocupa 2 × 2 células (4 × 4 m na cena). O centro principal ocupa 3 × 3 células (6 × 6 m). Os beirais ficam no interior dessas dimensões. As portas têm aproximadamente 1,46 m, adequadas à escala de uma abertura de serviço baixa. Na malha de referência, o tronco tem diâmetro de 0,30 m na base e as copas chegam a aproximadamente 2,5–3,1 m de largura. Os troncos usam alturas nominais de 2,8 ou 4,1 m antes das copas. A rocha tem cerca de 1,4 m no eixo mais largo. A instância escala tronco e rocha pelo raio procedural, e a copa pode ultrapassar o footprint pela altura. Os recursos lógicos continuam disponíveis independentemente de mesh e residência visual.

## Paleta e luz

Madeira escura #654731, carvalho #997347, rocha #797C74, folhagem #627C39 e #405E41, terra #79614B, areia #C7B184, neve #E0E3E1, água #538A98, grama #71895B, linho #D5C6A2, aço #969B96 e facções #356C81 e #AA4E45. Os swatches do protótipo ficam em uma textura de 22 × 1. O terreno procedural conserva a própria coloração por vértice. Luz direcional quente de dia, ambiente frio mais suave e shader URP Lit nas malhas de referência. O material de referência do terreno pode receber as sombras da cena.

## Leitura em câmera RTS

A silhueta e o equipamento dão a primeira leitura, a facção a segunda. Indicadores de seleção, destino e vida mantêm o contraste da UI existente. Poucas cores em cada superfície e cortes de material grandes sobrevivem ao zoom. O LOD usa altura projetada. Na ampliação distante, remove detalhes pequenos, usa pose estática e corta o lançamento de sombra por unidade. Objetos omitidos visualmente preservam seu estado ECS.

As referências são vilas de madeira e pedra, coberturas inclinadas, vestes de trabalho em linho e escudos de infantaria medievais. Os personagens, árvores, rochas e prédios nesta tarefa são protótipos próprios. O visual procedural de fundo permanece como referência para integração futura.

Na VisualStyleDemo, as silhuetas de árvore e rocha se repetem ao longo dos chunks residentes e simplificam com o tamanho projetado. No nível distante, folhagem e tronco conservam as cores próximas. Construções rígidas mantêm paredes e telhado nivelados sobre fundações de pedra nos terrenos inclinados.

## Biblioteca ambiental da VISUAL-002

As decíduas têm copas largas assimétricas, ramificações aparentes e faixas de verde oliva a musgo. Pinheiros e coníferas estreitas usam camadas mais frias. Formas secas preservam o desenho do tronco e dos galhos. Pedras combinam planos de granito, sílex e líquen. O ouro fica visível pelos veios âmbar. Vegetação baixa ocupa manchas de solo sem esconder os personagens. Areia úmida, juncos e seixos fazem a passagem para água rasa. A profundidade e o reflexo estilizado distinguem o corpo de água das margens.
