using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

public class NodeEditor : EditorWindow
{
    [MenuItem("Tools/Teraflu")]
    public static void Init()
    {
        var window = EditorWindow.CreateInstance<NodeEditor>();
        window.Construct();
        window.Show();
    }

    private class Node
    {
        public string Name;
        public Rect Position;
    }

    private class Edge
    {
        public Node Start;
        public Node End;
    }

    private Rect _currentArea = new Rect(0, 0, 10000, 10000);
    private Vector2 _position;
    private List<Node> _nodes;
    private List<Edge> _edges;

    private GUIStyle _guiStyle = new GUIStyle();

    private void Construct()
    {
        var numberOfNodes = 10000;
        _nodes = Enumerable.Range(0, numberOfNodes)
            .Select(i => new Node() {
                Name = i.ToString(),
                Position = new Rect(Random.Range(100f, 9900f), Random.Range(100f, 9900f), 100f, 100f)
            }).ToList();

        _edges = Enumerable.Range(0, 5000)
            .Select(i => new Edge {
                Start = _nodes[Random.Range(0, _nodes.Count)],
                End = _nodes[Random.Range(0, _nodes.Count)]
            })
            .ToList();
    }

    private void OnGUI()
    {
        _position = GUI.BeginScrollView(new Rect(Vector2.zero, this.position.size), _position, _currentArea);
        {
            var viewRect = new Rect(_position, this.position.size);

            foreach (var node in _nodes)
                if (node.Position.Overlaps(viewRect))
                    GUI.Box(node.Position, node.Name, GUI.skin.GetStyle("flow node 0"));

            foreach (var edge in _edges) {
                var union = Rect.MinMaxRect(
                    xmin: Mathf.Min(edge.Start.Position.xMin, edge.End.Position.xMin),
                    ymin: Mathf.Min(edge.Start.Position.yMin, edge.End.Position.yMin),
                    xmax: Mathf.Max(edge.Start.Position.xMax, edge.End.Position.xMax),
                    ymax: Mathf.Max(edge.Start.Position.yMax, edge.End.Position.yMax));

                if (union.Overlaps(viewRect))
                    Handles.DrawLine(edge.Start.Position.center, edge.End.Position.center);
            }
        }
        GUI.EndScrollView();
    }
}