using UnityEngine;

namespace RTSWorld.Presentation.Streaming
{
    /// <summary>Demo controls only: step observer and keep a fixed camera offset. Not an RTS camera.</summary>
    public sealed class StreamingDemo : MonoBehaviour
    {
        [SerializeField] private ChunkManager manager;
        [SerializeField] private Transform observer;
        [SerializeField] private Transform demoCamera;
        [SerializeField] private float stepDistance = 64;
        private Vector3 cameraOffset;

        public void Configure(ChunkManager chunks, Transform target, Transform cameraTransform)
        { manager = chunks; observer = target; demoCamera = cameraTransform; }

        private void OnEnable()
        { if (demoCamera != null && observer != null) cameraOffset = demoCamera.position - observer.position; }

        private void LateUpdate()
        { if (demoCamera != null && observer != null) demoCamera.position = observer.position + cameraOffset; }

        private void OnGUI()
        {
            if (manager == null || observer == null) return;
            GUILayout.BeginArea(new Rect(12, 12, 330, 275), GUI.skin.box);
            GUILayout.Label("WORLD-003 | Observador de teste");
            GUILayout.Label($"Centro {manager.Center} | ativos {manager.ActiveCount} | fila {manager.PendingCount}");
            GUILayout.Label($"Seed {manager.Seed} | raio {manager.LoadRadius}");
            GUILayout.Label($"Gerados {manager.TotalGenerated} | removidos {manager.TotalUnloaded}");
            GUILayout.Label($"Step {manager.LastStepMilliseconds:F2} ms | pico {manager.PeakStepMilliseconds:F2} ms");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("X -")) observer.position += Vector3.left * stepDistance;
            if (GUILayout.Button("X +")) observer.position += Vector3.right * stepDistance;
            if (GUILayout.Button("Z -")) observer.position += Vector3.back * stepDistance;
            if (GUILayout.Button("Z +")) observer.position += Vector3.forward * stepDistance;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Raio -")) manager.LoadRadius = Mathf.Max(0, manager.LoadRadius - 1);
            if (GUILayout.Button("Raio +")) manager.LoadRadius = Mathf.Min(8, manager.LoadRadius + 1);
            if (GUILayout.Button("Seed +")) manager.Seed = unchecked(manager.Seed + 1);
            GUILayout.EndHorizontal();
            GUILayout.Label("Ou mova Test Observer no Inspector.\nBuracos temporários = fila ainda carregando.");
            GUILayout.EndArea();
        }
    }
}
