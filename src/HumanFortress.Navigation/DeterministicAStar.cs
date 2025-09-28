using System.Diagnostics;

namespace HumanFortress.Navigation;

/// <summary>
/// Deterministic A* pathfinder per NAVIGATION_SPEC.md section 5.
/// Uses binary heap with stable ordering.
/// </summary>
public sealed class DeterministicAStar
{
    private readonly NavigationTuning _tuning;
    private readonly BinaryHeap _openSet;
    private readonly Dictionary<ulong, AStarNode> _nodeMap;
    private readonly HashSet<ulong> _closedSet;
    private int _nodesExpanded;
    private readonly Stopwatch _timer;

    public DeterministicAStar(NavigationTuning tuning)
    {
        _tuning = tuning;
        _openSet = new BinaryHeap(1024);
        _nodeMap = new Dictionary<ulong, AStarNode>(1024);
        _closedSet = new HashSet<ulong>(1024);
        _timer = new Stopwatch();
    }

    /// <summary>
    /// Find path from source to destination.
    /// </summary>
    public Path FindPath(PathRequest request, IWorldNavigationView world)
    {
        // Reset state
        _openSet.Clear();
        _nodeMap.Clear();
        _closedSet.Clear();
        _nodesExpanded = 0;
        _timer.Restart();

        // Validate request
        if (!world.IsValid(request.Source) || !world.IsValid(request.Destination))
        {
            return Path.Invalid;
        }

        // Check if source and destination are walkable
        var srcCaps = world.GetCapabilities(request.Source);
        var dstCaps = world.GetCapabilities(request.Destination);

        if (!HasRequiredCapability(srcCaps, request.Mode) ||
            !HasRequiredCapability(dstCaps, request.Mode))
        {
            return Path.Invalid;
        }

        // Initialize start node
        var startKey = PointToKey(request.Source);
        var startNode = new AStarNode
        {
            Position = request.Source,
            G = 0,
            H = Heuristic(request.Source, request.Destination),
            Parent = ulong.MaxValue,
        };
        startNode.F = (ushort)(startNode.G + startNode.H);

        _nodeMap[startKey] = startNode;
        _openSet.Push(new HeapEntry(startKey, startNode.F, startNode.H, startNode.G, GetLocalIndex(request.Source)));

        // A* main loop
        while (_openSet.Count > 0)
        {
            // Check limits
            if (_nodesExpanded >= _tuning.MaxNodesPerSearch)
            {
                return BuildPartialPath(request, world);
            }

            if (_timer.ElapsedMilliseconds > _tuning.MaxMsPerTickPathing)
            {
                return BuildPartialPath(request, world);
            }

            // Pop best node
            var current = _openSet.Pop();
            var currentNode = _nodeMap[current.Key];

            // Check if we reached the goal
            if (currentNode.Position == request.Destination)
            {
                return BuildCompletePath(request, current.Key, world);
            }

            // Move to closed set
            _closedSet.Add(current.Key);
            _nodesExpanded++;

            // Expand neighbors
            ExpandNeighbors(currentNode, request, world);
        }

        // No path found
        return Path.Failed;
    }

    private void ExpandNeighbors(AStarNode current, PathRequest request, IWorldNavigationView world)
    {
        // Get orthogonal neighbors (N, E, S, W)
        var neighbors = new[]
        {
            current.Position with { Y = current.Position.Y - 1 }, // North
            current.Position with { X = current.Position.X + 1 }, // East
            current.Position with { Y = current.Position.Y + 1 }, // South
            current.Position with { X = current.Position.X - 1 }, // West
        };

        // Process each neighbor
        for (int i = 0; i < neighbors.Length; i++)
        {
            ProcessNeighbor(current, neighbors[i], _tuning.OrthogonalCost, request, world);
        }

        // Add diagonal neighbors if allowed
        if (_tuning.AllowDiagonals && (request.Flags & PathFlags.AllowDiagonal) != 0)
        {
            var diagonals = new[]
            {
                current.Position with { X = current.Position.X + 1, Y = current.Position.Y - 1 }, // NE
                current.Position with { X = current.Position.X + 1, Y = current.Position.Y + 1 }, // SE
                current.Position with { X = current.Position.X - 1, Y = current.Position.Y + 1 }, // SW
                current.Position with { X = current.Position.X - 1, Y = current.Position.Y - 1 }, // NW
            };

            for (int i = 0; i < diagonals.Length; i++)
            {
                // Check corner-cutting rule
                var corner1 = neighbors[i % 4];
                var corner2 = neighbors[(i + 1) % 4];

                if (!world.IsWalkable(corner1, request.Mode) ||
                    !world.IsWalkable(corner2, request.Mode))
                {
                    continue; // Can't cut corners
                }

                ProcessNeighbor(current, diagonals[i], _tuning.DiagonalCost, request, world);
            }
        }

        // Check for vertical neighbors (stairs/ramps)
        CheckVerticalNeighbors(current, request, world);
    }

    private void ProcessNeighbor(AStarNode current, Point3 neighborPos, ushort edgeCost,
        PathRequest request, IWorldNavigationView world)
    {
        // Check if valid and walkable
        if (!world.IsValid(neighborPos))
            return;

        var neighborCaps = world.GetCapabilities(neighborPos);
        if (!HasRequiredCapability(neighborCaps, request.Mode))
            return;

        var neighborKey = PointToKey(neighborPos);

        // Skip if already closed
        if (_closedSet.Contains(neighborKey))
            return;

        // Calculate costs
        var moveCost = world.GetCost(neighborPos);
        var stepCost = (edgeCost * moveCost) / 10; // Normalize by base cost
        var tentativeG = current.G + stepCost;

        // Check if we've seen this node before
        if (_nodeMap.TryGetValue(neighborKey, out var existingNode))
        {
            // Only update if we found a better path
            if (tentativeG < existingNode.G)
            {
                existingNode.G = (ushort)tentativeG;
                existingNode.F = (ushort)(tentativeG + existingNode.H);
                existingNode.Parent = PointToKey(current.Position);
                _nodeMap[neighborKey] = existingNode;

                // Re-add to open set with updated priority
                _openSet.Push(new HeapEntry(neighborKey, existingNode.F, existingNode.H,
                    existingNode.G, GetLocalIndex(neighborPos)));
            }
        }
        else
        {
            // New node
            var h = Heuristic(neighborPos, request.Destination);
            var newNode = new AStarNode
            {
                Position = neighborPos,
                G = (ushort)tentativeG,
                H = h,
                F = (ushort)(tentativeG + h),
                Parent = PointToKey(current.Position),
            };

            _nodeMap[neighborKey] = newNode;
            _openSet.Push(new HeapEntry(neighborKey, newNode.F, newNode.H, newNode.G,
                GetLocalIndex(neighborPos)));
        }
    }

    private void CheckVerticalNeighbors(AStarNode current, PathRequest request, IWorldNavigationView world)
    {
        // Check for stairs up
        if (world.HasStairsUp(current.Position))
        {
            var upPos = current.Position.WithZ(current.Position.Z + 1);
            ProcessNeighbor(current, upPos, (ushort)(_tuning.OrthogonalCost + _tuning.StairDelta),
                request, world);
        }

        // Check for stairs down
        if (world.HasStairsDown(current.Position))
        {
            var downPos = current.Position.WithZ(current.Position.Z - 1);
            ProcessNeighbor(current, downPos, (ushort)(_tuning.OrthogonalCost + _tuning.StairDelta),
                request, world);
        }

        // Ramps: allow ascending from ramp base and descending from ramp top
        // Semantics: rampDirection points from base (z) toward top (z+1)
        if (world.TryGetRampDirection(current.Position, out var dir))
        {
            var (dx, dy) = GetDirectionOffset(dir);
            var topPos = new Point3(current.Position.X + dx, current.Position.Y + dy, current.Position.Z + 1);
            if (world.IsValid(topPos) && world.IsStandable(topPos))
            {
                ProcessNeighbor(current, topPos, (ushort)(_tuning.OrthogonalCost + _tuning.RampDelta), request, world);
            }
        }
        // If we're on a standable top tile, allow descending via cached down-ramp link
        if (world.IsStandable(current.Position) && world.TryGetDownRampDirection(current.Position, out var ddir))
        {
            var (dx, dy) = GetDirectionOffset(ddir);
            var rampPos = new Point3(current.Position.X - dx, current.Position.Y - dy, current.Position.Z - 1);
            if (world.IsValid(rampPos) && world.IsWalkable(rampPos, request.Mode))
            {
                ProcessNeighbor(current, rampPos, (ushort)(_tuning.OrthogonalCost + _tuning.RampDelta), request, world);
            }
        }
    }

    private static (int dx, int dy) GetDirectionOffset(byte direction)
    {
        return direction switch
        {
            0 => (0, -1),   // N
            1 => (1, -1),   // NE
            2 => (1, 0),    // E
            3 => (1, 1),    // SE
            4 => (0, 1),    // S
            5 => (-1, 1),   // SW
            6 => (-1, 0),   // W
            7 => (-1, -1),  // NW
            _ => (0, -1)
        };
    }

    private bool HasRequiredCapability(NavCapability caps, MoveMode mode)
    {
        return mode switch
        {
            MoveMode.Walk => (caps & NavCapability.Walk) != 0,
            MoveMode.Crawl => (caps & NavCapability.Crawl) != 0,
            MoveMode.Swim => (caps & NavCapability.Swim) != 0,
            MoveMode.Fly => (caps & NavCapability.Fly) != 0,
            _ => false,
        };
    }

    private ushort Heuristic(Point3 from, Point3 to)
    {
        // Manhattan distance for 4-neighbor movement
        var dx = Math.Abs(from.X - to.X);
        var dy = Math.Abs(from.Y - to.Y);
        var dz = Math.Abs(from.Z - to.Z);

        if (_tuning.AllowDiagonals)
        {
            // Octile distance for 8-neighbor
            var dMin = Math.Min(dx, dy);
            var dMax = Math.Max(dx, dy);
            return (ushort)(_tuning.DiagonalCost * dMin + _tuning.OrthogonalCost * (dMax - dMin) +
                _tuning.StairDelta * dz);
        }
        else
        {
            // Manhattan distance
            return (ushort)(_tuning.OrthogonalCost * (dx + dy) + _tuning.StairDelta * dz);
        }
    }

    private Path BuildCompletePath(PathRequest request, ulong goalKey, IWorldNavigationView world)
    {
        var path = new List<PathNode>();
        var current = goalKey;

        while (current != ulong.MaxValue && _nodeMap.TryGetValue(current, out var node))
        {
            var cost = world.GetCost(node.Position);
            path.Add(new PathNode(node.Position, cost));

            if (node.Parent == ulong.MaxValue)
                break;

            current = node.Parent;
        }

        path.Reverse();

        // Calculate path hash for determinism verification
        var hash = CalculatePathHash(path);

        return new Path(PathResultKind.Found, path.Count, hash, path.ToArray());
    }

    private Path BuildPartialPath(PathRequest request, IWorldNavigationView world)
    {
        // Find the best frontier node (lowest F)
        ulong bestKey = 0;
        ushort bestF = ushort.MaxValue;

        foreach (var entry in _openSet.GetAll())
        {
            if (entry.F < bestF)
            {
                bestF = entry.F;
                bestKey = entry.Key;
            }
        }

        if (bestKey == 0 || !_nodeMap.ContainsKey(bestKey))
            return Path.Failed;

        return BuildCompletePath(request, bestKey, world);
    }

    private static ulong PointToKey(Point3 point)
    {
        // Pack x, y, z into 64-bit key
        return ((ulong)point.Z << 32) | ((ulong)point.Y << 16) | ((ulong)point.X & 0xFFFF);
    }

    private static int GetLocalIndex(Point3 point)
    {
        // Local index within chunk (0..1023)
        const int ChunkSize = 32;
        int localX = ((point.X % ChunkSize) + ChunkSize) % ChunkSize;
        int localY = ((point.Y % ChunkSize) + ChunkSize) % ChunkSize;
        return localY * ChunkSize + localX;
    }

    private static uint CalculatePathHash(List<PathNode> path)
    {
        unchecked
        {
            uint hash = 17;
            foreach (var node in path)
            {
                hash = hash * 31 + (uint)node.Position.X;
                hash = hash * 31 + (uint)node.Position.Y;
                hash = hash * 31 + (uint)node.Position.Z;
            }
            return hash;
        }
    }

    /// <summary>
    /// Internal A* node.
    /// </summary>
    private struct AStarNode
    {
        public Point3 Position;
        public ushort G; // Cost from start
        public ushort H; // Heuristic to goal
        public ushort F; // G + H
        public ulong Parent; // Parent node key
    }
}
