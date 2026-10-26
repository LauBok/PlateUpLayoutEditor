using System;
using System.Collections.Generic;
using PlateUpLayoutEditor.Core;
using UnityEngine;

namespace KitchenLayoutEditor
{
    /// <summary>Local-only outlines drawn on the floor for the hovered tile, selection and paste footprint.</summary>
    internal sealed class TileOverlay : IDisposable
    {
        private const float Height = 0.06f;
        private readonly List<LineRenderer> _lines = new List<LineRenderer>();
        private readonly GameObject _root;
        private readonly Material? _material;

        public TileOverlay()
        {
            _root = new GameObject("LayoutEditorOverlay");
            UnityEngine.Object.DontDestroyOnLoad(_root);
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader != null) _material = new Material(shader);
        }

        public void Show(List<(GridRect rect, Color color)> rects)
        {
            if (_material == null) return;
            while (_lines.Count < rects.Count) _lines.Add(CreateLine());
            for (int i = 0; i < _lines.Count; i++)
            {
                var line = _lines[i];
                if (i >= rects.Count) { line.enabled = false; continue; }
                var (rect, color) = rects[i];
                float x0 = rect.MinX - 0.5f, x1 = rect.MaxX + 0.5f, z0 = rect.MinY - 0.5f, z1 = rect.MaxY + 0.5f;
                line.positionCount = 4;
                line.SetPosition(0, new Vector3(x0, Height, z0));
                line.SetPosition(1, new Vector3(x1, Height, z0));
                line.SetPosition(2, new Vector3(x1, Height, z1));
                line.SetPosition(3, new Vector3(x0, Height, z1));
                line.startColor = line.endColor = color;
                line.enabled = true;
            }
        }

        public void Hide()
        {
            foreach (var line in _lines) line.enabled = false;
        }

        private LineRenderer CreateLine()
        {
            var go = new GameObject("Outline");
            go.transform.SetParent(_root.transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.material = _material;
            line.loop = true;
            line.useWorldSpace = true;
            line.widthMultiplier = 0.06f;
            line.enabled = false;
            return line;
        }

        public void Dispose()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
        }
    }
}
