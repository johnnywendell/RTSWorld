using RTSWorld.Presentation.Terrain;
using UnityEditor;
using UnityEngine;

namespace RTSWorld.Presentation.Editor
{
    [CustomEditor(typeof(TerrainDemo))]
    public sealed class TerrainDemoEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Change seed/scales, then Rebuild Terrain. Four adjacent chunks; no streaming.", MessageType.Info);
            if (GUILayout.Button("Rebuild Terrain"))
            {
                try { ((TerrainDemo)target).Rebuild(); }
                catch (System.Exception exception) { Debug.LogException(exception, target); }
            }
        }
    }
}
