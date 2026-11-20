#if UNITY_EDITOR
using RTSWorld.Simulation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace RTSWorld.Units.Presentation
{
    public sealed partial class RtsInteractionDemo
    {
        [Header("Diagnóstico de navegação (Scene View, em Play)")]
        [SerializeField] private bool showNavigationGrid;
        [SerializeField] private bool showObstacleFootprints;
        [SerializeField] private bool showNavigationUnits;
        [SerializeField] private bool showFlowDirections;
        [SerializeField] private bool showSelectedRoute;
        [SerializeField, Range(1, 16)] private int diagnosticAreaRadius = 8;

        private static readonly ProfilerMarker DrawNavigationMarker = new("RTSWorld.NavigationDiagnostics.Draw");
        private System.WeakReference<UnitSession> diagnosedSession;
        private ulong diagnosedUnit;
        private int2 requestedCell, commandOrigin;
        private UnitOrderResult commandResult;

        private void CaptureMoveDiagnostic(Entity unit, int2 requested, UnitOrderResult result)
        {
            var manager = Session.World.EntityManager;
            if (diagnosedSession == null) diagnosedSession = new System.WeakReference<UnitSession>(Session);
            else diagnosedSession.SetTarget(Session);
            diagnosedUnit = manager.GetComponentData<LogicalEntityId>(unit).Value;
            commandOrigin = (int2)math.floor(manager.GetComponentData<LogicalPose>(unit).Position.xz);
            requestedCell = requested;
            commandResult = result;
        }

        private void DrawNavigationDiagnostics()
        {
            if (Session == null || navigation == null ||
                !(showNavigationGrid || showObstacleFootprints || showNavigationUnits ||
                  showFlowDirections || showSelectedRoute)) return;

            using var profile = DrawNavigationMarker.Auto();
            int2 focus = new(navigationCenter.x, navigationCenter.y);
            NavigationFlowField field = null;
            int2 destination = default;
            if (selection.Count > 0)
            {
                Session.CompleteJobs();
                var manager = Session.World.EntityManager;
                using var units = query.ToEntityArray(Allocator.Temp);
                int drawn = 0;
                foreach (var unit in units)
                {
                    ulong id = manager.GetComponentData<LogicalEntityId>(unit).Value;
                    if (!selection.Contains(id)) continue;

                    var pose = manager.GetComponentData<LogicalPose>(unit);
                    var motion = manager.GetComponentData<UnitMotion>(unit);
                    if (drawn++ == 0)
                    {
                        focus = (int2)math.floor(pose.Position.xz);
                        if (!Session.TryGetActiveField(motion, out field, out destination)) field = null;
                        if (showSelectedRoute)
                        {
                            var waypoints = manager.GetBuffer<FormationWaypoint>(unit);
                            int2 goal = waypoints.Length > 0 ? waypoints[0].Cell : destination;
                            DrawSelectedRoute(focus, goal, destination, field, motion, waypoints);
                            if (diagnosedUnit == id && diagnosedSession != null &&
                                diagnosedSession.TryGetTarget(out var sourceSession) && sourceSession == Session)
                            {
                                Handles.Label(DiagnosticPoint((double2)commandOrigin + .5), "Origem da última ordem Move");
                                var requested = DiagnosticPoint((double2)requestedCell + .5);
                                var reason = navigation.GetPlacementIssue(requestedCell);
                                Handles.Label(requested, reason == PlacementIssue.None
                                    ? $"Solicitado {requestedCell} · {commandResult}"
                                    : $"Solicitado {requestedCell} · {commandResult} · {reason}");
                            }
                        }
                    }

                    if (showNavigationUnits)
                    {
                        var attributes = manager.GetComponentData<UnitAttributes>(unit);
                        var velocity = manager.GetComponentData<MovementVelocity>(unit);
                        Handles.color = Color.cyan;
                        Vector3 origin = WorldPosition(pose.Position) + Vector3.up * .12f;
                        Handles.DrawWireDisc(origin, streaming.transform.up,
                            attributes.OccupancyRadius * WorldCellSize);
                        if (crowdDemo && attributes.OccupancyRadius > 0)
                        {
                            Handles.color = new Color(.3f, .7f, 1f, .5f);
                            Handles.DrawWireDisc(origin, streaming.transform.up,
                                (attributes.OccupancyRadius + .49f + .48f) * WorldCellSize);
                        }
                        Handles.color = Color.yellow;
                        Handles.DrawLine(origin, WorldPosition(pose.Position + velocity.Value * .3) + Vector3.up * .12f);
                        double2 desired = motion.DesiredVelocity;
                        var cell = (int2)math.floor(pose.Position.xz);
                        if (Session.TryGetActiveField(motion, out var unitField, out _) &&
                            unitField.TryGetDirection(cell, out int2 step))
                        {
                            var route = manager.GetBuffer<FormationWaypoint>(unit);
                            int waypoint = motion.FormationWaypointIndex;
                            if (waypoint < 0 && math.all(step == 0)) waypoint = route.Length - 1;
                            int2 next = waypoint >= 0 && waypoint < route.Length ? route[waypoint].Cell : cell + step;
                            desired = math.normalizesafe((double2)next + .5 - pose.Position.xz) * attributes.MaxSpeed;
                        }
                        if (math.any(desired != 0))
                        {
                            Handles.color = Color.white;
                            var intended = pose.Position;
                            intended.xz += desired * .3;
                            Handles.DrawLine(origin, WorldPosition(intended) + Vector3.up * .12f);
                        }
                    }

                    if (drawn >= 32) break;
                }
            }

            int2 first = math.max(navigation.Min, focus - diagnosticAreaRadius);
            int2 last = math.min(navigation.Min + navigation.Size - 1, focus + diagnosticAreaRadius);
            if (showNavigationGrid || showFlowDirections || showObstacleFootprints)
            {
                for (int z = first.y; z <= last.y; z++)
                for (int x = first.x; x <= last.x; x++)
                {
                    var cell = new int2(x, z);
                    bool present = navigation.TryGetCost(cell, out int cost);
                    int2 direction = default;
                    bool reachable = field != null && field.TryGetDirection(cell, out direction);
                    var issue = navigation.GetPlacementIssue(cell);
                    Vector3 point = DiagnosticPoint((double2)cell + .5);
                    if (showNavigationGrid)
                    {
                        Handles.color = !present ? Color.gray : cost == 0 ? Color.red :
                            field != null && !reachable ? Color.magenta : Color.Lerp(Color.green, Color.yellow, cost / 24f);
                        Handles.DrawWireDisc(point, streaming.transform.up, WorldCellSize * .23f);
                        if (x % navigation.CellsPerChunk == 0 || z % navigation.CellsPerChunk == 0)
                        {
                            Handles.color = Color.white;
                            if (x % navigation.CellsPerChunk == 0)
                                Handles.DrawLine(DiagnosticPoint(cell), DiagnosticPoint(cell + new int2(0, 1)));
                            if (z % navigation.CellsPerChunk == 0)
                                Handles.DrawLine(DiagnosticPoint(cell), DiagnosticPoint(cell + new int2(1, 0)));
                        }
                    }
                    if (showObstacleFootprints && (issue & (PlacementIssue.Obstacle | PlacementIssue.Building)) != 0)
                    {
                        Handles.color = new Color(1f, .45f, .15f);
                        Handles.DrawWireDisc(point, streaming.transform.up, WorldCellSize * .38f);
                    }
                    if (showFlowDirections && reachable)
                    {
                        Handles.color = field.TryGetIntegrationCost(cell, 96, out int remainingCost)
                            ? Color.Lerp(Color.cyan, Color.blue, remainingCost / 100f)
                            : Color.blue;
                        var tip = (double2)cell + .5 + (double2)direction * .42;
                        Handles.DrawLine(point, DiagnosticPoint(tip));
                        Handles.DrawWireDisc(DiagnosticPoint(tip), streaming.transform.up, WorldCellSize * .05f);
                    }
                }
                if (showFlowDirections && field != null)
                    Handles.Label(DiagnosticPoint((double2)destination + .5), $"Campo {destination}");
            }

            if (!showObstacleFootprints || navigation.Obstacles == null) return;
            int obstaclesDrawn = 0;
            foreach (var obstacle in navigation.Obstacles.Entries)
            {
                if (!obstacle.Available || obstacle.Radius <= 0 ||
                    math.any(obstacle.Center < first - 2) || math.any(obstacle.Center > last + 3)) continue;
                var origin = DiagnosticPoint(obstacle.Center);
                Handles.color = Color.white;
                Handles.DrawWireDisc(origin, streaming.transform.up, WorldCellSize * .05f);
                Handles.DrawWireDisc(origin, streaming.transform.up, (float)obstacle.Radius * WorldCellSize);
                Handles.color = Color.yellow;
                Handles.DrawWireDisc(origin, streaming.transform.up,
                    (float)(obstacle.Radius + navigation.Obstacles.Clearance) * WorldCellSize);
                if (++obstaclesDrawn >= 128) break;
            }
        }

        private float WorldCellSize => streaming.HorizontalScale * streaming.transform.TransformVector(Vector3.right).magnitude;

        private Vector3 DiagnosticPoint(double2 point)
        {
            double height = navigation.TryGetSurface(point.x, point.y, out double surface) ? surface :
                Session.TryGetTerrainElevation(point.x, point.y, out double terrainHeight) ? terrainHeight : 0;
            return WorldPosition(new double3(point.x, height, point.y)) + Vector3.up * .12f;
        }

        private void DrawSelectedRoute(int2 origin, int2 goal, int2 destination, NavigationFlowField field,
            UnitMotion motion, DynamicBuffer<FormationWaypoint> waypoints)
        {
            Vector3 startPoint = DiagnosticPoint((double2)origin + .5);
            Handles.Label(startPoint, $"{motion.Progress} · Tentativas {motion.RecoveryAttempts} · {feedback}\nRota {motion.RouteDistanceTravelled:F1} · Sem avanço {motion.NoProgressSeconds:F1}s · Espera {motion.PassageWaitSeconds:F1}s");
            if (field == null) return;

            Handles.Label(DiagnosticPoint((double2)destination + .5), $"Campo / âncora {destination}");
            Handles.Label(DiagnosticPoint((double2)goal + .5), $"Destino efetivo {goal}");
            Handles.color = Color.white;
            int budget = 128;
            int2 cell = origin;
            if (motion.FormationWaypointIndex >= 0)
            {
                for (int i = math.min(motion.FormationWaypointIndex, waypoints.Length - 1); i >= 0 && budget-- > 0; i--)
                {
                    DrawLeg(cell, waypoints[i].Cell);
                    cell = waypoints[i].Cell;
                }
                return;
            }
            while (budget-- > 0 && field.TryGetDirection(cell, out int2 direction) && math.any(direction != 0))
            {
                DrawLeg(cell, cell + direction);
                cell += direction;
            }
            if (cell.Equals(destination))
            {
                for (int i = waypoints.Length - 1; i >= 0 && budget-- > 0; i--)
                {
                    DrawLeg(cell, waypoints[i].Cell);
                    cell = waypoints[i].Cell;
                }
            }

            void DrawLeg(int2 from, int2 to)
            {
                Handles.DrawLine(DiagnosticPoint((double2)from + .5), DiagnosticPoint((double2)to + .5));
            }
        }
    }
}
#endif
