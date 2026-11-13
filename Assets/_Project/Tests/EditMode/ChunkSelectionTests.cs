using System;
using System.Collections.Generic;
using NUnit.Framework;
using RTSWorld.World.Chunks;

namespace RTSWorld.World.Tests
{
    public sealed class ChunkSelectionTests
    {
        [TestCase(0, 0, 0, 0)]
        [TestCase(31.999, 32, 0, 1)]
        [TestCase(-0.001, -32, -1, -1)]
        [TestCase(-32.001, 64, -2, 2)]
        [TestCase(64, -64, 2, -2)]
        public void PositionUsesFloor(double x, double z, int expectedX, int expectedZ) =>
            Assert.That(ChunkSelection.FromPosition(x, z, 32), Is.EqualTo(new ChunkCoordinate(expectedX, expectedZ)));

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(8)]
        public void WindowIsUniqueSquareAndNearFirst(int radius)
        {
            var list = new List<ChunkCoordinate> { new ChunkCoordinate(99, 99) };
            var center = new ChunkCoordinate(-3, 2);
            ChunkSelection.Fill(center, radius, list);
            Assert.That(list.Count, Is.EqualTo((2 * radius + 1) * (2 * radius + 1)));
            Assert.That(new HashSet<ChunkCoordinate>(list).Count, Is.EqualTo(list.Count));
            Assert.That(list[0], Is.EqualTo(center));
            int previous = 0;
            foreach (var coordinate in list)
            {
                int distance = Math.Max(Math.Abs(coordinate.X - center.X), Math.Abs(coordinate.Z - center.Z));
                Assert.That(distance, Is.InRange(previous, radius));
                previous = distance;
            }
        }

        [Test]
        public void ExtremeCoordinatesClipRatherThanWrap()
        {
            var list = new List<ChunkCoordinate>();
            ChunkSelection.Fill(new ChunkCoordinate(int.MaxValue, int.MinValue), 1, list);
            Assert.That(list.Count, Is.EqualTo(4));
            foreach (var coordinate in list)
            {
                Assert.That(coordinate.X, Is.GreaterThanOrEqualTo(int.MaxValue - 1));
                Assert.That(coordinate.Z, Is.LessThanOrEqualTo(int.MinValue + 1));
            }
        }

        [Test]
        public void InvalidInputsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ChunkSelection.FromPosition(double.NaN, 0, 32));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChunkSelection.FromPosition(double.PositiveInfinity, 0, 32));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChunkSelection.FromPosition(double.MaxValue, 0, 32));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChunkSelection.FromPosition(0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChunkSelection.ValidateRadius(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChunkSelection.ValidateRadius(9));
        }
    }
}
