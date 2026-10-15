using System;
using System.Collections.Generic;

namespace SBGames.Dungeon.Core.Pieces
{
    /// <summary>
    /// Represents an instantiated and placed piece in world placement grid coordinates.
    /// Pure C#, zero UnityEngine references.
    /// </summary>
    public class PlacedPiece
    {
        public int RoomId { get; set; } // >= 0 for Graph nodes, < 0 for inserted Pathways
        public DungeonPieceDescriptor Descriptor { get; set; }
        public int QuarterTurns { get; set; }
        public GridCoord WorldOrigin { get; set; }
        public int CellWidth { get; set; }
        public int CellHeight { get; set; }
        public List<PieceOpening> RotatedOpenings { get; set; } = new List<PieceOpening>();
        public HashSet<GridCoord> RotatedOccupiedTiles { get; set; } = new HashSet<GridCoord>();
        public HashSet<GridCoord> RotatedBuildableMask { get; set; } = new HashSet<GridCoord>();
        public HashSet<GridCoord> RotatedGroundCells { get; set; } = new HashSet<GridCoord>();

        public GridCoord LocalToWorld(GridCoord local) => WorldOrigin + local;
    }

    /// <summary>
    /// Represents an active doorway seam connection between two snapped piece openings.
    /// </summary>
    public class SeamConnection
    {
        public PlacedPiece PieceA { get; set; }
        public PieceOpening OpeningA { get; set; }
        public PlacedPiece PieceB { get; set; }
        public PieceOpening OpeningB { get; set; }
        public bool IsBossDoor { get; set; }
        public int DoorId { get; set; }
    }

    /// <summary>
    /// Represents an unused opening socket that must be sealed by an Opening Cap.
    /// </summary>
    public class UnusedSocket
    {
        public PlacedPiece Piece { get; set; }
        public PieceOpening Opening { get; set; }
    }

    /// <summary>
    /// Outcome of the seam-driven placement solver.
    /// </summary>
    public class DungeonSeamLayoutResult
    {
        public bool Success { get; set; }
        public Dictionary<int, PlacedPiece> PlacedRooms { get; set; } = new Dictionary<int, PlacedPiece>();
        public List<PlacedPiece> PlacedPathways { get; set; } = new List<PlacedPiece>();
        public List<SeamConnection> Seams { get; set; } = new List<SeamConnection>();
        public List<UnusedSocket> UnusedSockets { get; set; } = new List<UnusedSocket>();
        public HashSet<GridCoord> OccupiedDungeonTiles { get; set; } = new HashSet<GridCoord>();
        public string FailureReason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Pure C# seam-driven layout solver supporting dynamic room sizes, freeform openings,
    /// direct room-to-room snapping, and room-to-pathway-to-room connector pieces with zero tile overlap.
    /// Zero UnityEngine references.
    /// </summary>
    public static class DungeonSeamLayoutSolver
    {
        public static DungeonSeamLayoutResult Solve(
            DungeonLayoutGraph graph,
            IReadOnlyList<DungeonPieceDescriptor> descriptors,
            float pathwayProbability,
            int openingWidth,
            IDungeonRng rng)
        {
            var result = new DungeonSeamLayoutResult();
            if (graph == null || graph.Rooms.Count == 0)
            {
                result.Success = false;
                result.FailureReason = "Graph is null or empty.";
                return result;
            }

            if (descriptors == null || descriptors.Count == 0)
            {
                result.Success = false;
                result.FailureReason = "Piece descriptors collection is empty.";
                return result;
            }

            // Find Entrance node
            DungeonLayoutNode entranceNode = null;
            foreach (var node in graph.Rooms.Values)
            {
                if (node.RoomType == DungeonRoomType.Entrance)
                {
                    entranceNode = node;
                    break;
                }
            }

            if (entranceNode == null)
            {
                result.Success = false;
                result.FailureReason = "No Entrance room found in graph.";
                return result;
            }

            var entranceCandidates = FilterCandidates(descriptors, DungeonRoomType.Entrance, PieceRole.Room);
            if (entranceCandidates.Count == 0)
            {
                result.Success = false;
                result.FailureReason = "No Entrance piece descriptors available.";
                return result;
            }

            // Select entrance piece with rotation that satisfies all doors of entranceNode
            PlacedPiece placedEntrance = null;
            var requiredEntranceDirs = entranceNode.DoorsByDirection.Keys;
            int entranceReqCount = entranceNode.DoorsByDirection.Count;
            var sortedEntrance = new List<DungeonPieceDescriptor>(entranceCandidates);
            sortedEntrance.Sort((a, b) => Math.Abs(a.Openings.CountOpenings() - entranceReqCount).CompareTo(Math.Abs(b.Openings.CountOpenings() - entranceReqCount)));
            var shuffledEntrance = Shuffle(sortedEntrance, rng);

            foreach (var desc in shuffledEntrance)
            {
                if (desc.Openings.CountOpenings() < entranceReqCount) continue;

                int maxRot = desc.IsRotatable ? 4 : 1;
                int startRot = rng.Range(0, maxRot);
                for (int step = 0; step < maxRot; step++)
                {
                    int r = (startRot + step) % maxRot;
                    var candidate = CreatePlacedPiece(entranceNode.RoomId, desc, r, new GridCoord(0, 0), openingWidth);
                    if (HasOpeningsForDirections(candidate, requiredEntranceDirs))
                    {
                        placedEntrance = candidate;
                        break;
                    }
                }
                if (placedEntrance != null) break;
            }

            if (placedEntrance == null)
            {
                result.Success = false;
                result.FailureReason = "No Entrance piece matches the required door directions.";
                return result;
            }

            result.PlacedRooms[entranceNode.RoomId] = placedEntrance;
            MarkOccupied(result.OccupiedDungeonTiles, placedEntrance);

            int pathwayIdCounter = -1;
            var usedOpenings = new HashSet<(PlacedPiece piece, PieceOpening opening)>();

            // Queue for BFS traversal from placed rooms
            var queue = new Queue<int>();
            queue.Enqueue(entranceNode.RoomId);
            var visitedRooms = new HashSet<int> { entranceNode.RoomId };

            while (queue.Count > 0)
            {
                int currentRoomId = queue.Dequeue();
                var currentPlaced = result.PlacedRooms[currentRoomId];
                var currentNode = graph.Rooms[currentRoomId];

                // For each door from current node
                foreach (var kvp in currentNode.DoorsByDirection)
                {
                    var dirFromCurrent = kvp.Key;
                    int doorId = kvp.Value;
                    if (!graph.TryGetDoor(doorId, out var doorConn))
                    {
                        continue;
                    }

                    int targetRoomId = doorConn.GetOppositeRoomId(currentRoomId);
                    if (targetRoomId < 0 || visitedRooms.Contains(targetRoomId))
                    {
                        continue;
                    }

                    var targetNode = graph.Rooms[targetRoomId];
                    bool isBossDoor = (currentNode.RoomType == DungeonRoomType.Boss || targetNode.RoomType == DungeonRoomType.Boss);

                    // Find available opening on currentPlaced facing dirFromCurrent
                    PieceOpening openingFromCurrent = default;
                    bool foundOpening = false;
                    foreach (var op in currentPlaced.RotatedOpenings)
                    {
                        if (op.Direction == dirFromCurrent && !usedOpenings.Contains((currentPlaced, op)))
                        {
                            openingFromCurrent = op;
                            foundOpening = true;
                            break;
                        }
                    }

                    if (!foundOpening)
                    {
                        result.Success = false;
                        result.FailureReason = $"Room {currentRoomId} ({currentPlaced.Descriptor.PieceId}) has no available opening socket facing {dirFromCurrent}.";
                        return result;
                    }

                    var targetCandidates = FilterCandidates(descriptors, targetNode.RoomType, PieceRole.Room);
                    if (targetCandidates.Count == 0)
                    {
                        result.Success = false;
                        result.FailureReason = $"No piece descriptors available for room type {targetNode.RoomType}.";
                        return result;
                    }

                    var pathwayCandidates = FilterCandidates(descriptors, DungeonRoomType.Normal, PieceRole.Pathway);
                    bool wantPathway = (pathwayCandidates.Count > 0) && rng.Chance(pathwayProbability);

                    bool placedTarget = false;

                    // Option A: Try Direct Room -> Room connection if !wantPathway
                    if (!wantPathway)
                    {
                        placedTarget = TryPlaceDirectRoom(
                            targetNode,
                            targetCandidates,
                            currentPlaced,
                            openingFromCurrent,
                            openingWidth,
                            result.OccupiedDungeonTiles,
                            rng,
                            isBossDoor,
                            result,
                            usedOpenings
                        );
                    }

                    // Option B: If direct room connection failed or was bypassed by pathwayProbability, try Pathway insertion
                    if (!placedTarget && pathwayCandidates.Count > 0)
                    {
                        placedTarget = TryPlacePathwayConnector(
                            ref pathwayIdCounter,
                            pathwayCandidates,
                            targetNode,
                            targetCandidates,
                            currentPlaced,
                            openingFromCurrent,
                            openingWidth,
                            result.OccupiedDungeonTiles,
                            rng,
                            isBossDoor,
                            result,
                            usedOpenings
                        );
                    }

                    // Option C: If pathway failed but we initially wanted pathway, fallback to direct room
                    if (!placedTarget && wantPathway)
                    {
                        placedTarget = TryPlaceDirectRoom(
                            targetNode,
                            targetCandidates,
                            currentPlaced,
                            openingFromCurrent,
                            openingWidth,
                            result.OccupiedDungeonTiles,
                            rng,
                            isBossDoor,
                            result,
                            usedOpenings
                        );
                    }

                    if (!placedTarget)
                    {
                        result.Success = false;
                        result.FailureReason = $"Failed to place Room {targetRoomId} ({targetNode.RoomType}) adjacent to Room {currentRoomId} without overlap.";
                        return result;
                    }

                    visitedRooms.Add(targetRoomId);
                    queue.Enqueue(targetRoomId);
                }
            }

            // Collect all unused sockets for capping
            void CheckSockets(PlacedPiece p)
            {
                foreach (var op in p.RotatedOpenings)
                {
                    if (!usedOpenings.Contains((p, op)))
                    {
                        result.UnusedSockets.Add(new UnusedSocket { Piece = p, Opening = op });
                    }
                }
            }

            foreach (var r in result.PlacedRooms.Values) CheckSockets(r);
            foreach (var p in result.PlacedPathways) CheckSockets(p);

            result.Success = (visitedRooms.Count == graph.Rooms.Count);
            return result;
        }

        private static bool TryPlaceDirectRoom(
            DungeonLayoutNode targetNode,
            List<DungeonPieceDescriptor> candidates,
            PlacedPiece placedA,
            PieceOpening openingA,
            int openingWidth,
            HashSet<GridCoord> occupiedTiles,
            IDungeonRng rng,
            bool isBossDoor,
            DungeonSeamLayoutResult result,
            HashSet<(PlacedPiece, PieceOpening)> usedOpenings)
        {
            var oppositeDir = openingA.Direction.GetOpposite();
            var requiredDirs = targetNode.DoorsByDirection.Keys;
            int reqCount = targetNode.DoorsByDirection.Count;

            // Prioritize candidates with opening counts closest to required door count
            var sortedCandidates = new List<DungeonPieceDescriptor>(candidates);
            sortedCandidates.Sort((a, b) => Math.Abs(a.Openings.CountOpenings() - reqCount).CompareTo(Math.Abs(b.Openings.CountOpenings() - reqCount)));
            var shuffledCandidates = Shuffle(sortedCandidates, rng);

            foreach (var desc in shuffledCandidates)
            {
                if (desc.Openings.CountOpenings() < reqCount) continue;

                int maxRot = desc.IsRotatable ? 4 : 1;
                int startRot = rng.Range(0, maxRot);
                for (int step = 0; step < maxRot; step++)
                {
                    int r = (startRot + step) % maxRot;
                    var placedCandidate = CreatePlacedPiece(targetNode.RoomId, desc, r, new GridCoord(0, 0), openingWidth);

                    if (!HasOpeningsForDirections(placedCandidate, requiredDirs))
                    {
                        continue;
                    }

                    foreach (var opB in placedCandidate.RotatedOpenings)
                    {
                        if (opB.Direction == oppositeDir)
                        {
                            var originB = OpeningGeometry.CalculateSnappedWorldOrigin(placedA.WorldOrigin, openingA, opB);
                            placedCandidate.WorldOrigin = originB;

                            if (!CheckCollision(occupiedTiles, placedCandidate))
                            {
                                // Placement success!
                                result.PlacedRooms[targetNode.RoomId] = placedCandidate;
                                MarkOccupied(occupiedTiles, placedCandidate);
                                usedOpenings.Add((placedA, openingA));
                                usedOpenings.Add((placedCandidate, opB));

                                result.Seams.Add(new SeamConnection
                                {
                                    PieceA = placedA,
                                    OpeningA = openingA,
                                    PieceB = placedCandidate,
                                    OpeningB = opB,
                                    IsBossDoor = isBossDoor
                                });

                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static bool TryPlacePathwayConnector(
            ref int pathwayIdCounter,
            List<DungeonPieceDescriptor> pathwayCandidates,
            DungeonLayoutNode targetNode,
            List<DungeonPieceDescriptor> targetCandidates,
            PlacedPiece placedA,
            PieceOpening openingA,
            int openingWidth,
            HashSet<GridCoord> occupiedTiles,
            IDungeonRng rng,
            bool isBossDoor,
            DungeonSeamLayoutResult result,
            HashSet<(PlacedPiece, PieceOpening)> usedOpenings)
        {
            var oppositeDir = openingA.Direction.GetOpposite();
            var requiredDirs = targetNode.DoorsByDirection.Keys;
            int reqCount = targetNode.DoorsByDirection.Count;

            var shuffledPathways = Shuffle(pathwayCandidates, rng);

            foreach (var pathDesc in shuffledPathways)
            {
                int maxRotP = pathDesc.IsRotatable ? 4 : 1;
                for (int rp = 0; rp < maxRotP; rp++)
                {
                    var placedPathway = CreatePlacedPiece(pathwayIdCounter, pathDesc, rp, new GridCoord(0, 0), openingWidth);

                    foreach (var opP_in in placedPathway.RotatedOpenings)
                    {
                        if (opP_in.Direction == oppositeDir)
                        {
                            var originP = OpeningGeometry.CalculateSnappedWorldOrigin(placedA.WorldOrigin, openingA, opP_in);
                            placedPathway.WorldOrigin = originP;

                            if (!CheckCollision(occupiedTiles, placedPathway))
                            {
                                // Pathway fits! Now try snapping target room to any other opening on this pathway
                                foreach (var opP_out in placedPathway.RotatedOpenings)
                                {
                                    if (opP_out == opP_in) continue;

                                    var targetOppositeDir = opP_out.Direction.GetOpposite();
                                    var sortedTargets = new List<DungeonPieceDescriptor>(targetCandidates);
                                    sortedTargets.Sort((a, b) => Math.Abs(a.Openings.CountOpenings() - reqCount).CompareTo(Math.Abs(b.Openings.CountOpenings() - reqCount)));
                                    var shuffledTargets = Shuffle(sortedTargets, rng);

                                    foreach (var targetDesc in shuffledTargets)
                                    {
                                        if (targetDesc.Openings.CountOpenings() < reqCount) continue;

                                        int maxRotT = targetDesc.IsRotatable ? 4 : 1;
                                        int startRotT = rng.Range(0, maxRotT);
                                        for (int stepT = 0; stepT < maxRotT; stepT++)
                                        {
                                            int rt = (startRotT + stepT) % maxRotT;
                                            var placedTarget = CreatePlacedPiece(targetNode.RoomId, targetDesc, rt, new GridCoord(0, 0), openingWidth);

                                            if (!HasOpeningsForDirections(placedTarget, requiredDirs))
                                            {
                                                continue;
                                            }

                                            foreach (var opB in placedTarget.RotatedOpenings)
                                            {
                                                if (opB.Direction == targetOppositeDir)
                                                {
                                                    var originB = OpeningGeometry.CalculateSnappedWorldOrigin(placedPathway.WorldOrigin, opP_out, opB);
                                                    placedTarget.WorldOrigin = originB;

                                                    // Must not collide with occupied tiles AND not collide with new pathway tiles
                                                    if (!CheckCollision(occupiedTiles, placedTarget) && !CheckPairCollision(placedPathway, placedTarget))
                                                    {
                                                        // Both Pathway and Target Room fit!
                                                        pathwayIdCounter--;
                                                        result.PlacedPathways.Add(placedPathway);
                                                        result.PlacedRooms[targetNode.RoomId] = placedTarget;

                                                        MarkOccupied(occupiedTiles, placedPathway);
                                                        MarkOccupied(occupiedTiles, placedTarget);

                                                        usedOpenings.Add((placedA, openingA));
                                                        usedOpenings.Add((placedPathway, opP_in));
                                                        usedOpenings.Add((placedPathway, opP_out));
                                                        usedOpenings.Add((placedTarget, opB));

                                                        result.Seams.Add(new SeamConnection
                                                        {
                                                            PieceA = placedA,
                                                            OpeningA = openingA,
                                                            PieceB = placedPathway,
                                                            OpeningB = opP_in,
                                                            IsBossDoor = false
                                                        });

                                                        result.Seams.Add(new SeamConnection
                                                        {
                                                            PieceA = placedPathway,
                                                            OpeningA = opP_out,
                                                            PieceB = placedTarget,
                                                            OpeningB = opB,
                                                            IsBossDoor = isBossDoor
                                                        });

                                                        return true;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static bool HasOpeningsForDirections(PlacedPiece piece, IEnumerable<CardinalDirection> requiredDirs)
        {
            foreach (var dir in requiredDirs)
            {
                bool hasDir = false;
                foreach (var op in piece.RotatedOpenings)
                {
                    if (op.Direction == dir)
                    {
                        hasDir = true;
                        break;
                    }
                }
                if (!hasDir) return false;
            }
            return true;
        }

        public static PlacedPiece CreatePlacedPiece(
            int roomId,
            DungeonPieceDescriptor desc,
            int quarterTurns,
            GridCoord origin,
            int openingWidth)
        {
            int w = desc.CellWidth > 0 ? desc.CellWidth : 16;
            int h = desc.CellHeight > 0 ? desc.CellHeight : 16;
            OpeningGeometry.GetEffectiveDimensions(w, h, quarterTurns, out int effW, out int effH);

            var placed = new PlacedPiece
            {
                RoomId = roomId,
                Descriptor = desc,
                QuarterTurns = quarterTurns,
                WorldOrigin = origin,
                CellWidth = effW,
                CellHeight = effH
            };

            // Calculate rotated openings
            if (desc.OpeningsList != null && desc.OpeningsList.Count > 0)
            {
                placed.RotatedOpenings = desc.GetRotatedOpeningsList(quarterTurns, w, h);
            }
            else
            {
                // Synthesize from cardinal flags
                var baseOpenings = new List<PieceOpening>();
                for (int d = 0; d < 4; d++)
                {
                    var dir = (CardinalDirection)d;
                    if (desc.Openings.ContainsDirection(dir))
                    {
                        var edgeCoords = OpeningGeometry.GetOpeningEdgeCoords(w, h, openingWidth, dir);
                        if (edgeCoords.Count > 0)
                        {
                            baseOpenings.Add(new PieceOpening(edgeCoords[0], dir, (byte)openingWidth));
                        }
                    }
                }

                int k = ((quarterTurns % 4) + 4) % 4;
                placed.RotatedOpenings = new List<PieceOpening>(baseOpenings.Count);
                foreach (var op in baseOpenings)
                {
                    placed.RotatedOpenings.Add(op.RotateClockwise(w, h, k));
                }
            }

            // Rotated occupied tiles
            placed.RotatedOccupiedTiles = desc.GetRotatedOccupiedTiles(quarterTurns, w, h);
            if (placed.RotatedOccupiedTiles.Count == 0)
            {
                // Fallback footprint
                for (int x = 0; x < effW; x++)
                {
                    for (int y = 0; y < effH; y++)
                    {
                        placed.RotatedOccupiedTiles.Add(new GridCoord(x, y));
                    }
                }
            }

            placed.RotatedBuildableMask = desc.GetRotatedBuildableMask(quarterTurns, w, h);
            placed.RotatedGroundCells = desc.GetRotatedGroundCells(quarterTurns, w, h);

            return placed;
        }

        private static bool CheckCollision(HashSet<GridCoord> occupiedTiles, PlacedPiece piece)
        {
            foreach (var tile in piece.RotatedOccupiedTiles)
            {
                if (occupiedTiles.Contains(piece.WorldOrigin + tile))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool CheckPairCollision(PlacedPiece pieceA, PlacedPiece pieceB)
        {
            foreach (var tileB in pieceB.RotatedOccupiedTiles)
            {
                var worldB = pieceB.WorldOrigin + tileB;
                var localA = worldB - pieceA.WorldOrigin;
                if (pieceA.RotatedOccupiedTiles.Contains(localA))
                {
                    return true;
                }
            }
            return false;
        }

        private static void MarkOccupied(HashSet<GridCoord> occupiedTiles, PlacedPiece piece)
        {
            foreach (var tile in piece.RotatedOccupiedTiles)
            {
                occupiedTiles.Add(piece.WorldOrigin + tile);
            }
        }

        private static List<DungeonPieceDescriptor> FilterCandidates(
            IReadOnlyList<DungeonPieceDescriptor> descriptors,
            DungeonRoomType roomType,
            PieceRole role)
        {
            var list = new List<DungeonPieceDescriptor>();
            for (int i = 0; i < descriptors.Count; i++)
            {
                var d = descriptors[i];
                if (d.Role == role)
                {
                    if (role == PieceRole.Pathway || d.RoomType == roomType)
                    {
                        list.Add(d);
                    }
                }
            }
            return list;
        }

        private static DungeonPieceDescriptor SelectByWeight(List<DungeonPieceDescriptor> candidates, IDungeonRng rng)
        {
            if (candidates.Count == 1) return candidates[0];

            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++) totalWeight += candidates[i].Weight;

            float roll = rng.Range(0f, totalWeight);
            float accum = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                accum += candidates[i].Weight;
                if (roll <= accum) return candidates[i];
            }

            return candidates[0];
        }

        private static List<DungeonPieceDescriptor> Shuffle(List<DungeonPieceDescriptor> list, IDungeonRng rng)
        {
            var shuffled = new List<DungeonPieceDescriptor>(list);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                var temp = shuffled[i];
                shuffled[i] = shuffled[j];
                shuffled[j] = temp;
            }
            return shuffled;
        }
    }
}
