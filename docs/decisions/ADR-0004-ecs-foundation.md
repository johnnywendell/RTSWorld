# ADR-0004 — Fundação ECS isolada

Data: 2026-11-15. Status: implementada no escopo explicitamente autorizado da ECS-001.

Entities 6.6.0 foi selecionado pelo Package Manager do Unity 6000.6.9f1. Burst 2.0.0, Collections 6.6.0, Mathematics e Jobs já são fornecidos pelo Editor; não instalar pacotes legados redundantes. URP 17.6.0 preservado; Entities Graphics desnecessário para indicadores IMGUI.

A simulação possui World explícito por FoundationSession e FixedStepSimulationSystemGroup, sem registrar sistemas no World padrão. O adaptador MonoBehaviour fornece tempo/configuração, cria dados iniciais e apresenta contadores. Movimento e estado ficam em IJobEntity, agendado em paralelo, compilado com Burst. Sem migração do domínio World, streaming ou câmera.

LogicalPose usa double3 global e quaternion. LocalTransform foi considerado: sua posição float e função local de transformação não representam o contrato global; introduzir ambos criaria duas posições autoritativas. Uma futura apresentação poderá derivar LocalTransform relativo a uma origem. A identidade ulong é única dentro da sessão, não um protocolo persistente ou de rede.

ProceduralTerrainContract é fronteira managed que reutiliza geradores existentes. Amostras são produzidas antes dos jobs; geradores com classes/caches não entram em Burst. Integração futura poderá fornecer snapshots imutáveis NativeArray/BlobAsset aos jobs, com ownership explícito independente de residência visual. Não introduzir esse cache sem necessidade mensurada.

Determinismo é limitado ao mesmo Editor/runtime, versões, configuração e sequência de ticks. Nenhuma promessa entre CPUs/plataformas ou de lockstep. Coordenadas são globais lógicas, sem escala visual. Fundação não faz navegação, seguimento de relevo ou persistência.
