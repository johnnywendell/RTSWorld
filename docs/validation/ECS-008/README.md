# Validação ECS-008

Unity 6000.6.9f1, Linux Editor, URP, via Unity MCP. Resultados coletados em main/c690c43 com o trabalho da ECS-007 presente localmente.

## Reprodução

1. Abra ConstructionDemo em Play. Selecione aldeões e use o botão direito sobre árvore ou rocha para coletar. O depósito azul com caixote recebe as entregas. O centro principal é a estrutura maior.
2. Escolha Depósito, Centro principal ou Oficina no painel. A prévia mostra o retângulo e os motivos de rejeição. Quando a área e o estoque permitem, clique para confirmar. Use o botão direito para sair da prévia.
3. Aldeões já selecionados recebem Build ao confirmar. Para emitir a ordem novamente, selecione os aldeões e clique com o botão direito no local da obra. Verifique avanço, vida e cancelamento no painel.
4. Depósitos passam a receber entregas ao concluir. A ordem Move usa a mesma grade com a área das estruturas bloqueada desde o posicionamento.

## Testes focados e medições

- Edit Mode focado PASS 4/4, sem ignorados. Inclui água, declividade, ocupação, estoque, reembolso, trabalho no passo fixo com um e três aldeões, suspensão da entrega após destruição, preservação da carga e retomada no depósito concluído.
- Em grade lógica de 32 × 32, 100 chamadas de validação levaram 0,224 ms com um construtor e 0,223 ms com três, incluindo a asserção. A última chamada instrumentada levou aproximadamente 0,0006 ms.
- Picos de atualização da construção de 0,0059 e 0,0046 ms com um e três construtores, respectivamente. Esses valores incluem a interação dos aldeões e a sincronização dos jobs.
- A última remoção de footprint, incluindo publicação de navegabilidade e invalidação de campos, levou 0,0220 e 0,0206 ms nos mesmos casos. A última busca de depósito registrada levou 0,1193 e 0,1106 ms. O teste também confirmou que reservar o destino torna obsoleto o campo correspondente e libera as células ao remover a construção.
- Tempos pontuais de Editor. Os ensaios focados usam terreno sintético seco, recursos artificiais, passo de 50 ms e estoque obtido por Gather. Os casos de terreno usam a mesma grade com água alta ou tolerância de declividade zero.

## Inspeção e limites

- Inspeção direta em Play via Unity MCP identificou 100 unidades, depósito e centro principal concluídos. Para validar o clique em condições repetíveis, o estoque foi preparado em memória em 60 madeira e 80 pedra, e o tipo de prévia foi selecionado por código. O mouse virtual mostrou a área válida em `(-152, 338)` e confirmou o depósito com o botão esquerdo. A cena debitou 20 madeira e 10 pedra, atribuiu Build a 30 aldeões alcançáveis e concluiu a obra em 8 unidades de trabalho. Os testes automatizados obtêm estoque por coleta e fazem entrega no novo depósito.
- A comparação de FPS usa a mesma cena e 100 unidades após descarregar o chunk do depósito, em 120 frames por fase e 45 frames de aquecimento. Um ensaio focado registrou 423,9 FPS com as duas construções e 559,8 FPS após destruí-las. A ordem das fases é fixa, sem isolamento da variação do Editor, e parte das estruturas pode ficar fora da câmera. A diferença observada não isola o custo de renderização das estruturas.
- Construções não são alvos de Attack. A operação de dano da sessão altera vida, destrói e libera a área. Rotação, build de player, GPU timing e mouse físico NOT_RUN. A janela de navegação permanece regional e a simulação econômica permanece em memória.

## Regressão final

- PASS compilação, 262/262 Edit Mode e 17/17 Play Mode, sem ignorados. Uma execução com ConstructionDemo ativa passou 16/17 e falhou no teste preexistente de profiling de CombatDemo por não registrar combate em alcance dentro da janela amostrada. A regressão completa passou após restaurar CombatDemo.
- O último ensaio registrou 557,0 FPS com duas estruturas e 545,5 FPS sem estruturas, nas mesmas condições do ensaio focado. A variação entre rodadas impede atribuir a diferença às estruturas.
- Console final com zero erros e avisos. Editor em CombatDemo, fora de Play, zero Worlds de unidades. [Captura da ConstructionDemo](construction-demo.png)
