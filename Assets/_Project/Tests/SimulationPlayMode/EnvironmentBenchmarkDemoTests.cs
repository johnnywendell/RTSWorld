#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using RTSWorld.Units.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RTSWorld.Tests
{
    public sealed class EnvironmentBenchmarkDemoTests
    {
        [UnityTest]
        public IEnumerator BenchmarkProfilesDensityGroundCoverAndShadows()
        {
            const string path = "Assets/_Project/Scenes/EnvironmentBenchmarkDemo.unity";
            var scene = EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            AsyncOperation unload = null;
            try
            {
                yield return null;
                var demo = scene.GetRootGameObjects().Select(o => o.GetComponentInChildren<RtsInteractionDemo>())
                    .First(d => d != null);
                string csv = null;
                yield return demo.MeasureEnvironment(value => csv = value);
                Assert.That(csv, Is.Not.Null);
                Assert.That(csv.Split('\n').Count(row => row.Length > 0 && char.IsDigit(row[0])), Is.EqualTo(17));
                TestContext.WriteLine(csv);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) unload = SceneManager.UnloadSceneAsync(scene);
            }
            if (unload != null) yield return unload;
        }
    }
}
#endif
