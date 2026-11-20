# ADR-0013 — Protótipo de renderização medieval

## Contexto

Unity 6000.6.9f1, Entities 6.6.0, URP 17.6.0. O projeto já desenha unidades com RenderMeshInstanced e chunks por MeshRenderer. VISUAL-001 precisa comparar modelos jogáveis e preparar arte e animação sem mudar a autoridade da simulação.

## Alternativas e decisão

- **Instancing de malha compartilhada.** Backend principal. Reaproveita a API em uso, organiza submissões por arquétipo, facção, pose e sombra. Consome o snapshot da mesma sessão. A pose por mesh limita a quantidade de combinações, permite um material Lit e evita faixas de Animator por unidade.
- **MeshRenderer convencional.** Contraprova na VisualStyleDemo. Um objeto visual pooled por unidade submetida. Mesmas malhas, poses e material. Simples para visualização e inspeção, com custo de atualização de transform e representação por objeto. As árvores e os prédios mantêm seu backend para isolar a comparação de unidades.
- **Entities Graphics / Hybrid Renderer.** NOT_RUN. Ausentes no manifesto e lockfile. A simulação tem um World explícito e o render precisaria integrar seu ciclo de vida, metadados de materiais e organização de LOD. Instalação de pacote não é necessária para demonstrar a arquitetura atual.

## Consequências

Culling opcional por frustum e LOD por altura projetada. As poses próximas e neutras distantes são malhas sem rig. O culling não transfere autoridade para câmera ou renderizador. Lotes da aplicação não equivalem a chamadas de desenho físicas. A cena usa uma janela pequena de substituição visual de recursos do catálogo procedural.

No Editor a 2560 × 1440, em 5.000 unidades e com as mesmas opções de apresentação, instancing teve 2,659 ms de CPU na apresentação e 111 draw calls de frame, contra 4,177 ms e cerca de 3.008 com MeshRenderer. O recorte individual do MeshRenderer processou menos geometria que os lotes de instancing sem culling da aplicação. Com culling, LOD e sombras ligados, o instancing animou somente as 1.485 unidades visíveis e consumiu 1,960 ms. O caminho legado de blocos custou 1,743 ms e permanece no controle de comparação. GPU Frame Time e lote total ficaram NOT_RUN. [Relatório VISUAL-001](../validation/VISUAL-001/README.md)

Manter o instancing como backend de referência para a próxima integração visual. O MeshRenderer permanece disponível para inspeção e comparação. Antes de eleger uma estratégia de animação final, medir agrupamento espacial, orçamento de sombras, poses interpoladas ou skinning em GPU e desempenho em Player. A verificação de Entities Graphics pode voltar quando a adoção do pacote for avaliada por um objetivo próprio.
