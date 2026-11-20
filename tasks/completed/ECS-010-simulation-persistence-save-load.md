# TASK: ECS-010 — Simulation Persistence & Save/Load

**Arquivo:** `tasks/completed/ECS-010-simulation-persistence-save-load.md`

**Status:** DONE  
**Prioridade:** Alta  
**Dependências:** ECS-005, ECS-006, ECS-007, ECS-008, ECS-009

## Objetivo

Implementar persistência local da simulação do RTSWorld, permitindo salvar e carregar o estado lógico do mundo sem depender da existência de GameObjects visuais.

A solução deverá preparar a arquitetura para persistência futura em servidor, sem implementar multiplayer nesta tarefa.

## Contexto

O RTSWorld já possui:

- Mundo procedural determinístico.
- Streaming de chunks.
- Unidades ECS.
- Navegação.
- Economia.
- Combate.
- Construções.
- Comportamentos autônomos.

Esses sistemas possuem estados mutáveis que precisam ser reconstruídos corretamente.

A seed procedural sozinha não representa o estado completo de uma partida.

## Requisitos funcionais

### 1. Modelo de estado persistente

Definir uma representação serializável para:

- Versão do formato.
- Seed do mundo.
- Configuração procedural.
- Tempo de simulação.
- Facções.
- Unidades.
- Posições.
- Vida.
- Inventários.
- Ordens.
- Estados de IA.
- Recursos naturais.
- Recursos esgotados.
- Estoques.
- Construções.
- Progresso de construção.
- Estado econômico.

Não serializar diretamente referências transitórias de memória como identificadores ECS que não sejam estáveis entre sessões.

### 2. Identificadores persistentes

Definir identificadores estáveis para entidades que necessitam persistência.

Garantir reconstrução de relações entre:

- Trabalhador e recurso.
- Trabalhador e depósito.
- Soldado e alvo.
- Construção e trabalhadores.
- Unidade e facção.
- Recurso e chunk.

Referências a entidades inexistentes devem ser tratadas com segurança.

### 3. Salvamento

Implementar operação Save.

Requisitos:

- Capturar estado consistente da simulação.
- Evitar estados parcialmente atualizados.
- Definir ponto seguro de captura.
- Serializar dados necessários.
- Gravar em arquivo local.
- Utilizar escrita temporária e substituição segura quando apropriado.
- Tratar erros de gravação.
- Evitar corromper salvamentos anteriores.

Não bloquear o frame principal por períodos excessivos.

### 4. Carregamento

Implementar operação Load.

Fluxo esperado:

1. Ler arquivo.
2. Validar versão e integridade.
3. Recuperar configuração procedural.
4. Reconstruir mundo lógico.
5. Aplicar modificações persistentes.
6. Reconstruir unidades.
7. Reconstruir construções.
8. Restaurar economia.
9. Restaurar ordens.
10. Restaurar estados de IA.
11. Recriar índices espaciais.
12. Reconstruir caches transitórios.
13. Recriar apresentação visual.

Não serializar caches que possam ser reconstruídos com segurança, salvo justificativa.

### 5. Recursos procedurais

Preservar:

- Quantidade restante.
- Recursos esgotados.
- Recursos removidos.
- Modificações na ocupação.
- Consistência após streaming.

Não recriar uma árvore já esgotada simplesmente porque o chunk foi gerado novamente.

### 6. Construções

Persistir:

- Tipo.
- Posição.
- Rotação.
- Facção.
- Vida.
- Estado.
- Progresso.
- Custos ou recursos comprometidos quando necessário.

Restaurar corretamente ocupação e pontos de interação.

### 7. Unidades

Persistir:

- Tipo.
- Facção.
- Posição.
- Vida.
- Inventário.
- Estado de trabalho.
- Ordens relevantes.
- Estado de combate.
- Estado de IA.

Definir como lidar com ordens cujos alvos não existam após o carregamento.

### 8. Economia

Garantir consistência entre:

- Estoque armazenado.
- Carga transportada.
- Quantidade restante nos recursos.
- Recursos perdidos.
- Recursos consumidos por construções.

Evitar duplicações após salvar e carregar.

### 9. Versão do formato

Adicionar versão explícita ao arquivo.

Definir política para:

- Formatos suportados.
- Versões incompatíveis.
- Campos ausentes.
- Dados inválidos.
- Evolução futura.

Não é necessário criar um sistema complexo de migrações nesta tarefa.

### 10. Interface

Adicionar comandos simples:

- Save.
- Load.
- Novo jogo ou reset controlado.
- Indicação de sucesso.
- Indicação de erro.

Evitar interface definitiva.

## Cena demonstrativa

Criar:

`Assets/_Project/Scenes/PersistenceDemo.unity`

A cena deverá permitir:

1. Iniciar mundo.
2. Movimentar unidades.
3. Coletar recursos.
4. Construir.
5. Executar combate.
6. Alterar estados do mundo.
7. Salvar.
8. Reiniciar ou descarregar a simulação.
9. Carregar.
10. Verificar restauração dos estados.

A validação deve incluir a reconstrução efetiva, não apenas serializar e desserializar um objeto de teste.

## Testes

Criar testes Edit Mode e Play Mode para:

- Serialização.
- Desserialização.
- Versão de arquivo.
- Arquivo inválido.
- Arquivo incompleto.
- Referências inexistentes.
- Seed.
- Recursos esgotados.
- Unidades.
- Vida.
- Inventário.
- Economia.
- Construções.
- Progresso de construção.
- Ordens.
- Estados de IA.
- Navegação reconstruída.
- Streaming.
- Salvamento repetido.
- Carregamento repetido.
- Ausência de duplicação.
- Consistência após retomada.

## Desempenho

Medir:

- Tempo de captura.
- Tempo de serialização.
- Tempo de gravação.
- Tempo de leitura.
- Tempo de reconstrução.
- Memória utilizada.
- Tamanho do arquivo.
- Impacto sobre o frame principal.

Testar diferentes populações de unidades e quantidades de recursos.

Não estabelecer limites arbitrários sem medições.

## Critérios de aceite

- [x] Modelo persistente definido.
- [x] Identificadores estáveis.
- [x] Save funcional.
- [x] Load funcional.
- [x] Validação de versão.
- [x] Tratamento de arquivos inválidos.
- [x] Mundo procedural reconstruído.
- [x] Recursos restaurados.
- [x] Unidades restauradas.
- [x] Economia restaurada.
- [x] Construções restauradas.
- [x] Combate consistente.
- [x] Estados de IA restaurados ou retomados de forma documentada.
- [x] Índices e caches reconstruídos.
- [x] Streaming preservado.
- [x] Ausência de duplicação econômica.
- [x] Cena demonstrativa funcional.
- [x] Testes Edit Mode aprovados.
- [x] Testes Play Mode aprovados.
- [x] Compilação sem erros.
- [x] Console sem erros relacionados.
- [x] Profiling documentado.
- [x] Documentação e handoff atualizados.

## Execução e encerramento

Ao receber autorização:

1. Ler os documentos obrigatórios do projeto.
2. Verificar conclusão da ECS-009.
3. Verificar estado do Git.
4. Mover para `tasks/completed/`.
5. Atualizar planejamento.
6. Implementar por checkpoints.
7. Validar com Unity MCP.
8. Registrar evidências em `docs/validation/ECS-010/README.md`.
9. Atualizar arquitetura, ADRs, estado atual e handoff.
10. Mover para `tasks/completed/` somente após validação.

Não criar commits sem autorização.

Não iniciar tarefas posteriores.

Em caso de bloqueio, registrar e interromper.

## Checkpoint final

- Implementação final em SimulationSnapshot.cs, SimulationSaveFile.cs, UnitPersistence.cs e RtsInteractionPersistence.cs. A PersistenceDemo suporta salvar, novo jogo e recarregar uma sessão reconstruída. Construções recebem IDs estáveis, campos de navegação são reconstruídos e o streaming reaplica a definição e a disponibilidade de recursos.
- Compilação PASS. Edit Mode PASS 280/280. Play Mode PASS 21/21 na repetição integral. O teste histórico de profiling de combate falhou na primeira execução completa e passou na repetição com a mesma revisão. Console final PASS, sem erros ou avisos. Player e mouse físico NOT_RUN.
- Documentação atualizada em docs/ARCHITECTURE.md, docs/decisions/ADR-0012-local-simulation-persistence.md, docs/CURRENT_STATE.md e docs/HANDOFF.md. Medições e condições em docs/validation/ECS-010/README.md. A reconstrução síncrona levou 96,104 ms no caso de 5.000 unidades.
- Git main/c690c43, sincronizado com origin/main. Alterações preexistentes preservadas e artefatos históricos repostos byte a byte após os testes. Sem commits. Sem trabalho pendente para os critérios da ECS-010.
