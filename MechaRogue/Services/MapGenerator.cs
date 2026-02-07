namespace MechaRogue.Services;

using MechaRogue.Models;

/// <summary>
/// Generates simple branching roguelike maps.
/// </summary>
public static class MapGenerator
{
    private static readonly Random _rng = new();

    public static List<RunNode> Generate(int floors = 15)
    {
        var nodes = new List<RunNode>();
        int id = 0;

        // For each floor, generate 2-3 node choices
        for (int depth = 1; depth <= floors; depth++)
        {
            int nodesOnFloor = depth == floors ? 1 : _rng.Next(2, 4);
            var floorNodes = new List<RunNode>();

            for (int i = 0; i < nodesOnFloor; i++)
            {
                var type = PickNodeType(depth, floors);
                floorNodes.Add(new RunNode
                {
                    Id = id++,
                    Type = type,
                    Label = NodeLabel(type, depth),
                    Depth = depth
                });
            }
            nodes.AddRange(floorNodes);
        }

        // Wire up connections: each node connects to all nodes on the next floor
        for (int depth = 1; depth < floors; depth++)
        {
            var current = nodes.Where(n => n.Depth == depth).ToList();
            var next = nodes.Where(n => n.Depth == depth + 1).ToList();

            foreach (var node in current)
            {
                // Connect to 1-2 random nodes on the next floor
                int connections = Math.Min(next.Count, _rng.Next(1, 3));
                var shuffled = next.OrderBy(_ => _rng.Next()).Take(connections);
                foreach (var n in shuffled)
                    node.NextNodes.Add(n.Id);
            }
        }

        return nodes;
    }

    private static NodeType PickNodeType(int depth, int maxFloors)
    {
        if (depth == maxFloors) return NodeType.Boss;
        if (depth % 5 == 0) return NodeType.EliteBattle;

        int roll = _rng.Next(100);
        return roll switch
        {
            < 45 => NodeType.Battle,
            < 60 => NodeType.EliteBattle,
            < 75 => NodeType.Shop,
            < 90 => NodeType.Rest,
            _ => NodeType.Event
        };
    }

    private static string NodeLabel(NodeType type, int depth) => type switch
    {
        NodeType.Battle => $"Robattle (Floor {depth})",
        NodeType.EliteBattle => $"Elite Robattle (Floor {depth})",
        NodeType.Shop => "Parts Shop",
        NodeType.Rest => "Repair Station",
        NodeType.Event => "Mystery Event",
        NodeType.Boss => "★ BOSS ROBATTLE ★",
        _ => "???"
    };
}
