using System;
using System.Collections.Generic;
using UnityEngine;
using VikingClimate.Configuration;
using VikingClimate.Core;
using VikingClimate.Features.Temperature;

namespace VikingClimate.UI
{
    /// <summary>
    /// Interactive HUD inspector that displays live thermodynamic metrics for the piece in the player's crosshair
    /// and highlights the piece (gold) and all its structurally connected neighbors (cyan) in 3D world space.
    /// </summary>
    public class ThermalPieceInspector : MonoBehaviour
    {
        private static ThermalPieceInspector? s_instance;
        public static ThermalPieceInspector? Instance => s_instance;

        private PieceThermalNode? m_activeHovered;
        private DwellingThermalProfile? m_activeDwellingProfile;
        private readonly List<PieceThermalNode> m_activeNeighbors = new List<PieceThermalNode>();
        private ActiveHeatSource? m_activeHeatSource;
        private readonly List<PieceThermalNode> m_activeFireImpacted = new List<PieceThermalNode>();
        private ActiveHeatSource? m_lockedHeatSource;
        private readonly List<PieceThermalNode> m_lockedFireImpacted = new List<PieceThermalNode>();
        private readonly HashSet<GameObject> m_highlightedObjects = new HashSet<GameObject>();

        private int m_emissionPropId;
        private int m_colorPropId;

        private GUIStyle? m_boxStyle;
        private GUIStyle? m_headerStyle;
        private GUIStyle? m_bodyStyle;

        private void Awake()
        {
            s_instance = this;
            m_emissionPropId = Shader.PropertyToID("_EmissionColor");
            m_colorPropId = Shader.PropertyToID("_Color");
        }

        private void OnDestroy()
        {
            ClearHighlights();
        }

        private void Update()
        {
            // Toggle inspection display via hotkey
            if (Input.GetKeyDown(ModConfig.TogglePieceHoverHotkey.Value))
            {
                ModConfig.ShowPieceHoverDebug.Value = !ModConfig.ShowPieceHoverDebug.Value;
                if (!ModConfig.ShowPieceHoverDebug.Value)
                {
                    m_lockedHeatSource = null;
                    m_lockedFireImpacted.Clear();
                    ClearHighlights();
                    m_activeHovered = null;
                    m_activeNeighbors.Clear();
                    m_activeHeatSource = null;
                    m_activeFireImpacted.Clear();
                }
            }

            // Lock / unlock heat source highlights via hotkey
            if (Input.GetKeyDown(ModConfig.LockHeatSourceHotkey.Value))
            {
                if (m_lockedHeatSource != null)
                {
                    m_lockedHeatSource = null;
                    m_lockedFireImpacted.Clear();
                    ClearHighlights();
                }
                else if (m_activeHeatSource != null)
                {
                    m_lockedHeatSource = m_activeHeatSource;
                    m_lockedFireImpacted.Clear();
                    m_lockedFireImpacted.AddRange(m_activeFireImpacted);
                    ApplyHeatSourceHighlights(m_lockedHeatSource, m_lockedFireImpacted);
                }
            }

            // Toggle inspection mode (single piece vs aggregated graph) via hotkey
            if (Input.GetKeyDown(ModConfig.ToggleInspectionModeHotkey.Value))
            {
                ModConfig.InspectAggregatedGraph.Value = !ModConfig.InspectAggregatedGraph.Value;
            }

            if (!ModConfig.ShowPieceHoverDebug.Value || Player.m_localPlayer == null)
            {
                if (m_highlightedObjects.Count > 0)
                {
                    ClearHighlights();
                }
                m_lockedHeatSource = null;
                m_lockedFireImpacted.Clear();
                m_activeHovered = null;
                m_activeNeighbors.Clear();
                m_activeHeatSource = null;
                m_activeFireImpacted.Clear();
                return;
            }

            // Query piece or heat source under player's crosshair
            bool hasTarget = ThermalGraphManager.Instance.GetCrosshairPieceInfo(
                Player.m_localPlayer,
                out var hovered,
                out var neighbors,
                out ActiveHeatSource? hoveredHeatSource,
                out var fireImpacted
            );

            if (hasTarget)
            {
                if (hoveredHeatSource != null)
                {
                    // Targeting a heat source (Fireplace, Smelter, Charcoal Kiln, Blast Furnace)
                    m_activeHovered = null;
                    m_activeNeighbors.Clear();
                    m_activeHeatSource = hoveredHeatSource;
                    m_activeFireImpacted.Clear();
                    m_activeFireImpacted.AddRange(fireImpacted);

                    if (ModConfig.HighlightConnectedPieces.Value)
                    {
                        ApplyHeatSourceHighlights(hoveredHeatSource, fireImpacted);
                    }
                }
                else if (hovered != null && hovered.PieceRef != null && hovered.PieceRef.gameObject != null)
                {
                    // Targeting a structural building piece
                    m_activeHovered = hovered;
                    m_activeNeighbors.Clear();
                    m_activeNeighbors.AddRange(neighbors);
                    m_activeHeatSource = null;
                    m_activeFireImpacted.Clear();

                    if (ModConfig.InspectAggregatedGraph.Value)
                    {
                        float outdoorWeather = SurvivalManager.CurrentState.Breakdown.OutdoorAmbient;
                        if (outdoorWeather == 0.0f) outdoorWeather = SurvivalManager.CurrentState.AmbientTemperature;
                        var profile = ThermalGraphManager.Instance.GetDwellingProfile(hovered, outdoorWeather);
                        m_activeDwellingProfile = profile;
                    }
                    else
                    {
                        m_activeDwellingProfile = null;
                    }

                    if (m_lockedHeatSource != null)
                    {
                        ApplyLockedAndHoverHighlights(m_lockedHeatSource, m_lockedFireImpacted, hovered);
                    }
                    else if (ModConfig.InspectAggregatedGraph.Value)
                    {
                        if (ModConfig.HighlightConnectedPieces.Value && m_activeDwellingProfile.HasValue && m_activeDwellingProfile.Value.AllPieces != null)
                        {
                            ApplyDwellingHighlights(hovered, m_activeDwellingProfile.Value.AllPieces);
                        }
                    }
                    else
                    {
                        if (ModConfig.HighlightConnectedPieces.Value)
                        {
                            ApplyPieceHighlights(hovered, neighbors);
                        }
                    }
                }
                else
                {
                    ResetSelection();
                }
            }
            else
            {
                ResetSelection();
            }
        }

        private void ResetSelection()
        {
            m_activeHovered = null;
            m_activeNeighbors.Clear();
            m_activeHeatSource = null;
            m_activeFireImpacted.Clear();

            if (m_lockedHeatSource != null)
            {
                ApplyHeatSourceHighlights(m_lockedHeatSource, m_lockedFireImpacted);
                return;
            }

            if (m_highlightedObjects.Count > 0)
            {
                ClearHighlights();
            }
            m_activeHovered = null;
            m_activeDwellingProfile = null;
            m_activeNeighbors.Clear();
            m_activeHeatSource = null;
            m_activeFireImpacted.Clear();
        }

        private void ApplyDwellingHighlights(PieceThermalNode target, HashSet<PieceThermalNode> dwellingPieces)
        {
            var currentSet = new HashSet<GameObject>();

            // 1. Highlight target hovered piece in Warm Amber/Gold
            if (target.PieceRef != null && target.PieceRef.gameObject != null)
            {
                GameObject targetGo = target.PieceRef.gameObject;
                currentSet.Add(targetGo);
                SetPieceHighlight(targetGo, new Color(1.0f, 0.65f, 0.15f) * 0.85f);
            }

            // 2. Highlight all other connected pieces in the entire dwelling in Cyan/Teal
            foreach (var pieceNode in dwellingPieces)
            {
                if (pieceNode == target) continue;
                if (pieceNode.PieceRef != null && pieceNode.PieceRef.gameObject != null)
                {
                    GameObject go = pieceNode.PieceRef.gameObject;
                    currentSet.Add(go);
                    SetPieceHighlight(go, new Color(0.20f, 0.80f, 0.90f) * 0.40f);
                }
            }

            SyncHighlightedObjects(currentSet);
        }

        private void ApplyPieceHighlights(PieceThermalNode target, List<PieceThermalNode> neighbors)
        {
            var currentSet = new HashSet<GameObject>();

            // 1. Highlight target hovered piece in Warm Gold/Amber
            if (target.PieceRef != null && target.PieceRef.gameObject != null)
            {
                GameObject targetGo = target.PieceRef.gameObject;
                currentSet.Add(targetGo);
                SetPieceHighlight(targetGo, new Color(1.0f, 0.60f, 0.15f) * 0.75f);
            }

            // 2. Highlight all structurally touching neighbor pieces in Emerald/Cyan
            for (int i = 0; i < neighbors.Count; i++)
            {
                var neighbor = neighbors[i];
                if (neighbor.PieceRef != null && neighbor.PieceRef.gameObject != null)
                {
                    GameObject neighborGo = neighbor.PieceRef.gameObject;
                    currentSet.Add(neighborGo);
                    SetPieceHighlight(neighborGo, new Color(0.20f, 0.85f, 0.65f) * 0.45f);
                }
            }

            SyncHighlightedObjects(currentSet);
        }

        private void ApplyHeatSourceHighlights(ActiveHeatSource heatSource, List<PieceThermalNode> impactedPieces)
        {
            var currentSet = new HashSet<GameObject>();

            // 1. Highlight heat source in bright radiant fiery Orange/Red
            if (heatSource != null && heatSource.GameObject != null)
            {
                GameObject hsGo = heatSource.GameObject;
                currentSet.Add(hsGo);
                SetPieceHighlight(hsGo, new Color(1.0f, 0.35f, 0.05f) * 0.90f);
            }

            // 2. Find max energy deposited on any single piece to normalize the gradient
            float maxEnergy = 0.001f;
            if (heatSource != null)
            {
                for (int i = 0; i < impactedPieces.Count; i++)
                {
                    int id = impactedPieces[i].InstanceId;
                    if (heatSource.ImpactedEnergyPerNode.TryGetValue(id, out float e) && e > maxEnergy)
                    {
                        maxEnergy = e;
                    }
                }
            }

            // 3. Highlight all directly impacted pieces colored by energy received
            for (int i = 0; i < impactedPieces.Count; i++)
            {
                var node = impactedPieces[i];
                if (node.PieceRef != null && node.PieceRef.gameObject != null)
                {
                    GameObject nodeGo = node.PieceRef.gameObject;
                    currentSet.Add(nodeGo);

                    float energy = (heatSource != null && heatSource.ImpactedEnergyPerNode.TryGetValue(node.InstanceId, out float e)) ? e : 0f;
                    float t = Mathf.Clamp01(energy / maxEnergy);

                    // Two-stage gradient:
                    // t = 0.0: Pale soft straw/amber (0.35 intensity)
                    // t = 0.5: Golden yellow (0.65 intensity)
                    // t = 1.0: Bright fiery radiant deep orange-red (0.95 intensity)
                    Color highlightColor;
                    if (t < 0.5f)
                    {
                        float subT = t / 0.5f;
                        highlightColor = Color.Lerp(
                            new Color(0.92f, 0.88f, 0.60f) * 0.35f, 
                            new Color(1.0f, 0.75f, 0.15f) * 0.65f, 
                            subT
                        );
                    }
                    else
                    {
                        float subT = (t - 0.5f) / 0.5f;
                        highlightColor = Color.Lerp(
                            new Color(1.0f, 0.75f, 0.15f) * 0.65f, 
                            new Color(1.0f, 0.20f, 0.02f) * 0.95f, 
                            subT
                        );
                    }

                    SetPieceHighlight(nodeGo, highlightColor);
                }
            }

            SyncHighlightedObjects(currentSet);
        }

        private void ApplyLockedAndHoverHighlights(ActiveHeatSource heatSource, List<PieceThermalNode> impactedPieces, PieceThermalNode hoveredPiece)
        {
            var currentSet = new HashSet<GameObject>();

            // 1. Highlight heat source in bright radiant fiery Orange/Red
            if (heatSource != null && heatSource.GameObject != null)
            {
                GameObject hsGo = heatSource.GameObject;
                currentSet.Add(hsGo);
                SetPieceHighlight(hsGo, new Color(1.0f, 0.35f, 0.05f) * 0.90f);
            }

            // 2. Find max energy deposited on any single piece
            float maxEnergy = 0.001f;
            if (heatSource != null)
            {
                for (int i = 0; i < impactedPieces.Count; i++)
                {
                    int id = impactedPieces[i].InstanceId;
                    if (heatSource.ImpactedEnergyPerNode.TryGetValue(id, out float e) && e > maxEnergy)
                    {
                        maxEnergy = e;
                    }
                }
            }

            // 3. Highlight all directly impacted pieces colored by energy received
            for (int i = 0; i < impactedPieces.Count; i++)
            {
                var node = impactedPieces[i];
                if (node.PieceRef != null && node.PieceRef.gameObject != null)
                {
                    GameObject nodeGo = node.PieceRef.gameObject;
                    currentSet.Add(nodeGo);

                    float energy = (heatSource != null && heatSource.ImpactedEnergyPerNode.TryGetValue(node.InstanceId, out float e)) ? e : 0f;
                    float t = Mathf.Clamp01(energy / maxEnergy);

                    Color highlightColor;
                    if (t < 0.5f)
                    {
                        float subT = t / 0.5f;
                        highlightColor = Color.Lerp(
                            new Color(0.92f, 0.88f, 0.60f) * 0.35f, 
                            new Color(1.0f, 0.75f, 0.15f) * 0.65f, 
                            subT
                        );
                    }
                    else
                    {
                        float subT = (t - 0.5f) / 0.5f;
                        highlightColor = Color.Lerp(
                            new Color(1.0f, 0.75f, 0.15f) * 0.65f, 
                            new Color(1.0f, 0.20f, 0.02f) * 0.95f, 
                            subT
                        );
                    }

                    SetPieceHighlight(nodeGo, highlightColor);
                }
            }

            // 4. Highlight hovered piece with distinct bright cursor highlight
            if (hoveredPiece != null && hoveredPiece.PieceRef != null && hoveredPiece.PieceRef.gameObject != null)
            {
                GameObject hGo = hoveredPiece.PieceRef.gameObject;
                currentSet.Add(hGo);
                SetPieceHighlight(hGo, new Color(1.0f, 0.95f, 0.35f) * 0.90f);
            }

            SyncHighlightedObjects(currentSet);
        }

        private void SyncHighlightedObjects(HashSet<GameObject> currentSet)
        {
            var toRemove = new List<GameObject>();
            foreach (var go in m_highlightedObjects)
            {
                if (!currentSet.Contains(go))
                {
                    ResetPieceHighlight(go);
                    toRemove.Add(go);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                m_highlightedObjects.Remove(toRemove[i]);
            }

            foreach (var go in currentSet)
            {
                m_highlightedObjects.Add(go);
            }
        }

        private void SetPieceHighlight(GameObject go, Color color)
        {
            if (go == null || MaterialMan.instance == null) return;
            try
            {
                MaterialMan.instance.SetValue(go, m_emissionPropId, color, true);
            }
            catch
            {
                // Soft fallback if GameObject lacks standard emission shaders
            }
        }

        private void ResetPieceHighlight(GameObject go)
        {
            if (go == null || MaterialMan.instance == null) return;
            try
            {
                MaterialMan.instance.ResetValue(go, m_emissionPropId);
            }
            catch
            {
                // Ignored on cleanup
            }
        }

        private void ClearHighlights()
        {
            if (MaterialMan.instance == null)
            {
                m_highlightedObjects.Clear();
                return;
            }

            foreach (var go in m_highlightedObjects)
            {
                if (go != null)
                {
                    ResetPieceHighlight(go);
                }
            }
            m_highlightedObjects.Clear();
        }

        private void OnGUI()
        {
            if (!ModConfig.ShowPieceHoverDebug.Value || Player.m_localPlayer == null) return;

            InitStyles();

            // Status indicator in lower-left corner
            string modeNotice = ModConfig.InspectAggregatedGraph.Value ? "Connected Graph" : "Single Piece";
            string toggleKey = ModConfig.ToggleInspectionModeHotkey.Value.ToString();
            string lockNotice = m_lockedHeatSource != null ? $" <color=#81C784>[Locked to {m_lockedHeatSource.DisplayName}]</color>" : "";
            GUI.Label(new Rect(20, Screen.height - 35, 550, 25), $"<color=#80CBC4>[{ModConfig.TogglePieceHoverHotkey.Value}: Thermal Inspection ON ({modeNotice} | [{toggleKey}]: Toggle Mode)]</color>{lockNotice}", m_bodyStyle);

            if (m_activeHeatSource != null && m_activeHeatSource.GameObject != null)
            {
                RenderHeatSourceHUD();
                return;
            }

            if (m_activeHovered == null || m_activeHovered.PieceRef == null) return;

            var node = m_activeHovered;

            // True natural outdoor weather temperature (without fire/shelter warmth)
            float outdoorWeather = SurvivalManager.CurrentState.Breakdown.OutdoorAmbient;
            if (outdoorWeather == 0.0f)
            {
                outdoorWeather = SurvivalManager.CurrentState.AmbientTemperature - SurvivalManager.CurrentState.FireWarmth;
            }

            if (m_activeDwellingProfile != null && ModConfig.InspectAggregatedGraph.Value)
            {
                RenderInstantDwellingHUD(m_activeDwellingProfile.Value, node, outdoorWeather);
                return;
            }
            float delta = node.CurrentTemperature - outdoorWeather;
            string deltaSign = delta >= 0 ? "+" : "";

            string tempColor = node.CurrentTemperature >= 25.0f ? "#FFA726" : (node.CurrentTemperature >= 16.0f ? "#81C784" : "#90CAF9");

            // Localize piece name (e.g. $piece_woodwall -> Wood wall)
            string rawName = node.PieceName;
            if (node.PieceRef != null && !string.IsNullOrEmpty(node.PieceRef.m_name))
            {
                rawName = node.PieceRef.m_name;
            }
            string displayName = (Localization.instance != null) ? Localization.instance.Localize(rawName) : rawName;

            // Calculate precise energy balance (Fire, Joints, Air, Net)
            float directInflux = node.DirectHeatFlux;
            float conductiveInflux = node.ActualConductiveInflux;
            float conductiveOutflux = node.ActualConductiveOutflux;
            float neighborTempSum = 0.0f;
            int validNeighborCount = 0;

            for (int i = 0; i < node.Edges.Count; i++)
            {
                var edge = node.Edges[i];
                if (edge.Target.PieceRef != null && edge.Target.PieceRef.gameObject != null)
                {
                    validNeighborCount++;
                    neighborTempSum += edge.Target.CurrentTemperature;
                }
            }

            // Fallback for un-stepped or newly discovered pieces so HUD is never blank
            if (conductiveInflux == 0.0f && conductiveOutflux == 0.0f)
            {
                for (int i = 0; i < node.Edges.Count; i++)
                {
                    var edge = node.Edges[i];
                    if (edge.Target.PieceRef != null && edge.Target.PieceRef.gameObject != null)
                    {
                        float tempDiff = edge.Target.CurrentTemperature - node.CurrentTemperature;
                        float flow = edge.InterfaceConductivity * tempDiff;
                        if (flow > 0f) conductiveInflux += flow;
                        else conductiveOutflux += -flow;
                    }
                }
            }

            float avgNeighborTemp = validNeighborCount > 0 ? (neighborTempSum / validNeighborCount) : node.CurrentTemperature;

            // Physical bidirectional air heat exchange: warm outdoor air heats cold structures, cold air cools warm structures
            float effectiveAirLossCoeff = node.EffectiveAirLossRate;
            float netAir = effectiveAirLossCoeff * (outdoorWeather - node.CurrentTemperature);
            float netJoints = conductiveInflux - conductiveOutflux;
            float netFlux = directInflux + netJoints + netAir;

            // Physical rate of temperature change (°C / sec)
            float expectedRate = Mathf.Abs(netFlux) / Mathf.Max(1.0f, node.HeatCapacity);

            string netStatus;
            if (Mathf.Abs(netFlux) < 0.25f)
            {
                netStatus = "<color=#81C784>Balanced (Equilibrium)</color>";
            }
            else if (netFlux > 0f)
            {
                netStatus = $"<color=#FFB74D>Warming (+{expectedRate:F2}°C/s)</color>";
            }
            else
            {
                netStatus = $"<color=#90CAF9>Cooling (-{expectedRate:F2}°C/s)</color>";
            }

            ThermalGraphManager.UpdateNodeCoverAndRoof(node);

            string massDesc = node.MaterialType == "Stone" ? "Dense Heat Sink" : (node.MaterialType == "CoreWood" ? "Heavy Timber" : "Wood");
            int insulationPct = Mathf.RoundToInt(node.InsulationFactor * 100f);
            string roofStr = node.UnderRoof ? "Roofed" : "Open Sky";
            string insulationDesc = insulationPct >= 70 ? "<color=#81C784>Enclosed</color>" : (insulationPct >= 35 ? "<color=#FFB74D>Partial</color>" : "<color=#EF5350>Exposed</color>");

            int panelWidth = 560;
            int panelHeight = 180;
            int panelX = Screen.width - panelWidth - 25 + (int)ModConfig.PieceHoverOffsetX.Value;
            int panelY = Screen.height - panelHeight - 35 - (int)ModConfig.PieceHoverOffsetY.Value;

            Rect panelRect = new Rect(panelX, panelY, panelWidth, panelHeight);
            GUI.Box(panelRect, GUIContent.none, m_boxStyle!);

            GUILayout.BeginArea(new Rect(panelX + 14, panelY + 12, panelWidth - 28, panelHeight - 24));

            string fireStr = $"+{directInflux:F1}W";
            string jointStr = netJoints >= 0f ? $"+{netJoints:F1}W" : $"-{Mathf.Abs(netJoints):F1}W";
            string airStr = netAir >= 0f ? $"+{netAir:F1}W" : $"-{Mathf.Abs(netAir):F1}W";
            string netStr = netFlux >= 0f ? $"+{netFlux:F1}W" : $"-{Mathf.Abs(netFlux):F1}W";

            GUILayout.Label($"<b><color=orange>[ THERMAL INSPECTION ]</color></b> <color=yellow><b>{displayName}</b></color> ({node.MaterialType}) <color=#B0BEC5>([{toggleKey}]: Graph Mode)</color>", m_headerStyle);
            GUILayout.Label($"• Temperature: <color={tempColor}><b>{node.CurrentTemperature:F1}°C</b></color> (Outdoor Weather: {outdoorWeather:F1}°C | {deltaSign}{delta:F1}°C)", m_bodyStyle);
            GUILayout.Label($"• Material: Mass Capacity <b>{node.HeatCapacity:F0}</b> ({massDesc}) | Conduction <b>{node.MaterialConductivity:F1}</b> | Air Coeff <b>{effectiveAirLossCoeff:F3}</b> (Base {node.AirLossRate:F2})", m_bodyStyle);
            GUILayout.Label($"• Shelter/Insulation: <b>{insulationPct}%</b> ({roofStr} | {insulationDesc} | Cover {node.CoverPercentage * 100f:F0}%)", m_bodyStyle);
            GUILayout.Label($"• Energy: Fire <b>{fireStr}</b> | Joints <b>{jointStr}</b> | Air <b>{airStr}</b> (Net <b>{netStr}</b>)", m_bodyStyle);
            GUILayout.Label($"• Dynamics: {netStatus} | Neighbors: <b>{validNeighborCount} Connected Pieces</b> (Avg {avgNeighborTemp:F1}°C)", m_bodyStyle);

            GUILayout.EndArea();
        }

        private void RenderInstantDwellingHUD(DwellingThermalProfile profile, PieceThermalNode targetNode, float outdoorWeather)
        {
            float delta = profile.UniformTemperature - outdoorWeather;
            string deltaSign = delta >= 0 ? "+" : "";
            string tempColor = profile.UniformTemperature >= 25.0f ? "#FFA726" : (profile.UniformTemperature >= 16.0f ? "#81C784" : "#90CAF9");

            string rawName = targetNode.PieceName;
            if (targetNode.PieceRef != null && !string.IsNullOrEmpty(targetNode.PieceRef.m_name))
            {
                rawName = targetNode.PieceRef.m_name;
            }
            string displayName = (Localization.instance != null) ? Localization.instance.Localize(rawName) : rawName;

            var matParts = new List<string>();
            if (profile.WoodCount > 0) matParts.Add($"{profile.WoodCount} Wood");
            if (profile.StoneCount > 0) matParts.Add($"{profile.StoneCount} Stone");
            if (profile.CoreWoodCount > 0) matParts.Add($"{profile.CoreWoodCount} CoreWood");
            if (profile.OtherCount > 0) matParts.Add($"{profile.OtherCount} Other");
            string matStr = matParts.Count > 0 ? string.Join(", ", matParts) : "Mixed";

            string netSign = profile.NetFlux >= 0 ? "+" : "";
            string rateSign = profile.NetFlux >= 0 ? "+" : "-";

            string netStatus;
            if (Mathf.Abs(profile.NetFlux) < 0.25f)
            {
                netStatus = "<color=#81C784>Equilibrium</color>";
            }
            else if (profile.NetFlux > 0f)
            {
                netStatus = $"<color=#FFB74D>Warming ({rateSign}{profile.ExpectedRateOfChange:F3}°C/s)</color>";
            }
            else
            {
                netStatus = $"<color=#90CAF9>Cooling ({rateSign}{profile.ExpectedRateOfChange:F3}°C/s)</color>";
            }

            int panelWidth = 640;
            int panelHeight = 210;
            int panelX = Screen.width - panelWidth - 25 + (int)ModConfig.PieceHoverOffsetX.Value;
            int panelY = Screen.height - panelHeight - 35 - (int)ModConfig.PieceHoverOffsetY.Value;

            Rect panelRect = new Rect(panelX, panelY, panelWidth, panelHeight);
            GUI.Box(panelRect, GUIContent.none, m_boxStyle!);

            GUILayout.BeginArea(new Rect(panelX + 14, panelY + 12, panelWidth - 28, panelHeight - 24));

            string toggleKey = ModConfig.ToggleInspectionModeHotkey.Value.ToString();
            string instantTag = (ModConfig.DebugInstantConduction != null && ModConfig.DebugInstantConduction.Value) ? " <color=#FFB74D>[Instant Conduction]</color>" : "";
            GUILayout.Label($"<b><color=#00E5FF>[ CONNECTED GRAPH PROFILE ]</color></b> <color=yellow><b>{displayName}</b></color>{instantTag} <color=#B0BEC5>([{toggleKey}]: Single Piece)</color>", m_headerStyle);
            GUILayout.Label($"• Uniform Temperature: <color={tempColor}><b>{profile.UniformTemperature:F1}°C</b></color> (Outdoor Weather: {outdoorWeather:F1}°C | {deltaSign}{delta:F1}°C)", m_bodyStyle);
            GUILayout.Label($"• Structure: <b>{profile.PieceCount} Connected Pieces</b> ({matStr}) | Thermal Mass: <b>{profile.TotalHeatCapacity:F0} J/°C</b>", m_bodyStyle);
            GUILayout.Label($"• Envelope & Cover: <b>{profile.RoofedCount}/{profile.PieceCount} Roofed</b> (Avg Cover {profile.AverageCoverPct * 100f:F0}%) | Total Air Loss Coeff: <b>{profile.TotalAirLossCoeff:F2} W/°C</b>", m_bodyStyle);
            string airExchangeStr = profile.TotalAirGain > 0f 
                ? $"Gain <b>+{profile.TotalAirGain:F1}W</b> (Air)" 
                : $"Loss <b>-{profile.TotalAirLoss:F1}W</b> (Air)";
            GUILayout.Label($"• Energy Exchange: Influx <b>+{profile.TotalFireInflux:F1}W</b> (Fires) | {airExchangeStr} | Net <b>{netSign}{profile.NetFlux:F1}W</b> ({netStatus})", m_bodyStyle);
            GUILayout.Label($"• Projected Equilibrium: <color=yellow><b>{profile.ExpectedEquilibriumTemp:F1}°C</b></color> (Steady-state target where Influx == Air Loss)", m_bodyStyle);
            GUILayout.Label($"• Calibration: <color=#B0BEC5>Adjust Wood/Stone Air Loss in config to shift equilibrium target up or down</color>", m_bodyStyle);

            GUILayout.EndArea();
        }

        private void RenderHeatSourceHUD()
        {
            if (m_activeHeatSource == null || m_activeHeatSource.GameObject == null) return;

            string displayName = m_activeHeatSource.DisplayName;
            bool isActive = m_activeHeatSource.IsActive;
            float actualPower = isActive ? m_activeHeatSource.Power : 0f;

            string statusColor = isActive ? "#81C784" : "#EF5350";
            string statusText = isActive ? m_activeHeatSource.ActiveStatusText : m_activeHeatSource.InactiveStatusText;

            float piecesW = isActive ? m_activeHeatSource.AbsorbedStructureEnergy : 0f;
            float groundW = isActive ? m_activeHeatSource.AbsorbedGroundEnergy : 0f;
            float airW = isActive ? m_activeHeatSource.EscapedAirEnergy : 0f;

            string lockKey = ModConfig.LockHeatSourceHotkey.Value.ToString();
            bool isLocked = m_lockedHeatSource != null && m_lockedHeatSource.GameObject == m_activeHeatSource.GameObject;
            string lockStatus = isLocked 
                ? $"<color=#81C784>Locked</color> ([{lockKey}] to unlock)" 
                : $"[{lockKey}] to lock";

            int panelWidth = 480;
            int panelHeight = 110;
            int panelX = Screen.width - panelWidth - 25 + (int)ModConfig.PieceHoverOffsetX.Value;
            int panelY = Screen.height - panelHeight - 35 - (int)ModConfig.PieceHoverOffsetY.Value;

            Rect panelRect = new Rect(panelX, panelY, panelWidth, panelHeight);
            GUI.Box(panelRect, GUIContent.none, m_boxStyle!);

            GUILayout.BeginArea(new Rect(panelX + 14, panelY + 12, panelWidth - 28, panelHeight - 24));

            GUILayout.Label($"<b><color=#FF7043>[ HEAT SOURCE INSPECTION ]</color></b> <color=yellow><b>{displayName}</b></color>", m_headerStyle);
            GUILayout.Label($"• State: <color={statusColor}><b>{statusText}</b></color> | Output: <color=#FFA726><b>{actualPower:F0}W</b></color>", m_bodyStyle);
            GUILayout.Label($"• Absorption: <color=#FFD54F><b>{piecesW:F0}W Pieces</b></color> | <color=#90CAF9><b>{airW:F0}W Air</b></color> | <color=#BCAAA4><b>{groundW:F0}W Ground</b></color>", m_bodyStyle);
            GUILayout.Label($"• Display: {lockStatus}", m_bodyStyle);

            GUILayout.EndArea();
        }

        private void InitStyles()
        {
            if (m_boxStyle == null)
            {
                Texture2D bg = new Texture2D(1, 1);
                bg.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.11f, 0.88f));
                bg.Apply();

                m_boxStyle = new GUIStyle(GUI.skin.box);
                m_boxStyle.normal.background = bg;

                m_headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };
                m_headerStyle.normal.textColor = Color.white;

                m_bodyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    richText = true
                };
                m_bodyStyle.normal.textColor = new Color(0.88f, 0.88f, 0.88f);
            }
        }
    }
}
