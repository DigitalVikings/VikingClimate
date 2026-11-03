using System;
using System.Collections.Generic;
using UnityEngine;
using VikingClimate.Configuration;
using VikingClimate.Core;
using VikingClimate.Features.Temperature;

namespace VikingClimate.UI
{
    /// <summary>
    /// In-game debug visualizer that highlights all nearby structural building pieces
    /// based on their evaluated thermal insulation / shelter factor.
    /// Green (#22C55E) = 100% insulated / zero air heat leak.
    /// Yellow/Amber (#F59E0B) = 50% partial shelter / intermediate leak.
    /// Red (#EF4444) = 0% uninsulated / exposed outdoor cooling fin.
    /// </summary>
    public class PieceInsulationVisualizer : MonoBehaviour
    {
        private static PieceInsulationVisualizer? s_instance;
        public static PieceInsulationVisualizer? Instance => s_instance;

        // Color Spectrum (Hex values matched to UI standard)
        private static readonly Color ColorRed = new Color(0.9373f, 0.2667f, 0.2667f);     // #EF4444 - 0% insulation
        private static readonly Color ColorYellow = new Color(0.9608f, 0.6196f, 0.0431f);  // #F59E0B - 50% insulation
        private static readonly Color ColorGreen = new Color(0.1333f, 0.7725f, 0.3686f);   // #22C55E - 100% insulation

        private const float QueryRadius = 25.0f;
        private const float UpdateInterval = 0.5f;

        private int m_emissionPropId;
        private float m_updateTimer = 0.0f;
        private Vector3 m_lastPlayerPos = Vector3.positiveInfinity;

        private readonly HashSet<GameObject> m_highlightedObjects = new HashSet<GameObject>();
        private readonly HashSet<GameObject> m_currentSet = new HashSet<GameObject>();
        private readonly List<GameObject> m_toRemove = new List<GameObject>();
        private readonly List<Piece> m_candidatePieces = new List<Piece>(256);

        private void Awake()
        {
            s_instance = this;
            m_emissionPropId = Shader.PropertyToID("_EmissionColor");

            if (ModConfig.HighlightPieceInsulation != null)
            {
                ModConfig.HighlightPieceInsulation.SettingChanged += OnSettingChanged;
            }
        }

        private void OnDestroy()
        {
            if (ModConfig.HighlightPieceInsulation != null)
            {
                ModConfig.HighlightPieceInsulation.SettingChanged -= OnSettingChanged;
            }

            ClearHighlights();
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        private void OnDisable()
        {
            ClearHighlights();
        }

        private void OnSettingChanged(object sender, EventArgs args)
        {
            if (!ModConfig.HighlightPieceInsulation.Value)
            {
                ClearHighlights();
            }
            else
            {
                m_updateTimer = 999.0f; // Force immediate update on next frame
            }
        }

        private void Update()
        {
            if (ModConfig.HighlightPieceInsulation == null || !ModConfig.HighlightPieceInsulation.Value)
            {
                if (m_highlightedObjects.Count > 0)
                {
                    ClearHighlights();
                }
                return;
            }

            Player player = Player.m_localPlayer;
            if (player == null || player.IsDead())
            {
                if (m_highlightedObjects.Count > 0)
                {
                    ClearHighlights();
                }
                return;
            }

            Vector3 playerPos = player.transform.position;
            m_updateTimer += Time.deltaTime;
            float distMoved = Vector3.Distance(playerPos, m_lastPlayerPos);

            if (m_updateTimer >= UpdateInterval || distMoved > 2.0f)
            {
                m_updateTimer = 0.0f;
                m_lastPlayerPos = playerPos;
                UpdateVisualizer(playerPos);
            }
        }

        private void UpdateVisualizer(Vector3 center)
        {
            m_currentSet.Clear();
            m_candidatePieces.Clear();

            // Query pieces within 25m radius around local player
            Piece.GetAllPiecesInRadius(center, QueryRadius, m_candidatePieces);

            float ambientTemp = SurvivalManager.CurrentState.Breakdown.OutdoorAmbient;
            if (ambientTemp == 0.0f)
            {
                ambientTemp = SurvivalManager.CurrentState.AmbientTemperature;
            }

            for (int i = 0; i < m_candidatePieces.Count; i++)
            {
                Piece piece = m_candidatePieces[i];
                if (piece == null || piece.gameObject == null) continue;
                if (!ThermalGraphManager.IsStructuralPiece(piece)) continue;

                int id = piece.gameObject.GetInstanceID();
                if (!ThermalGraphManager.Instance.TryGetNode(id, out var node) || node == null)
                {
                    node = ThermalGraphManager.Instance.RegisterPiece(piece, ambientTemp, wakeCycle: false);
                }

                if (node == null) continue;

                // Ensure cover and roof metrics are evaluated
                if (node.CoverPercentage <= 0.001f && !node.UnderRoof)
                {
                    ThermalGraphManager.UpdateNodeCoverAndRoof(node);
                }

                float insulationFactor = node.InsulationFactor;
                Color highlightColor = GetInsulationColor(insulationFactor);

                GameObject go = piece.gameObject;
                SetPieceHighlight(go, highlightColor);
                m_currentSet.Add(go);
            }

            // Cleanly restore pieces that exited the radius or were removed
            m_toRemove.Clear();
            foreach (var go in m_highlightedObjects)
            {
                if (go == null)
                {
                    continue;
                }

                if (!m_currentSet.Contains(go))
                {
                    ResetPieceHighlight(go);
                    m_toRemove.Add(go);
                }
            }

            for (int i = 0; i < m_toRemove.Count; i++)
            {
                m_highlightedObjects.Remove(m_toRemove[i]);
            }
            m_highlightedObjects.RemoveWhere(go => go == null);

            foreach (var go in m_currentSet)
            {
                m_highlightedObjects.Add(go);
            }
        }

        public static Color GetInsulationColor(float t)
        {
            t = Mathf.Clamp01(t);

            Color baseColor;
            if (t < 0.5f)
            {
                float subT = t / 0.5f;
                baseColor = Color.Lerp(ColorRed, ColorYellow, subT);
            }
            else
            {
                float subT = (t - 0.5f) / 0.5f;
                baseColor = Color.Lerp(ColorYellow, ColorGreen, subT);
            }

            // Scale by 0.85 intensity for vibrant in-game emission
            return baseColor * 0.85f;
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
            }
        }

        public void ClearHighlights()
        {
            if (MaterialMan.instance != null)
            {
                foreach (var go in m_highlightedObjects)
                {
                    if (go != null)
                    {
                        ResetPieceHighlight(go);
                    }
                }
            }

            m_highlightedObjects.Clear();
            m_currentSet.Clear();
            m_toRemove.Clear();
            m_candidatePieces.Clear();
            m_lastPlayerPos = Vector3.positiveInfinity;
        }
    }
}
