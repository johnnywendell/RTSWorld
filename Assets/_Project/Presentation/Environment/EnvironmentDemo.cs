using RTSWorld.Presentation.Streaming;
using UnityEngine;

namespace RTSWorld.Presentation.Environment
{
    public sealed class EnvironmentDemo : MonoBehaviour
    {
        [System.Serializable]
        public struct Site { public string Name; public long X; public long Z; }
        [SerializeField] private ChunkManager manager;
        [SerializeField] private Transform observer;
        [SerializeField] private Site[] sites;
        public void Configure(ChunkManager stream, Transform target, Site[] landmarks)
        { manager = stream; observer = target; sites = landmarks; }
        private void OnGUI()
        {
            if (manager == null || observer == null || sites == null) return;
            GUILayout.BeginArea(new Rect(Screen.width - 245, 12, 230, 440), GUI.skin.box);
            GUILayout.Label("WORLD-004 | Biomas, água e recursos");
            GUILayout.Label("Referências para seed 12345 / mar 38");
            foreach (var site in sites)
            {
                if (GUILayout.Button(site.Name))
                    observer.position = manager.transform.TransformPoint(new Vector3(site.X * manager.HorizontalScale, 0, site.Z * manager.HorizontalScale));
            }
            GUILayout.Label("Árvores verdes | pedra cinza | ouro amarelo\nÁgua azul opaca, sem simulação física.");
            GUILayout.EndArea();
        }
    }
}
