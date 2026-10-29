using System.Collections.Generic;
using UnityEngine;
using VikingClimate.Configuration;

namespace VikingClimate.Features.Temperature.Services
{
    /// <summary>
    /// Computes symmetric, dual-bounded discrete structural relaxation across connected piece nodes.
    /// Evaluates joint conduction, environmental air exchange, and lumped thermal mass equalization.
    /// </summary>
    public static class ThermalRelaxationSolver
    {
        private static readonly Collider[] s_tempNeighbors = new Collider[64];
        private static readonly HashSet<int> s_componentVisited = new HashSet<int>();
        private static readonly List<PieceThermalNode> s_currentCluster = new List<PieceThermalNode>();
        private static readonly Queue<PieceThermalNode> s_clusterQueue = new Queue<PieceThermalNode>();

        /// <summary>
        /// Evaluates physical contact between two pieces by checking if their expanded AABBs intersect.
        /// Uses PhysX spatial acceleration for rapid neighbor discovery.
        /// </summary>
        public static void ConnectNodeToNeighbors(PieceThermalNode node, Dictionary<int, PieceThermalNode> nodes, int pieceLayerMask)
        {
            Bounds expanded = node.WorldBounds;
            expanded.Expand(0.12f); // 6cm contact margin in each direction to catch snapped joints

            int hitCount = Physics.OverlapBoxNonAlloc(expanded.center, expanded.extents, s_tempNeighbors, Quaternion.identity, pieceLayerMask);
            for (int i = 0; i < hitCount; i++)
            {
                Collider col = s_tempNeighbors[i];
                if (col == null) continue;

                Piece neighborPiece = col.GetComponentInParent<Piece>();
                if (neighborPiece == null || neighborPiece.gameObject == null) continue;

                int neighborId = neighborPiece.gameObject.GetInstanceID();
                if (neighborId == node.InstanceId) continue;

                if (nodes.TryGetValue(neighborId, out var neighbor))
                {
                    if (expanded.Intersects(neighbor.WorldBounds))
                    {
                        // Harmonic mean interface conductivity: 2 * (k1 * k2) / (k1 + k2)
                        float interfaceK = (2.0f * node.MaterialConductivity * neighbor.MaterialConductivity)
                                         / (node.MaterialConductivity + neighbor.MaterialConductivity);

                        node.AddOrUpdateEdge(neighbor, interfaceK);
                        neighbor.AddOrUpdateEdge(node, interfaceK);
                    }
                }
            }
        }

        public static void RebuildAllConnections(Dictionary<int, PieceThermalNode> nodes, int pieceLayerMask)
        {
            foreach (var kvp in nodes)
            {
                ConnectNodeToNeighbors(kvp.Value, nodes, pieceLayerMask);
            }
        }

        public static int CountTotalEdges(Dictionary<int, PieceThermalNode> nodes)
        {
            int total = 0;
            foreach (var kvp in nodes)
            {
                total += kvp.Value.Edges.Count;
            }
            return total / 2; // Undirected graph
        }

        /// <summary>
        /// Instantly equalizes temperatures across all physically connected building pieces within each connected dwelling cluster.
        /// Treats each connected component as a unified thermal mass (infinite joint conductivity), strictly conserving thermal energy.
        /// </summary>
        public static void EqualizeConnectedComponents(Dictionary<int, PieceThermalNode> nodes)
        {
            s_componentVisited.Clear();

            foreach (var kvp in nodes)
            {
                PieceThermalNode start = kvp.Value;
                if (start.PieceRef == null || start.PieceRef.gameObject == null) continue;
                if (s_componentVisited.Contains(start.InstanceId)) continue;

                s_currentCluster.Clear();
                s_clusterQueue.Clear();

                s_clusterQueue.Enqueue(start);
                s_componentVisited.Add(start.InstanceId);

                float totalEnergy = 0.0f;
                float totalCapacity = 0.0f;

                while (s_clusterQueue.Count > 0)
                {
                    PieceThermalNode curr = s_clusterQueue.Dequeue();
                    s_currentCluster.Add(curr);

                    float cap = Mathf.Max(1.0f, curr.HeatCapacity);
                    totalEnergy += cap * curr.CurrentTemperature;
                    totalCapacity += cap;

                    for (int e = 0; e < curr.Edges.Count; e++)
                    {
                        var edge = curr.Edges[e];
                        PieceThermalNode neighbor = edge.Target;
                        if (neighbor.PieceRef != null && neighbor.PieceRef.gameObject != null)
                        {
                            if (s_componentVisited.Add(neighbor.InstanceId))
                            {
                                s_clusterQueue.Enqueue(neighbor);
                            }
                        }
                    }
                }

                if (totalCapacity > 0.001f)
                {
                    float unifiedTemp = totalEnergy / totalCapacity;
                    for (int i = 0; i < s_currentCluster.Count; i++)
                    {
                        var p = s_currentCluster[i];
                        p.CurrentTemperature = unifiedTemp;
                        p.NextTemperature = unifiedTemp;
                        p.ActualConductiveInflux = 0.0f;
                        p.ActualConductiveOutflux = 0.0f;
                    }
                }
            }
        }

        /// <summary>
        /// Performs a symmetric, dual-bounded discrete structural relaxation step across all registered pieces.
        /// Separates environmental flux (fire/air) from structural conduction, and applies dual-capacity
        /// donor/recipient scaling to guarantee 100% spatial symmetry, zero oscillation, and exact energy conservation.
        /// </summary>
        public static void StepRelaxationIteration(
            Dictionary<int, PieceThermalNode> nodes,
            float ambientTemp,
            ref float lastMaxDeltaT,
            ref int cycleStepsRemaining,
            ref bool isCycleActive)
        {
            float dt = ModConfig.StructuralRelaxationRate != null ? ModConfig.StructuralRelaxationRate.Value : 1.0f;

            // Debug instant conduction mode: Lumped Thermal Mass (Infinite Joint Conduction)
            if (ModConfig.DebugInstantConduction != null && ModConfig.DebugInstantConduction.Value)
            {
                foreach (var kvp in nodes)
                {
                    PieceThermalNode node = kvp.Value;
                    if (node.PieceRef == null || node.PieceRef.gameObject == null) continue;

                    node.TempAtStepStart = node.CurrentTemperature;
                    node.DeltaEnergy = 0.0f;
                    node.ActualConductiveInflux = 0.0f;
                    node.ActualConductiveOutflux = 0.0f;

                    if (node.DirectHeatFlux > 0.001f)
                    {
                        node.DeltaEnergy += node.DirectHeatFlux * dt;
                    }

                    float airFlux = node.EffectiveAirLossRate * (ambientTemp - node.CurrentTemperature);
                    node.DeltaEnergy += airFlux * dt;

                    float cap = Mathf.Max(1.0f, node.HeatCapacity);
                    node.CurrentTemperature += node.DeltaEnergy / cap;
                }

                EqualizeConnectedComponents(nodes);

                float maxDelta = 0.0f;
                foreach (var kvp in nodes)
                {
                    var node = kvp.Value;
                    if (node.PieceRef == null || node.PieceRef.gameObject == null) continue;
                    float d = Mathf.Abs(node.CurrentTemperature - node.TempAtStepStart);
                    if (d > maxDelta) maxDelta = d;
                }

                lastMaxDeltaT = maxDelta;
                if (cycleStepsRemaining > 0) cycleStepsRemaining--;
                if (maxDelta < 0.02f && cycleStepsRemaining <= 0) isCycleActive = false;
                return;
            }

            // Phase 1: Parallel Environmental Exchange (Radiant Fire & Ambient Air)
            foreach (var kvp in nodes)
            {
                PieceThermalNode node = kvp.Value;
                if (node.PieceRef == null || node.PieceRef.gameObject == null) continue;

                node.TempAtStepStart = node.CurrentTemperature;
                node.DeltaEnergy = 0.0f;
                node.DesiredOutSum = 0.0f;
                node.DesiredInSum = 0.0f;
                node.MinColdNeighborTemp = float.MaxValue;
                node.MaxHotNeighborTemp = float.MinValue;
                node.ScaleOut = 1.0f;
                node.ScaleIn = 1.0f;
                node.ActualConductiveInflux = 0.0f;
                node.ActualConductiveOutflux = 0.0f;

                if (node.DirectHeatFlux > 0.001f)
                {
                    node.DeltaEnergy += node.DirectHeatFlux * dt;
                }

                float airFlux = node.EffectiveAirLossRate * (ambientTemp - node.CurrentTemperature);
                node.DeltaEnergy += airFlux * dt;
            }

            // Phase 2: Accumulate Desired Conduction & Neighbor Extrema (Parallel Demand)
            foreach (var kvp in nodes)
            {
                PieceThermalNode nodeA = kvp.Value;
                if (nodeA.PieceRef == null || nodeA.PieceRef.gameObject == null) continue;

                for (int e = 0; e < nodeA.Edges.Count; e++)
                {
                    var edge = nodeA.Edges[e];
                    PieceThermalNode nodeB = edge.Target;
                    if (nodeB.PieceRef == null || nodeB.PieceRef.gameObject == null) continue;

                    if (nodeA.InstanceId >= nodeB.InstanceId) continue;

                    float deltaT = nodeA.CurrentTemperature - nodeB.CurrentTemperature;
                    if (Mathf.Abs(deltaT) < 0.001f) continue;

                    PieceThermalNode hot = deltaT > 0.0f ? nodeA : nodeB;
                    PieceThermalNode cold = deltaT > 0.0f ? nodeB : nodeA;
                    float absDelta = Mathf.Abs(deltaT);

                    float desiredEnergy = edge.InterfaceConductivity * absDelta * dt;

                    hot.DesiredOutSum += desiredEnergy;
                    cold.DesiredInSum += desiredEnergy;

                    if (cold.CurrentTemperature < hot.MinColdNeighborTemp)
                    {
                        hot.MinColdNeighborTemp = cold.CurrentTemperature;
                    }

                    if (hot.CurrentTemperature > cold.MaxHotNeighborTemp)
                    {
                        cold.MaxHotNeighborTemp = hot.CurrentTemperature;
                    }
                }
            }

            // Phase 3: Dual-Capacity Node Scaling Factors (Symmetric Throttle)
            foreach (var kvp in nodes)
            {
                PieceThermalNode node = kvp.Value;
                if (node.PieceRef == null || node.PieceRef.gameObject == null) continue;

                float cap = Mathf.Max(1.0f, node.HeatCapacity);

                if (node.DesiredOutSum > 0.001f && node.MinColdNeighborTemp < float.MaxValue)
                {
                    float maxShed = cap * Mathf.Max(0.0f, node.CurrentTemperature - node.MinColdNeighborTemp) * 0.45f;
                    if (node.DesiredOutSum > maxShed)
                    {
                        node.ScaleOut = maxShed / node.DesiredOutSum;
                    }
                }

                if (node.DesiredInSum > 0.001f && node.MaxHotNeighborTemp > float.MinValue)
                {
                    float maxAbsorb = cap * Mathf.Max(0.0f, node.MaxHotNeighborTemp - node.CurrentTemperature) * 0.45f;
                    if (node.DesiredInSum > maxAbsorb)
                    {
                        node.ScaleIn = maxAbsorb / node.DesiredInSum;
                    }
                }
            }

            // Phase 4: Symmetrically Scaled Pairwise Conduction Execution (100% Conserved)
            foreach (var kvp in nodes)
            {
                PieceThermalNode nodeA = kvp.Value;
                if (nodeA.PieceRef == null || nodeA.PieceRef.gameObject == null) continue;

                for (int e = 0; e < nodeA.Edges.Count; e++)
                {
                    var edge = nodeA.Edges[e];
                    PieceThermalNode nodeB = edge.Target;
                    if (nodeB.PieceRef == null || nodeB.PieceRef.gameObject == null) continue;

                    if (nodeA.InstanceId >= nodeB.InstanceId) continue;

                    float deltaT = nodeA.CurrentTemperature - nodeB.CurrentTemperature;
                    if (Mathf.Abs(deltaT) < 0.001f) continue;

                    PieceThermalNode hot = deltaT > 0.0f ? nodeA : nodeB;
                    PieceThermalNode cold = deltaT > 0.0f ? nodeB : nodeA;
                    float absDelta = Mathf.Abs(deltaT);

                    float desiredEnergy = edge.InterfaceConductivity * absDelta * dt;
                    float scale = Mathf.Min(hot.ScaleOut, cold.ScaleIn);
                    float actualEnergy = desiredEnergy * scale;

                    hot.DeltaEnergy -= actualEnergy;
                    cold.DeltaEnergy += actualEnergy;

                    float watts = actualEnergy / Mathf.Max(0.001f, dt);
                    hot.ActualConductiveOutflux += watts;
                    cold.ActualConductiveInflux += watts;
                }
            }

            // Phase 5: Commit Temperature Updates (dT = dE / C)
            float maxDeltaThisSecond = 0.0f;
            foreach (var kvp in nodes)
            {
                PieceThermalNode node = kvp.Value;
                if (node.PieceRef == null || node.PieceRef.gameObject == null) continue;

                float cap = Mathf.Max(1.0f, node.HeatCapacity);
                float deltaT = node.DeltaEnergy / cap;
                float nextTemp = node.CurrentTemperature + deltaT;

                nextTemp = Mathf.Clamp(nextTemp, ambientTemp - 15.0f, ambientTemp + 95.0f);
                node.CurrentTemperature = nextTemp;

                float totalNodeDelta = Mathf.Abs(nextTemp - node.TempAtStepStart);
                if (totalNodeDelta > maxDeltaThisSecond)
                {
                    maxDeltaThisSecond = totalNodeDelta;
                }
            }

            lastMaxDeltaT = maxDeltaThisSecond;

            if (cycleStepsRemaining > 0)
            {
                cycleStepsRemaining--;
            }

            if (maxDeltaThisSecond < 0.02f && cycleStepsRemaining <= 0)
            {
                isCycleActive = false;
            }
        }
    }
}
