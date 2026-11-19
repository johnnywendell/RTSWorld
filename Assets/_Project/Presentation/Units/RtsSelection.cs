using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace RTSWorld.Units.Presentation
{
    public enum SelectionMode { Replace, Add, Toggle }

    public readonly struct SelectableUnit
    {
        public SelectableUnit(ulong id, Entity entity, Vector3 center, Vector3 size)
        {
            Id = id;
            Entity = entity;
            Bounds = new Bounds(center, size);
        }

        public ulong Id { get; }
        public Entity Entity { get; }
        public Bounds Bounds { get; }
    }

    public sealed class RtsSelection
    {
        private readonly HashSet<ulong> selected = new();
        private readonly HashSet<ulong> live = new();
        public int Count => selected.Count;
        public bool Contains(ulong id) => selected.Contains(id);
        public void Clear() => selected.Clear();

        public void RetainLive(List<SelectableUnit> units)
        {
            if (selected.Count == 0) return;
            live.Clear();
            foreach (var unit in units)
                if (selected.Contains(unit.Id)) live.Add(unit.Id);
            selected.IntersectWith(live);
        }

        public void Click(Camera camera, Vector2 point, List<SelectableUnit> units, SelectionMode mode, float maximumDistance)
        {
            if (mode == SelectionMode.Replace) selected.Clear();
            Ray ray = camera.ScreenPointToRay(point);
            ulong nearest = 0;
            foreach (var unit in units)
            {
                if (!unit.Bounds.IntersectRay(ray, out float distance) || distance >= maximumDistance)
                    continue;
                nearest = unit.Id;
                maximumDistance = distance;
            }

            if (nearest != 0) Apply(nearest, mode);
        }

        public void Rectangle(Camera camera, Rect rect, List<SelectableUnit> units, SelectionMode mode,
            System.Func<Vector3, bool> visible)
        {
            if (mode == SelectionMode.Replace) selected.Clear();
            foreach (var unit in units)
            {
                Vector3 point = camera.WorldToScreenPoint(unit.Bounds.center);
                if (point.z < camera.nearClipPlane || point.z > camera.farClipPlane ||
                    !camera.pixelRect.Contains(point) || !rect.Contains(point) || !visible(unit.Bounds.center))
                    continue;
                Apply(unit.Id, mode);
            }
        }

        private void Apply(ulong id, SelectionMode mode)
        {
            if (mode == SelectionMode.Toggle && selected.Remove(id)) return;
            selected.Add(id);
        }
    }
}
