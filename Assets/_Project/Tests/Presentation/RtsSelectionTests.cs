using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using RTSWorld.Units.Presentation;
using UnityEngine;

namespace RTSWorld.Tests
{
    public sealed class RtsSelectionTests
    {
        private GameObject cameraObject;
        private Camera camera;

        [SetUp]
        public void Setup()
        {
            cameraObject = new GameObject("Selection test camera");
            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 16;
            camera.pixelRect = new Rect(0, 0, 800, 600);
            Pose(50, 30);
        }

        [TearDown]
        public void Cleanup() => Object.DestroyImmediate(cameraObject);

        [Test]
        public void PointSelectsNearestAndSupportsAddToggleClearAndOcclusion()
        {
            var units = new List<SelectableUnit> { Unit(1, Vector3.zero), Unit(2, new Vector3(5, 0, 0)) };
            var selection = new RtsSelection();
            Vector2 first = camera.WorldToScreenPoint(units[0].Bounds.center);
            Vector2 second = camera.WorldToScreenPoint(units[1].Bounds.center);
            selection.Click(camera, first, units, SelectionMode.Replace, 100);
            Assert.That(selection.Contains(1), Is.True);
            selection.Click(camera, second, units, SelectionMode.Add, 100);
            Assert.That(selection.Count, Is.EqualTo(2));
            selection.Click(camera, first, units, SelectionMode.Toggle, 100);
            Assert.That(selection.Contains(1), Is.False);
            selection.Click(camera, second, units, SelectionMode.Replace, 1);
            Assert.That(selection.Count, Is.Zero);
            selection.Click(camera, new Vector2(10, 10), units, SelectionMode.Replace, 100);
            Assert.That(selection.Count, Is.Zero);
        }

        [TestCase(48, 20, 16)]
        [TestCase(62, 120, 8)]
        [TestCase(76, 245, 25)]
        public void RectangleUsesCameraProjectionRejectsOcclusionAndPrunesDestroyedEntities(int pitch, int yaw, int zoom)
        {
            Pose(pitch, yaw);
            camera.orthographicSize = zoom;
            var units = new List<SelectableUnit> { Unit(1, Vector3.zero), Unit(2, new Vector3(5, 0, 0)) };
            var selection = new RtsSelection();
            Vector2 first = camera.WorldToScreenPoint(units[0].Bounds.center);
            selection.Rectangle(camera, new Rect(first - Vector2.one * 10, Vector2.one * 20), units, SelectionMode.Replace, _ => true);
            Assert.That(selection.Count, Is.EqualTo(1));
            Vector2 second = camera.WorldToScreenPoint(units[1].Bounds.center);
            var sector = new Rect(second - Vector2.one * 10, Vector2.one * 20);
            selection.Rectangle(camera, sector, units, SelectionMode.Add, _ => false);
            Assert.That(selection.Count, Is.EqualTo(1));
            selection.Rectangle(camera, sector, units, SelectionMode.Add, _ => true);
            Assert.That(selection.Count, Is.EqualTo(2));
            units.RemoveAt(1);
            selection.RetainLive(units);
            Assert.That(selection.Contains(2), Is.False);
        }

        [TestCase(100)]
        [TestCase(500)]
        [TestCase(1000)]
        [TestCase(5000)]
        public void ProfilesProjectionSelection(int count)
        {
            var units = new List<SelectableUnit>(count);
            for (int i = 0; i < count; i++)
                units.Add(Unit((ulong)i + 1, new Vector3((i % 80) * .05f - 2, 0, (i / 80) * .05f - 2)));
            var selection = new RtsSelection();
            selection.Rectangle(camera, camera.pixelRect, units, SelectionMode.Replace, _ => true);
            var timer = Stopwatch.StartNew();
            for (int i = 0; i < 20; i++) selection.Rectangle(camera, camera.pixelRect, units, SelectionMode.Replace, _ => true);
            Assert.That(selection.Count, Is.EqualTo(count));
            TestContext.WriteLine(System.FormattableString.Invariant($"RTS_SELECTION count={count} mean_ms={timer.Elapsed.TotalMilliseconds / 20:F5}"));
        }

        private void Pose(float pitch, float yaw)
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            camera.transform.SetPositionAndRotation(rotation * Vector3.back * 30, rotation);
        }

        private static SelectableUnit Unit(ulong id, Vector3 point) =>
            new SelectableUnit(id, default, point, Vector3.one);
    }
}
