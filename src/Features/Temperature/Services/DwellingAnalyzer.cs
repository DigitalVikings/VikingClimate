using System;
using System.Collections.Generic;
using UnityEngine;
using VikingClimate.Configuration;

namespace VikingClimate.Features.Temperature.Services
{
    public readonly struct ThermalSampleRay
    {
        public readonly Vector3 Direction;
        public readonly float Weight;
        public readonly bool IsAtmospheric;

        public ThermalSampleRay(Vector3 direction, float weight, bool isAtmospheric)
        {
            Direction = direction;
            Weight = weight;
            IsAtmospheric = isAtmospheric;
        }
    }

    /// <summary>
    /// Evaluates player dwelling boundaries, spherical Fibonacci solid-angle field radiation,
    /// structural enclosure ratios, and connected dwelling comfort aggregation.
    /// </summary>
    public static class DwellingAnalyzer
    {
        private static readonly ThermalSampleRay[] s_sampleRays = InitializeSampleRays(64);
        private static readonly Collider[] s_tempNeighbors = new Collider[64];

        public static ThermalSampleRay[] SampleRays => s_sampleRays;

        private static ThermalSampleRay[] InitializeSampleRays(int sampleCount = 64)
        {
            var rays = new ThermalSampleRay[sampleCount];
            float phi = Mathf.PI * (3.0f - Mathf.Sqrt(5.0f));

            const int upperCount = 52;
            const int lowerCount = 12;

            float upperWeight = 0.575f / upperCount;
            float lowerWeight = 0.425f / lowerCount;

            for (int i = 0; i < upperCount; i++)
            {
                float t = (float)i / (float)(upperCount - 1);
                float y = Mathf.Lerp(1.0f, -0.15f, t);
                float radius = Mathf.Sqrt(Mathf.Max(0.0f, 1.0f - y * y));
                float theta = phi * i;

                float x = Mathf.Cos(theta) * radius;
                float z = Mathf.Sin(theta) * radius;

                Vector3 dir = new Vector3(x, y, z).normalized;
                rays[i] = new ThermalSampleRay(dir, upperWeight, isAtmospheric: true);
            }

            rays[upperCount] = new ThermalSampleRay(new Vector3(0.0f, -1.0f, 0.0f), lowerWeight, isAtmospheric: false);

            for (int j = 0; j < lowerCount - 1; j++)
            {
                float t = (float)j / (float)(lowerCount - 2);
                float y = Mathf.Lerp(-0.25f, -0.85f, t);
                float radius = Mathf.Sqrt(Mathf.Max(0.0f, 1.0f - y * y));
                float theta = phi * (j + upperCount);

                float x = Mathf.Cos(theta) * radius;
                float z = Mathf.Sin(theta) * radius;

                Vector3 dir = new Vector3(x, y, z).normalized;
                rays[upperCount + 1 + j] = new ThermalSampleRay(dir, lowerWeight, isAtmospheric: false);
            }

            return rays;
        }

        public static void PerformFullThermalScan(
            Player? player,
            Vector3 pos,
            float outdoorAmbient,
            float groundTemp,
            Dictionary<int, PieceThermalNode> nodes,
            List<ActiveHeatSource> trackedHeatSources,
            int pieceLayerMask,
            int raycastLayerMask,
            int terrainLayer,
            bool isCycleActive,
            int cycleStepsRemaining,
            float lastMaxDeltaT,
            out float outDirectFlame,
            out float outStructuralWarmth,
            out float outEnclosureRatio,
            out bool outIsSheltered,
            out ThermalGraphTelemetry outTelemetry)
        {
            Vector3 chestPos = pos + (Vector3.up * 1.0f);

            int visibleFiresInLOS = 0;
            float directFlameWarmth = 0.0f;
            for (int i = 0; i < trackedHeatSources.Count; i++)
            {
                ActiveHeatSource source = trackedHeatSources[i];
                if (!source.IsActive || source.GameObject == null) continue;

                Vector3 firePos = source.Position;
                float dist = Vector3.Distance(firePos, chestPos);
                if (dist > 8.0f) continue;

                Vector3 dir = (chestPos - firePos).normalized;
                if (!Physics.Raycast(firePos, dir, dist, pieceLayerMask))
                {
                    visibleFiresInLOS++;
                    float coeff = ModConfig.DirectFlameWarmthCoeff != null ? ModConfig.DirectFlameWarmthCoeff.Value : 0.008f;
                    float directWarmth = (source.Power * coeff) / Mathf.Max(1.0f, dist * dist);
                    directFlameWarmth += directWarmth;
                }
            }

            int pieceRaysHit = 0;
            int groundRaysHit = 0;
            int airRaysHit = 0;
            int atmosphericRayCount = 0;
            int atmosphericAirEscaped = 0;
            float totalWeightedRayTemp = 0.0f;
            float pieceSurfaceTempSum = 0.0f;
            const float maxStructureSightline = 25.0f;

            for (int r = 0; r < s_sampleRays.Length; r++)
            {
                var ray = s_sampleRays[r];
                Vector3 dir = ray.Direction;
                if (ray.IsAtmospheric) atmosphericRayCount++;

                float rayTemp;

                if (Physics.Raycast(chestPos, dir, out RaycastHit hit, maxStructureSightline, raycastLayerMask))
                {
                    Piece hitPiece = hit.collider.GetComponentInParent<Piece>();
                    if (hitPiece != null && hitPiece.gameObject != null)
                    {
                        int id = hitPiece.gameObject.GetInstanceID();
                        if (nodes.TryGetValue(id, out var node))
                        {
                            pieceRaysHit++;
                            pieceSurfaceTempSum += node.CurrentTemperature;
                            rayTemp = node.CurrentTemperature;
                            totalWeightedRayTemp += rayTemp * ray.Weight;
                            continue;
                        }

                        WearNTear wnt = hit.collider.GetComponentInParent<WearNTear>();
                        if (wnt != null && !hitPiece.m_waterPiece)
                        {
                            pieceRaysHit++;
                            pieceSurfaceTempSum += outdoorAmbient;
                            rayTemp = outdoorAmbient;
                            totalWeightedRayTemp += rayTemp * ray.Weight;
                            continue;
                        }
                    }

                    bool isTerrain = (hit.collider.gameObject.layer == terrainLayer) || 
                                     (hit.collider.GetComponentInParent<Heightmap>() != null);
                    if (isTerrain)
                    {
                        groundRaysHit++;
                        rayTemp = groundTemp;
                    }
                    else
                    {
                        groundRaysHit++;
                        rayTemp = groundTemp;
                    }
                }
                else
                {
                    airRaysHit++;
                    rayTemp = outdoorAmbient;
                    if (ray.IsAtmospheric) atmosphericAirEscaped++;
                }

                totalWeightedRayTemp += rayTemp * ray.Weight;
            }

            float integratedFieldTemp = totalWeightedRayTemp;
            float enclosureRatio = atmosphericRayCount > 0 ? (1.0f - ((float)atmosphericAirEscaped / atmosphericRayCount)) : 0.0f;
            outEnclosureRatio = enclosureRatio;

            float threshold = ModConfig.EnclosureShelterThreshold != null ? ModConfig.EnclosureShelterThreshold.Value : 0.80f;
            bool hasDirectRoof = Physics.Raycast(chestPos, Vector3.up, 20.0f, pieceLayerMask);
            bool hasRoof = hasDirectRoof || (player != null && player.InShelter());

            outIsSheltered = (enclosureRatio >= threshold) && (pieceRaysHit >= 12) && hasRoof;
            float structuralWarmth = outIsSheltered ? Mathf.Max(0.0f, integratedFieldTemp - outdoorAmbient) : 0.0f;
            float maxFlameWarmth = ModConfig.DirectFlameWarmthMax != null ? ModConfig.DirectFlameWarmthMax.Value : 18.0f;
            directFlameWarmth = Mathf.Clamp(directFlameWarmth, 0.0f, maxFlameWarmth);

            float avgPieceTemp = pieceRaysHit > 0 ? (pieceSurfaceTempSum / pieceRaysHit) : outdoorAmbient;

            outDirectFlame = directFlameWarmth;
            outStructuralWarmth = structuralWarmth;

            outTelemetry = new ThermalGraphTelemetry
            {
                IsActive = true,
                TotalNodes = nodes.Count,
                TotalEdges = ThermalRelaxationSolver.CountTotalEdges(nodes),
                IsCycleActive = isCycleActive,
                CycleStepsRemaining = cycleStepsRemaining,
                MaxDeltaT = lastMaxDeltaT,
                ActiveFiresCount = visibleFiresInLOS,
                PieceRaysHit = pieceRaysHit,
                GroundRaysHit = groundRaysHit,
                AirRaysHit = airRaysHit,
                AveragePieceSurfaceTemp = avgPieceTemp,
                GroundTemp = groundTemp,
                OutdoorAirTemp = outdoorAmbient,
                DirectFlameWarmth = directFlameWarmth,
                StructuralWarmth = structuralWarmth,
                EnclosureRatio = enclosureRatio,
                IsSheltered = outIsSheltered
            };
        }

        private static Vector3 s_lastComfortPos = Vector3.positiveInfinity;
        private static float s_lastComfortTime = -999.0f;
        private static int s_cachedComfortLevel = 1;
        private static bool s_cachedComfortShelter = false;
        private static readonly List<(string name, int comfort, string group)> s_cachedContributingItems = new List<(string, int, string)>();
        private static int s_cachedRedundantCount = 0;

        public static void InvalidateComfortCache()
        {
            s_lastComfortTime = -999.0f;
            s_lastComfortPos = Vector3.positiveInfinity;
        }

        public static HashSet<PieceThermalNode> ExpandConnectedDwelling(
            PieceThermalNode startNode,
            float ambientTemp,
            Dictionary<int, PieceThermalNode> nodes,
            int pieceLayerMask,
            Func<Piece, float, bool, PieceThermalNode?> registerPieceFunc,
            float maxDistance = 100.0f)
        {
            var connectedNodes = new HashSet<PieceThermalNode>();
            if (startNode == null) return connectedNodes;

            float maxDistSqr = maxDistance * maxDistance;
            Vector3 originPos = startNode.Position;

            var queue = new Queue<PieceThermalNode>();
            queue.Enqueue(startNode);
            connectedNodes.Add(startNode);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                for (int i = 0; i < current.Edges.Count; i++)
                {
                    var neighbor = current.Edges[i].Target;
                    if (connectedNodes.Add(neighbor))
                    {
                        if ((neighbor.Position - originPos).sqrMagnitude <= maxDistSqr)
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            return connectedNodes;
        }

        public static int CalculateDwellingComfort(
            Player player,
            bool inShelter,
            Vector3 position,
            bool isInsideCompound,
            Dictionary<int, PieceThermalNode> nodes,
            int pieceLayerMask,
            float lastAmbientTemp,
            DwellingComfortInfo comfortInfo,
            Func<Piece, float, bool, PieceThermalNode?> registerPieceFunc)
        {
            bool isInside = player != null && (inShelter || isInsideCompound);
            if (!isInside)
            {
                int fbComfort = FallbackComfort(position, false);
                comfortInfo.TotalComfort = fbComfort;
                comfortInfo.InShelter = false;
                comfortInfo.ContributingItems.Clear();
                comfortInfo.RedundantCount = 0;
                return fbComfort;
            }

            float now = Time.time;
            if (now - s_lastComfortTime < 10.0f && Vector3.Distance(position, s_lastComfortPos) < 4.0f)
            {
                comfortInfo.TotalComfort = s_cachedComfortLevel;
                comfortInfo.InShelter = s_cachedComfortShelter;
                comfortInfo.ContributingItems.Clear();
                comfortInfo.ContributingItems.AddRange(s_cachedContributingItems);
                comfortInfo.RedundantCount = s_cachedRedundantCount;
                return s_cachedComfortLevel;
            }

            PieceThermalNode? startNode = FindStructuralNodeNearPlayer(position, nodes, pieceLayerMask, isInsideCompound);
            if (startNode == null)
            {
                int fbComfort = FallbackComfort(position, true);
                comfortInfo.TotalComfort = fbComfort;
                comfortInfo.InShelter = true;
                comfortInfo.ContributingItems.Clear();
                comfortInfo.RedundantCount = 0;
                return fbComfort;
            }

            var connectedNodes = ExpandConnectedDwelling(startNode, lastAmbientTemp, nodes, pieceLayerMask, registerPieceFunc, 100.0f);

            var connectedNodeIds = new HashSet<int>();
            Bounds dwellingBounds = startNode.WorldBounds;
            foreach (var node in connectedNodes)
            {
                connectedNodeIds.Add(node.InstanceId);
                dwellingBounds.Encapsulate(node.WorldBounds);
            }

            dwellingBounds.Expand(3.5f);

            var dwellingComfortPieces = new List<Piece>();
            float sphereRadius = dwellingBounds.extents.magnitude + 4.0f;
            var nearbyCandidates = new List<Piece>();
            Piece.GetAllComfortPiecesInRadius(dwellingBounds.center, sphereRadius, nearbyCandidates);

            for (int i = 0; i < nearbyCandidates.Count; i++)
            {
                Piece cp = nearbyCandidates[i];
                if (cp == null || cp.gameObject == null) continue;

                Vector3 cpPos = cp.transform.position;
                if (!dwellingBounds.Contains(cpPos)) continue;

                int cpId = cp.gameObject.GetInstanceID();
                bool belongsToDwelling = connectedNodeIds.Contains(cpId);

                if (!belongsToDwelling)
                {
                    // Downward raycast to check if resting directly on a floor/ground piece belonging to the dwelling
                    if (Physics.Raycast(cpPos + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, 3.5f, pieceLayerMask))
                    {
                        Piece supportingPiece = hit.collider.GetComponentInParent<Piece>();
                        if (supportingPiece != null && connectedNodeIds.Contains(supportingPiece.gameObject.GetInstanceID()))
                        {
                            belongsToDwelling = true;
                        }
                    }
                }

                if (!belongsToDwelling)
                {
                    // Check direct physical proximity for wall hangings (banners, trophies)
                    int hitCount = Physics.OverlapSphereNonAlloc(cpPos, 2.5f, s_tempNeighbors, pieceLayerMask);
                    for (int k = 0; k < hitCount; k++)
                    {
                        Piece np = s_tempNeighbors[k].GetComponentInParent<Piece>();
                        if (np != null && connectedNodeIds.Contains(np.gameObject.GetInstanceID()))
                        {
                            belongsToDwelling = true;
                            break;
                        }
                    }
                }

                if (belongsToDwelling)
                {
                    dwellingComfortPieces.Add(cp);
                }
            }

            dwellingComfortPieces.Sort(PieceComfortSort);

            int comfort = 1;
            comfort++; // In shelter (+1)

            var contributingItems = new List<(string name, int comfort, string group)>();
            int redundantCount = 0;

            for (int i = 0; i < dwellingComfortPieces.Count; i++)
            {
                Piece curr = dwellingComfortPieces[i];
                if (i > 0)
                {
                    Piece prev = dwellingComfortPieces[i - 1];
                    if (curr.m_comfortGroup != Piece.ComfortGroup.None && curr.m_comfortGroup == prev.m_comfortGroup)
                    {
                        redundantCount++;
                        continue;
                    }
                    if (curr.m_name == prev.m_name)
                    {
                        redundantCount++;
                        continue;
                    }
                }
                int pieceComfort = curr.GetComfort();
                comfort += pieceComfort;
                string cleanName = (Localization.instance != null) ? Localization.instance.Localize(curr.m_name) : curr.m_name;
                if (string.IsNullOrEmpty(cleanName))
                {
                    cleanName = curr.gameObject.name.Replace("(Clone)", "").Trim();
                }
                contributingItems.Add((cleanName, pieceComfort, curr.m_comfortGroup.ToString()));
            }

            comfortInfo.TotalComfort = comfort;
            comfortInfo.InShelter = true;
            comfortInfo.ContributingItems.Clear();
            comfortInfo.ContributingItems.AddRange(contributingItems);
            comfortInfo.RedundantCount = redundantCount;

            s_lastComfortTime = now;
            s_lastComfortPos = position;
            s_cachedComfortLevel = comfort;
            s_cachedComfortShelter = true;
            s_cachedContributingItems.Clear();
            s_cachedContributingItems.AddRange(contributingItems);
            s_cachedRedundantCount = redundantCount;

            if (ModConfig.EnableLogging.Value)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"[Dwelling Comfort] Comfort Level: {comfort} for '{player?.GetPlayerName() ?? "Player"}' (Base: 1, Shelter: +1, Items: +{comfort - 2})");
                sb.AppendLine($"  Contributing Comfort Items ({contributingItems.Count}):");
                for (int c = 0; c < contributingItems.Count; c++)
                {
                    var item = contributingItems[c];
                    string grpStr = item.group != "None" ? $" (Group: {item.group})" : "";
                    sb.AppendLine($"    • {item.name}: +{item.comfort}{grpStr}");
                }
                if (redundantCount > 0)
                {
                    sb.AppendLine($"  ({redundantCount} redundant items omitted by group priority/duplicate rules)");
                }
                VikingClimatePlugin.Log.LogInfo(sb.ToString().TrimEnd());
            }

            return comfort;
        }

        public static PieceThermalNode? FindStructuralNodeNearPlayer(
            Vector3 position,
            Dictionary<int, PieceThermalNode> nodes,
            int pieceLayerMask,
            bool isSheltered)
        {
            Vector3 origin = position + Vector3.up * 0.5f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 2.5f, pieceLayerMask))
            {
                Piece p = hit.collider.GetComponentInParent<Piece>();
                if (p != null && nodes.TryGetValue(p.gameObject.GetInstanceID(), out var node))
                {
                    return node;
                }
            }

            if (Physics.Raycast(origin, Vector3.up, out RaycastHit roofHit, 15.0f, pieceLayerMask))
            {
                Piece rp = roofHit.collider.GetComponentInParent<Piece>();
                if (rp != null && nodes.TryGetValue(rp.gameObject.GetInstanceID(), out var rNode))
                {
                    return rNode;
                }
            }

            if (isSheltered)
            {
                PieceThermalNode? closest = null;
                float closestDistSqr = 16.0f;
                foreach (var kvp in nodes)
                {
                    float d2 = (kvp.Value.Position - position).sqrMagnitude;
                    if (d2 < closestDistSqr)
                    {
                        closestDistSqr = d2;
                        closest = kvp.Value;
                    }
                }
                return closest;
            }

            return null;
        }

        public static DwellingThermalProfile GetDwellingProfile(
            PieceThermalNode startNode,
            float outdoorWeather,
            Dictionary<int, PieceThermalNode> nodes)
        {
            var profile = new DwellingThermalProfile
            {
                AllPieces = new HashSet<PieceThermalNode>()
            };

            if (startNode == null) return profile;

            var queue = new Queue<PieceThermalNode>();
            queue.Enqueue(startNode);
            profile.AllPieces.Add(startNode);

            float totalTemp = 0.0f;
            float totalCapacity = 0.0f;
            float totalFire = 0.0f;
            float totalAirCoeff = 0.0f;
            float totalCoverPct = 0.0f;

            while (queue.Count > 0)
            {
                PieceThermalNode curr = queue.Dequeue();

                if (curr.CoverPercentage <= 0.001f && !curr.UnderRoof)
                {
                    ThermalGraphManager.UpdateNodeCoverAndRoof(curr);
                }

                profile.PieceCount++;
                if (curr.MaterialType == "Wood") profile.WoodCount++;
                else if (curr.MaterialType == "Stone") profile.StoneCount++;
                else if (curr.MaterialType == "CoreWood") profile.CoreWoodCount++;
                else profile.OtherCount++;

                if (curr.UnderRoof) profile.RoofedCount++;
                totalCoverPct += curr.CoverPercentage;

                float cap = Mathf.Max(1.0f, curr.HeatCapacity);
                totalTemp += curr.CurrentTemperature * cap;
                totalCapacity += cap;

                totalFire += curr.DirectHeatFlux;
                totalAirCoeff += curr.EffectiveAirLossRate;

                for (int i = 0; i < curr.Edges.Count; i++)
                {
                    var neighbor = curr.Edges[i].Target;
                    if (profile.AllPieces.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            profile.UniformTemperature = totalCapacity > 0.001f ? (totalTemp / totalCapacity) : outdoorWeather;
            profile.TotalHeatCapacity = totalCapacity;
            profile.TotalFireInflux = totalFire;
            profile.TotalAirLossCoeff = totalAirCoeff;
            profile.AverageCoverPct = profile.PieceCount > 0 ? (totalCoverPct / profile.PieceCount) : 0.0f;

            float deltaT = profile.UniformTemperature - outdoorWeather;
            profile.TotalAirLoss = deltaT > 0f ? (totalAirCoeff * deltaT) : 0f;
            profile.TotalAirGain = deltaT < 0f ? (totalAirCoeff * -deltaT) : 0f;
            profile.NetFlux = totalFire - (totalAirCoeff * deltaT);

            profile.ExpectedRateOfChange = totalCapacity > 0.001f ? (Mathf.Abs(profile.NetFlux) / totalCapacity) : 0.0f;
            profile.ExpectedEquilibriumTemp = totalAirCoeff > 0.001f ? (outdoorWeather + (totalFire / totalAirCoeff)) : outdoorWeather;

            return profile;
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

        private static int FallbackComfort(Vector3 position, bool sheltered)
        {
            var pieces = new List<Piece>();
            Piece.GetAllComfortPiecesInRadius(position, 10.0f, pieces);
            pieces.Sort(PieceComfortSort);
            int comfort = sheltered ? 2 : 1;
            for (int i = 0; i < pieces.Count; i++)
            {
                Piece curr = pieces[i];
                if (i > 0)
                {
                    Piece prev = pieces[i - 1];
                    if (curr.m_comfortGroup != Piece.ComfortGroup.None && curr.m_comfortGroup == prev.m_comfortGroup)
                    {
                        continue;
                    }
                    if (curr.m_name == prev.m_name)
                    {
                        continue;
                    }
                }
                comfort += curr.GetComfort();
            }
            return comfort;
        }
    }
}
