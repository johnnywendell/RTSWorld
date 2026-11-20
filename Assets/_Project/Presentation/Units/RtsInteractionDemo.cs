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
    public sealed partial class RtsInteractionDemo : MonoBehaviour
    {
        [SerializeField] private RtsCameraController rig;
        [SerializeField] private RtsCameraInput cameraInput;
        [SerializeField] private ChunkManager streaming;
        [SerializeField, Range(1, 8)] private int spacing = 1;
        [SerializeField, Range(2, 5000)] private int population = 100;
        [SerializeField] private bool integratedWorld;
        [SerializeField] private bool economyDemo;
        [SerializeField] private bool constructionDemo;
        [SerializeField] private bool autonomyDemo;
        [SerializeField] private bool combatDemo;
        private BuildingKind? placement;
        private int2 previewCell;
        private bool previewVisible;
        private PlacementIssue previewIssue;
        private Entity selectedSite;
        private static readonly Rect ConstructionPanel = new(510, 12, 335, 145);
        [SerializeField] private bool crowdDemo;
        [SerializeField] private bool crowdRecoveryDemo;
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
            InitializeVisualStyle();
            cube = UnitDemo.MakeCube();
            pickingHeight = PickingHeight;
            var shader = visualMarkerMaterial != null ? visualMarkerMaterial.shader : Shader.Find("Universal Render Pipeline/Unlit");
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
            selectedSite = Entity.Null;
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
            Session = new UnitSession(LogicalTerrain, min, max, crowdEnabled: crowdDemo);
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
                int spawned = IntegratedSpawn.Place(Session, navigation, center, count, combatDemo, visualStyleDemo ? 4 : 12);
                if (spawned != count) feedback = $"Terreno navegável para {spawned}/{count} unidades";
                if (economyDemo && spawned > 0 && obstacles != null)
                {
                    var depot = FindDepot(center);
                    Session.ConfigureEconomy(depot, gatherProductivity,
                        startingWood: autonomyDemo ? 30 : 0, startingStone: autonomyDemo ? 20 : 0);
                    if ((constructionDemo || (!combatDemo && !crowdDemo)) && !Session.EnablePhysicalDepots(depot, 0, preservePassages: true))
                        feedback = "Sem área para o depósito inicial";
                    if (constructionDemo && Session.SpawnMainCenter(depot, 0, preservePassages: true) == Entity.Null)
                        feedback = "Sem área para o centro inicial";
                    if (autonomyDemo) PrepareAutonomyDemo(depot);
                    Session.ResourceDepleted += ResourceDepleted;
                    SetDemoResourceFilter();
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

        private void PrepareAutonomyDemo(int2 depot)
        {
            var manager = Session.World.EntityManager;
            using var allUnits = manager.CreateEntityQuery(typeof(UnitIdentity));
            using var entities = allUnits.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var identity = manager.GetComponentData<UnitIdentity>(entity);
                if (identity.Owner == 0)
                    Session.EnableAutonomy(entity, ModeFor(identity), autoBuild: true);
            }

            for (int radius = 3; radius <= 12; radius++)
                for (int z = -radius; z <= radius; z++)
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (math.max(math.abs(x), math.abs(z)) != radius) continue;
                        if (Session.PlaceBuilding(BuildingKind.Depot, depot + new int2(x, z), 0, preservePassages: true) != Entity.Null)
                            return;
                    }
        }

        private static AutonomyMode ModeFor(UnitIdentity identity) =>
            identity.Kind == UnitKind.Villager ? AutonomyMode.Work : AutonomyMode.Guard;

        private void ResumeSelectedAutonomy()
        {
            var manager = Session.World.EntityManager;
            foreach (var unit in selectable)
            {
                if (!selection.Contains(unit.Id) || !manager.Exists(unit.Entity)) continue;
                Session.EnableAutonomy(unit.Entity, ModeFor(manager.GetComponentData<UnitIdentity>(unit.Entity)), true);
            }
        }

        private void Update()
        {
            if (Session == null) return;
            if (persistenceDemo) CompletePersistenceOperation();
            if (integratedWorld && WorldConfigurationChanged()) Rebuild(population);
            long stamp = Stopwatch.GetTimestamp();
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null || !Application.isFocused)
            {
                previewVisible = false;
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
                allowed &= !constructionDemo || !ConstructionPanel.Contains(ToGui(pointer));
                allowed &= !persistenceDemo || !PersistencePanel.Contains(ToGui(pointer));
                previewVisible = placement.HasValue && allowed && TryConstructionCell(out previewCell);
                if (previewVisible)
                    previewIssue = Session.ValidateBuilding(placement.Value, previewCell, 0);

                if (placement.HasValue && rightPressed)
                {
                    placement = null;
                    previewVisible = false;
                    rightPressed = false;
                }
                if (placement.HasValue && leftPressed)
                {
                    if (previewVisible && previewIssue == PlacementIssue.None)
                    {
                        var site = Session.PlaceBuilding(placement.Value, previewCell, 0);
                        if (site != Entity.Null)
                        {
                            selectedSite = site;
                            Issue(UnitOrder.Build(site));
                            placement = null;
                            previewVisible = false;
                        }
                    }
                    leftPressed = false;
                }
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

            if (constructionDemo && TryPickBuilding(ray, distance, out var site))
            {
                selectedSite = site;
                return Issue(UnitOrder.Build(site));
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

        private bool TryConstructionCell(out int2 cell)
        {
            cell = default;
            if (!RtsSurfaceRay.TryHit(Surface, View.ScreenPointToRay(pointer), View.farClipPlane,
                out _, out var logical, pickingHeight)) return false;
            cell = new int2(Mathf.FloorToInt(logical.x), Mathf.FloorToInt(logical.y));
            return true;
        }

        private bool TryBuildingBounds(BuildingKind kind, int2 origin, float height, out Bounds bounds)
        {
            bounds = default;
            var definition = BuildingDefinition.Get(kind);
            double2 center = origin + (double2)definition.Size * .5;
            if (!Session.TryGetTerrainElevation(center.x, center.y, out double elevation)) return false;
            bounds = new Bounds(WorldPosition(new double3(center.x, elevation, center.y)) + Vector3.up * height * .5f,
                new Vector3(definition.Size.x, height, definition.Size.y));
            return true;
        }

        private bool TryPickBuilding(Ray ray, float distance, out Entity target)
        {
            target = Entity.Null;
            var manager = Session.World.EntityManager;
            foreach (Entity entity in Session.Buildings)
            {
                var site = manager.GetComponentData<Building>(entity);
                if (site.State == ConstructionState.Completed ||
                    !TryBuildingBounds(site.Kind, site.Origin, .5f, out var bounds)) continue;
                if (Themed && TryGetVisualBuildingSupport(site.Kind, site.Origin, out _, out double high))
                {
                    double2 center = site.Origin + (double2)BuildingDefinition.Get(site.Kind).Size * .5;
                    bounds.center = WorldPosition(new double3(center.x, high, center.y)) + Vector3.up * bounds.extents.y;
                }
                if (bounds.IntersectRay(ray, out float depth) && depth <= distance)
                {
                    target = entity;
                    distance = depth;
                }
            }
            return target != Entity.Null;
        }

        private void DrawBuildings()
        {
            var manager = Session.World.EntityManager;
            foreach (Entity entity in Session.Buildings)
            {
                var building = manager.GetComponentData<Building>(entity);
                var definition = BuildingDefinition.Get(building.Kind);
                float progress = (float)(building.Work / definition.Work);
                float height = .22f + progress * (building.Kind == BuildingKind.MainCenter ? 2f : 1.2f);
                if (!TryBuildingBounds(building.Kind, building.Origin, height, out var bounds)) continue;
                if (StyledBuilding(building, ref bounds)) continue;
                Add(building.State == ConstructionState.Completed ? 2 : 1, bounds.center, bounds.size);
                if (building.State == ConstructionState.Completed)
                {
                    Add(0, bounds.center + Vector3.up * height * .55f,
                        new Vector3(bounds.size.x * .8f, .24f, bounds.size.z * .8f));
                    if (building.Kind == BuildingKind.Depot)
                        Add(1, bounds.center + Vector3.up * (height * .5f + .35f), Vector3.one * .58f);
                }
                else
                {
                    Add(0, bounds.center + Vector3.up * (height * .5f + .12f),
                        new Vector3(math.max(.05f, progress) * bounds.size.x, .12f, .28f));
                }
            }

            if (previewVisible && placement.HasValue &&
                TryBuildingBounds(placement.Value, previewCell, .13f, out var preview))
                Add(previewIssue == PlacementIssue.None ? 2 : 1, preview.center + Vector3.up * .18f, preview.size);
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
            Session == null || Session.IsResourceAvailable(key);

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
            var members = storage.GetSubArray(0, FillSelected(storage));
            var result = Session.ApplyOrder(order, 0, members);
#if UNITY_EDITOR
            if (showSelectedRoute && members.Length == 1 && order.Kind == UnitOrderKind.Move)
                CaptureMoveDiagnostic(members[0], order.Destination, result);
#endif
            if (result == UnitOrderResult.Accepted) commands++;
            feedback = result switch
            {
                UnitOrderResult.Accepted => order.Kind switch
                {
                    UnitOrderKind.Move => "Mover",
                    UnitOrderKind.Attack => "Atacar",
                    UnitOrderKind.Build => "Construir",
                    _ => "Parar"
                },
                UnitOrderResult.Empty when order.Kind == UnitOrderKind.Attack => "Selecione soldados",
                UnitOrderResult.Empty when order.Kind == UnitOrderKind.Build => "Selecione aldeões",
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
            BeginVisualStyle();
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
                StyledUnit(poses[i], identities[i], ids[i].Value, motions[i].Activity == UnitActivity.Moving, feet);
                if (!Themed) Add(combatDemo && identities[i].Owner != 0 ? Enemy : kind, center, size);
                if (combatDemo)
                    DrawCombat(feet, center, size, health[i], combat[i], combatTargets.Contains(entities[i]));
                if (selection.Contains(ids[i].Value)) Add(2, feet + Vector3.up * .07f, new Vector3(1.04f, .12f, 1.04f));
            }

            DrawBuildings();
            DrawStyledEnvironment();
            prototypes?.End();
            if (Session.EconomyEnabled && !Session.UsesPhysicalDepots)
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

        public bool OrderCrowd(bool converge)
        {
            if (!crowdDemo || !Session.EconomyEnabled) return false;
            Session.CompleteJobs();
            var depot = Session.DepotCell;
            if (!navigation.TryGetCost(depot, out int cost) || cost == 0) return false;
            var component = NavigationSearch.BuildFlowField(navigation, depot);
            int2 west = NearestReachable(depot - new int2(18, 0));
            int2 east = NearestReachable(depot + new int2(18, 0));
            using var units = query.ToEntityArray(Allocator.Temp);
            using var identities = query.ToComponentDataArray<UnitIdentity>(Allocator.Temp);
            using var left = new NativeList<Entity>(Allocator.Temp);
            using var right = new NativeList<Entity>(Allocator.Temp);
            for (int i = 0; i < units.Length; i++)
            {
                if (identities[i].Owner == 0) left.Add(units[i]);
                else right.Add(units[i]);
            }
            bool first = left.Length > 0 && Session.SetDestination(left.AsArray(), converge ? depot : east);
            bool second = right.Length > 0 && Session.SetDestination(right.AsArray(), converge ? depot : west);
            return first && second;

            int2 NearestReachable(int2 target)
            {
                int2 result = depot;
                double best = math.lengthsq((double2)target - depot);
                for (int i = 0; i < navigation.Size.x * navigation.Size.y; i++)
                {
                    int2 cell = navigation.Min + new int2(i % navigation.Size.x, i / navigation.Size.x);
                    double distance = math.lengthsq((double2)target - cell);
                    if (distance >= best || !component.TryGetDirection(cell, out _)) continue;
                    best = distance;
                    result = cell;
                }
                return result;
            }
        }

        private void OnDrawGizmos()
        {
            DrawVisualTerrainGizmos();
#if UNITY_EDITOR
            DrawNavigationDiagnostics();
#endif
            if (!crowdDemo || !diagnostics || Session == null) return;
            Session.CompleteJobs();
            var manager = Session.World.EntityManager;
            using var units = query.ToEntityArray(Allocator.Temp);
            int shown = math.min(units.Length, 100);
            for (int i = 0; i < shown; i++)
            {
                var entity = units[i];
                var pose = manager.GetComponentData<LogicalPose>(entity);
                var motion = manager.GetComponentData<UnitMotion>(entity);
                var attributes = manager.GetComponentData<UnitAttributes>(entity);
                var velocity = manager.GetComponentData<MovementVelocity>(entity);
                var start = WorldPosition(pose.Position) + Vector3.up * .08f;
                var end = WorldPosition(pose.Position + velocity.Value * .3) + Vector3.up * .08f;
                Gizmos.color = motion.IsCongested ? Color.red : Color.Lerp(Color.cyan, Color.yellow, motion.NearbyUnits / 8f);
                Gizmos.DrawWireSphere(start, attributes.OccupancyRadius);
                Gizmos.DrawLine(start, end);
            }
        }

        private static string PlacementDescription(PlacementIssue issue)
        {
            if (issue == PlacementIssue.None) return "Área disponível";
            var reasons = new List<string>();
            if (issue.HasFlag(PlacementIssue.Bounds)) reasons.Add("fora da área");
            if (issue.HasFlag(PlacementIssue.Water)) reasons.Add("água");
            if (issue.HasFlag(PlacementIssue.Slope)) reasons.Add("declive");
            if (issue.HasFlag(PlacementIssue.Obstacle)) reasons.Add("árvores ou rochas");
            if (issue.HasFlag(PlacementIssue.Building)) reasons.Add("construção");
            if (issue.HasFlag(PlacementIssue.Unit)) reasons.Add("unidade");
            if (issue.HasFlag(PlacementIssue.Access)) reasons.Add("sem acesso");
            if (issue.HasFlag(PlacementIssue.Cost)) reasons.Add("estoque insuficiente");
            return string.Join(" · ", reasons);
        }

        private void OnGUI()
        {
            if (Session == null) return;
            if (visualObserver != null && visualObserver.LoadingFirstView)
            {
                GUI.Box(new Rect((Screen.width - 240) * .5f, (Screen.height - 34) * .5f, 240, 34), "Carregando terreno");
                return;
            }
            if (persistenceDemo) DrawPersistenceControls();
            if (constructionDemo)
            {
                GUILayout.BeginArea(ConstructionPanel, GUI.skin.box);
                GUILayout.Label("Construir · clique para posicionar · direito para sair");
                foreach (BuildingKind kind in System.Enum.GetValues(typeof(BuildingKind)))
                {
                    var definition = BuildingDefinition.Get(kind);
                    if (GUILayout.Toggle(placement == kind, $"{definition.Name} · {definition.Wood} madeira · {definition.Stone} pedra", GUI.skin.button))
                        placement = kind;
                }
                if (previewVisible) GUILayout.Label(PlacementDescription(previewIssue));
                else if (selectedSite != Entity.Null)
                {
                    var building = Session.World.EntityManager.GetComponentData<Building>(selectedSite);
                    var definition = BuildingDefinition.Get(building.Kind);
                    GUILayout.Label($"{definition.Name} · {building.Work / definition.Work:P0} · Vida {building.Health}/{definition.Health}");
                    if (building.State is ConstructionState.Planned or ConstructionState.UnderConstruction &&
                        GUILayout.Button("Cancelar construção") && Session.CancelBuilding(selectedSite, 0))
                        selectedSite = Entity.Null;
                }
                GUILayout.EndArea();
            }
            var controls = Panel;
            if (autonomyDemo) controls.height += 24;
            if (visualStyleDemo) controls.height += (VisualTerrainDiagnostics ? 137 : 105) + (environmentBenchmark ? 23 : 0);
            if (crowdRecoveryDemo) controls.height += 48;
            GUILayout.BeginArea(controls, GUI.skin.box);
            GUILayout.Label("Selecionar: clique ou arraste · Shift: adicionar · Ctrl: alternar");
            GUILayout.Label(autonomyDemo ? "Direito: atacar, coletar, construir ou mover · Espaço: parar" :
                constructionDemo ? "Direito: coletar, construir ou mover · Espaço: parar · WASD: pan" :
                combatDemo ? "Direito: atacar, coletar ou mover · Espaço: parar" : economyDemo ? "Direito: coletar ou mover · Espaço: parar · WASD: pan" :
                "Direito: mover · Espaço: parar · WASD / meio do mouse: pan");
            GUILayout.Label("Scroll: zoom · Q/E: girar");
            GUILayout.BeginHorizontal();
            foreach (int count in Populations)
                if (GUILayout.Button(count.ToString())) Rebuild(count);
            diagnostics = GUILayout.Toggle(diagnostics, "Diagnóstico");
            rig.Settings.EnableEdgePan = GUILayout.Toggle(rig.Settings.EnableEdgePan, "Bordas");
            GUILayout.EndHorizontal();
            if (crowdDemo && !constructionDemo)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Fluxos opostos")) feedback = OrderCrowd(false) ? "Fluxos em movimento" : "Rota indisponível";
                if (GUILayout.Button("Convergir")) feedback = OrderCrowd(true) ? "Destino compartilhado" : "Rota indisponível";
                GUILayout.EndHorizontal();
            }
            if (crowdRecoveryDemo)
            {
                Session.RecoveryEnabled = GUILayout.Toggle(Session.RecoveryEnabled, "Recuperação lateral");
                DrawCrowdScenarios();
            }
            if (autonomyDemo && GUILayout.Button("Retomar autonomia das selecionadas"))
                ResumeSelectedAutonomy();
            DrawVisualControls();
            GUILayout.Label($"Selecionadas {selection.Count} · {feedback}");
            GUILayout.EndArea();
            float hudHeight = crowdDemo ? 112 : integratedWorld ? 92 : 74;
            float nextPanel = (autonomyDemo ? 183 : 159) + (crowdRecoveryDemo ? 48 : 0) + (visualStyleDemo ? ((VisualTerrainDiagnostics ? 137 : 105) + (environmentBenchmark ? 23 : 0)) : 0);
            if (diagnostics)
            {
                var label = crowdDemo
                    ? statistics + $"\nAtlas {Session.FieldHits}/{Session.FieldHits + Session.FieldMisses} acertos · {Session.FieldCapacityDenials} recusas"
                    : statistics;
                GUI.Box(new Rect(12, nextPanel, 490, hudHeight), label);
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
            if (autonomyDemo)
            {
                GUI.Box(new Rect(12, nextPanel, 490, 24),
                    $"IA · {Session.AutonomyDecisions} decisões no tick · {Session.AutonomyAssignments} atribuições");
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
            if (visualStyleDemo && visualPipeline != null && QualitySettings.renderPipeline == visualPipeline)
                QualitySettings.renderPipeline = previousPipeline;
            if (visualStyleDemo && streaming != null && streaming.EnvironmentLayer != null)
            {
                streaming.EnvironmentLayer.SetInstanceTreesAndRocks(false);
                streaming.EnvironmentLayer.SetArtProfile(false);
            }
            if (visualObserver != null) visualObserver.HoldUncoveredView = false;
            environmentChunks.Clear();
            prototypes?.Dispose();
            prototypes = null;
            if (cube != null) Destroy(cube);
            if (materials == null) return;
            foreach (var material in materials)
                if (material != null) Destroy(material);
        }
    }
}
