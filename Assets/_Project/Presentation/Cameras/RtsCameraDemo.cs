using RTSWorld.Presentation.Streaming;
using UnityEngine;

namespace RTSWorld.Presentation.Cameras
{
    public sealed class RtsCameraDemo : MonoBehaviour
    {
        [SerializeField] private RtsCameraController controller;
        [SerializeField] private RtsCameraInput input;
        [SerializeField] private ChunkManager manager;
        private readonly Rect panel = new Rect(12, 12, 365, 200);
        public void Configure(RtsCameraController rig, RtsCameraInput reader, ChunkManager stream)
        { controller = rig; input = reader; manager = stream; if (input != null) input.ExcludedGuiRect = panel; }
        private void OnEnable() { if (input != null) input.ExcludedGuiRect = panel; }
        private void OnGUI()
        {
            if (controller == null || input == null || manager == null) return;
            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Label("RTSWorld | Câmera RTS");
            GUILayout.Label("WASD: mover · Meio do mouse: arrastar\nScroll: zoom · Q/E: girar");
            controller.Settings.EnableEdgePan = GUILayout.Toggle(controller.Settings.EnableEdgePan, "Mover pelas bordas da tela");
            if (GUILayout.Button("Voltar ao ponto inicial")) controller.ResetView();
            GUILayout.Label($"Regiões carregadas: {manager.ActiveCount} · pendentes: {manager.PendingCount}");
            GUILayout.Label("Velocidade, zoom e inclinação: RTS Camera Rig no Inspector.");
            GUILayout.EndArea();
        }
    }
}
