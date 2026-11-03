using System;
using System.Collections.Generic;
using UnityEngine;
using VikingClimate.Configuration;

namespace VikingClimate.Features.Temperature.Services
{
    /// <summary>
    /// Evaluates proximity-based convective heat propagation from active fires to building structures.
    /// Distributes thermal power across nearby unoccluded pieces up to a configurable per-piece wattage cap,
    /// preventing small huts from instantly boiling while allowing large hearths to effectively warm
    /// extensive longhouse envelopes without relying on line-of-sight ray density.
    /// </summary>
    public static class ConvectiveHeatPropagator
    {
        private static readonly RaycastHit[] s_occlusionHits = new RaycastHit[32];
        private static readonly Comparison<RaycastHit> s_hitComparison = (a, b) => a.distance.CompareTo(b.distance);

        private static readonly List<PieceThermalNode> s_candidates = new List<PieceThermalNode>(128);
        private static readonly List<float> s_demands = new List<float>(128);

        public static void GetConvectiveParameters(
            ActiveHeatSource source,
            out float totalWatts,
            out float maxWattsPerPiece,
            out float radius)
        {
            string name = (source.DisplayName ?? "").ToLowerInvariant();
            if (source.GameObject != null)
            {
                name += " " + source.GameObject.name.ToLowerInvariant();
            }

            if (name.Contains("hearth"))
            {
                totalWatts = ModConfig.ConvectiveHearthTotalWatts != null ? ModConfig.ConvectiveHearthTotalWatts.Value : 3200.0f;
                maxWattsPerPiece = ModConfig.ConvectiveHearthMaxWattsPerPiece != null ? ModConfig.ConvectiveHearthMaxWattsPerPiece.Value : 50.0f;
                radius = ModConfig.ConvectiveHearthRadius != null ? ModConfig.ConvectiveHearthRadius.Value : 12.0f;
            }
            else if (name.Contains("hanging") || name.Contains("ceiling"))
            {
                totalWatts = ModConfig.ConvectiveHangingBrazierTotalWatts != null ? ModConfig.ConvectiveHangingBrazierTotalWatts.Value : 600.0f;
                maxWattsPerPiece = ModConfig.ConvectiveHangingBrazierMaxWattsPerPiece != null ? ModConfig.ConvectiveHangingBrazierMaxWattsPerPiece.Value : 30.0f;
                radius = ModConfig.ConvectiveHangingBrazierRadius != null ? ModConfig.ConvectiveHangingBrazierRadius.Value : 7.0f;
            }
            else if (name.Contains("brazier"))
            {
                totalWatts = ModConfig.ConvectiveStandingBrazierTotalWatts != null ? ModConfig.ConvectiveStandingBrazierTotalWatts.Value : 600.0f;
                maxWattsPerPiece = ModConfig.ConvectiveStandingBrazierMaxWattsPerPiece != null ? ModConfig.ConvectiveStandingBrazierMaxWattsPerPiece.Value : 30.0f;
                radius = ModConfig.ConvectiveStandingBrazierRadius != null ? ModConfig.ConvectiveStandingBrazierRadius.Value : 7.0f;
            }
            else if (name.Contains("bonfire"))
            {
                totalWatts = ModConfig.ConvectiveBonfireTotalWatts != null ? ModConfig.ConvectiveBonfireTotalWatts.Value : 2000.0f;
                maxWattsPerPiece = ModConfig.ConvectiveBonfireMaxWattsPerPiece != null ? ModConfig.ConvectiveBonfireMaxWattsPerPiece.Value : 60.0f;
                radius = ModConfig.ConvectiveBonfireRadius != null ? ModConfig.ConvectiveBonfireRadius.Value : 14.0f;
            }
            else if (name.Contains("smelter") || name.Contains("blast") || name.Contains("kiln"))
            {
                totalWatts = ModConfig.ConvectiveIndustrialTotalWatts != null ? ModConfig.ConvectiveIndustrialTotalWatts.Value : 3000.0f;
                maxWattsPerPiece = ModConfig.ConvectiveIndustrialMaxWattsPerPiece != null ? ModConfig.ConvectiveIndustrialMaxWattsPerPiece.Value : 60.0f;
                radius = ModConfig.ConvectiveIndustrialRadius != null ? ModConfig.ConvectiveIndustrialRadius.Value : 12.0f;
            }
            else if (name.Contains("torch") || name.Contains("sconce"))
            {
                totalWatts = ModConfig.ConvectiveTorchTotalWatts != null ? ModConfig.ConvectiveTorchTotalWatts.Value : 50.0f;
                maxWattsPerPiece = ModConfig.ConvectiveTorchMaxWattsPerPiece != null ? ModConfig.ConvectiveTorchMaxWattsPerPiece.Value : 10.0f;
                radius = ModConfig.ConvectiveTorchRadius != null ? ModConfig.ConvectiveTorchRadius.Value : 3.0f;
            }
            else // Standard Campfire fallback
            {
                totalWatts = ModConfig.ConvectiveCampfireTotalWatts != null ? ModConfig.ConvectiveCampfireTotalWatts.Value : 800.0f;
                maxWattsPerPiece = ModConfig.ConvectiveCampfireMaxWattsPerPiece != null ? ModConfig.ConvectiveCampfireMaxWattsPerPiece.Value : 25.0f;
                radius = ModConfig.ConvectiveCampfireRadius != null ? ModConfig.ConvectiveCampfireRadius.Value : 6.0f;
            }
        }

        public static void ExecuteConvection(
            List<ActiveHeatSource> trackedHeatSources,
            Dictionary<int, PieceThermalNode> nodes,
            int pieceLayerMask)
        {
            ThermalRelaxationSolver.UpdateClusterIds(nodes);

            for (int i = 0; i < trackedHeatSources.Count; i++)
            {
                ActiveHeatSource source = trackedHeatSources[i];
                if (source.IsActive && source.GameObject != null)
                {
                    PropagateConvectiveHeat(source, nodes, pieceLayerMask);
                }
            }
        }

        public static void RecalculateAllDirectHeat(
            List<ActiveHeatSource> trackedHeatSources,
            Dictionary<int, PieceThermalNode> nodes,
            int pieceLayerMask,
            Action<int> wakeCycle)
        {
            foreach (var kvp in nodes)
            {
                kvp.Value.DirectHeatFlux = 0.0f;
            }

            bool anyFireActive = false;
            for (int i = 0; i < trackedHeatSources.Count; i++)
            {
                if (trackedHeatSources[i].IsActive && trackedHeatSources[i].GameObject != null)
                {
                    anyFireActive = true;
                    break;
                }
            }

            if (anyFireActive)
            {
                ExecuteConvection(trackedHeatSources, nodes, pieceLayerMask);
                wakeCycle(15);
            }
        }

        public static void PropagateConvectiveHeat(
            ActiveHeatSource source,
            Dictionary<int, PieceThermalNode> nodes,
            int pieceLayerMask,
            bool recordOnly = false,
            List<PieceThermalNode>? impactedOut = null)
        {
            if (source == null || source.GameObject == null || !source.IsActive) return;

            GetConvectiveParameters(source, out float fireMaxWatts, out float maxWattsPerPiece, out float radius);

            // Safe origin offset: Elevate by +0.20m above the source position to prevent self-intersection with hearth base/floor
            Vector3 firePos = source.Position + (Vector3.up * 0.20f);
            Transform sourceTransform = source.Transform;

            s_candidates.Clear();
            s_demands.Clear();

            foreach (var kvp in nodes)
            {
                PieceThermalNode node = kvp.Value;
                if (node.PieceRef == null || node.PieceRef.gameObject == null) continue;

                Vector3 piecePos = node.Position;
                Vector3 toPiece = piecePos - firePos;
                float dist = toPiece.magnitude;

                if (dist > radius || dist < 0.01f) continue;

                Vector3 dir = toPiece / dist;

                // Wall Occlusion Check: Graph-connected pieces (hearth lip, alcove framing, adjacent floors) do NOT block,
                // but disconnected pieces (exterior palisade, separate shed, terrain) DO block.
                if (IsOccludedByWall(firePos, dist, dir, node, sourceTransform, pieceLayerMask, nodes))
                {
                    continue;
                }

                // Smooth Euclidean distance weighting: w_i = max(0, 1 - d / R)
                float weight = Mathf.Max(0.0f, 1.0f - (dist / radius));
                float demand = maxWattsPerPiece * weight * node.DirectAbsorption;

                if (demand <= 0.001f) continue;

                s_candidates.Add(node);
                s_demands.Add(demand);
            }

            // Budget Normalization: Sum requested demands; throttle proportionally if exceeding heat source power
            float totalDemand = 0.0f;
            for (int i = 0; i < s_demands.Count; i++)
            {
                totalDemand += s_demands[i];
            }

            float budgetScale = (totalDemand > fireMaxWatts) ? (fireMaxWatts / totalDemand) : 1.0f;
            float totalStructureEnergy = 0.0f;

            for (int i = 0; i < s_candidates.Count; i++)
            {
                PieceThermalNode node = s_candidates[i];
                float energy = s_demands[i] * budgetScale;

                if (!recordOnly)
                {
                    node.DirectHeatFlux += energy;
                }

                totalStructureEnergy += energy;
                source.ImpactedNodeIds.Add(node.InstanceId);
                source.ImpactedEnergyPerNode[node.InstanceId] = source.ImpactedEnergyPerNode.TryGetValue(node.InstanceId, out float cur)
                    ? cur + energy
                    : energy;

                if (impactedOut != null)
                {
                    impactedOut.Add(node);
                }
            }

            source.AbsorbedStructureEnergy += totalStructureEnergy;
            source.EscapedAirEnergy = Mathf.Max(0.0f, source.Power - source.AbsorbedStructureEnergy - source.AbsorbedGroundEnergy);
        }

        private static bool IsOccludedByWall(
            Vector3 firePos,
            float dist,
            Vector3 dir,
            PieceThermalNode targetNode,
            Transform sourceTransform,
            int pieceLayerMask,
            Dictionary<int, PieceThermalNode> nodes)
        {
            int hitCount = Physics.RaycastNonAlloc(firePos, dir, s_occlusionHits, dist, pieceLayerMask);
            if (hitCount == 0) return false;

            Array.Sort(s_occlusionHits, 0, hitCount, Comparer<RaycastHit>.Create(s_hitComparison));

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = s_occlusionHits[i];
                if (hit.collider == null) continue;
                if (hit.collider.transform.IsChildOf(sourceTransform)) continue;

                Piece hitPiece = hit.collider.GetComponentInParent<Piece>();
                if (hitPiece != null)
                {
                    int hitId = hitPiece.gameObject.GetInstanceID();
                    if (hitId == targetNode.InstanceId)
                    {
                        return false; // Struck target piece with no intervening non-connected obstacle
                    }

                    if (hit.distance < dist - 0.20f)
                    {
                        // Struck an intermediate piece!
                        // 1. Non-heatable pieces (workbench, forge, chests, furniture, cooking stations, etc.)
                        // are transparent to heat propagation and must NEVER occlude rays!
                        if (!nodes.TryGetValue(hitId, out var hitNode) || hitNode == null)
                        {
                            // If the hit piece is not registered as a structural thermal node, verify whether it's structural
                            if (!ThermalMaterialDatabase.IsStructuralPiece(hitPiece))
                            {
                                // Non-structural piece (furniture, crafting station, chest, decor) - penetrate through
                                continue;
                            }
                        }

                        // 2. If the intermediate piece belongs to the SAME structural graph cluster,
                        // it must NOT block convective heat transfer from bathing the floors/walls of that structure.
                        if (hitNode != null && hitNode.ClusterId != 0 && hitNode.ClusterId == targetNode.ClusterId)
                        {
                            // Same graph structure (e.g. hearth trim, floor tile, alcove frame).
                            // Does NOT block convective warmth reaching connected pieces of this building.
                            continue;
                        }

                        // Intermediate piece is a structural wall/roof NOT graph-connected to target (e.g. separate outdoor wall, perimeter fence).
                        return true;
                    }
                }
                else
                {
                    // Non-piece static collider (terrain, static rock, world boundary)
                    if (hit.distance < dist - 0.20f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static List<PieceThermalNode> GetHeatSourceImpactedPieces(
            ActiveHeatSource source,
            Dictionary<int, PieceThermalNode> nodes,
            int pieceLayerMask)
        {
            var impacted = new List<PieceThermalNode>();
            if (source == null || source.GameObject == null || !source.IsActive) return impacted;

            if (source.ImpactedNodeIds.Count > 0)
            {
                foreach (int id in source.ImpactedNodeIds)
                {
                    if (nodes.TryGetValue(id, out var node))
                    {
                        impacted.Add(node);
                    }
                }
                return impacted;
            }

            PropagateConvectiveHeat(source, nodes, pieceLayerMask, recordOnly: false, impactedOut: impacted);
            return impacted;
        }
    }
}
