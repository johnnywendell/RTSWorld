using System.Diagnostics;
using RTSWorld.Simulation;
using RTSWorld.World.Generation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace RTSWorld.Units.Presentation
{
    /// <summary>Authoring, session adapter and batched presentation. No per-unit GameObjects.</summary>
    public sealed class UnitDemo : MonoBehaviour
    {
        [SerializeField, Range(2, 5000)] private int population = 1000;
        [SerializeField] private int seed = 7;
        [SerializeField] private UnitDefinition villager = UnitDefinition.Villager;
        [SerializeField] private UnitDefinition soldier = UnitDefinition.Soldier;
        [SerializeField] private bool navigation;
        [SerializeField] private Vector2Int villagerDestination = new Vector2Int(16, 45);
        [SerializeField] private Vector2Int soldierDestination = new Vector2Int(-16, 45);
        public UnitSession Session { get; private set; }
        public double SimulationMilliseconds { get; private set; }
        public float AverageFrameMilliseconds { get; private set; }
        public int Villagers { get; private set; }
        public int Soldiers { get; private set; }
        public ulong UpdatedEntities { get; private set; }
        public int DrawBatches { get; private set; }
        public double NavigationBuildMilliseconds { get; private set; }
        public double CommandsMilliseconds { get; private set; }
        public bool DestinationsAccepted { get; private set; }
        private NavigationDemoOverlay overlay;
        private bool diagnostics;
        private EntityQuery query;
        private Mesh mesh, terrainMesh;
        private Material terrainMaterial;
        private static readonly int[] Populations = {100,500,1000,5000};
        private Material villagerMaterial, soldierMaterial;
        private readonly Matrix4x4[] villagerMatrices = new Matrix4x4[1023];
        private readonly Matrix4x4[] soldierMatrices = new Matrix4x4[1023];
        private readonly Stopwatch stopwatch = new Stopwatch();
        private float frameSum;
        private int frames;
        private bool visible = true;

        private void OnEnable()
        {
            mesh = MakeCube();
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            villagerMaterial = new Material(shader) { enableInstancing = true };
            soldierMaterial = new Material(shader) { enableInstancing = true };
            villagerMaterial.SetColor("_BaseColor", new Color(0.2f, 0.85f, 0.55f));
            soldierMaterial.SetColor("_BaseColor", new Color(0.95f, 0.35f, 0.2f));
            terrainMaterial = new Material(shader);
            terrainMaterial.SetColor("_BaseColor", new Color(0.18f, 0.25f, 0.22f));
            terrainMesh = MakeTerrain(new ProceduralTerrainContract(new WorldDefinition(seed)));
            Rebuild(population);
        }
        public void Rebuild(int count)
        {
            if (count < 2 || count > 5000) throw new System.ArgumentOutOfRangeException(nameof(count));
            Session?.Dispose();
            population = count;
            var terrain = new ProceduralTerrainContract(new WorldDefinition(seed));
            Session = new UnitSession(terrain, new int2(-64), new int2(64));
            overlay?.Dispose();
            overlay = null;
            Villagers = (count+1)/2; Soldiers = count/2;
            if (navigation)
            {
                stopwatch.Restart();
                var grid = new NavigationGrid(terrain, new int2(-64), new int2(64), 100,
                    cell => cell.y == 0 && math.abs(cell.x) > 4 && math.abs(cell.x) < 50,
                    region => region.X != -2 || region.Z != 1);
                Session.SetNavigationGrid(grid);
                NavigationBuildMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

                var firstGoal = new int2(villagerDestination.x, villagerDestination.y);
                var secondGoal = new int2(soldierDestination.x, soldierDestination.y);
                using var first = Session.SpawnBatch(villager, 0, Villagers, new double3(-31.5,0,-58.5), default, 1);
                using var second = Session.SpawnBatch(soldier, 1, Soldiers, new double3(1.5,0,-58.5), default, 1);
                stopwatch.Restart();
                bool firstAccepted = Session.SetDestination(first, firstGoal);
                bool secondAccepted = Session.SetDestination(second, secondGoal);
                CommandsMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                DestinationsAccepted = firstAccepted && secondAccepted;
                overlay = new NavigationDemoOverlay(terrain, grid, firstGoal, secondGoal);
            }
            else
            {
                using (Session.SpawnBatch(villager, 0, Villagers, new double3(-60,0,-50), new double2(0,2))) { }
                using (Session.SpawnBatch(soldier, 1, Soldiers, new double3(0,0,-50), new double2(0,3), 1.1)) { }
            }
            query = Session.World.EntityManager.CreateEntityQuery(typeof(UnitIdentity), typeof(LogicalPose), typeof(EntitySimulationState));
            frames = 0; frameSum = 0; AverageFrameMilliseconds = 0; UpdatedEntities = 0;
        }
        private void Update()
        {
            stopwatch.Restart();
            Session.Advance(Time.deltaTime);
            Session.CompleteJobs();
            stopwatch.Stop();
            SimulationMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            frameSum += Time.unscaledDeltaTime * 1000; frames++;
            if (frames >= 30) { AverageFrameMilliseconds=frameSum/frames; frameSum=0; frames=0; }
        }
        private void LateUpdate()
        {
            DrawBatches=0;
            if (!visible) return;
            Graphics.RenderMesh(new RenderParams(terrainMaterial), terrainMesh, 0, Matrix4x4.identity);
            overlay?.Draw(diagnostics);
            int v=0,s=0;
            UpdatedEntities=0;
            using (var poses=query.ToComponentDataArray<LogicalPose>(Allocator.Temp))
            using (var identities=query.ToComponentDataArray<UnitIdentity>(Allocator.Temp))
            using (var states=query.ToComponentDataArray<EntitySimulationState>(Allocator.Temp))
            {
                for (int i=0;i<poses.Length;i++)
                {
                    if (states[i].Updates>0) UpdatedEntities++;
                    bool isVillager=identities[i].Kind==UnitKind.Villager;
                    float height=isVillager ? 1.2f : 1.8f;
                    Vector3 p=(Vector3)(float3)poses[i].Position + Vector3.up*(height*0.5f);
                    var matrix=Matrix4x4.TRS(p, (Quaternion)poses[i].Rotation,
                        new Vector3(navigation ? 0.15f : isVillager ? 0.5f : 0.7f, height, navigation ? 0.15f : 0.5f));
                    if (isVillager) { villagerMatrices[v++]=matrix; if(v==1023) { Draw(villagerMatrices,v,villagerMaterial); v=0; } }
                    else { soldierMatrices[s++]=matrix; if(s==1023) { Draw(soldierMatrices,s,soldierMaterial); s=0; } }
                }
            }
            if(v>0) Draw(villagerMatrices,v,villagerMaterial);
            if(s>0) Draw(soldierMatrices,s,soldierMaterial);
        }
        private void Draw(Matrix4x4[] matrices,int count,Material material)
        {
            var parameters=new RenderParams(material) { shadowCastingMode=ShadowCastingMode.Off, receiveShadows=false };
            Graphics.RenderMeshInstanced(parameters,mesh,0,matrices,count);
            DrawBatches++;
        }
        public void SetVisible(bool value) { visible=value; }
        private void OnGUI()
        {
            if(Session==null) return;
            GUI.Box(new Rect(12,12,740,navigation ? 207 : 155), navigation
                ? "ECS-003 — Navegação | Verde: aldeão | Vermelho: soldado"
                : "ECS-002 — Unidades | Verde: aldeão | Vermelho: soldado");
            GUI.Label(new Rect(24,40,720,75),$"Total {Session.Count} | Aldeões {Villagers} | Soldados {Soldiers} | Atualizadas {UpdatedEntities}\nTick {Session.Tick} | Passo {Session.FixedDeltaTime:F3}s | Simulação + sync {SimulationMilliseconds:F3} ms\nFPS médio {(AverageFrameMilliseconds>0?1000/AverageFrameMilliseconds:0):F1} | Frame {AverageFrameMilliseconds:F2} ms | Lotes {DrawBatches}");
            if(GUI.Button(new Rect(24,120,100,30),Session.Paused?"Retomar":"Pausar")) Session.Paused=!Session.Paused;
            int[] counts=Populations;
            for(int i=0;i<counts.Length;i++) if(GUI.Button(new Rect(135+i*90,120,80,30),counts[i].ToString())) Rebuild(counts[i]);
            if(GUI.Button(new Rect(505,120,220,30),visible?"Ocultar apresentação":"Mostrar apresentação")) visible=!visible;
            if (navigation)
            {
                diagnostics = GUI.Toggle(new Rect(24, 156, 125, 22), diagnostics, "Diagnóstico");
                GUI.Label(new Rect(154, 156, 570, 22), $"Malha {NavigationBuildMilliseconds:F2} ms | Comandos {CommandsMilliseconds:F2} ms" +
                    (DestinationsAccepted ? "" : " | Destino indisponível"));
                if (diagnostics)
                    GUI.Label(new Rect(24, 181, 710, 22),
                        "Custo verde→amarelo | Bloqueado vermelho | Sem dados cinza | A* branco | Fluxo azul | Borda roxa");
            }
        }
        private void OnDisable()
        {
            Session?.Dispose(); Session=null;
            overlay?.Dispose(); overlay=null;
            if(mesh) Destroy(mesh);
            if(terrainMesh) Destroy(terrainMesh);
            if(terrainMaterial) Destroy(terrainMaterial);
            if(villagerMaterial) Destroy(villagerMaterial);
            if(soldierMaterial) Destroy(soldierMaterial);
        }
        private static Mesh MakeTerrain(ProceduralTerrainContract terrain)
        {
            const int n=129;
            var vertices=new Vector3[n*n];
            var indices=new int[(n-1)*(n-1)*6];
            for(int z=0;z<n;z++) for(int x=0;x<n;x++)
                vertices[x+z*n]=new Vector3(x-64,(float)terrain.Elevation(x-64,z-64),z-64);
            int t=0;
            for(int z=0;z<n-1;z++) for(int x=0;x<n-1;x++)
            {
                int a=x+z*n; indices[t++]=a;indices[t++]=a+n;indices[t++]=a+1;
                indices[t++]=a+1;indices[t++]=a+n;indices[t++]=a+n+1;
            }
            var result=new Mesh { name="Unit demo terrain" };
            result.vertices=vertices;result.triangles=indices;result.RecalculateBounds();result.RecalculateNormals();
            return result;
        }
        internal static Mesh MakeCube()
        {
            var result=new Mesh { name="Unit shared box" };
            result.vertices=new[] { new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),
                new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f) };
            result.triangles=new[] {0,2,1,0,3,2,1,2,6,1,6,5,5,6,7,5,7,4,4,7,3,4,3,0,3,7,6,3,6,2,4,0,1,4,1,5};
            result.RecalculateBounds(); result.RecalculateNormals();
            return result;
        }
    }
}
