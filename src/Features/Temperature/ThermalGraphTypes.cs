using System;
using System.Collections.Generic;
using UnityEngine;

namespace VikingClimate.Features.Temperature
{
    /// <summary>
    /// Directed or bidirectional conductive interface between two physically touching/snapped building pieces.
    /// </summary>
    public class ThermalContactEdge
    {
        public PieceThermalNode Source { get; }
        public PieceThermalNode Target { get; }

        public float InterfaceConductivity
        {
            get
            {
                if (Source != null && Target != null)
                {
                    float k1 = Source.MaterialConductivity;
                    float k2 = Target.MaterialConductivity;
                    return (2.0f * k1 * k2) / Mathf.Max(0.001f, k1 + k2);
                }
                return m_fallbackK;
            }
        }

        private readonly float m_fallbackK;

        public ThermalContactEdge(PieceThermalNode source, PieceThermalNode target)
        {
            Source = source;
            Target = target;
            m_fallbackK = 40.0f;
        }

        public ThermalContactEdge(PieceThermalNode target, float interfaceConductivity)
        {
            Source = target;
            Target = target;
            m_fallbackK = interfaceConductivity;
        }
    }

    /// <summary>
    /// Represents a discrete structural node in the physical dwelling thermal graph.
    /// </summary>
    public class PieceThermalNode
    {
        public int InstanceId { get; }
        public ZDOID Zdoid { get; }
        public Piece PieceRef { get; }
        public Vector3 Position { get; set; }
        public Bounds WorldBounds { get; set; }

        public string PieceName { get; }
        public string MaterialType { get; }

        // Live Thermodynamic State
        public float CurrentTemperature { get; set; }
        public float NextTemperature { get; set; }
        public float TempAtStepStart { get; set; }
        public float DeltaEnergy { get; set; }
        public float DirectHeatFlux { get; set; } // Q_i from all line-of-sight fires

        // Phase-Synchronized Conduction Scratch State (Zero-Alloc)
        public float DesiredOutSum { get; set; }
        public float DesiredInSum { get; set; }
        public float MinColdNeighborTemp { get; set; }
        public float MaxHotNeighborTemp { get; set; }
        public float ScaleOut { get; set; } = 1.0f;
        public float ScaleIn { get; set; } = 1.0f;
        public float ActualConductiveInflux { get; set; }
        public float ActualConductiveOutflux { get; set; }

        // Physical Material Parameters (resolves dynamically from live ModConfig via MaterialType)
        public float HeatCapacity => ThermalGraphManager.GetMaterialHeatCapacity(MaterialType);
        public float AirLossRate => ThermalGraphManager.GetMaterialAirLoss(MaterialType);
        public float MaterialConductivity => ThermalGraphManager.GetMaterialConductivity(MaterialType);
        public float DirectAbsorption => ThermalGraphManager.GetMaterialAbsorption(MaterialType);

        // Insulation & Shelter State (from Cover.GetCoverForPoint)
        public float CoverPercentage { get; set; } = 0.0f;
        public bool UnderRoof { get; set; } = false;
        public float InsulationFactor => CoverPercentage * (UnderRoof ? 1.0f : (Configuration.ModConfig.InsulationUnroofedFactor != null ? Configuration.ModConfig.InsulationUnroofedFactor.Value : 0.40f));
        public float EffectiveAirLossRate => AirLossRate * Mathf.Max(0.0f, 1.0f - InsulationFactor);

        // Adjacency Graph Edges
        public List<ThermalContactEdge> Edges { get; } = new List<ThermalContactEdge>(6);

        public PieceThermalNode(
            int instanceId,
            ZDOID zdoid,
            Piece pieceRef,
            Vector3 position,
            Bounds bounds,
            string pieceName,
            string materialType,
            float initialTemperature,
            float heatCapacity = 0f,
            float airLossRate = 0f,
            float materialConductivity = 0f,
            float directAbsorption = 0f)
        {
            InstanceId = instanceId;
            Zdoid = zdoid;
            PieceRef = pieceRef;
            Position = position;
            WorldBounds = bounds;
            PieceName = pieceName;
            MaterialType = materialType;
            CurrentTemperature = initialTemperature;
            NextTemperature = initialTemperature;
        }

        public void AddOrUpdateEdge(PieceThermalNode neighbor, float conductivity = 0.0f)
        {
            for (int i = 0; i < Edges.Count; i++)
            {
                if (Edges[i].Target.InstanceId == neighbor.InstanceId)
                {
                    Edges[i] = new ThermalContactEdge(this, neighbor);
                    return;
                }
            }
            Edges.Add(new ThermalContactEdge(this, neighbor));
        }

        public void RemoveEdge(int neighborInstanceId)
        {
            for (int i = Edges.Count - 1; i >= 0; i--)
            {
                if (Edges[i].Target.InstanceId == neighborInstanceId)
                {
                    Edges.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>
    /// Diagnostic telemetry for the Structural Graph thermal system.
    /// </summary>
    public struct ThermalGraphTelemetry
    {
        public bool IsActive;
        public int TotalNodes;
        public int TotalEdges;
        public bool IsCycleActive;
        public int CycleStepsRemaining;
        public float MaxDeltaT;
        public int ActiveFiresCount;
        public int PieceRaysHit;
        public int GroundRaysHit;
        public int AirRaysHit;
        public float AveragePieceSurfaceTemp;
        public float GroundTemp;
        public float OutdoorAirTemp;
        public float DirectFlameWarmth;
        public float StructuralWarmth;
        public float EnclosureRatio;
        public bool IsSheltered;
    }

    /// <summary>
    /// Aggregated thermal, structural, and energy balance profile of an entire connected dwelling component.
    /// Used by the Lumped Thermal Mass (Instant Conduction) debug mode and whole-building telemetry.
    /// </summary>
    public struct DwellingThermalProfile
    {
        public float UniformTemperature;
        public float TotalHeatCapacity;
        public float TotalFireInflux;
        public float TotalAirLossCoeff;
        public float TotalAirGain;
        public float TotalAirLoss;
        public float NetFlux;
        public float ExpectedRateOfChange;
        public float ExpectedEquilibriumTemp;

        public int PieceCount;
        public int WoodCount;
        public int StoneCount;
        public int CoreWoodCount;
        public int OtherCount;

        public int RoofedCount;
        public float AverageCoverPct;

        public HashSet<PieceThermalNode> AllPieces;
    }
}
