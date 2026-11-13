using RTSWorld.Presentation.Streaming;
using UnityEditor;
using UnityEngine;

namespace RTSWorld.Presentation.Editor
{
    [CustomEditor(typeof(ChunkManager))]
    public sealed class ChunkManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var manager = (ChunkManager)target;
            EditorGUILayout.LabelField("Centro", manager.Center.ToString());
            EditorGUILayout.LabelField("Ativos / fila", $"{manager.ActiveCount} / {manager.PendingCount}");
            EditorGUILayout.LabelField("Gerados / removidos", $"{manager.TotalGenerated} / {manager.TotalUnloaded}");
            EditorGUILayout.LabelField("Step / pico ms", $"{manager.LastStepMilliseconds:F3} / {manager.PeakStepMilliseconds:F3}");
            if (GUILayout.Button("Processar um frame de streaming"))
            {
                try { manager.Step(); }
                catch (System.Exception exception) { Debug.LogException(exception, manager); }
            }
            if (GUILayout.Button("Limpar apresentação")) manager.Clear();
            if (manager.PendingCount > 0 || Application.isPlaying) Repaint();
        }
    }
}
