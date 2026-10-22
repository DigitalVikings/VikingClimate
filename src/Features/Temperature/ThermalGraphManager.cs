using System;
using System.Collections.Generic;
using UnityEngine;
using VikingClimate.Configuration;
using VikingClimate.Core;

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
            IsActive = fp.IsBurning();
            Power = ThermalGraphManager.GetFireplacePower(fp);
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
            IsActive = sm.IsActive();
            Power = ThermalGraphManager.GetSmelterPower(sm);
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

        private float m_samplingTimer = 999.0f; // Force scan on initial run
        private Vector3 m_lastSamplePos = Vector3.positiveInfinity;
        private float m_cachedPerceivedTemp = 20.0f;
        private float m_cachedDirectFlame = 0.0f;
        private float m_cachedStructuralWarmth = 0.0f;
        private ThermalGraphTelemetry m_cachedTelemetry = default;
        private bool m_cachedIsSheltered = false;
        private float m_cachedEnclosureRatio = 0.0f;

        public bool IsPlayerSheltered => m_cachedIsSheltered;
        public float EnclosureRatio => m_cachedEnclosureRatio;

        /// <summary>
        /// Evaluates whether the player is genuinely inside their enclosed dwelling/compound,
        /// driven directly by the comprehensive 64-ray Fibonacci sphere scan.
        /// </summary>
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

        private readonly struct ThermalSampleRay
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

        private static readonly ThermalSampleRay[] s_sampleRays = InitializeSampleRays(64);
        private static readonly Vector3[] s_heatSourceRays = InitializeFibonacciSphere(128);

        public bool IsCycleActive => m_isCycleActive;
        public int NodeCount => m_nodes.Count;

        private float m_lastAmbientTemp = 10.0f;

        /// <summary>
        /// Precomputes an optimized 64-ray distribution for physical thermal sampling:
        /// - 52 rays (81.25%) allocated to the upper hemisphere and horizontal sightlines (where walls, roof, windows, and sky exist).
        /// - 12 rays (18.75%) allocated to the floor directly beneath the player (1 straight down, 11 angled floor perimeter).
        /// Solid-angle weights integrate to 1.0 across the full 4π steradians, providing exact spherical radiance math without floor oversampling.
        /// </summary>
        private static ThermalSampleRay[] InitializeSampleRays(int sampleCount = 64)
        {
            var rays = new ThermalSampleRay[sampleCount];
            float phi = Mathf.PI * (3.0f - Mathf.Sqrt(5.0f)); // Golden ratio angle ~2.3999632 rad

            const int upperCount = 52;
            const int lowerCount = 12;

            // Solid angle fractions of the full 4π unit sphere:
            // Upper zone: y in [-0.15, 1.0] -> (1 - (-0.15)) / 2 = 1.15 / 2 = 0.575 (57.5% of sphere)
            // Lower zone: y in [-1.0, -0.15] -> (-0.15 - (-1.0)) / 2 = 0.85 / 2 = 0.425 (42.5% of sphere)
            float upperWeight = 0.575f / upperCount;
            float lowerWeight = 0.425f / lowerCount;

            // 1. Upper & Horizontal Fibonacci Dome (52 rays covering walls, windows, roof slopes, and sky)
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

            // 2. Lower Floor & Ground Distribution (12 rays)
            // Ray 0: straight down beneath player feet
            rays[upperCount] = new ThermalSampleRay(new Vector3(0.0f, -1.0f, 0.0f), lowerWeight, isAtmospheric: false);

            // Rays 1-11: downward angles sampling floor perimeter
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

        /// <summary>
        /// Precomputes an upward-biased 128-direction Fibonacci distribution across the solid angle surrounding heat sources.
        /// Fires naturally radiate and convect upward and outward rather than deep into the ground.
        /// Rays span from zenith (+1.0) down to floor perimeter (-0.40), allocating over 70% of thermal energy
        /// to the upper dome and horizontal living space.
        /// </summary>
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
        }

        /// <summary>
        /// Instantly forces all registered pieces in the structural thermal graph to a target temperature.
        /// Used by debug toggles and sliders to reset and test building thermodynamics.
        /// </summary>
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

        /// <summary>
        /// Wakes up the structural relaxation cycle for a given number of seconds.
        /// If the player continues building, calling this resets and extends the cycle.
        /// </summary>
        public void WakeCycle(int durationSeconds = 25)
        {
            m_isCycleActive = true;
            m_cycleStepsRemaining = Mathf.Max(m_cycleStepsRemaining, durationSeconds);
        }

        /// <summary>
        /// Main update loop called every frame or survival tick.
        /// Runs a single structural relaxation iteration once per second when a cycle is active.
        /// </summary>
        public void Update(float dt, Player player, float ambientTemp)
        {
            if (player == null) return;
            m_lastAmbientTemp = ambientTemp;

            Vector3 playerPos = player.transform.position;

            // 1. Initial scan or periodic re-scan around player when moving
            if (Vector3.Distance(playerPos, m_lastScanPosition) > 12.0f || m_nodes.Count == 0)
            {
                ScanNearbyPieces(playerPos, ambientTemp);
                m_lastScanPosition = playerPos;

                PieceThermalNode? playerNode = FindStructuralNodeNearPlayer(playerPos);
                if (playerNode != null)
                {
                    ExpandConnectedDwelling(playerNode, ambientTemp, 100.0f);
                }
            }

            // 2. Periodic active fireplace scan (every 2 seconds)
            m_fireScanTimer += dt;
            if (m_fireScanTimer >= 2.0f)
            {
                m_fireScanTimer = 0.0f;
                UpdateActiveFires(playerPos);
            }

            // 3. Step structural relaxation cycle at 1.0 Hz (1 iteration per second)
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

            // 4. Periodic ZDO persistence sync (every 10 seconds)
            m_zdoSyncTimer += dt;
            if (m_zdoSyncTimer >= 10.0f)
            {
                m_zdoSyncTimer = 0.0f;
                SyncAllNodesToZdo();
            }
        }

        /// <summary>
        /// Flushes current in-memory piece temperatures into their persistent ZDOs.
        /// </summary>
        public void SyncAllNodesToZdo()
        {
            if (ZNet.instance == null) return;
            double now = ZNet.instance.GetTimeSeconds();
            long nowBits = BitConverter.DoubleToInt64Bits(now);

            foreach (var kvp in m_nodes)
            {
                PieceThermalNode node = kvp.Value;
                if (node.PieceRef == null || node.PieceRef.gameObject == null) continue;

                ZNetView nview = node.PieceRef.GetComponent<ZNetView>();
                if (nview != null && nview.IsValid())
                {
                    ZDO zdo = nview.GetZDO();
                    if (zdo != null)
                    {
                        if (!nview.IsOwner())
                        {
                            nview.ClaimOwnership();
                        }
                        zdo.Set(s_zdoKeyTemp, node.CurrentTemperature);
                        zdo.Set(s_zdoKeyTime, nowBits);
                    }
                }
            }
        }

        /// <summary>
        /// Scans pieces within loaded zones around the player and builds the local structural contact graph.
        /// Uses 64m radius to encompass the full active compound/dwelling on login and movement.
        /// </summary>
        public void ScanNearbyPieces(Vector3 center, float ambientTemp)
        {
            m_tempPieces.Clear();
            Piece.GetAllPiecesInRadius(center, 64.0f, m_tempPieces);

            for (int i = 0; i < m_tempPieces.Count; i++)
            {
                Piece piece = m_tempPieces[i];
                if (piece != null && piece.gameObject != null)
                {
                    RegisterPiece(piece, ambientTemp, wakeCycle: false);
                }
            }

            RebuildAllConnections();
            RecalculateAllDirectHeat();
            WakeCycle(20);
        }

        /// <summary>
        /// Registers a single piece into the thermal graph and links adjacent touching pieces.
        /// Only registers genuine construction pieces (walls, roofs, beams, etc.) using the whitelist/fallback.
        /// </summary>
        public PieceThermalNode? RegisterPiece(Piece piece, float ambientTemp, bool wakeCycle = true)
        {
            if (piece == null || piece.gameObject == null) return null;

            int id = piece.gameObject.GetInstanceID();
            if (m_nodes.TryGetValue(id, out var existing))
            {
                return existing;
            }

            // Gating: Only real construction/structural pieces enter the thermal graph
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
            else if (nview != null && nview.IsValid())
            {
                ZDO zdo = nview.GetZDO();
                float savedTemp = zdo.GetFloat(s_zdoKeyTemp, float.NaN);
                if (float.IsNaN(savedTemp))
                {
                    savedTemp = zdo.GetFloat(s_legacyZdoKeyTemp, float.NaN);
                }
                if (!float.IsNaN(savedTemp))
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

                        // If unloaded for a duration, passively cool towards ambient:
                        // T(t) = T_amb + (T_saved - T_amb) * exp(- (airLoss / heatCap) * elapsed)
                        float decayRate = airLoss / Mathf.Max(0.1f, heatCap);
                        float decayFactor = Mathf.Exp(-decayRate * (float)elapsed);
                        initialTemp = ambientTemp + (savedTemp - ambientTemp) * decayFactor;
                    }
                    else
                    {
                        initialTemp = savedTemp;
                    }

                    // Physical safety clamp
                    initialTemp = Mathf.Clamp(initialTemp, ambientTemp - 15.0f, ambientTemp + 95.0f);
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

            // Compute shelter and insulation factor for this piece
            Cover.GetCoverForPoint(node.Position, out float coverPct, out bool underRoof);
            node.CoverPercentage = coverPct;
            node.UnderRoof = underRoof;

            m_nodes[id] = node;

            // Connect to nearby existing pieces whose bounds touch
            ConnectNodeToNeighbors(node);

            // Compute direct fire influx from any active fireplaces in range
            UpdateDirectHeatForNode(node);

            if (wakeCycle)
            {
                WakeCycle(25);
            }

            return node;
        }

        /// <summary>
        /// Removes a piece from the graph and severs its edges with all connected neighbors.
        /// </summary>
        public void UnregisterPiece(Piece piece)
        {
            if (piece == null || piece.gameObject == null) return;
            int id = piece.gameObject.GetInstanceID();

            if (!m_nodes.TryGetValue(id, out var node)) return;

            // Save temperature to ZDO before unregistering
            if (node.PieceRef != null && node.PieceRef.gameObject != null)
            {
                ZNetView nview = node.PieceRef.GetComponent<ZNetView>();
                if (nview != null && nview.IsValid())
                {
                    ZDO zdo = nview.GetZDO();
                    if (zdo != null)
                    {
                        if (!nview.IsOwner())
                        {
                            nview.ClaimOwnership();
                        }
                        zdo.Set(s_zdoKeyTemp, node.CurrentTemperature);
                        if (ZNet.instance != null)
                        {
                            zdo.Set(s_zdoKeyTime, BitConverter.DoubleToInt64Bits(ZNet.instance.GetTimeSeconds()));
                        }
                    }
                }
            }

            // Disconnect from neighbors
            for (int i = 0; i < node.Edges.Count; i++)
            {
                node.Edges[i].Target.RemoveEdge(id);
            }

            m_nodes.Remove(id);
            WakeCycle(20);
        }

        private static readonly Collider[] s_tempNeighbors = new Collider[64];

        /// <summary>
        /// Evaluates physical contact between two pieces by checking if their expanded AABBs intersect.
        /// Uses PhysX spatial acceleration for rapid O(k) neighbor discovery.
        /// </summary>
        private void ConnectNodeToNeighbors(PieceThermalNode node)
        {
            Bounds expanded = node.WorldBounds;
            expanded.Expand(0.12f); // 6cm contact margin in each direction to catch snapped joints

            int hitCount = Physics.OverlapBoxNonAlloc(expanded.center, expanded.extents, s_tempNeighbors, Quaternion.identity, m_pieceLayerMask);
            for (int i = 0; i < hitCount; i++)
            {
                Collider col = s_tempNeighbors[i];
                if (col == null) continue;

                Piece neighborPiece = col.GetComponentInParent<Piece>();
                if (neighborPiece == null || neighborPiece.gameObject == null) continue;

                int neighborId = neighborPiece.gameObject.GetInstanceID();
                if (neighborId == node.InstanceId) continue;

                if (m_nodes.TryGetValue(neighborId, out var neighbor))
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

        private void RebuildAllConnections()
        {
            foreach (var kvp in m_nodes)
            {
                ConnectNodeToNeighbors(kvp.Value);
            }
        }

        /// <summary>
        /// Scans active burning fireplaces and metallurgical furnaces (smelters, kilns) within thermal reach of the player.
        /// </summary>
        public void UpdateActiveFires(Vector3 center)
        {
            m_trackedHeatSources.Clear();

            // Find all active Fireplace & Smelter instances within 16m
            var colliders = Physics.OverlapSphere(center, 16.0f, m_pieceLayerMask);
            var seen = new HashSet<int>();

            for (int i = 0; i < colliders.Length; i++)
            {
                var col = colliders[i];
                Fireplace fp = col.GetComponentInParent<Fireplace>();
                if (fp != null && fp.IsBurning())
                {
                    int fpId = fp.gameObject.GetInstanceID();
                    if (seen.Add(fpId))
                    {
                        m_trackedHeatSources.Add(new ActiveHeatSource(fp));
                    }
                    continue;
                }

                Smelter sm = col.GetComponentInParent<Smelter>();
                if (sm != null && sm.m_windmill == null && sm.IsActive())
                {
                    int smId = sm.gameObject.GetInstanceID();
                    if (seen.Add(smId))
                    {
                        m_trackedHeatSources.Add(new ActiveHeatSource(sm));
                    }
                }
            }

            RecalculateAllDirectHeat();
        }

        private void RecalculateAllDirectHeat()
        {
            // 1. Reset direct heat flux across all registered nodes
            foreach (var kvp in m_nodes)
            {
                kvp.Value.DirectHeatFlux = 0.0f;
            }

            bool anyFireActive = false;

            // 2. Forward ray dispatch from each active heat source
            for (int i = 0; i < m_trackedHeatSources.Count; i++)
            {
                ActiveHeatSource source = m_trackedHeatSources[i];
                if (source.IsActive && source.GameObject != null)
                {
                    anyFireActive = true;
                    CastHeatSourceRays(source);
                }
            }

            if (anyFireActive)
            {
                WakeCycle(15);
            }
        }

        /// <summary>
        /// Casts 128 discrete heat rays across 4π steradians outward from the heat source.
        /// Each ray carries (Power / 128) Watts of energy.
        /// Only structural pieces in the thermal graph (and solid terrain) absorb heat and block rays.
        /// Non-absorbing pieces (furniture, fences, item stands, cooking stations, etc.) let heat rays pass through unimpeded.
        /// Unabsorbed rays naturally escape through windows, doorways, or open sky.
        /// </summary>
        public void CastHeatSourceRays(ActiveHeatSource source, bool recordOnly = false, List<PieceThermalNode>? impactedOut = null)
        {
            if (source == null || source.GameObject == null || !source.IsActive) return;

            source.ImpactedNodeIds.Clear();
            float rayPower = source.Power / (float)s_heatSourceRays.Length;
            Vector3 firePos = source.Position;
            float maxRange = ModConfig.FireRayRange != null ? ModConfig.FireRayRange.Value : 16.0f;

            for (int r = 0; r < s_heatSourceRays.Length; r++)
            {
                Vector3 dir = s_heatSourceRays[r];
                RaycastHit[] hits = Physics.RaycastAll(firePos, dir, maxRange, m_raycastLayerMask);
                if (hits == null || hits.Length == 0) continue;

                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                for (int h = 0; h < hits.Length; h++)
                {
                    var hit = hits[h];
                    if (hit.collider == null) continue;
                    if (hit.collider.transform.IsChildOf(source.Transform)) continue;

                    Piece hitPiece = hit.collider.GetComponentInParent<Piece>();
                    if (hitPiece != null)
                    {
                        int pieceId = hitPiece.gameObject.GetInstanceID();
                        if (m_nodes.TryGetValue(pieceId, out PieceThermalNode node))
                        {
                            // Piece is registered in the thermal graph: it absorbs heat and blocks this ray
                            if (!recordOnly)
                            {
                                node.DirectHeatFlux += rayPower * node.DirectAbsorption;
                            }
                            if (source.ImpactedNodeIds.Add(pieceId) && impactedOut != null)
                            {
                                impactedOut.Add(node);
                            }
                            break; // Stop at first absorbing thermal piece
                        }

                        // Piece is NOT in the thermal graph (furniture, fence, clutter, cooking spit, item stand):
                        // It does NOT absorb heat, so it DOES NOT block the ray! Let ray pass right through!
                        continue;
                    }

                    // Check if ray struck solid terrain/ground
                    bool isTerrain = (hit.collider.gameObject.layer == m_terrainLayer) ||
                                     (hit.collider.GetComponentInParent<Heightmap>() != null);
                    if (isTerrain)
                    {
                        // Solid earth absorbs heat and occludes line of sight
                        break;
                    }

                    // Any other collider: let ray pass through
                    continue;
                }
            }
        }

        /// <summary>
        /// Recalculates direct heat flux from all active heat sources.
        /// </summary>
        public void UpdateDirectHeatForNode(PieceThermalNode node)
        {
            RecalculateAllDirectHeat();
        }

        private readonly HashSet<int> m_componentVisited = new HashSet<int>();
        private readonly List<PieceThermalNode> m_currentCluster = new List<PieceThermalNode>();
        private readonly Queue<PieceThermalNode> m_clusterQueue = new Queue<PieceThermalNode>();

        /// <summary>
        /// Instantly equalizes temperatures across all physically connected building pieces within each connected dwelling cluster.
        /// Treats each connected component as a unified thermal mass (infinite joint conductivity), strictly conserving thermal energy.
        /// </summary>
        public void EqualizeConnectedComponents()
        {
            m_componentVisited.Clear();

            foreach (var kvp in m_nodes)
            {
                PieceThermalNode start = kvp.Value;
                if (start.PieceRef == null || start.PieceRef.gameObject == null) continue;
                if (m_componentVisited.Contains(start.InstanceId)) continue;

                m_currentCluster.Clear();
                m_clusterQueue.Clear();

                m_clusterQueue.Enqueue(start);
                m_componentVisited.Add(start.InstanceId);

                float totalEnergy = 0.0f;
                float totalCapacity = 0.0f;

                while (m_clusterQueue.Count > 0)
                {
                    PieceThermalNode curr = m_clusterQueue.Dequeue();
                    m_currentCluster.Add(curr);

                    float cap = Mathf.Max(1.0f, curr.HeatCapacity);
                    totalEnergy += cap * curr.CurrentTemperature;
                    totalCapacity += cap;

                    for (int e = 0; e < curr.Edges.Count; e++)
                    {
                        var edge = curr.Edges[e];
                        PieceThermalNode neighbor = edge.Target;
                        if (neighbor.PieceRef != null && neighbor.PieceRef.gameObject != null)
                        {
                            if (m_componentVisited.Add(neighbor.InstanceId))
                            {
                                m_clusterQueue.Enqueue(neighbor);
                            }
                        }
                    }
                }

                if (totalCapacity > 0.001f)
                {
                    float unifiedTemp = totalEnergy / totalCapacity;
                    for (int i = 0; i < m_currentCluster.Count; i++)
                    {
                        var p = m_currentCluster[i];
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
        private void StepRelaxationIteration(float ambientTemp)
        {
            float dt = ModConfig.StructuralRelaxationRate != null ? ModConfig.StructuralRelaxationRate.Value : 1.0f;

            // =========================================================================
            // DEBUG INSTANT CONDUCTION MODE: Lumped Thermal Mass (Infinite Joint Conduction)
            // =========================================================================
            if (ModConfig.DebugInstantConduction != null && ModConfig.DebugInstantConduction.Value)
            {
                // Step 1: Parallel Environmental Exchange (Radiant Fire & Ambient Air)
                foreach (var kvp in m_nodes)
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

                // Step 2: Instantly equalize connected components (Infinite joint conductivity)
                EqualizeConnectedComponents();

                // Step 3: Track max delta for cycle sleep
                float maxDelta = 0.0f;
                foreach (var kvp in m_nodes)
                {
                    var node = kvp.Value;
                    if (node.PieceRef == null || node.PieceRef.gameObject == null) continue;
                    float d = Mathf.Abs(node.CurrentTemperature - node.TempAtStepStart);
                    if (d > maxDelta) maxDelta = d;
                }

                m_lastMaxDeltaT = maxDelta;
                if (m_cycleStepsRemaining > 0) m_cycleStepsRemaining--;
                if (maxDelta < 0.02f && m_cycleStepsRemaining <= 0) m_isCycleActive = false;
                return;
            }

            // =========================================================================
            // PHASE 1: Parallel Environmental Exchange (Radiant Fire & Ambient Air)
            // =========================================================================
            foreach (var kvp in m_nodes)
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

                // Step 1A: Radiant fire energy influx (Watts * seconds = Joules)
                if (node.DirectHeatFlux > 0.001f)
                {
                    node.DeltaEnergy += node.DirectHeatFlux * dt;
                }

                // Step 1B: Air heat exchange (Watts * seconds = Joules)
                // Cold ambient air cools warm pieces; warm ambient air warms cold pieces
                float airFlux = node.EffectiveAirLossRate * (ambientTemp - node.CurrentTemperature);
                node.DeltaEnergy += airFlux * dt;
            }

            // =========================================================================
            // PHASE 2: Accumulate Desired Conduction & Neighbor Extrema (Parallel Demand)
            // =========================================================================
            foreach (var kvp in m_nodes)
            {
                PieceThermalNode nodeA = kvp.Value;
                if (nodeA.PieceRef == null || nodeA.PieceRef.gameObject == null) continue;

                for (int e = 0; e < nodeA.Edges.Count; e++)
                {
                    var edge = nodeA.Edges[e];
                    PieceThermalNode nodeB = edge.Target;
                    if (nodeB.PieceRef == null || nodeB.PieceRef.gameObject == null) continue;

                    // Visit each undirected connection once (A.InstanceId < B.InstanceId)
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

            // =========================================================================
            // PHASE 3: Dual-Capacity Node Scaling Factors (Symmetric Throttle)
            // =========================================================================
            foreach (var kvp in m_nodes)
            {
                PieceThermalNode node = kvp.Value;
                if (node.PieceRef == null || node.PieceRef.gameObject == null) continue;

                float cap = Mathf.Max(1.0f, node.HeatCapacity);

                // Donor Limit: A piece cannot shed more heat than available above its coolest neighbor
                if (node.DesiredOutSum > 0.001f && node.MinColdNeighborTemp < float.MaxValue)
                {
                    float maxShed = cap * Mathf.Max(0.0f, node.CurrentTemperature - node.MinColdNeighborTemp) * 0.45f;
                    if (node.DesiredOutSum > maxShed)
                    {
                        node.ScaleOut = maxShed / node.DesiredOutSum;
                    }
                }

                // Recipient Limit: A piece cannot absorb more heat than would raise it above its warmest donor
                if (node.DesiredInSum > 0.001f && node.MaxHotNeighborTemp > float.MinValue)
                {
                    float maxAbsorb = cap * Mathf.Max(0.0f, node.MaxHotNeighborTemp - node.CurrentTemperature) * 0.45f;
                    if (node.DesiredInSum > maxAbsorb)
                    {
                        node.ScaleIn = maxAbsorb / node.DesiredInSum;
                    }
                }
            }

            // =========================================================================
            // PHASE 4: Symmetrically Scaled Pairwise Conduction Execution (100% Conserved)
            // =========================================================================
            foreach (var kvp in m_nodes)
            {
                PieceThermalNode nodeA = kvp.Value;
                if (nodeA.PieceRef == null || nodeA.PieceRef.gameObject == null) continue;

                for (int e = 0; e < nodeA.Edges.Count; e++)
                {
                    var edge = nodeA.Edges[e];
                    PieceThermalNode nodeB = edge.Target;
                    if (nodeB.PieceRef == null || nodeB.PieceRef.gameObject == null) continue;

                    // Visit each undirected connection once (A.InstanceId < B.InstanceId)
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

                    // Record actual telemetry rate in Watts for live HUD inspection
                    float watts = actualEnergy / Mathf.Max(0.001f, dt);
                    hot.ActualConductiveOutflux += watts;
                    cold.ActualConductiveInflux += watts;
                }
            }

            // =========================================================================
            // PHASE 5: Commit Temperature Updates (dT = dE / C)
            // =========================================================================
            float maxDeltaThisSecond = 0.0f;
            foreach (var kvp in m_nodes)
            {
                PieceThermalNode node = kvp.Value;
                if (node.PieceRef == null || node.PieceRef.gameObject == null) continue;

                float cap = Mathf.Max(1.0f, node.HeatCapacity);
                float deltaT = node.DeltaEnergy / cap;
                float nextTemp = node.CurrentTemperature + deltaT;

                // Physical bounds clamp (prevent absolute freezing below outdoor ambient or molten extremes)
                nextTemp = Mathf.Clamp(nextTemp, ambientTemp - 15.0f, ambientTemp + 95.0f);
                node.CurrentTemperature = nextTemp;

                float totalNodeDelta = Mathf.Abs(nextTemp - node.TempAtStepStart);
                if (totalNodeDelta > maxDeltaThisSecond)
                {
                    maxDeltaThisSecond = totalNodeDelta;
                }
            }

            m_lastMaxDeltaT = maxDeltaThisSecond;

            if (m_cycleStepsRemaining > 0)
            {
                m_cycleStepsRemaining--;
            }

            // Put cycle to sleep if structural temperatures are settled (< 0.02C/s) and timer expired
            if (maxDeltaThisSecond < 0.02f && m_cycleStepsRemaining <= 0)
            {
                m_isCycleActive = false;
            }
        }

        /// <summary>
        /// Calculates the final dwelling comfort temperature felt by the player.
        /// Samples a 64-ray Fibonacci sphere across all 4π steradians at a configurable interval (default 4s),
        /// computing physical radiative heat exchange and authoritative spatial compound enclosure.
        /// </summary>
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

            m_samplingTimer += dt;
            float interval = ModConfig.ThermalSamplingInterval != null ? ModConfig.ThermalSamplingInterval.Value : 4.0f;
            float distMoved = Vector3.Distance(pos, m_lastSamplePos);

            // Re-sample when timer expires, or player moves > 2.5m, or shelter state changed, or on initial run
            bool shelterChanged = (player != null && player.InShelter() != m_cachedIsSheltered);
            bool needScan = m_samplingTimer >= interval || distMoved > 2.5f || shelterChanged || float.IsPositiveInfinity(m_lastSamplePos.x);

            if (needScan)
            {
                m_samplingTimer = 0.0f;
                m_lastSamplePos = pos;
                PerformFullThermalScan(player, pos, outdoorAmbient, groundTemp);
            }

            directFlameWarmth = m_cachedDirectFlame;
            structuralWarmth = m_cachedStructuralWarmth;
            telemetry = m_cachedTelemetry;

            // Dynamically re-anchor perceived temperature to current outdoor ambient between scans
            float finalFeelsLike = outdoorAmbient + m_cachedStructuralWarmth + m_cachedDirectFlame;
            return finalFeelsLike;
        }

        private void PerformFullThermalScan(Player? player, Vector3 pos, float outdoorAmbient, float groundTemp)
        {
            Vector3 chestPos = pos + (Vector3.up * 1.0f);

            // 1. Direct Flame Warmth (1 raycast per active heat source to player chest)
            int visibleFiresInLOS = 0;
            float directFlameWarmth = 0.0f;
            for (int i = 0; i < m_trackedHeatSources.Count; i++)
            {
                ActiveHeatSource source = m_trackedHeatSources[i];
                if (!source.IsActive || source.GameObject == null) continue;

                Vector3 firePos = source.Position;
                float dist = Vector3.Distance(firePos, chestPos);
                if (dist > 8.0f) continue;

                Vector3 dir = (chestPos - firePos).normalized;
                if (!Physics.Raycast(firePos, dir, dist, m_pieceLayerMask))
                {
                    // Clear line of sight from flame to player chest
                    visibleFiresInLOS++;
                    float coeff = ModConfig.DirectFlameWarmthCoeff != null ? ModConfig.DirectFlameWarmthCoeff.Value : 0.008f;
                    float directWarmth = (source.Power * coeff) / Mathf.Max(1.0f, dist * dist);
                    directFlameWarmth += directWarmth;
                }
            }

            // 2. Surrounding Field Radiation & Enclosure Scan (64 rays: 52 upper/horizon, 12 floor/ground)
            // Long ray sightline (25m) provides full spatial containment awareness for great halls & compounds.
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

                if (Physics.Raycast(chestPos, dir, out RaycastHit hit, maxStructureSightline, m_raycastLayerMask))
                {
                    Piece hitPiece = hit.collider.GetComponentInParent<Piece>();
                    if (hitPiece != null && hitPiece.gameObject != null)
                    {
                        int id = hitPiece.gameObject.GetInstanceID();
                        if (m_nodes.TryGetValue(id, out var node))
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

                    // Check if it hit terrain / raw earth
                    bool isTerrain = (hit.collider.gameObject.layer == m_terrainLayer) || 
                                     (hit.collider.GetComponentInParent<Heightmap>() != null);
                    if (isTerrain)
                    {
                        groundRaysHit++;
                        rayTemp = groundTemp;
                    }
                    else
                    {
                        // Unmanaged static clutter (rock, boulder)
                        groundRaysHit++;
                        rayTemp = groundTemp;
                    }
                }
                else
                {
                    // Ray escaped out into open air / window / sky
                    airRaysHit++;
                    rayTemp = outdoorAmbient;
                    if (ray.IsAtmospheric) atmosphericAirEscaped++;
                }

                totalWeightedRayTemp += rayTemp * ray.Weight;
            }

            // Solid angle integral: weighted radiance sum across all 64 rays (weights sum to 1.0)
            float integratedFieldTemp = totalWeightedRayTemp;

            // Enclosure Calculation:
            // Measures what percentage of atmospheric sightlines (52 upper & horizontal rays) are shielded from open atmosphere.
            float enclosureRatio = atmosphericRayCount > 0 ? (1.0f - ((float)atmosphericAirEscaped / atmosphericRayCount)) : 0.0f;
            m_cachedEnclosureRatio = enclosureRatio;

            float threshold = ModConfig.EnclosureShelterThreshold != null ? ModConfig.EnclosureShelterThreshold.Value : 0.80f;

            // Direct vertical roof test: require structural cover overhead or native shelter
            bool hasDirectRoof = Physics.Raycast(chestPos, Vector3.up, out RaycastHit roofHit, 20.0f, m_pieceLayerMask);
            bool hasRoof = hasDirectRoof || (player != null && player.InShelter());

            // Inside compound requires:
            // 1. Enclosure ratio >= threshold (default 80% of upper & horizontal sightlines shielded)
            // 2. Minimum structural pieces present (pieceRaysHit >= 12)
            // 3. Structural roof overhead (either steep ray hit or native shelter)
            m_cachedIsSheltered = (enclosureRatio >= threshold) && (pieceRaysHit >= 12) && hasRoof;

            // Structural warmth is the net difference between the integrated field and outdoor air
            float structuralWarmth = integratedFieldTemp - outdoorAmbient;
            float maxFlameWarmth = ModConfig.DirectFlameWarmthMax != null ? ModConfig.DirectFlameWarmthMax.Value : 18.0f;
            directFlameWarmth = Mathf.Clamp(directFlameWarmth, 0.0f, maxFlameWarmth);

            float avgPieceTemp = pieceRaysHit > 0 ? (pieceSurfaceTempSum / pieceRaysHit) : outdoorAmbient;

            m_cachedDirectFlame = directFlameWarmth;
            m_cachedStructuralWarmth = structuralWarmth;
            m_cachedPerceivedTemp = integratedFieldTemp + directFlameWarmth;

            // Populate diagnostic telemetry
            m_cachedTelemetry = new ThermalGraphTelemetry
            {
                IsActive = true,
                TotalNodes = m_nodes.Count,
                TotalEdges = CountTotalEdges(),
                IsCycleActive = m_isCycleActive,
                CycleStepsRemaining = m_cycleStepsRemaining,
                MaxDeltaT = m_lastMaxDeltaT,
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
                IsSheltered = m_cachedIsSheltered
            };
        }

        /// <summary>
        /// Finds the piece currently in the player's crosshair and returns its node and connected neighbors,
        /// OR detects if a heat source (Fireplace, Smelter, Charcoal Kiln, Blast Furnace) is targeted and returns all directly impacted pieces.
        /// </summary>
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
                // 1. Check if hit object is a Fireplace heat source
                Fireplace fp = hit.collider.GetComponentInParent<Fireplace>();
                if (fp != null && fp.gameObject != null)
                {
                    hoveredHeatSource = GetTrackedHeatSource(fp.gameObject.GetInstanceID()) ?? new ActiveHeatSource(fp);
                    fireImpactedPieces = GetHeatSourceImpactedPieces(hoveredHeatSource);
                    return true;
                }

                // 2. Check if hit object is a Smelter/Kiln heat source (excluding windmills)
                Smelter sm = hit.collider.GetComponentInParent<Smelter>();
                if (sm != null && sm.gameObject != null && sm.m_windmill == null)
                {
                    hoveredHeatSource = GetTrackedHeatSource(sm.gameObject.GetInstanceID()) ?? new ActiveHeatSource(sm);
                    fireImpactedPieces = GetHeatSourceImpactedPieces(hoveredHeatSource);
                    return true;
                }

                // 3. Otherwise, check if hit object is a structural building piece
                Piece piece = hit.collider.GetComponentInParent<Piece>();
                if (piece != null && piece.gameObject != null)
                {
                    int id = piece.gameObject.GetInstanceID();
                    if (!m_nodes.TryGetValue(id, out hoveredNode) && IsStructuralPiece(piece))
                    {
                        // On-demand register if piece exists and is structural (e.g. freshly placed or newly streamed in)
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

        /// <summary>
        /// Aggregates the full thermal, physical, and energy balance profile of the connected dwelling cluster containing startNode.
        /// </summary>
        public DwellingThermalProfile GetDwellingProfile(PieceThermalNode startNode, float outdoorWeather)
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

                profile.PieceCount++;
                if (curr.MaterialType == "Wood") profile.WoodCount++;
                else if (curr.MaterialType == "Stone") profile.StoneCount++;
                else if (curr.MaterialType == "CoreWood") profile.CoreWoodCount++;
                else profile.OtherCount++;

                if (curr.UnderRoof) profile.RoofedCount++;
                totalCoverPct += curr.CoverPercentage;

                float cap = Mathf.Max(1.0f, curr.HeatCapacity);
                totalCapacity += cap;
                totalTemp += curr.CurrentTemperature;

                totalFire += curr.DirectHeatFlux;
                totalAirCoeff += curr.EffectiveAirLossRate;

                for (int e = 0; e < curr.Edges.Count; e++)
                {
                    var edge = curr.Edges[e];
                    PieceThermalNode neighbor = edge.Target;
                    if (neighbor.PieceRef != null && neighbor.PieceRef.gameObject != null)
                    {
                        if (profile.AllPieces.Add(neighbor))
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            profile.UniformTemperature = profile.PieceCount > 0 ? (totalTemp / profile.PieceCount) : startNode.CurrentTemperature;
            profile.TotalHeatCapacity = totalCapacity;
            profile.TotalFireInflux = totalFire;
            profile.TotalAirLossCoeff = totalAirCoeff;

            float airTempDiff = outdoorWeather - profile.UniformTemperature;
            float airFlux = totalAirCoeff * airTempDiff;
            profile.TotalAirGain = airFlux > 0f ? airFlux : 0f;
            profile.TotalAirLoss = airFlux < 0f ? -airFlux : 0f;

            profile.NetFlux = profile.TotalFireInflux + airFlux;
            profile.ExpectedRateOfChange = Mathf.Abs(profile.NetFlux) / Mathf.Max(1.0f, profile.TotalHeatCapacity);

            if (profile.TotalAirLossCoeff > 0.001f)
            {
                profile.ExpectedEquilibriumTemp = outdoorWeather + (profile.TotalFireInflux / profile.TotalAirLossCoeff);
            }
            else
            {
                profile.ExpectedEquilibriumTemp = profile.UniformTemperature;
            }

            profile.AverageCoverPct = profile.PieceCount > 0 ? (totalCoverPct / profile.PieceCount) : 0f;

            return profile;
        }

        private ActiveHeatSource? GetTrackedHeatSource(int instanceId)
        {
            for (int i = 0; i < m_trackedHeatSources.Count; i++)
            {
                if (m_trackedHeatSources[i].GameObject != null && m_trackedHeatSources[i].GameObject.GetInstanceID() == instanceId)
                {
                    return m_trackedHeatSources[i];
                }
            }
            return null;
        }

        private int CountTotalEdges()
        {
            int total = 0;
            foreach (var kvp in m_nodes)
            {
                total += kvp.Value.Edges.Count;
            }
            return total / 2; // Undirected edges
        }

        /// <summary>
        /// Whitelist filter to ensure real construction elements
        /// (walls, roofs, floors, beams, pillars, stairs, arches, gates/doors)
        /// enter the structural thermal graph.
        /// Excludes heat sources (which emit outward radiant flux rather than conduct),
        /// perimeter fences/stakewalls/palisades/hedges, furniture, rugs, beds, chairs, tables, item stands, banners, archery targets,
        /// item stacks/piles, and utility crafting stations.
        /// </summary>
        public static bool IsStructuralPiece(Piece piece)
        {
            if (piece == null || piece.gameObject == null) return false;

            // 1. Heat sources emit heat via raycasting; exclude them from structural graph conduction
            if (piece.GetComponent<Fireplace>() != null ||
                (piece.GetComponent<Smelter>() != null && piece.GetComponent<Smelter>().m_windmill == null))
            {
                return false;
            }

            string name = (piece.m_name ?? piece.gameObject.name).ToLowerInvariant();
            bool isStoneFence = (name.Contains("stone") && name.Contains("fence")) || name.Contains("stonefence");

            // 2. Reject non-structural clutter & utility components
            if (piece.GetComponent<Bed>() != null ||
                piece.GetComponent<Chair>() != null ||
                piece.GetComponent<Container>() != null ||
                piece.GetComponent<Sign>() != null ||
                piece.GetComponent<ItemStand>() != null ||
                piece.GetComponent<Plant>() != null ||
                piece.GetComponent<CookingStation>() != null ||
                piece.GetComponent<Fermenter>() != null ||
                piece.GetComponent<Ship>() != null ||
                piece.GetComponent<TeleportWorld>() != null)
            {
                return false;
            }

            // Reject Furniture category
            if (piece.m_category == Piece.PieceCategory.Furniture)
            {
                return false;
            }

            // 3. Name-based Blacklist (wooden fences, stakewalls, furniture, clutter, decorative, portals, and stackable items)
            if (name.Contains("portal") || name.Contains("teleport") ||
                (name.Contains("fence") && !isStoneFence) || name.Contains("stakewall") || name.Contains("palisade") ||
                name.Contains("sharpstakes") || name.Contains("hedge") ||
                name.Contains("rug") || name.Contains("carpet") ||
                name.Contains("bed") ||
                name.Contains("chair") || name.Contains("stool") || name.Contains("bench") || name.Contains("throne") ||
                name.Contains("table") ||
                name.Contains("itemstand") || name.Contains("banner") || name.Contains("sign") ||
                name.Contains("target") ||
                name.Contains("stack") || name.Contains("pile") ||
                name.Contains("chest") || name.Contains("barrel") ||
                name.Contains("torch") || name.Contains("sconce") || name.Contains("candle") || name.Contains("lantern") ||
                name.Contains("cart") || name.Contains("trophy") ||
                name.Contains("bath") || name.Contains("beehive") ||
                name.Contains("trap"))
            {
                return false;
            }

            // 4. Name-based Structural Whitelist
            if (isStoneFence)
            {
                return true;
            }

            // Includes roofs (thatch, wood, darkwood, grausten, stave, scale),
            // walls, floors, beams, pillars, stairs, arches, and gates.
            if (name.Contains("roof") ||
                name.Contains("wall") ||
                name.Contains("floor") ||
                name.Contains("beam") ||
                name.Contains("pillar") || name.Contains("pole") || name.Contains("post") || name.Contains("column") ||
                name.Contains("stair") || name.Contains("ladder") || name.Contains("step") ||
                name.Contains("arch") ||
                name.Contains("door") || name.Contains("gate") ||
                name.Contains("shingle") || name.Contains("thatch") || name.Contains("slate") ||
                name.Contains("scale_") || name.Contains("stave_") ||
                name.Contains("stone_") || name.Contains("grausten_") || name.Contains("marble_") || name.Contains("dvergr_"))
            {
                return true;
            }

            // 5. Category-based Whitelist (Building tabs)
            if (piece.m_category == Piece.PieceCategory.BuildingWorkbench ||
                piece.m_category == Piece.PieceCategory.BuildingStonecutter)
            {
                return true;
            }

            // Expanded / custom hammer tab string check
            string catName = piece.m_category.ToString().ToLowerInvariant();
            if (catName.Contains("build") ||
                catName.Contains("floor") ||
                catName.Contains("wall") ||
                catName.Contains("roof") ||
                catName.Contains("arch"))
            {
                return true;
            }

            // 6. Fallback Heuristic: WearNTear structural support check
            WearNTear wnt = piece.GetComponent<WearNTear>();
            if (wnt != null && wnt.m_supports)
            {
                var mat = wnt.m_materialType;
                if (mat == WearNTear.MaterialType.Wood ||
                    mat == WearNTear.MaterialType.Stone ||
                    mat == WearNTear.MaterialType.Iron ||
                    mat == WearNTear.MaterialType.HardWood ||
                    mat == WearNTear.MaterialType.Marble ||
                    mat == WearNTear.MaterialType.Ashstone ||
                    mat == WearNTear.MaterialType.Timberwood)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds all structural pieces in direct line-of-sight that receive radiant heat flux from a heat source.
        /// </summary>
        public List<PieceThermalNode> GetHeatSourceImpactedPieces(ActiveHeatSource source)
        {
            var impacted = new List<PieceThermalNode>();
            if (source == null || source.GameObject == null || !source.IsActive) return impacted;

            if (source.ImpactedNodeIds.Count > 0)
            {
                foreach (int id in source.ImpactedNodeIds)
                {
                    if (m_nodes.TryGetValue(id, out var node))
                    {
                        impacted.Add(node);
                    }
                }
                return impacted;
            }

            // Fallback: cast rays to populate impacted set if inspected off-cycle
            CastHeatSourceRays(source, recordOnly: true, impactedOut: impacted);
            return impacted;
        }

        public List<PieceThermalNode> GetFireplaceImpactedPieces(Fireplace fp)
        {
            return GetHeatSourceImpactedPieces(new ActiveHeatSource(fp));
        }

        private static Bounds CalculatePieceBounds(Piece piece)
        {
            var colliders = piece.GetComponentsInChildren<Collider>();
            if (colliders.Length == 0)
            {
                return new Bounds(piece.transform.position, Vector3.one);
            }

            Bounds b = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
            {
                if (!colliders[i].isTrigger)
                {
                    b.Encapsulate(colliders[i].bounds);
                }
            }
            return b;
        }

        public static float GetMaterialHeatCapacity(string matType)
        {
            switch (matType)
            {
                case "Stone": return ModConfig.MatStoneHeatCapacity != null ? ModConfig.MatStoneHeatCapacity.Value : 800.0f;
                case "Metal": return ModConfig.MatMetalHeatCapacity != null ? ModConfig.MatMetalHeatCapacity.Value : 500.0f;
                case "CoreWood": return ModConfig.MatCoreWoodHeatCapacity != null ? ModConfig.MatCoreWoodHeatCapacity.Value : 400.0f;
                default: return ModConfig.MatWoodHeatCapacity != null ? ModConfig.MatWoodHeatCapacity.Value : 200.0f;
            }
        }

        public static float GetMaterialAirLoss(string matType)
        {
            switch (matType)
            {
                case "Stone": return ModConfig.MatStoneAirLoss != null ? ModConfig.MatStoneAirLoss.Value : 1.0f;
                case "Metal": return ModConfig.MatMetalAirLoss != null ? ModConfig.MatMetalAirLoss.Value : 1.0f;
                case "CoreWood": return ModConfig.MatCoreWoodAirLoss != null ? ModConfig.MatCoreWoodAirLoss.Value : 2.0f;
                default: return ModConfig.MatWoodAirLoss != null ? ModConfig.MatWoodAirLoss.Value : 2.0f;
            }
        }

        public static float GetMaterialConductivity(string matType)
        {
            switch (matType)
            {
                case "Stone": return ModConfig.MatStoneConductivity != null ? ModConfig.MatStoneConductivity.Value : 75.0f;
                case "Metal": return ModConfig.MatMetalConductivity != null ? ModConfig.MatMetalConductivity.Value : 120.0f;
                case "CoreWood": return ModConfig.MatCoreWoodConductivity != null ? ModConfig.MatCoreWoodConductivity.Value : 55.0f;
                default: return ModConfig.MatWoodConductivity != null ? ModConfig.MatWoodConductivity.Value : 40.0f;
            }
        }

        public static float GetMaterialAbsorption(string matType)
        {
            switch (matType)
            {
                case "Stone": return ModConfig.MatStoneAbsorption != null ? ModConfig.MatStoneAbsorption.Value : 1.0f;
                case "Metal": return ModConfig.MatMetalAbsorption != null ? ModConfig.MatMetalAbsorption.Value : 1.0f;
                case "CoreWood": return ModConfig.MatCoreWoodAbsorption != null ? ModConfig.MatCoreWoodAbsorption.Value : 1.0f;
                default: return ModConfig.MatWoodAbsorption != null ? ModConfig.MatWoodAbsorption.Value : 1.0f;
            }
        }

        public static void ClassifyMaterial(
            Piece piece,
            out string matType,
            out float heatCap,
            out float airLoss,
            out float conductivity,
            out float absorption)
        {
            string name = (piece.m_name ?? piece.gameObject.name).ToLowerInvariant();

            if (name.Contains("stone") || name.Contains("hearth") || name.Contains("rock") ||
                name.Contains("ashland") || name.Contains("grausten") || name.Contains("marble") ||
                (name.Contains("stone") && name.Contains("fence")))
            {
                matType = "Stone";
            }
            else if (name.Contains("iron") || name.Contains("metal") || name.Contains("cage") || name.Contains("darkmetal"))
            {
                matType = "Metal";
            }
            else if (name.Contains("corewood") || name.Contains("log") || name.Contains("roundlog"))
            {
                matType = "CoreWood";
            }
            else
            {
                matType = "Wood";
            }

            heatCap = GetMaterialHeatCapacity(matType);
            airLoss = GetMaterialAirLoss(matType);
            conductivity = GetMaterialConductivity(matType);
            absorption = GetMaterialAbsorption(matType);
        }

        public static float GetFireplacePower(Fireplace fp)
        {
            string name = fp.gameObject.name.ToLowerInvariant();
            if (name.Contains("hearth")) return ModConfig.FireOutputHearth.Value;
            if (name.Contains("brazier")) return ModConfig.FireOutputBrazier.Value;
            if (name.Contains("bonfire")) return ModConfig.FireOutputBonfire.Value;
            if (name.Contains("groundtorch") || name.Contains("sconce") || name.Contains("torch")) return ModConfig.FireOutputTorch.Value;
            return ModConfig.FireOutputCampfire.Value; // Standard campfire & other fires
        }

        public static float GetSmelterPower(Smelter sm)
        {
            string name = (sm.m_name ?? sm.gameObject.name).ToLowerInvariant();
            if (name.Contains("kiln")) return ModConfig.FireOutputKiln.Value;
            if (name.Contains("blast")) return ModConfig.FireOutputBlastFurnace.Value;
            return ModConfig.FireOutputSmelter.Value;
        }

        /// <summary>
        /// Expands the connected structural dwelling graph outward from the starting node along physical joints.
        /// Discovers and registers touching construction pieces in the world up to a strict maximum distance limit (default 100m).
        /// Halts at open air boundaries or distance threshold, preventing runaway traversals on continent-spanning structures.
        /// </summary>
        public HashSet<PieceThermalNode> ExpandConnectedDwelling(PieceThermalNode startNode, float ambientTemp, float maxDistance = 100.0f)
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

                // 1. Traverse existing edges
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

                // 2. Discover physically touching pieces via PhysX spatial query
                Bounds expanded = current.WorldBounds;
                expanded.Expand(0.12f); // 6cm contact margin for snapped joints

                int hitCount = Physics.OverlapBoxNonAlloc(expanded.center, expanded.extents, s_tempNeighbors, Quaternion.identity, m_pieceLayerMask);
                for (int i = 0; i < hitCount; i++)
                {
                    Collider col = s_tempNeighbors[i];
                    if (col == null) continue;

                    Piece neighborPiece = col.GetComponentInParent<Piece>();
                    if (neighborPiece == null || neighborPiece.gameObject == null) continue;

                    int neighborId = neighborPiece.gameObject.GetInstanceID();
                    if (neighborId == current.InstanceId) continue;

                    if ((neighborPiece.transform.position - originPos).sqrMagnitude > maxDistSqr)
                    {
                        continue;
                    }

                    if (!IsStructuralPiece(neighborPiece)) continue;

                    if (!m_nodes.TryGetValue(neighborId, out var neighborNode))
                    {
                        neighborNode = RegisterPiece(neighborPiece, ambientTemp, wakeCycle: false);
                    }

                    if (neighborNode != null && expanded.Intersects(neighborNode.WorldBounds))
                    {
                        float interfaceK = (2.0f * current.MaterialConductivity * neighborNode.MaterialConductivity)
                                         / (current.MaterialConductivity + neighborNode.MaterialConductivity);

                        current.AddOrUpdateEdge(neighborNode, interfaceK);
                        neighborNode.AddOrUpdateEdge(current, interfaceK);

                        if (connectedNodes.Add(neighborNode))
                        {
                            queue.Enqueue(neighborNode);
                        }
                    }
                }
            }

            return connectedNodes;
        }

        /// <summary>
        /// Calculates the total comfort rating of the connected structural dwelling component containing the player.
        /// Searches all comfort items (furniture, rugs, banners, hearths) anchored within the connected structure,
        /// sorts them, deduplicates according to vanilla ComfortGroup / name rules, and returns the unified dwelling comfort.
        /// Falls back to vanilla 10m radius if the player is outside a tracked structural component.
        /// </summary>
        public int CalculateDwellingComfort(Player player, bool inShelter, Vector3 position)
        {
            bool isInside = player != null && (inShelter || IsInsideCompound(player));
            if (!isInside)
            {
                int fbComfort = FallbackComfort(position, false);
                LastComfortInfo.TotalComfort = fbComfort;
                LastComfortInfo.InShelter = false;
                LastComfortInfo.ContributingItems.Clear();
                LastComfortInfo.RedundantCount = 0;
                return fbComfort;
            }

            // 1. Locate the structural node associated with the player's position
            PieceThermalNode? startNode = FindStructuralNodeNearPlayer(position);
            if (startNode == null)
            {
                // Fallback to local 10m sphere if not in a tracked structural graph
                int fbComfort = FallbackComfort(position, true);
                LastComfortInfo.TotalComfort = fbComfort;
                LastComfortInfo.InShelter = true;
                LastComfortInfo.ContributingItems.Clear();
                LastComfortInfo.RedundantCount = 0;
                return fbComfort;
            }

            // 2. Perform BFS flood-fill along joints to collect all connected pieces in this dwelling up to 100m
            var connectedNodes = ExpandConnectedDwelling(startNode, m_lastAmbientTemp, 100.0f);

            Bounds dwellingBounds = startNode.WorldBounds;
            foreach (var node in connectedNodes)
            {
                dwellingBounds.Encapsulate(node.WorldBounds);
            }

            dwellingBounds.Expand(3.5f); // 1.75m margin to catch wall-mounted banners and perimeter items

            // 3. Scan all active comfort pieces in loaded zones that reside within this dwelling
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

                // Check proximity to at least one structural piece in the connected component
                bool belongsToDwelling = false;
                foreach (var node in connectedNodes)
                {
                    if (Vector3.Distance(cpPos, node.Position) <= 3.0f || node.WorldBounds.SqrDistance(cpPos) <= 6.25f)
                    {
                        belongsToDwelling = true;
                        break;
                    }
                }

                if (belongsToDwelling)
                {
                    dwellingComfortPieces.Add(cp);
                }
            }

            // 4. Sort and deduplicate by vanilla rules (highest per ComfortGroup, unique by name)
            dwellingComfortPieces.Sort(PieceComfortSort);

            int comfort = 1; // Base comfort
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

            // Cache breakdown for Hugin's Compendium UI
            LastComfortInfo.TotalComfort = comfort;
            LastComfortInfo.InShelter = true;
            LastComfortInfo.ContributingItems.Clear();
            LastComfortInfo.ContributingItems.AddRange(contributingItems);
            LastComfortInfo.RedundantCount = redundantCount;

            // Only log to console if verbose logging is explicitly enabled in ModConfig
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

        private PieceThermalNode? FindStructuralNodeNearPlayer(Vector3 position)
        {
            // 1. Raycast down to detect floor beneath player
            Vector3 origin = position + Vector3.up * 0.5f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 2.5f, m_pieceLayerMask))
            {
                Piece p = hit.collider.GetComponentInParent<Piece>();
                if (p != null && m_nodes.TryGetValue(p.gameObject.GetInstanceID(), out var node))
                {
                    return node;
                }
            }

            // 2. Raycast up to detect roof overhead
            if (Physics.Raycast(origin, Vector3.up, out RaycastHit roofHit, 15.0f, m_pieceLayerMask))
            {
                Piece rp = roofHit.collider.GetComponentInParent<Piece>();
                if (rp != null && m_nodes.TryGetValue(rp.gameObject.GetInstanceID(), out var rNode))
                {
                    return rNode;
                }
            }

            // 3. Proximity search within 4.0m ONLY IF the player is inside shelter
            // Prevents players standing outside on grass from grabbing interior walls through the exterior
            if (m_cachedIsSheltered)
            {
                PieceThermalNode? closest = null;
                float closestDistSqr = 16.0f; // 4m squared
                foreach (var kvp in m_nodes)
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
            int comfort = sheltered ? 2 : 1; // Base 1 (+1 if sheltered)
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
