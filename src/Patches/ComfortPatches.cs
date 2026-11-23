using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using VikingClimate.Configuration;
using VikingClimate.Features.Temperature;

namespace VikingClimate.Patches
{
    /// <summary>
    /// Harmony patches integrating dwelling-wide comfort propagation across the structural graph
    /// and temperature-gated resting requirements for beds.
    /// </summary>
    public static class ComfortPatches
    {
        /// <summary>
        /// Intercepts Player comfort calculations (called by Player.UpdateBaseValue every 2 seconds).
        /// Replaces the vanilla 10m sphere check with the connected structural graph dwelling pool.
        /// </summary>
        [HarmonyPatch(typeof(SE_Rested), nameof(SE_Rested.CalculateComfortLevel), typeof(Player))]
        public static class SE_Rested_CalculateComfortLevel_Player_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(Player player, ref int __result)
            {
                if (!ModConfig.ModEnabled.Value || !ModConfig.EnableStructureComfort.Value || 
                    ModConfig.ThermalMassSimulationMode.Value != ThermalMassMode.StructuralGraph || player == null)
                {
                    return true;
                }

                bool isInside = player.InShelter() || ThermalGraphManager.IsInsideCompound(player);
                __result = ThermalGraphManager.Instance.CalculateDwellingComfort(player, isInside, player.transform.position);
                return false;
            }
        }

        /// <summary>
        /// Intercepts position-based comfort calculations (e.g. fireplace/hearth scans from cross-mod
        /// hospitality scanners or headless dedicated server queries). Evaluates dwelling-wide comfort
        /// whenever the queried position resolves inside a known thermal dwelling, and falls back
        /// gracefully to vanilla when outdoors or unresolved.
        /// </summary>
        [HarmonyPatch(typeof(SE_Rested), nameof(SE_Rested.CalculateComfortLevel), typeof(bool), typeof(Vector3))]
        public static class SE_Rested_CalculateComfortLevel_Pos_Patch
        {
            private struct PositionComfortCacheEntry
            {
                public Vector3 Position;
                public float Timestamp;
                public int NodeCount;
                public int Comfort;
            }

            private const int CacheSize = 4;
            private const float CacheTtlSeconds = 5.0f;
            private const float CachePosToleranceSqr = 4.0f; // 2.0m
            private const float MaxDwellingExpandDistSqr = 100.0f * 100.0f;

            private static readonly PositionComfortCacheEntry[] s_posCache = new PositionComfortCacheEntry[CacheSize];
            private static int s_posCacheCursor = 0;

            // Pre-allocated reusable buffers for zero per-call heap allocations
            private static readonly HashSet<PieceThermalNode> s_bfsVisited = new HashSet<PieceThermalNode>();
            private static readonly Queue<PieceThermalNode> s_bfsQueue = new Queue<PieceThermalNode>();
            private static readonly HashSet<int> s_connectedNodeIds = new HashSet<int>();
            private static readonly List<Piece> s_nearbyCandidates = new List<Piece>();
            private static readonly List<Piece> s_dwellingComfortPieces = new List<Piece>();
            private static readonly Collider[] s_overlapColliders = new Collider[64];
            private static readonly Comparison<Piece> s_pieceComfortSort = PieceComfortSort;
            private static int s_pieceLayerMask = 0;

            [HarmonyPrefix]
            public static bool Prefix(bool inShelter, Vector3 position, ref int __result, bool __runOriginal)
            {
                if (!__runOriginal)
                {
                    return false;
                }

                if (!ModConfig.ModEnabled.Value || !ModConfig.EnableStructureComfort.Value ||
                    ModConfig.ThermalMassSimulationMode.Value != ThermalMassMode.StructuralGraph)
                {
                    return true;
                }

                // Fast path when querying the local player's position (keeps LastComfortInfo updated for compendium UI)
                Player localPlayer = Player.m_localPlayer;
                if (localPlayer != null && (position - localPlayer.transform.position).sqrMagnitude < 9.0f)
                {
                    bool isInside = inShelter || ThermalGraphManager.IsInsideCompound(localPlayer);
                    __result = ThermalGraphManager.Instance.CalculateDwellingComfort(localPlayer, isInside, position);
                    return false;
                }

                // Outdoors position queries fall back cleanly to vanilla
                if (!inShelter)
                {
                    return true;
                }

                if (TryCalculatePositionDwellingComfort(position, out int dwellingComfort))
                {
                    __result = dwellingComfort;
                    return false;
                }

                // Unresolved thermal dwelling (e.g. headless server without local graph or distant zone): fallback to vanilla
                return true;
            }

            private static int GetPieceLayerMask()
            {
                if (s_pieceLayerMask == 0)
                {
                    s_pieceLayerMask = LayerMask.GetMask("piece", "Default", "static_solid");
                    if (s_pieceLayerMask == 0)
                    {
                        int pieceLayer = LayerMask.NameToLayer("piece");
                        if (pieceLayer >= 0)
                        {
                            s_pieceLayerMask = 1 << pieceLayer;
                        }
                    }
                }
                return s_pieceLayerMask;
            }

            private static bool TryCalculatePositionDwellingComfort(Vector3 position, out int comfort)
            {
                comfort = 1;

                var manager = ThermalGraphManager.Instance;
                var nodes = manager.Nodes;
                if (nodes == null || nodes.Count == 0)
                {
                    return false;
                }

                float now = Time.time;
                int nodeCount = nodes.Count;

                // Check fixed-size ring cache first (zero raycasts, zero allocations)
                for (int i = 0; i < CacheSize; i++)
                {
                    var entry = s_posCache[i];
                    if (entry.NodeCount == nodeCount &&
                        entry.Comfort > 0 &&
                        (now - entry.Timestamp) < CacheTtlSeconds &&
                        (entry.Position - position).sqrMagnitude < CachePosToleranceSqr)
                    {
                        comfort = entry.Comfort;
                        return true;
                    }
                }

                int pieceLayerMask = GetPieceLayerMask();
                PieceThermalNode? startNode = FindStructuralNodeNearPosition(position, nodes, pieceLayerMask);
                if (startNode == null)
                {
                    return false;
                }

                try
                {
                    s_bfsVisited.Clear();
                    s_bfsQueue.Clear();
                    s_connectedNodeIds.Clear();
                    s_nearbyCandidates.Clear();
                    s_dwellingComfortPieces.Clear();

                    Vector3 originPos = startNode.Position;
                    s_bfsQueue.Enqueue(startNode);
                    s_bfsVisited.Add(startNode);
                    s_connectedNodeIds.Add(startNode.InstanceId);

                    Bounds dwellingBounds = startNode.WorldBounds;

                    while (s_bfsQueue.Count > 0)
                    {
                        PieceThermalNode current = s_bfsQueue.Dequeue();
                        var edges = current.Edges;
                        for (int i = 0; i < edges.Count; i++)
                        {
                            PieceThermalNode neighbor = edges[i].Target;
                            if (neighbor != null && s_bfsVisited.Add(neighbor))
                            {
                                s_connectedNodeIds.Add(neighbor.InstanceId);
                                dwellingBounds.Encapsulate(neighbor.WorldBounds);
                                if ((neighbor.Position - originPos).sqrMagnitude <= MaxDwellingExpandDistSqr)
                                {
                                    s_bfsQueue.Enqueue(neighbor);
                                }
                            }
                        }
                    }

                    dwellingBounds.Expand(3.5f);

                    float sphereRadius = dwellingBounds.extents.magnitude + 4.0f;
                    Piece.GetAllComfortPiecesInRadius(dwellingBounds.center, sphereRadius, s_nearbyCandidates);

                    for (int i = 0; i < s_nearbyCandidates.Count; i++)
                    {
                        Piece cp = s_nearbyCandidates[i];
                        if (cp == null || cp.gameObject == null) continue;

                        Vector3 cpPos = cp.transform.position;
                        if (!dwellingBounds.Contains(cpPos)) continue;

                        int cpId = cp.gameObject.GetInstanceID();
                        bool belongsToDwelling = s_connectedNodeIds.Contains(cpId);

                        if (!belongsToDwelling)
                        {
                            if (Physics.Raycast(cpPos + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, 3.5f, pieceLayerMask))
                            {
                                Piece supportingPiece = hit.collider.GetComponentInParent<Piece>();
                                if (supportingPiece != null && s_connectedNodeIds.Contains(supportingPiece.gameObject.GetInstanceID()))
                                {
                                    belongsToDwelling = true;
                                }
                            }
                        }

                        if (!belongsToDwelling)
                        {
                            int hitCount = Physics.OverlapSphereNonAlloc(cpPos, 2.5f, s_overlapColliders, pieceLayerMask);
                            for (int k = 0; k < hitCount; k++)
                            {
                                Collider col = s_overlapColliders[k];
                                if (col == null) continue;
                                Piece np = col.GetComponentInParent<Piece>();
                                if (np != null && s_connectedNodeIds.Contains(np.gameObject.GetInstanceID()))
                                {
                                    belongsToDwelling = true;
                                    break;
                                }
                            }
                            Array.Clear(s_overlapColliders, 0, hitCount);
                        }

                        if (belongsToDwelling)
                        {
                            s_dwellingComfortPieces.Add(cp);
                        }
                    }

                    s_dwellingComfortPieces.Sort(s_pieceComfortSort);

                    int totalComfort = 2; // Base (1) + Sheltered (1)
                    for (int i = 0; i < s_dwellingComfortPieces.Count; i++)
                    {
                        Piece curr = s_dwellingComfortPieces[i];
                        if (i > 0)
                        {
                            Piece prev = s_dwellingComfortPieces[i - 1];
                            if (curr.m_comfortGroup != Piece.ComfortGroup.None && curr.m_comfortGroup == prev.m_comfortGroup)
                            {
                                continue;
                            }
                            if (curr.m_name == prev.m_name)
                            {
                                continue;
                            }
                        }
                        totalComfort += curr.GetComfort();
                    }

                    s_posCache[s_posCacheCursor & (CacheSize - 1)] = new PositionComfortCacheEntry
                    {
                        Position = position,
                        Timestamp = now,
                        NodeCount = nodeCount,
                        Comfort = totalComfort
                    };
                    s_posCacheCursor++;

                    comfort = totalComfort;
                    return true;
                }
                finally
                {
                    // Never pin Unity Piece/Node references in static scratch buffers across ticks/teleports
                    s_bfsVisited.Clear();
                    s_bfsQueue.Clear();
                    s_connectedNodeIds.Clear();
                    s_nearbyCandidates.Clear();
                    s_dwellingComfortPieces.Clear();
                }
            }

            private static PieceThermalNode? FindStructuralNodeNearPosition(
                Vector3 position,
                IReadOnlyDictionary<int, PieceThermalNode> nodes,
                int pieceLayerMask)
            {
                Vector3 origin = position + Vector3.up * 0.5f;

                if (Physics.Raycast(origin, Vector3.down, out RaycastHit floorHit, 2.5f, pieceLayerMask))
                {
                    Piece p = floorHit.collider.GetComponentInParent<Piece>();
                    if (p != null && nodes.TryGetValue(p.gameObject.GetInstanceID(), out var floorNode))
                    {
                        return floorNode;
                    }
                }

                if (Physics.Raycast(origin, Vector3.up, out RaycastHit roofHit, 20.0f, pieceLayerMask))
                {
                    Piece rp = roofHit.collider.GetComponentInParent<Piece>();
                    if (rp != null && nodes.TryGetValue(rp.gameObject.GetInstanceID(), out var roofNode))
                    {
                        return roofNode;
                    }
                }

                PieceThermalNode? closest = null;
                float closestDistSqr = 16.0f; // 4.0m fallback radius for hearths placed on bare terrain inside walls

                if (nodes is Dictionary<int, PieceThermalNode> concreteDict)
                {
                    // Struct enumerator avoids boxing allocation
                    foreach (var kvp in concreteDict)
                    {
                        float d2 = (kvp.Value.Position - position).sqrMagnitude;
                        if (d2 < closestDistSqr)
                        {
                            closestDistSqr = d2;
                            closest = kvp.Value;
                        }
                    }
                }
                else
                {
                    foreach (var kvp in nodes)
                    {
                        float d2 = (kvp.Value.Position - position).sqrMagnitude;
                        if (d2 < closestDistSqr)
                        {
                            closestDistSqr = d2;
                            closest = kvp.Value;
                        }
                    }
                }

                return closest;
            }

            private static int PieceComfortSort(Piece x, Piece y)
            {
                if (x.m_comfortGroup != y.m_comfortGroup)
                {
                    return x.m_comfortGroup.CompareTo(y.m_comfortGroup);
                }
                float cx = x.GetComfort();
                float cy = y.GetComfort();
                if (cx != cy)
                {
                    return cy.CompareTo(cx);
                }
                return string.Compare(y.m_name, x.m_name, StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// Replaces the hardcoded campfire proximity check for sleeping in beds
        /// with an evaluation of the player's thermal comfort and shelter.
        /// </summary>
        [HarmonyPatch(typeof(Bed), "CheckFire")]
        public static class Bed_CheckFire_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(Bed __instance, Player human, ref bool __result)
            {
                if (!ModConfig.ModEnabled.Value || !ModConfig.EnableThermalResting.Value || human == null)
                {
                    return true;
                }

                float playerTemp = VikingClimate.Core.SurvivalManager.CurrentState.BodyWarmth;
                float minTemp = ModConfig.ComfortMinTemp.Value;
                float maxTemp = ModConfig.ComfortMaxTemp.Value;

                bool isInside = ThermalGraphManager.IsInsideCompound(human) || human.InShelter();

                if (!isInside)
                {
                    human.Message(MessageHud.MessageType.Center, "$msg_bedneedroof");
                    __result = false;
                    return false;
                }

                if (ModConfig.RequireWarmthToSleep.Value && playerTemp < minTemp)
                {
                    human.Message(MessageHud.MessageType.Center, $"You are too cold to sleep ({playerTemp:F0}°C / Need {minTemp:F0}°C)");
                    __result = false;
                    return false;
                }

                if (playerTemp > maxTemp)
                {
                    human.Message(MessageHud.MessageType.Center, $"You are too hot to sleep ({playerTemp:F0}°C)");
                    __result = false;
                    return false;
                }

                __result = true;
                return false;
            }
        }

        /// <summary>
        /// Configures the countdown timer required on SE_Cozy before the Rested buff is granted or refreshed.
        /// Requires staying in the good zone for at least 10s.
        /// </summary>
        [HarmonyPatch(typeof(SE_Cozy), nameof(SE_Cozy.Setup))]
        public static class SE_Cozy_Setup_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(SE_Cozy __instance)
            {
                if (ModConfig.ModEnabled.Value && ModConfig.EnableThermalResting.Value)
                {
                    __instance.m_delay = Mathf.Max(5.0f, ModConfig.RestedDelaySeconds.Value);
                }
            }
        }
    }
}
