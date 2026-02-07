namespace MechaRogue.Services;

using MechaRogue.Models;

/// <summary>
/// Generates branching roguelike maps for each run.
/// Each floor has 2-3 nodes; the last floor is always a boss.
/// </summary>
public static class MapGenerator
{
    private static readonly Random _rng = new();

    public static List<RunNode> Generate(int totalFloors)
    {
        var nodes = new List<RunNode>();
        int id = 0;

        for (int depth = 1; depth <= totalFloors; depth++)
        {
            int count = depth == totalFloors ? 1 : _rng.Next(2, 4);

            for (int i = 0; i < count; i++)
            {
                var type = depth == totalFloors
                    ? NodeType.Boss
                    : RollNodeType();

                nodes.Add(new RunNode
                {
                    Id = id++,
                    Depth = depth,
                    Type = type,
                    Label = type.ToString()
                });
            }
        }

        // Wire connections: each node → 1-2 random nodes on next floor
        var byDepth = nodes.GroupBy(n => n.Depth).ToDictionary(g => g.Key, g => g.ToList());
        for (int d = 1; d < totalFloors; d++)
        {
            if (!byDepth.ContainsKey(d) || !byDepth.ContainsKey(d + 1)) continue;
            foreach (var node in byDepth[d])
            {
                var nextFloor = byDepth[d + 1];
                int links = Math.Min(nextFloor.Count, _rng.Next(1, 3));
                var targets = nextFloor.OrderBy(_ => _rng.Next()).Take(links);
                foreach (var t in targets)
                    node.NextNodes.Add(t.Id);
            }
        }

        return nodes;
    }

    private static NodeType RollNodeType()
    {
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
}
