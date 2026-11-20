using System;
using System.IO;
using System.Threading.Tasks;
using RTSWorld.Simulation;
using Unity.Entities;
using UnityEngine;

namespace RTSWorld.Units.Presentation
{
    public sealed partial class RtsInteractionDemo
    {
        [SerializeField] private bool persistenceDemo;
        private static readonly Rect PersistencePanel = new(510, 164, 335, 68);
        private Task persistenceOperation;
        private string persistenceStatus = "";
        public bool PersistenceBusy => persistenceOperation != null;
        public string PersistenceStatus => persistenceStatus;

        private static string SavePath =>
#if UNITY_EDITOR
            Path.Combine(Application.dataPath, "..", "Library", "RTSWorld", "simulation.rts");
#else
            Path.Combine(Application.persistentDataPath, "simulation.rts");
#endif

        private void DrawPersistenceControls()
        {
            GUILayout.BeginArea(PersistencePanel, GUI.skin.box);
            GUILayout.BeginHorizontal();
            bool guiEnabled = GUI.enabled;
            GUI.enabled = guiEnabled && !PersistenceBusy;
            if (GUILayout.Button("Salvar")) SaveSimulation(SavePath);
            if (GUILayout.Button("Carregar")) LoadSimulation(SavePath);
            if (GUILayout.Button("Novo jogo"))
            {
                Rebuild(population);
                persistenceStatus = "Novo jogo iniciado.";
            }
            GUI.enabled = guiEnabled;
            GUILayout.EndHorizontal();
            if (persistenceStatus.Length != 0) GUILayout.Label(persistenceStatus);
            GUILayout.EndArea();
        }

        public void SaveSimulation(string path)
        {
            if (PersistenceBusy || Session == null) return;
            try
            {
                persistenceOperation = SimulationSaveFile.SaveAsync(path, Session.CaptureSnapshot());
                persistenceStatus = "Salvando…";
            }
            catch (Exception exception)
            {
                persistenceStatus = $"Falha ao salvar. {exception.Message}";
            }
        }

        public void LoadSimulation(string path)
        {
            if (PersistenceBusy || Session == null) return;
            persistenceOperation = SimulationSaveFile.LoadAsync(path);
            persistenceStatus = "Carregando…";
        }

        private void CompletePersistenceOperation()
        {
            if (persistenceOperation == null || !persistenceOperation.IsCompleted) return;
            var operation = persistenceOperation;
            persistenceOperation = null;
            try
            {
                operation.GetAwaiter().GetResult();
                if (operation is Task<SimulationSnapshot> load)
                {
                    ApplyLoadedSimulation(load.Result);
                    persistenceStatus = "Jogo carregado.";
                }
                else persistenceStatus = "Jogo salvo.";
            }
            catch (Exception exception)
            {
                persistenceStatus = $"Falha na persistência. {exception.Message}";
            }
        }

        private void ApplyLoadedSimulation(SimulationSnapshot saved)
        {
            var layer = streaming.EnvironmentLayer;
            if (!saved.HasNavigation || saved.EnvironmentVersion == 0 || layer == null || layer.Material == null)
                throw new InvalidDataException("O salvamento exige a configuração ambiental da demonstração.");

            var loaded = UnitSession.RestoreSnapshot(saved);
            Session.Dispose();
            Session = loaded;
            LogicalTerrain = loaded.Terrain;
            navigation = loaded.Navigation;
            useTerrainSnapshot = false;
            streaming.ApplyWorldDefinition(LogicalTerrain.Definition);
            layer.enabled = true;
            layer.Configure(layer.Material, saved.SeaLevel, saved.EnvironmentVersion);
            loaded.ResourceDepleted += ResourceDepleted;
            SetDemoResourceFilter();
            query = loaded.World.EntityManager.CreateEntityQuery(typeof(UnitIdentity), typeof(LogicalEntityId),
                typeof(LogicalPose), typeof(UnitMotion), typeof(UnitHealth), typeof(UnitCombat));
            selection.Clear();
            selectable.Clear();
            enemies.Clear();
            combatTargets.Clear();
            selectedSite = Entity.Null;
            placement = null;
            tracking = dragging = previewVisible = false;
            moving = commands = 0;
            markerUntil = 0;
            feedback = "";
        }
    }
}
