using System;
using System.Collections.Generic;
using UnityEngine;
using VikingClimate.Configuration;
using VikingClimate.Core;
using VikingClimate.Features.Temperature.Services;

namespace VikingClimate.Features.Temperature
{
    /// <summary>
    /// Represents any active heat source (Fireplace, Smelter, Charcoal Kiln, Blast Furnace)
    /// that radiates direct line-of-sight thermal energy.
    /// </summary>
    public class ActiveHeatSource
    {
        public GameObject GameObject { get; }
        public Transform Transform { get; }
        public Vector3 Position { get; }
        public string DisplayName { get; }
        public bool IsActive { get; }
        public float Power { get; }
        public string ActiveStatusText { get; }
        public string InactiveStatusText { get; }
        public Fireplace? FireplaceRef { get; }
        public Smelter? SmelterRef { get; }
        public HashSet<int> ImpactedNodeIds { get; } = new HashSet<int>();
        public Dictionary<int, float> ImpactedEnergyPerNode { get; } = new Dictionary<int, float>();
        public float AbsorbedStructureEnergy { get; set; }
        public float AbsorbedGroundEnergy { get; set; }
        public float EscapedAirEnergy { get; set; }

        public float TotalDepositedEnergy
        {
            get
            {
                float sum = 0f;
                foreach (var val in ImpactedEnergyPerNode.Values)
                {
                    sum += val;
                }
                return sum;
            }
        }

        public ActiveHeatSource(Fireplace fp)
        {
            FireplaceRef = fp;
            GameObject = fp.gameObject;
            Transform = fp.transform;
            Position = fp.transform.position + Vector3.up * 0.35f;

            string rawName = fp.m_name;
            if (string.IsNullOrEmpty(rawName))
            {
                Piece p = fp.GetComponent<Piece>();
                rawName = p != null && !string.IsNullOrEmpty(p.m_name) ? p.m_name : fp.gameObject.name.Replace("(Clone)", "").Trim();
            }
            DisplayName = (Localization.instance != null) ? Localization.instance.Localize(rawName) : rawName;
            IsActive = ThermalMaterialDatabase.IsFireplaceLit(fp);
            Power = ThermalMaterialDatabase.GetFireplacePower(fp);
            ActiveStatusText = "Burning (Active)";
            InactiveStatusText = "Unlit / Extinguished";
        }

        public ActiveHeatSource(Smelter sm)
        {
            SmelterRef = sm;
            GameObject = sm.gameObject;
            Transform = sm.transform;
            Position = sm.transform.position + Vector3.up * 1.2f;

            string rawName = sm.m_name;
            if (string.IsNullOrEmpty(rawName))
            {
                Piece p = sm.GetComponent<Piece>();
                rawName = p != null && !string.IsNullOrEmpty(p.m_name) ? p.m_name : sm.gameObject.name.Replace("(Clone)", "").Trim();
            }
            DisplayName = (Localization.instance != null) ? Localization.instance.Localize(rawName) : rawName;
            IsActive = ThermalMaterialDatabase.IsSmelterActive(sm);
            Power = ThermalMaterialDatabase.GetSmelterPower(sm);
            ActiveStatusText = rawName.ToLowerInvariant().Contains("kiln") ? "Burning (Active)" : "Smelting (Active)";
            InactiveStatusText = "Idle / No Fuel or Ore";
        }
    }

    /// <summary>
    /// Holds the cached comfort breakdown of the dwelling containing the player
    /// for sparse, localized presentation in Hugin's Compendium.
    /// </summary>
    public class DwellingComfortInfo
    {
        public int TotalComfort { get; set; } = 1;
        public bool InShelter { get; set; }
        public List<(string Name, int Comfort, string Group)> ContributingItems { get; } = new List<(string Name, int Comfort, string Group)>();
        public int RedundantCount { get; set; }
    }

    /// <summary>
    /// Coordinates the piece-centric Structural Thermal Graph.
    /// Manages piece adjacency, multi-heat-source line-of-sight influx,
    /// dynamic cycle-based structural relaxation, and player dwelling warmth.
    /// </summary>
    public class ThermalGraphManager
    {
        private static ThermalGraphManager? s_instance;
        public static ThermalGraphManager Instance => s_instance ??= new ThermalGraphManager();

        public DwellingComfortInfo LastComfortInfo { get; } = new DwellingComfortInfo();

        private float m_samplingTimer = 999.0f;
        private Vector3 m_lastSamplePos = Vector3.positiveInfinity;
        private float m_cachedPerceivedTemp = 20.0f;
        private float m_cachedDirectFlame = 0.0f;
        private float m_cachedStructuralWarmth = 0.0f;
        private ThermalGraphTelemetry m_cachedTelemetry = default;
        private bool m_cachedIsSheltered = false;
        private bool m_cachedHasDirectRoof = false;
        private bool m_lastNativeShelter = false;
        private float m_cachedEnclosureRatio = 0.0f;

        public bool IsPlayerSheltered => m_cachedIsSheltered;
        public float EnclosureRatio => m_cachedEnclosureRatio;

        public static bool IsInsideCompound(Player player)
        {
            if (player == null) return false;
            return Instance.m_cachedIsSheltered;
        }

        private readonly Dictionary<int, PieceThermalNode> m_nodes = new Dictionary<int, PieceThermalNode>();
        private readonly List<ActiveHeatSource> m_trackedHeatSources = new List<ActiveHeatSource>();
        public IReadOnlyList<ActiveHeatSource> TrackedHeatSources => m_trackedHeatSources;
        private readonly List<Piece> m_tempPieces = new List<Piece>();

        private bool m_isCycleActive = true;
        private int m_cycleStepsRemaining = 25;
        private float m_stepTimer = 0.0f;
        private float m_lastMaxDeltaT = 0.0f;
        private float m_fireScanTimer = 0.0f;
        private float m_zdoSyncTimer = 0.0f;
        private Vector3 m_lastScanPosition = Vector3.positiveInfinity;

        private int m_pieceLayerMask = 0;
        private int m_raycastLayerMask = 0;
        private int m_terrainLayer = 0;

        public static readonly int s_zdoKeyTemp = "VikingClimate_PieceTemp".GetStableHashCode();
        public static readonly int s_zdoKeyTime = "VikingClimate_PieceTime".GetStableHashCode();
        public static readonly int s_legacyZdoKeyTemp = "VikingSurvival_PieceTemp".GetStableHashCode();
        public static readonly int s_legacyZdoKeyTime = "VikingSurvival_PieceTime".GetStableHashCode();

        public bool IsCycleActive => m_isCycleActive;
        public int NodeCount => m_nodes.Count;

        private float m_lastAmbientTemp = 10.0f;

        public ThermalGraphManager()
        {
            m_pieceLayerMask = LayerMask.GetMask("piece", "Default", "static_solid");
            if (m_pieceLayerMask == 0)
            {
                m_pieceLayerMask = 1 << LayerMask.NameToLayer("piece");
            }

            m_terrainLayer = LayerMask.NameToLayer("terrain");
            m_raycastLayerMask = LayerMask.GetMask("piece", "Default", "static_solid", "terrain");
            if (m_raycastLayerMask == 0)
            {
                m_raycastLayerMask = m_pieceLayerMask | (1 << m_terrainLayer);
            }

            if (ModConfig.DebugFreezeStructureTemps != null)
            {
                ModConfig.DebugFreezeStructureTemps.SettingChanged += (sender, args) =>
                {
                    if (ModConfig.DebugFreezeStructureTemps.Value)
                    {
                        ForceAllStructureTemperatures(ModConfig.DebugForcedStructureTemp != null ? ModConfig.DebugForcedStructureTemp.Value : 15.0f);
                    }
                    else
                    {
                        WakeCycle(30);
                    }
                };
            }

            if (ModConfig.DebugForcedStructureTemp != null)
            {
                ModConfig.DebugForcedStructureTemp.SettingChanged += (sender, args) =>
                {
                    if (ModConfig.DebugFreezeStructureTemps != null && ModConfig.DebugFreezeStructureTemps.Value)
                    {
                        ForceAllStructureTemperatures(ModConfig.DebugForcedStructureTemp.Value);
                    }
                };
            }

            if (ModConfig.DebugInstantConduction != null)
            {
                ModConfig.DebugInstantConduction.SettingChanged += (sender, args) =>
                {
                    if (ModConfig.DebugInstantConduction.Value)
                    {
                        EqualizeConnectedComponents();
                    }
                    WakeCycle(30);
                };
            }

            if (ModConfig.ThermalMassSimulationMode != null)
            {
                ModConfig.ThermalMassSimulationMode.SettingChanged += (sender, args) =>
                {
                    if (ModConfig.ThermalMassSimulationMode.Value != ThermalMassMode.StructuralGraph)
                    {
                        Clear();
                    }
                };
            }
        }

        public void Clear()
        {
            m_nodes.Clear();
            m_trackedHeatSources.Clear();
            m_tempPieces.Clear();
            m_cachedIsSheltered = false;
            m_cachedHasDirectRoof = false;
            m_lastNativeShelter = false;
            m_cachedEnclosureRatio = 0.0f;
            m_cachedPerceivedTemp = 20.0f;
            m_cachedDirectFlame = 0.0f;
            m_cachedStructuralWarmth = 0.0f;
            m_lastScanPosition = Vector3.positiveInfinity;
            m_lastSamplePos = Vector3.positiveInfinity;
            m_samplingTimer = 999.0f;
            m_fireScanTimer = 0.0f;
            m_stepTimer = 0.0f;
            m_isCycleActive = false;
            m_cycleStepsRemaining = 0;
            LastComfortInfo.ContributingItems.Clear();
            LastComfortInfo.TotalComfort = 1;
            LastComfortInfo.RedundantCount = 0;
            DwellingAnalyzer.InvalidateComfortCache();
        }

        public void ForceAllStructureTemperatures(float targetTemp)
        {
            foreach (var kvp in m_nodes)
            {
                PieceThermalNode node = kvp.Value;
                node.CurrentTemperature = targetTemp;
                node.NextTemperature = targetTemp;
                node.TempAtStepStart = targetTemp;
                node.DeltaEnergy = 0.0f;
                node.DesiredOutSum = 0.0f;
                node.DesiredInSum = 0.0f;
                node.ActualConductiveInflux = 0.0f;
                node.ActualConductiveOutflux = 0.0f;
            }
        }

        public void WakeCycle(int durationSeconds = 25)
        {
            m_isCycleActive = true;
            m_cycleStepsRemaining = Mathf.Max(m_cycleStepsRemaining, durationSeconds);
        }

        public void Update(float dt, Player player, float ambientTemp)
        {
            if (player == null || !ModConfig.ModEnabled.Value || ModConfig.ThermalMassSimulationMode.Value != ThermalMassMode.StructuralGraph) return;
            m_lastAmbientTemp = ambientTemp;

            Vector3 playerPos = player.transform.position;

            if (Vector3.Distance(playerPos, m_lastScanPosition) > 16.0f || m_nodes.Count == 0)
            {
                ScanNearbyPieces(playerPos, ambientTemp);
                m_lastScanPosition = playerPos;
            }

            m_fireScanTimer += dt;
            if (m_fireScanTimer >= 2.0f)
            {
                m_fireScanTimer = 0.0f;
                UpdateActiveFires(playerPos);
            }

            m_stepTimer += dt;
            if (m_stepTimer >= 1.0f)
            {
                m_stepTimer = 0.0f;
                if (ModConfig.DebugFreezeStructureTemps != null && ModConfig.DebugFreezeStructureTemps.Value)
                {
                    float forcedTemp = ModConfig.DebugForcedStructureTemp != null ? ModConfig.DebugForcedStructureTemp.Value : 15.0f;
                    ForceAllStructureTemperatures(forcedTemp);
                    m_cycleStepsRemaining = 30;
                    m_isCycleActive = true;
                }
                else if (m_isCycleActive)
                {
                    StepRelaxationIteration(ambientTemp);
                }
            }

            m_zdoSyncTimer += dt;
            if (m_zdoSyncTimer >= 10.0f)
            {
                m_zdoSyncTimer = 0.0f;
                SyncAllNodesToZdo();
            }
        }

        public void SyncAllNodesToZdo()
        {
            if (ZNet.instance == null) return;
            double now = ZNet.instance.GetTimeSeconds();
            long nowBits = BitConverter.DoubleToInt64Bits(now);

            foreach (var kvp in m_nodes)
            {
                PieceThermalNode node = kvp.Value;
                ZDO? zdo = null;

                if (node.PieceRef != null && node.PieceRef.gameObject != null)
                {
                    ZNetView nview = node.PieceRef.GetComponent<ZNetView>();
                    if (nview != null && nview.IsValid())
                    {
                        zdo = nview.GetZDO();
                    }
                }

                if (zdo == null && node.Zdoid != ZDOID.None && ZDOMan.instance != null)
                {
                    zdo = ZDOMan.instance.GetZDO(node.Zdoid);
                }

                if (zdo != null)
                {
                    zdo.Set(s_zdoKeyTemp, node.CurrentTemperature);
                    zdo.Set(s_zdoKeyTime, nowBits);
                }
            }
        }

        public bool HasActiveFireNearPosition(Vector3 position, float radius)
        {
            float rSqr = radius * radius;
            for (int i = 0; i < m_trackedHeatSources.Count; i++)
            {
                var hs = m_trackedHeatSources[i];
                if (hs != null && hs.IsActive && hs.GameObject != null)
                {
                    if ((hs.Position - position).sqrMagnitude <= rSqr)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static void UpdateNodeCoverAndRoof(PieceThermalNode node)
        {
            if (node == null || node.PieceRef == null || node.PieceRef.gameObject == null) return;

            bool isRoof = ThermalMaterialDatabase.IsRoofPiece(node.PieceRef);

            // Roof pieces are inherently roofs themselves
            if (isRoof)
            {
                node.UnderRoof = true;
                // Sample enclosure from slightly below the roof underside into the interior
                Vector3 interiorPos = node.Position - (Vector3.up * 0.40f);
                Cover.GetCoverForPoint(interiorPos, out float roofCoverPct, out _);
                // An installed roof piece sheltering a building has at least 60% cover from the structure beneath
                node.CoverPercentage = Mathf.Max(roofCoverPct, 0.60f);
                return;
            }

            // For structural walls, floors, beams, and columns:
            // Sample cover and upward roof occlusion from node position
            Cover.GetCoverForPoint(node.Position, out float coverPct, out bool underRoof);

            // If upward point test missed due to thin colliders or overhang offsets,
            // cast an upward spherecast to detect any roof piece or upper floor overhead
            if (!underRoof)
            {
                int mask = LayerMask.GetMask("piece", "Default", "static_solid");
                if (Physics.SphereCast(node.Position, 0.35f, Vector3.up, out RaycastHit roofHit, 35.0f, mask))
                {
                    Piece hitPiece = roofHit.collider.GetComponentInParent<Piece>();
                    if (hitPiece != null)
                    {
                        string hitName = hitPiece.gameObject.name.ToLowerInvariant();
                        if (hitName.Contains("roof") || hitName.Contains("floor"))
                        {
                            underRoof = true;
                        }
                    }
                }
            }

            node.CoverPercentage = coverPct;
            node.UnderRoof = underRoof;
        }

        public void ScanNearbyPieces(Vector3 center, float ambientTemp)
        {
            // Prune dead piece references
            var deadKeys = new List<int>();
            foreach (var kvp in m_nodes)
            {
                if (kvp.Value.PieceRef == null || kvp.Value.PieceRef.gameObject == null)
                {
                    deadKeys.Add(kvp.Key);
                }
            }
            for (int i = 0; i < deadKeys.Count; i++)
            {
                m_nodes.Remove(deadKeys[i]);
            }

            // Index active fires first so heat sources are known during piece initialization
            UpdateActiveFires(center);

            m_tempPieces.Clear();
            Piece.GetAllPiecesInRadius(center, 64.0f, m_tempPieces);

            for (int i = 0; i < m_tempPieces.Count; i++)
            {
                Piece piece = m_tempPieces[i];
                if (piece != null && piece.gameObject != null)
                {
                    var node = RegisterPiece(piece, ambientTemp, wakeCycle: false);
                    if (node != null && node.CoverPercentage <= 0.001f && !node.UnderRoof)
                    {
                        UpdateNodeCoverAndRoof(node);
                    }
                }
            }

            WakeCycle(20);
        }

        public PieceThermalNode? RegisterPiece(Piece piece, float ambientTemp, bool wakeCycle = true)
        {
            if (piece == null || piece.gameObject == null) return null;

            int id = piece.gameObject.GetInstanceID();
            if (m_nodes.TryGetValue(id, out var existing))
            {
                return existing;
            }

            if (!IsStructuralPiece(piece))
            {
                return null;
            }

            Bounds bounds = CalculatePieceBounds(piece);
            string pieceName = piece.m_name ?? piece.gameObject.name;
            ClassifyMaterial(piece, out string matType, out float heatCap, out float airLoss, out float conductivity, out float absorption);

            ZNetView nview = piece.GetComponent<ZNetView>();
            ZDOID zdoid = (nview != null && nview.IsValid()) ? nview.GetZDO().m_uid : ZDOID.None;

            float initialTemp = ambientTemp;
            if (ModConfig.DebugFreezeStructureTemps != null && ModConfig.DebugFreezeStructureTemps.Value)
            {
                initialTemp = ModConfig.DebugForcedStructureTemp != null ? ModConfig.DebugForcedStructureTemp.Value : 15.0f;
            }
            else
            {
                ZDO? zdo = null;
                if (nview != null && nview.IsValid())
                {
                    zdo = nview.GetZDO();
                }
                else if (zdoid != ZDOID.None && ZDOMan.instance != null)
                {
                    zdo = ZDOMan.instance.GetZDO(zdoid);
                }

                if (zdo != null)
                {
                    float savedTemp = zdo.GetFloat(s_zdoKeyTemp, float.NaN);
                    if (float.IsNaN(savedTemp))
                    {
                        savedTemp = zdo.GetFloat(s_legacyZdoKeyTemp, float.NaN);
                    }

                    if (!float.IsNaN(savedTemp))
                    {
                        var offlineMode = ModConfig.OfflineThermalBehavior != null
                            ? ModConfig.OfflineThermalBehavior.Value
                            : OfflineThermalMode.Freeze;

                        if (offlineMode == OfflineThermalMode.Freeze)
                        {
                            initialTemp = savedTemp;
                        }
                        else
                        {
                            long savedTimeBits = zdo.GetLong(s_zdoKeyTime, 0L);
                            if (savedTimeBits == 0L)
                            {
                                savedTimeBits = zdo.GetLong(s_legacyZdoKeyTime, 0L);
                            }

                            if (savedTimeBits != 0L && ZNet.instance != null)
                            {
                                double savedTime = BitConverter.Int64BitsToDouble(savedTimeBits);
                                double now = ZNet.instance.GetTimeSeconds();
                                double elapsed = Math.Max(0.0, now - savedTime);

                                if (elapsed < 120.0) // 2-minute grace period for portal hops / quick runs
                                {
                                    initialTemp = savedTemp;
                                }
                                else if (offlineMode == OfflineThermalMode.DecayWhenUnlit)
                                {
                                    bool hasNearbyActiveFire = HasActiveFireNearPosition(bounds.center, 30.0f);
                                    if (hasNearbyActiveFire)
                                    {
                                        initialTemp = savedTemp; // Dwelling was heated while away
                                    }
                                    else
                                    {
                                        float decayRate = airLoss / Mathf.Max(0.1f, heatCap);
                                        float decayFactor = Mathf.Exp(-decayRate * (float)elapsed);
                                        initialTemp = ambientTemp + (savedTemp - ambientTemp) * decayFactor;
                                    }
                                }
                                else // AlwaysDecay
                                {
                                    float decayRate = airLoss / Mathf.Max(0.1f, heatCap);
                                    float decayFactor = Mathf.Exp(-decayRate * (float)elapsed);
                                    initialTemp = ambientTemp + (savedTemp - ambientTemp) * decayFactor;
                                }
                            }
                            else
                            {
                                initialTemp = savedTemp;
                            }
                        }

                        initialTemp = Mathf.Clamp(initialTemp, ambientTemp - 15.0f, ambientTemp + 95.0f);
                    }
                }
            }

            var node = new PieceThermalNode(
                id,
                zdoid,
                piece,
                bounds.center,
                bounds,
                pieceName,
                matType,
                initialTemp,
                heatCap,
                airLoss,
                conductivity,
                absorption
            );

            m_nodes[id] = node;

            UpdateNodeCoverAndRoof(node);

            ConnectNodeToNeighbors(node);

            if (wakeCycle)
            {
                UpdateDirectHeatForNode(node);
                WakeCycle(25);
            }

            return node;
        }

        public void UnregisterPiece(Piece piece)
        {
            if (piece == null || piece.gameObject == null) return;
            int id = piece.gameObject.GetInstanceID();

            if (!m_nodes.TryGetValue(id, out var node)) return;

            ZDO? zdo = null;
            if (node.PieceRef != null && node.PieceRef.gameObject != null)
            {
                ZNetView nview = node.PieceRef.GetComponent<ZNetView>();
                if (nview != null && nview.IsValid())
                {
                    zdo = nview.GetZDO();
                }
            }

            if (zdo == null && node.Zdoid != ZDOID.None && ZDOMan.instance != null)
            {
                zdo = ZDOMan.instance.GetZDO(node.Zdoid);
            }

            if (zdo != null)
            {
                zdo.Set(s_zdoKeyTemp, node.CurrentTemperature);
                if (ZNet.instance != null)
                {
                    zdo.Set(s_zdoKeyTime, BitConverter.DoubleToInt64Bits(ZNet.instance.GetTimeSeconds()));
                }
            }

            for (int i = 0; i < node.Edges.Count; i++)
            {
                node.Edges[i].Target.RemoveEdge(id);
            }

            m_nodes.Remove(id);
            WakeCycle(20);
        }

        private void ConnectNodeToNeighbors(PieceThermalNode node) =>
            ThermalRelaxationSolver.ConnectNodeToNeighbors(node, m_nodes, m_pieceLayerMask);

        private void RebuildAllConnections() =>
            ThermalRelaxationSolver.RebuildAllConnections(m_nodes, m_pieceLayerMask);

        public void UpdateActiveFires(Vector3 center) =>
            HeatRaycaster.UpdateActiveFires(center, m_trackedHeatSources, m_nodes, m_pieceLayerMask, m_raycastLayerMask, m_terrainLayer, WakeCycle);

        private void RecalculateAllDirectHeat() =>
            HeatRaycaster.RecalculateAllDirectHeat(m_trackedHeatSources, m_nodes, m_raycastLayerMask, m_terrainLayer, WakeCycle);

        public void CastHeatSourceRays(ActiveHeatSource source, bool recordOnly = false, List<PieceThermalNode>? impactedOut = null) =>
            HeatRaycaster.CastHeatSourceRays(source, m_nodes, m_raycastLayerMask, m_terrainLayer, recordOnly, impactedOut);

        public void UpdateDirectHeatForNode(PieceThermalNode node) =>
            RecalculateAllDirectHeat();

        public void EqualizeConnectedComponents() =>
            ThermalRelaxationSolver.EqualizeConnectedComponents(m_nodes);

        private void StepRelaxationIteration(float ambientTemp) =>
            ThermalRelaxationSolver.StepRelaxationIteration(m_nodes, ambientTemp, ref m_lastMaxDeltaT, ref m_cycleStepsRemaining, ref m_isCycleActive);

        public float GetPlayerWarmth(
            Player player,
            Vector3 pos,
            float outdoorAmbient,
            float groundTemp,
            float dt,
            out float directFlameWarmth,
            out float structuralWarmth,
            out ThermalGraphTelemetry telemetry)
        {
            directFlameWarmth = 0.0f;
            structuralWarmth = 0.0f;
            telemetry = default;

            if (player == null) return outdoorAmbient;

            Vector3 chestPos = pos + (Vector3.up * 1.0f);
            bool nativeShelter = player.InShelter();
            bool hasDirectRoof = Physics.Raycast(chestPos, Vector3.up, 20.0f, m_pieceLayerMask);
            bool hasRoof = hasDirectRoof || nativeShelter;

            if (!hasRoof)
            {
                m_cachedIsSheltered = false;
                m_cachedStructuralWarmth = 0.0f;
                m_cachedEnclosureRatio = 0.0f;
            }

            m_samplingTimer += dt;
            float interval = ModConfig.ThermalSamplingInterval != null ? ModConfig.ThermalSamplingInterval.Value : 4.0f;
            float distMoved = Vector3.Distance(pos, m_lastSamplePos);

            bool shelterChanged = (nativeShelter != m_lastNativeShelter) || (hasDirectRoof != m_cachedHasDirectRoof);
            m_lastNativeShelter = nativeShelter;
            m_cachedHasDirectRoof = hasDirectRoof;
            bool needScan = m_samplingTimer >= interval || distMoved > 3.0f || shelterChanged || float.IsPositiveInfinity(m_lastSamplePos.x);

            if (needScan)
            {
                m_samplingTimer = 0.0f;
                m_lastSamplePos = pos;
                DwellingAnalyzer.PerformFullThermalScan(
                    player,
                    pos,
                    outdoorAmbient,
                    groundTemp,
                    m_nodes,
                    m_trackedHeatSources,
                    m_pieceLayerMask,
                    m_raycastLayerMask,
                    m_terrainLayer,
                    m_isCycleActive,
                    m_cycleStepsRemaining,
                    m_lastMaxDeltaT,
                    out m_cachedDirectFlame,
                    out m_cachedStructuralWarmth,
                    out m_cachedEnclosureRatio,
                    out m_cachedIsSheltered,
                    out m_cachedTelemetry
                );
                m_cachedPerceivedTemp = outdoorAmbient + m_cachedStructuralWarmth + m_cachedDirectFlame;
            }

            directFlameWarmth = m_cachedDirectFlame;
            structuralWarmth = m_cachedStructuralWarmth;
            telemetry = m_cachedTelemetry;

            return outdoorAmbient + m_cachedStructuralWarmth + m_cachedDirectFlame;
        }

        public bool GetCrosshairPieceInfo(
            Player player,
            out PieceThermalNode? hoveredNode,
            out List<PieceThermalNode> connectedNeighbors,
            out ActiveHeatSource? hoveredHeatSource,
            out List<PieceThermalNode> fireImpactedPieces)
        {
            hoveredNode = null;
            connectedNeighbors = new List<PieceThermalNode>();
            hoveredHeatSource = null;
            fireImpactedPieces = new List<PieceThermalNode>();

            if (player == null || GameCamera.instance == null) return false;

            Transform cam = GameCamera.instance.transform;
            Vector3 origin = cam.position;
            Vector3 forward = cam.forward;

            if (Physics.Raycast(origin, forward, out RaycastHit hit, 20.0f, m_pieceLayerMask))
            {
                Fireplace fp = hit.collider.GetComponentInParent<Fireplace>();
                if (fp != null && fp.gameObject != null)
                {
                    hoveredHeatSource = GetTrackedHeatSource(fp.gameObject.GetInstanceID());
                    if (hoveredHeatSource == null)
                    {
                        hoveredHeatSource = new ActiveHeatSource(fp);
                        m_trackedHeatSources.Add(hoveredHeatSource);
                        CastHeatSourceRays(hoveredHeatSource, recordOnly: false);
                    }
                    fireImpactedPieces = GetHeatSourceImpactedPieces(hoveredHeatSource);
                    return true;
                }

                Smelter sm = hit.collider.GetComponentInParent<Smelter>();
                if (sm != null && sm.gameObject != null && sm.m_windmill == null)
                {
                    hoveredHeatSource = GetTrackedHeatSource(sm.gameObject.GetInstanceID());
                    if (hoveredHeatSource == null)
                    {
                        hoveredHeatSource = new ActiveHeatSource(sm);
                        m_trackedHeatSources.Add(hoveredHeatSource);
                        CastHeatSourceRays(hoveredHeatSource, recordOnly: false);
                    }
                    fireImpactedPieces = GetHeatSourceImpactedPieces(hoveredHeatSource);
                    return true;
                }

                Piece piece = hit.collider.GetComponentInParent<Piece>();
                if (piece != null && piece.gameObject != null)
                {
                    int id = piece.gameObject.GetInstanceID();
                    if (!m_nodes.TryGetValue(id, out hoveredNode) && IsStructuralPiece(piece))
                    {
                        float amb = SurvivalManager.CurrentState.Breakdown.OutdoorAmbient;
                        if (amb == 0.0f) amb = SurvivalManager.CurrentState.AmbientTemperature;
                        hoveredNode = RegisterPiece(piece, amb, wakeCycle: false);
                    }

                    if (hoveredNode != null)
                    {
                        for (int i = 0; i < hoveredNode.Edges.Count; i++)
                        {
                            var neighbor = hoveredNode.Edges[i].Target;
                            if (neighbor.PieceRef != null && neighbor.PieceRef.gameObject != null)
                            {
                                connectedNeighbors.Add(neighbor);
                            }
                        }
                        return true;
                    }
                }
            }

            return false;
        }

        public bool GetCrosshairPieceInfo(
            Player player,
            out PieceThermalNode? hoveredNode,
            out List<PieceThermalNode> connectedNeighbors,
            out Fireplace? hoveredFireplace,
            out List<PieceThermalNode> fireImpactedPieces)
        {
            bool res = GetCrosshairPieceInfo(player, out hoveredNode, out connectedNeighbors, out ActiveHeatSource? hs, out fireImpactedPieces);
            hoveredFireplace = hs?.FireplaceRef;
            return res;
        }

        public DwellingThermalProfile GetDwellingProfile(PieceThermalNode startNode, float outdoorWeather) =>
            DwellingAnalyzer.GetDwellingProfile(startNode, outdoorWeather, m_nodes);

        private ActiveHeatSource? GetTrackedHeatSource(int instanceId)
        {
            for (int i = 0; i < m_trackedHeatSources.Count; i++)
            {
                if (m_trackedHeatSources[i].GameObject != null &&
                    m_trackedHeatSources[i].GameObject.GetInstanceID() == instanceId)
                {
                    return m_trackedHeatSources[i];
                }
            }
            return null;
        }

        private int CountTotalEdges() =>
            ThermalRelaxationSolver.CountTotalEdges(m_nodes);

        public List<PieceThermalNode> GetHeatSourceImpactedPieces(ActiveHeatSource source) =>
            HeatRaycaster.GetHeatSourceImpactedPieces(source, m_nodes, m_raycastLayerMask, m_terrainLayer);

        public List<PieceThermalNode> GetFireplaceImpactedPieces(Fireplace fp) =>
            HeatRaycaster.GetFireplaceImpactedPieces(fp, m_nodes, m_raycastLayerMask, m_terrainLayer);

        public HashSet<PieceThermalNode> ExpandConnectedDwelling(PieceThermalNode startNode, float ambientTemp, float maxDistance = 100.0f) =>
            DwellingAnalyzer.ExpandConnectedDwelling(startNode, ambientTemp, m_nodes, m_pieceLayerMask, (p, a, w) => RegisterPiece(p, a, w), maxDistance);

        public int CalculateDwellingComfort(Player player, bool inShelter, Vector3 position) =>
            DwellingAnalyzer.CalculateDwellingComfort(player, inShelter, position, m_cachedIsSheltered, m_nodes, m_pieceLayerMask, m_lastAmbientTemp, LastComfortInfo, (p, a, w) => RegisterPiece(p, a, w));

        private PieceThermalNode? FindStructuralNodeNearPlayer(Vector3 position) =>
            DwellingAnalyzer.FindStructuralNodeNearPlayer(position, m_nodes, m_pieceLayerMask, m_cachedIsSheltered);

        // Forwarding methods for backward compatibility
        public static bool IsStructuralPiece(Piece piece) => ThermalMaterialDatabase.IsStructuralPiece(piece);
        public static Bounds CalculatePieceBounds(Piece piece) => ThermalMaterialDatabase.CalculatePieceBounds(piece);
        public static float GetMaterialHeatCapacity(string matType) => ThermalMaterialDatabase.GetMaterialHeatCapacity(matType);
        public static float GetMaterialAirLoss(string matType) => ThermalMaterialDatabase.GetMaterialAirLoss(matType);
        public static float GetMaterialConductivity(string matType) => ThermalMaterialDatabase.GetMaterialConductivity(matType);
        public static float GetMaterialAbsorption(string matType) => ThermalMaterialDatabase.GetMaterialAbsorption(matType);
        public static void ClassifyMaterial(Piece piece, out string matType, out float heatCap, out float airLoss, out float conductivity, out float absorption) =>
            ThermalMaterialDatabase.ClassifyMaterial(piece, out matType, out heatCap, out airLoss, out conductivity, out absorption);
        public static float GetFireplacePower(Fireplace fp) => ThermalMaterialDatabase.GetFireplacePower(fp);
        public static float GetSmelterPower(Smelter sm) => ThermalMaterialDatabase.GetSmelterPower(sm);
    }
}
