using System.Collections.Generic;
using System.Diagnostics;
using RTSWorld.Presentation.Cameras;
using RTSWorld.Presentation.Streaming;
using RTSWorld.Simulation;
using RTSWorld.World.Environment;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace RTSWorld.Units.Presentation
{
    public sealed class RtsInteractionDemo : MonoBehaviour
    {
        [SerializeField] private RtsCameraController rig;
        [SerializeField] private RtsCameraInput cameraInput;
        [SerializeField] private ChunkManager streaming;
        [SerializeField, Range(1, 8)] private int spacing = 1;
        [SerializeField, Range(2, 5000)] private int population = 100;
        [SerializeField] private bool integratedWorld;
        [SerializeField] private bool economyDemo;
        [SerializeField] private bool combatDemo;
        [SerializeField, Min(.01f)] private float gatherProductivity = 4;
        [SerializeField, Range(0, 1)] private float environmentalClearance = .22f;
        [SerializeField] private Vector2Int navigationCenter = new(-152, 352);
        private static readonly Rect Panel = new(12, 12, 490, 142);
        private static readonly int[] Populations = { 100, 500, 1000, 5000 };
        private readonly List<SelectableUnit> selectable = new();
        private readonly List<SelectableUnit> enemies = new();
        private readonly HashSet<Entity> combatTargets = new();
        private readonly RtsSelection selection = new();
        private Matrix4x4[][] matrices;
        private int[] counts;
        private Material[] materials;
        private EntityQuery query;
        private System.Func<double, double, float> pickingHeight;
        private bool useTerrainSnapshot;
        private NavigationGrid navigation;
        private string economyText, combatText;
        private const int Enemy = 3, Health = 4, Hit = 5, Strike = 6, HealthBackground = 7, Target = 8;
        private Mesh cube;
        private bool dragging, tracking, diagnostics;
        private Vector2 start, pointer;
        private int moving, commands;
        private double inputMs, pickingMs, simMs, drawMs;
        private float nextRefresh;
        private string statistics;
        private string feedback;
        private Vector3 marker;
        private float markerUntil;
        private bool markerAccepted;
        public UnitSession Session { get; private set; }
        public int SelectionCount => selection.Count;
        public int MovingCount => moving;
        public int CommandsSent => commands;
        public double SelectionMilliseconds => pickingMs;
        public double SimulationMilliseconds => simMs;
        public double PresentationMilliseconds => drawMs;
        public double WorldSetupMilliseconds { get; private set; }
        public double ObstacleBuildMilliseconds { get; private set; }
        public double NavigationBuildMilliseconds { get; private set; }
        public ProceduralTerrainContract LogicalTerrain { get; private set; }
        public NavigationGrid Navigation => navigation;
        private Camera View => rig.OutputCamera;
        private ICameraSurface Surface => rig.Surface;

        private void OnEnable()
        {
            if (rig == null || streaming == null || cameraInput == null) return;
            cube = UnitDemo.MakeCube();
            pickingHeight = PickingHeight;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            int kinds = combatDemo ? 9 : 3;
            matrices = new Matrix4x4[kinds][];
            counts = new int[kinds];
            materials = new Material[kinds];
            for (int i = 0; i < kinds; i++) matrices[i] = new Matrix4x4[1023];
            materials[0] = MakeMaterial(shader, new Color(.24f, .8f, .45f));
            materials[1] = MakeMaterial(shader, new Color(.93f, .59f, .25f));
            materials[2] = MakeMaterial(shader, new Color(.28f, .88f, 1));
            if (combatDemo)
            {
                materials[Enemy] = MakeMaterial(shader, new Color(.72f, .26f, .58f));
                materials[Health] = MakeMaterial(shader, new Color(.40f, .92f, .46f));
                materials[Hit] = MakeMaterial(shader, new Color(1f, .32f, .29f));
                materials[Strike] = MakeMaterial(shader, new Color(1f, .98f, .57f));
                materials[HealthBackground] = MakeMaterial(shader, new Color(.10f, .12f, .15f));
                materials[Target] = MakeMaterial(shader, new Color(.94f, .37f, .66f));
            }
            cameraInput.ExcludedGuiRect = Panel;
            Rebuild(population);
        }

        public void Rebuild(int count)
        {
            if (count < 2 || count > 5000) throw new System.ArgumentOutOfRangeException(nameof(count));
            if (economyDemo && Session != null) streaming.EnvironmentLayer?.SetResourceFilter(null);
            Session?.Dispose();
            selection.Clear();
            selectable.Clear();
            enemies.Clear();
            moving = commands = 0;
            markerUntil = 0;
            feedback = "";
            population = count;
            long setupStarted = Stopwatch.GetTimestamp();
            var environment = streaming.ActiveEnvironment?.CreateDefinition();
            useTerrainSnapshot = environment == null;
            LogicalTerrain = new ProceduralTerrainContract(streaming.CreateWorldDefinition(), environment);
            var center = integratedWorld ? new int2(navigationCenter.x, navigationCenter.y) : int2.zero;
            int halfSize = integratedWorld ? 96 : 64;
            int2 min = center - halfSize;
            int2 max = center + halfSize;
            Session = new UnitSession(LogicalTerrain, min, max);
            long obstacleStarted = Stopwatch.GetTimestamp();
            var obstacles = integratedWorld && environment != null
                ? EnvironmentalObstacles.Build(LogicalTerrain, min, max, environmentalClearance) : null;
            ObstacleBuildMilliseconds = Since(obstacleStarted);
            long navigationStarted = Stopwatch.GetTimestamp();
            navigation = new NavigationGrid(LogicalTerrain, min, max, integratedWorld ? 2.25 : 100,
                captureSurface: integratedWorld, obstacles: obstacles);
            Session.SetNavigationGrid(navigation);
            NavigationBuildMilliseconds = Since(navigationStarted);
            if (integratedWorld)
            {
                int spawned = IntegratedSpawn.Place(Session, navigation, center, count, combatDemo);
                if (spawned != count) feedback = $"Terreno navegável para {spawned}/{count} unidades";
                if (economyDemo && spawned > 0 && obstacles != null)
                {
                    var depot = FindDepot(center);
                    Session.ConfigureEconomy(depot, gatherProductivity);
                    Session.ResourceDepleted += ResourceDepleted;
                    streaming.ActiveEnvironment?.SetResourceFilter(ResourceVisible);
                }
            }
            else
            {
                int villagers = (count + 1) / 2;
                using (Session.SpawnBatch(UnitDefinition.Villager, 0, villagers, new double3(-30, 0, -16), default, .75)) { }
                using (Session.SpawnBatch(UnitDefinition.Soldier, 0, count / 2, new double3(10, 0, -16), default, .75)) { }
            }
            WorldSetupMilliseconds = Since(setupStarted);
            query = Session.World.EntityManager.CreateEntityQuery(typeof(UnitIdentity), typeof(LogicalEntityId),
                typeof(LogicalPose), typeof(UnitMotion), typeof(UnitHealth), typeof(UnitCombat));
        }

        private void Update()
        {
            if (Session == null) return;
            if (integratedWorld && WorldConfigurationChanged()) Rebuild(population);
            long stamp = Stopwatch.GetTimestamp();
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null || !Application.isFocused)
            {
                tracking = dragging = false;
            }
            else
            {
                pointer = mouse.position.ReadValue();
                bool allowed = View.pixelRect.Contains(pointer) && !cameraInput.ExcludedGuiRect.Contains(ToGui(pointer));
                bool leftPressed = mouse.leftButton.wasPressedThisFrame;
                bool leftReleased = mouse.leftButton.wasReleasedThisFrame;
                bool rightPressed = mouse.rightButton.wasPressedThisFrame;
                bool stopPressed = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
                var mode = keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed)
                    ? SelectionMode.Toggle
                    : keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
                        ? SelectionMode.Add : SelectionMode.Replace;
                inputMs = Since(stamp);
                if (allowed && leftPressed)
                {
                    tracking = true;
                    dragging = false;
                    start = pointer;
                }

                if (tracking && (pointer - start).sqrMagnitude >= 36) dragging = true;
                if (tracking && leftReleased)
                {
                    if (allowed) Select(start, pointer, dragging, mode);
                    tracking = dragging = false;
                }

                if (allowed && rightPressed) MoveAt(pointer);
                if (stopPressed) Issue(UnitOrder.Stop());
            }

            stamp = Stopwatch.GetTimestamp();
            Session.Advance(Time.deltaTime);
            Session.CompleteJobs();
            simMs = Since(stamp);
        }

        public void Select(Vector2 from, Vector2 to, bool rectangle, SelectionMode mode)
        {
            long stamp = Stopwatch.GetTimestamp();
            if (rectangle)
            {
                selection.Rectangle(View, Rect.MinMaxRect(Mathf.Min(from.x, to.x), Mathf.Min(from.y, to.y),
                    Mathf.Max(from.x, to.x), Mathf.Max(from.y, to.y)), selectable, mode, Visible);
            }
            else
            {
                Ray ray = View.ScreenPointToRay(to);
                selection.Click(View, to, selectable, mode, OcclusionDistance(ray, View.farClipPlane));
            }

            pickingMs = Since(stamp);
        }

        public UnitOrderResult MoveAt(Vector2 point)
        {
            Ray ray = View.ScreenPointToRay(point);
            if (combatDemo)
            {
                float limit = OcclusionDistance(ray, View.farClipPlane);
                Entity enemy = Entity.Null;
                foreach (var candidate in enemies)
                {
                    if (!candidate.Bounds.IntersectRay(ray, out float depth) || depth >= limit) continue;
                    limit = depth;
                    enemy = candidate.Entity;
                }

                if (enemy != Entity.Null)
                {
                    markerUntil = 0;
                    return Issue(UnitOrder.Attack(enemy));
                }
            }
            if (!RtsSurfaceRay.TryHit(Surface, ray, View.farClipPlane, out float distance, out var logical, pickingHeight))
            {
                feedback = "Terreno fora de alcance";
                markerUntil = 0;
                return UnitOrderResult.InvalidDestination;
            }

            UnitOrderResult result;
            if (Session.EconomyEnabled && TryPickResource(ray, logical, distance, out var resource) &&
                navigation.Obstacles.TryGet(resource, out var picked))
            {
                result = GatherSelected(resource);
                marker = WorldPosition(new double3(picked.Center.x, picked.Placement.Elevation, picked.Center.y))
                    + Vector3.up * .15f;
            }
            else
            {
                result = Issue(UnitOrder.Move(new int2(Mathf.FloorToInt(logical.x), Mathf.FloorToInt(logical.y)), spacing));
                marker = ray.GetPoint(distance) + Vector3.up * .15f;
            }
            markerUntil = Time.unscaledTime + 1.5f;
            markerAccepted = result == UnitOrderResult.Accepted;
            return result;
        }

        public UnitOrderResult GatherSelected(ResourceKey key)
        {
            using var storage = new NativeArray<Entity>(selection.Count, Allocator.Temp);
            int count = FillSelected(storage);
            var result = Session.Gather(key, 0, storage.GetSubArray(0, count));
            if (result == UnitOrderResult.Accepted) commands++;
            feedback = result switch
            {
                UnitOrderResult.Accepted => "Coletar",
                UnitOrderResult.Empty => "Selecione aldeões",
                UnitOrderResult.InvalidDestination => "Recurso indisponível",
                UnitOrderResult.Unreachable => "Recurso ou depósito inacessível",
                _ => "Campos de navegação em uso"
            };
            return result;
        }

        private bool TryPickResource(Ray ray, Vector2 logical, float surfaceDistance, out ResourceKey key)
        {
            key = default;
            bool found = false;
            float best = surfaceDistance + .01f;
            var definition = LogicalTerrain.Definition;
            int version = LogicalTerrain.Environment.Version;
            int x = Mathf.FloorToInt(logical.x), z = Mathf.FloorToInt(logical.y);
            for (int dz = -3; dz <= 3; dz++)
                for (int dx = -3; dx <= 3; dx++)
                {
                    var candidate = new ResourceKey(definition.Seed, version, x + dx, z + dz);
                    if (!Session.TryGetResource(candidate, out var amount) || amount.Remaining <= 0 ||
                        !navigation.Obstacles.TryGet(candidate, out var resource)) continue;
                    var floor = WorldPosition(new double3(resource.Center.x, resource.Placement.Elevation, resource.Center.y));
                    float height = streaming.HorizontalScale * (amount.Kind == NaturalResource.Tree ? 2.9f : .85f);
                    float width = streaming.HorizontalScale;
                    var volume = new Bounds(floor + Vector3.up * (height * .5f), new Vector3(width, height, width));
                    if (!volume.IntersectRay(ray, out float hit) || hit >= best) continue;
                    key = candidate;
                    best = hit;
                    found = true;
                }
            return found;
        }

        private int2 FindDepot(int2 center)
        {
            int2 depot = center;
            double nearest = double.PositiveInfinity;
            int2 end = navigation.Min + navigation.Size;
            for (int z = navigation.Min.y; z < end.y; z++)
                for (int x = navigation.Min.x; x < end.x; x++)
                {
                    var cell = new int2(x, z);
                    double distance = math.lengthsq((double2)cell - center);
                    if (distance < nearest && navigation.TryGetCost(cell, out int cost) && cost > 0)
                    {
                        nearest = distance;
                        depot = cell;
                    }
                }
            return depot;
        }

        private bool ResourceVisible(ResourceKey key) =>
            Session == null || !Session.TryGetResource(key, out var amount) || amount.Remaining > 0;

        private void ResourceDepleted(ResourceKey key) =>
            streaming.RefreshResourceChunk(LogicalTerrain.Region(key.X, key.Z));

        private int FillSelected(NativeArray<Entity> members)
        {
            int count = 0;
            foreach (var unit in selectable)
                if (selection.Contains(unit.Id)) members[count++] = unit.Entity;
            return count;
        }

        public UnitOrderResult Issue(UnitOrder order)
        {
            using var storage = new NativeArray<Entity>(selection.Count, Allocator.Temp);
            var result = Session.ApplyOrder(order, 0, storage.GetSubArray(0, FillSelected(storage)));
            if (result == UnitOrderResult.Accepted) commands++;
            feedback = result switch
            {
                UnitOrderResult.Accepted => order.Kind switch
                {
                    UnitOrderKind.Move => "Mover",
                    UnitOrderKind.Attack => "Atacar",
                    _ => "Parar"
                },
                UnitOrderResult.Empty when order.Kind == UnitOrderKind.Attack => "Selecione soldados",
                UnitOrderResult.InvalidTarget => "Alvo indisponível",
                UnitOrderResult.Empty => "Selecione unidades",
                UnitOrderResult.InvalidDestination => "Destino indisponível",
                UnitOrderResult.Unreachable => "Sem rota para o grupo",
                UnitOrderResult.InsufficientSpace => "Espaço insuficiente",
                UnitOrderResult.CacheBusy => "Campos de navegação em uso",
                _ => "Seleção desatualizada"
            };
            return result;
        }

        public Vector3 WorldPosition(double3 logical)
        {
            var local = new Vector3((float)logical.x * streaming.HorizontalScale,
                (float)logical.y * streaming.VerticalScale, (float)logical.z * streaming.HorizontalScale);
            return streaming.transform.TransformPoint(local);
        }

        private bool Visible(Vector3 point)
        {
            Vector3 screen = View.WorldToScreenPoint(point);
            Ray ray = View.ScreenPointToRay(screen);
            float distance = Vector3.Dot(point - ray.origin, ray.direction);
            return OcclusionDistance(ray, distance) >= distance;
        }

        private float OcclusionDistance(Ray ray, float limit)
        {
            if (Physics.Raycast(ray, out var geometry, limit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                limit = geometry.distance;
            return RtsSurfaceRay.TryHit(Surface, ray, limit, out float surface, out _, pickingHeight) ? surface : limit;
        }

        private bool WorldConfigurationChanged()
        {
            var world = LogicalTerrain.Definition;
            var environment = LogicalTerrain.Environment;
            var layer = streaming.ActiveEnvironment;
            return world.Seed != streaming.Seed || world.CellsPerChunk != streaming.CellsPerChunk ||
                (layer == null ? environment != null :
                    environment == null || layer.EnvironmentVersion != environment.Version ||
                    layer.SeaLevel != environment.SeaLevel);
        }

        private float PickingHeight(double x, double z)
        {
            if (integratedWorld && navigation.TryGetSurface(x, z, out double surfaceHeight))
                return (float)(surfaceHeight * streaming.VerticalScale);
            if (useTerrainSnapshot && Session.TryGetTerrainElevation(x, z, out double elevation))
                return (float)(elevation * streaming.VerticalScale);

            return Surface.HeightAt(x, z);
        }

        private void LateUpdate()
        {
            if (Session == null) return;
            long stamp = Stopwatch.GetTimestamp();
            selectable.Clear();
            enemies.Clear();
            moving = 0;
            using var poses = query.ToComponentDataArray<LogicalPose>(Allocator.Temp);
            using var identities = query.ToComponentDataArray<UnitIdentity>(Allocator.Temp);
            using var ids = query.ToComponentDataArray<LogicalEntityId>(Allocator.Temp);
            using var motions = query.ToComponentDataArray<UnitMotion>(Allocator.Temp);
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var health = combatDemo ? query.ToComponentDataArray<UnitHealth>(Allocator.Temp) : default;
            using var combat = combatDemo ? query.ToComponentDataArray<UnitCombat>(Allocator.Temp) : default;
            combatTargets.Clear();
            if (combatDemo)
                for (int i = 0; i < combat.Length; i++)
                    if (identities[i].Owner == 0 && selection.Contains(ids[i].Value) && combat[i].Target != Entity.Null)
                        combatTargets.Add(combat[i].Target);
            for (int i = 0; i < poses.Length; i++)
            {
                Vector3 feet = WorldPosition(poses[i].Position);
                int kind = identities[i].Kind == UnitKind.Villager ? 0 : 1;
                Vector3 size = kind == 0 ? new Vector3(.62f, 1.25f, .62f) : new Vector3(.75f, 1.65f, .75f);
                Vector3 center = feet + Vector3.up * (size.y * .5f);
                if (identities[i].Owner == 0)
                    selectable.Add(new SelectableUnit(ids[i].Value, entities[i], center, size));
                else if (combatDemo)
                    enemies.Add(new SelectableUnit(ids[i].Value, entities[i], center, size));
                if (motions[i].Activity == UnitActivity.Moving) moving++;
                Add(combatDemo && identities[i].Owner != 0 ? Enemy : kind, center, size);
                if (combatDemo)
                    DrawCombat(feet, center, size, health[i], combat[i], combatTargets.Contains(entities[i]));
                if (selection.Contains(ids[i].Value)) Add(2, feet + Vector3.up * .07f, new Vector3(1.04f, .12f, 1.04f));
            }

            if (Session.EconomyEnabled)
            {
                double2 center = (double2)Session.DepotCell + .5;
                if (Session.TryGetTerrainElevation(center.x, center.y, out double elevation))
                    Add(2, WorldPosition(new double3(center.x, elevation, center.y)) + Vector3.up * .55f,
                        new Vector3(1.45f, 1.1f, 1.45f));
            }
            for (int i = 0; i < counts.Length; i++) Flush(i);
            selection.RetainLive(selectable);
            if (Time.unscaledTime < markerUntil)
            {
                var parameters = new RenderParams(materials[markerAccepted ? 2 : 1]);
                Graphics.RenderMesh(parameters, cube, 0, Matrix4x4.TRS(marker,
                    Quaternion.Euler(0, 45, 0), new Vector3(.9f, .16f, .9f)));
            }
            drawMs = Since(stamp);

            if ((diagnostics || Session.EconomyEnabled) && Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + .25f;
                statistics = $"Total {Session.Count} · Seleção {selection.Count} · Em movimento {moving} · Paradas {Session.Count - moving} · Ordens {commands}\n" +
                    $"Input {inputMs:F2} · Seleção {pickingMs:F2} · Navegação {Session.OrderNavigationMilliseconds:F2} · Distribuição {Session.OrderDistributionMilliseconds:F2}\n" +
                    $"Aplicação {Session.OrderApplicationMilliseconds:F2} · ECS {simMs:F2} · Visual {drawMs:F2} ms · FPS {(1f / Mathf.Max(.0001f, Time.unscaledDeltaTime)):F0}";
                if (Session.EconomyEnabled)
                    economyText = $"Madeira {Session.GetStock(0, NaturalResource.Tree):F1} · Pedra {Session.GetStock(0, NaturalResource.Stone):F1}" +
                        $" · Entregue {Session.DeliveredTotal:F1}\nAtivos {Session.ActiveWorkers} · Coletando {Session.GatheringWorkers}" +
                        $" · Transportando {Session.TransportingWorkers} · Economia {Session.EconomyMilliseconds:F3} ms";
                if (combatDemo)
                    combatText = $"Laranjas e verdes {selectable.Count} · Roxos {enemies.Count} · Ordens de ataque {Session.EngagedUnits} · Baixas {Session.DefeatedUnits}";
                if (integratedWorld)
                    statistics += $"\nSeed {LogicalTerrain.Definition.Seed} · Chunks {streaming.ActiveCount} · Fila {streaming.PendingCount}" +
                        $" · Recursos residentes {streaming.ActiveResourceCount}";
            }
        }

        private void DrawCombat(Vector3 feet, Vector3 center, Vector3 size, UnitHealth health, UnitCombat combat, bool targeted)
        {
            double now = Session.CombatTime;
            if (targeted) Add(Target, feet + Vector3.up * .08f, new Vector3(1.15f, .13f, 1.15f));
            if (combat.StrikeUntil > now)
                Add(Strike, center + Vector3.up * (size.y * .65f), new Vector3(.22f, .24f, .22f));
            if (health.Current >= health.Maximum && !targeted) return;
            float fraction = Mathf.Clamp01(health.Current / health.Maximum);
            Vector3 bar = feet + Vector3.up * (size.y + .38f);
            Add(HealthBackground, bar, new Vector3(.84f, .10f, .17f));
            Add(combat.HitUntil > now ? Hit : Health, bar + new Vector3((fraction - 1) * .4f, .055f, 0),
                new Vector3(.8f * fraction, .09f, .15f));
        }

        private void Add(int kind, Vector3 position, Vector3 size)
        {
            matrices[kind][counts[kind]++] = Matrix4x4.TRS(position, Quaternion.identity, size);
            if (counts[kind] == matrices[kind].Length) Flush(kind);
        }

        private void Flush(int kind)
        {
            if (counts[kind] == 0) return;
            var parameters = new RenderParams(materials[kind])
                { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            Graphics.RenderMeshInstanced(parameters, cube, 0, matrices[kind], counts[kind]);
            counts[kind] = 0;
        }

        private void OnGUI()
        {
            if (Session == null) return;
            GUILayout.BeginArea(Panel, GUI.skin.box);
            GUILayout.Label("Selecionar: clique ou arraste · Shift: adicionar · Ctrl: alternar");
            GUILayout.Label(combatDemo ? "Direito: atacar, coletar ou mover · Espaço: parar" : economyDemo ? "Direito: coletar ou mover · Espaço: parar · WASD: pan" :
                "Direito: mover · Espaço: parar · WASD / meio do mouse: pan");
            GUILayout.Label("Scroll: zoom · Q/E: girar");
            GUILayout.BeginHorizontal();
            foreach (int count in Populations)
                if (GUILayout.Button(count.ToString())) Rebuild(count);
            diagnostics = GUILayout.Toggle(diagnostics, "Diagnóstico");
            rig.Settings.EnableEdgePan = GUILayout.Toggle(rig.Settings.EnableEdgePan, "Bordas");
            GUILayout.EndHorizontal();
            GUILayout.Label($"Selecionadas {selection.Count} · {feedback}");
            GUILayout.EndArea();
            float hudHeight = integratedWorld ? 92 : 74;
            float nextPanel = 159;
            if (diagnostics)
            {
                GUI.Box(new Rect(12, nextPanel, 490, hudHeight), statistics);
                nextPanel += hudHeight + 5;
            }
            if (Session.EconomyEnabled)
            {
                GUI.Box(new Rect(12, nextPanel, 490, 47), economyText);
                nextPanel += 47;
            }
            if (combatDemo)
            {
                GUI.Box(new Rect(12, nextPanel, 490, 24), combatText);
                nextPanel += 24;
            }
            cameraInput.ExcludedGuiRect = diagnostics || Session.EconomyEnabled || combatDemo
                ? new Rect(12, 12, 490, nextPanel - 12) : Panel;
            if (!dragging) return;
            Vector2 a = ToGui(start), b = ToGui(pointer);
            var area = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            Color saved = GUI.color;
            GUI.color = new Color(.2f, .8f, 1, .22f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(.35f, .9f, 1, .9f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(area.x, area.yMax, area.width, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(area.x, area.y, 1, area.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(area.xMax, area.y, 1, area.height), Texture2D.whiteTexture);
            GUI.color = saved;
        }

        private static Vector2 ToGui(Vector2 point) => new(point.x, Screen.height - point.y);
        private static double Since(long timestamp) => (Stopwatch.GetTimestamp() - timestamp) * (1000.0 / Stopwatch.Frequency);
        private static Material MakeMaterial(Shader shader, Color color)
        {
            var material = new Material(shader) { enableInstancing = true };
            material.SetColor("_BaseColor", color);
            return material;
        }

        private void OnDisable()
        {
            if (economyDemo && streaming != null) streaming.EnvironmentLayer?.SetResourceFilter(null);
            Session?.Dispose();
            Session = null;
            if (cube != null) Destroy(cube);
            if (materials == null) return;
            foreach (var material in materials)
                if (material != null) Destroy(material);
        }
    }
}
