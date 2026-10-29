using System;
using System.Collections.Generic;
using UnityEngine;
using VikingClimate.Configuration;

namespace VikingClimate.Features.Temperature.Services
{
    /// <summary>
    /// Coordinates directional heat rays from active fireplaces and smelters outward to structural pieces.
    /// Uses an upward-biased 64-direction Fibonacci distribution across the solid angle surrounding heat sources.
    /// </summary>
    public static class HeatRaycaster
    {
        private static readonly Vector3[] s_heatSourceRays = InitializeFibonacciSphere(64);
        private static readonly RaycastHit[] s_rayHits = new RaycastHit[32];
        private static readonly System.Comparison<RaycastHit> s_hitComparison = (a, b) => a.distance.CompareTo(b.distance);
        private static readonly List<Piece> s_candidatePieces = new List<Piece>();
        private static readonly HashSet<int> s_seenFires = new HashSet<int>();

        public static Vector3[] HeatSourceRays => s_heatSourceRays;

        private static Vector3[] InitializeFibonacciSphere(int count)
        {
            var dirs = new Vector3[count];
            float phi = Mathf.PI * (3.0f - Mathf.Sqrt(5.0f)); // Golden ratio angle ~2.3999632 rad
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                // Non-linear upward bias: y ranges from +1.0 (straight up) down to -0.40 (floor perimeter around hearth)
                float y = Mathf.Lerp(1.0f, -0.40f, Mathf.Pow(t, 0.90f));
                float radius = Mathf.Sqrt(Mathf.Max(0.0f, 1.0f - y * y));
                float theta = phi * i;

                float x = Mathf.Cos(theta) * radius;
                float z = Mathf.Sin(theta) * radius;

                dirs[i] = new Vector3(x, y, z).normalized;
            }
            return dirs;
        }

        public static void UpdateActiveFires(
            Vector3 center,
            List<ActiveHeatSource> trackedHeatSources,
            Dictionary<int, PieceThermalNode> nodes,
            int pieceLayerMask,
            int raycastLayerMask,
            int terrainLayer,
            Action<int> wakeCycle)
        {
            trackedHeatSources.Clear();
            s_seenFires.Clear();

            // 1. Query Valheim's in-memory piece spatial list within 64m (matches structural dwelling discovery radius)
            s_candidatePieces.Clear();
            Piece.GetAllPiecesInRadius(center, 64.0f, s_candidatePieces);

            for (int i = 0; i < s_candidatePieces.Count; i++)
            {
                Piece p = s_candidatePieces[i];
                if (p == null || p.gameObject == null) continue;

                Fireplace fp = p.GetComponent<Fireplace>();
                if (fp != null && ThermalMaterialDatabase.IsFireplaceLit(fp))
                {
                    int fpId = fp.gameObject.GetInstanceID();
                    if (s_seenFires.Add(fpId))
                    {
                        trackedHeatSources.Add(new ActiveHeatSource(fp));
                    }
                    continue;
                }

                Smelter sm = p.GetComponent<Smelter>();
                if (sm != null && ThermalMaterialDatabase.IsSmelterActive(sm))
                {
                    int smId = sm.gameObject.GetInstanceID();
                    if (s_seenFires.Add(smId))
                    {
                        trackedHeatSources.Add(new ActiveHeatSource(sm));
                    }
                }
            }

            // 2. Cross-check Valheim's internal heat EffectArea list as a robust fallback for custom mod heat sources
            var areas = EffectArea.GetAllAreas();
            if (areas != null)
            {
                for (int a = 0; a < areas.Count; a++)
                {
                    var area = areas[a];
                    if (area == null || (area.m_type & EffectArea.Type.Heat) == 0) continue;
                    if (Vector3.Distance(area.transform.position, center) > 64.0f) continue;

                    Fireplace fp = area.GetComponentInParent<Fireplace>();
                    if (fp != null && ThermalMaterialDatabase.IsFireplaceLit(fp))
                    {
                        int fpId = fp.gameObject.GetInstanceID();
                        if (s_seenFires.Add(fpId))
                        {
                            trackedHeatSources.Add(new ActiveHeatSource(fp));
                        }
                    }
                }
            }

            RecalculateAllDirectHeat(trackedHeatSources, nodes, raycastLayerMask, terrainLayer, wakeCycle);
        }

        public static void RecalculateAllDirectHeat(
            List<ActiveHeatSource> trackedHeatSources,
            Dictionary<int, PieceThermalNode> nodes,
            int raycastLayerMask,
            int terrainLayer,
            Action<int> wakeCycle)
        {
            foreach (var kvp in nodes)
            {
                kvp.Value.DirectHeatFlux = 0.0f;
            }

            bool anyFireActive = false;

            for (int i = 0; i < trackedHeatSources.Count; i++)
            {
                ActiveHeatSource source = trackedHeatSources[i];
                if (source.IsActive && source.GameObject != null)
                {
                    anyFireActive = true;
                    CastHeatSourceRays(source, nodes, raycastLayerMask, terrainLayer);
                }
            }

            if (anyFireActive)
            {
                wakeCycle(15);
            }
        }

        public static void CastHeatSourceRays(
            ActiveHeatSource source,
            Dictionary<int, PieceThermalNode> nodes,
            int raycastLayerMask,
            int terrainLayer,
            bool recordOnly = false,
            List<PieceThermalNode>? impactedOut = null)
        {
            if (source == null || source.GameObject == null || !source.IsActive) return;

            source.ImpactedNodeIds.Clear();
            source.ImpactedEnergyPerNode.Clear();
            float rayPower = source.Power / (float)s_heatSourceRays.Length;
            Vector3 firePos = source.Position;
            float maxRange = ModConfig.FireRayRange != null ? ModConfig.FireRayRange.Value : 16.0f;
            float totalStructureEnergy = 0f;
            float totalGroundEnergy = 0f;

            for (int r = 0; r < s_heatSourceRays.Length; r++)
            {
                Vector3 dir = s_heatSourceRays[r];
                int hitCount = Physics.RaycastNonAlloc(firePos, dir, s_rayHits, maxRange, raycastLayerMask);
                if (hitCount == 0) continue;

                Array.Sort(s_rayHits, 0, hitCount, Comparer<RaycastHit>.Create(s_hitComparison));
                for (int h = 0; h < hitCount; h++)
                {
                    var hit = s_rayHits[h];
                    if (hit.collider == null) continue;
                    if (hit.collider.transform.IsChildOf(source.Transform)) continue;

                    Piece hitPiece = hit.collider.GetComponentInParent<Piece>();
                    if (hitPiece != null)
                    {
                        int pieceId = hitPiece.gameObject.GetInstanceID();
                        if (nodes.TryGetValue(pieceId, out PieceThermalNode node))
                        {
                            float energy = rayPower * node.DirectAbsorption;
                            if (!recordOnly)
                            {
                                node.DirectHeatFlux += energy;
                            }
                            totalStructureEnergy += energy;
                            source.ImpactedEnergyPerNode[pieceId] = source.ImpactedEnergyPerNode.TryGetValue(pieceId, out float cur)
                                ? cur + energy
                                : energy;
                            if (source.ImpactedNodeIds.Add(pieceId) && impactedOut != null)
                            {
                                impactedOut.Add(node);
                            }
                            break;
                        }
                        continue;
                    }

                    bool isTerrain = (hit.collider.gameObject.layer == terrainLayer) ||
                                     (hit.collider.GetComponentInParent<Heightmap>() != null);
                    if (isTerrain)
                    {
                        totalGroundEnergy += rayPower;
                        break;
                    }
                }
            }

            source.AbsorbedStructureEnergy = totalStructureEnergy;
            source.AbsorbedGroundEnergy = totalGroundEnergy;
            source.EscapedAirEnergy = Mathf.Max(0f, source.Power - totalStructureEnergy - totalGroundEnergy);
        }

        public static List<PieceThermalNode> GetHeatSourceImpactedPieces(
            ActiveHeatSource source,
            Dictionary<int, PieceThermalNode> nodes,
            int raycastLayerMask,
            int terrainLayer)
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

            CastHeatSourceRays(source, nodes, raycastLayerMask, terrainLayer, recordOnly: false, impactedOut: impacted);
            return impacted;
        }

        public static List<PieceThermalNode> GetFireplaceImpactedPieces(
            Fireplace fp,
            Dictionary<int, PieceThermalNode> nodes,
            int raycastLayerMask,
            int terrainLayer)
        {
            return GetHeatSourceImpactedPieces(new ActiveHeatSource(fp), nodes, raycastLayerMask, terrainLayer);
        }
    }
}
