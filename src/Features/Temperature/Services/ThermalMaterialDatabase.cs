using System;
using UnityEngine;
using VikingClimate.Configuration;

namespace VikingClimate.Features.Temperature.Services
{
    /// <summary>
    /// Physical thermodynamic properties for a structural material type.
    /// </summary>
    public readonly struct MaterialThermalProperties
    {
        public float HeatCapacity { get; }
        public float AirLoss { get; }
        public float Conductivity { get; }
        public float Absorption { get; }

        public MaterialThermalProperties(float heatCapacity, float airLoss, float conductivity, float absorption)
        {
            HeatCapacity = heatCapacity;
            AirLoss = airLoss;
            Conductivity = conductivity;
            Absorption = absorption;
        }
    }

    /// <summary>
    /// Authoritative database for material classifications, thermodynamic parameters,
    /// heat source power ratings, and structural piece identification.
    /// </summary>
    public static class ThermalMaterialDatabase
    {
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

        public static MaterialThermalProperties GetProperties(string matType)
        {
            return new MaterialThermalProperties(
                GetMaterialHeatCapacity(matType),
                GetMaterialAirLoss(matType),
                GetMaterialConductivity(matType),
                GetMaterialAbsorption(matType)
            );
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

            var props = GetProperties(matType);
            heatCap = props.HeatCapacity;
            airLoss = props.AirLoss;
            conductivity = props.Conductivity;
            absorption = props.Absorption;
        }

        public static float GetFireplacePower(Fireplace fp)
        {
            string name = fp.gameObject.name.ToLowerInvariant();
            if (name.Contains("hearth")) return ModConfig.FireOutputHearth.Value;
            if (name.Contains("brazier")) return ModConfig.FireOutputBrazier.Value;
            if (name.Contains("bonfire")) return ModConfig.FireOutputBonfire.Value;
            if (name.Contains("groundtorch") || name.Contains("sconce") || name.Contains("torch")) return ModConfig.FireOutputTorch.Value;
            return ModConfig.FireOutputCampfire.Value;
        }

        public static float GetSmelterPower(Smelter sm)
        {
            string name = (sm.m_name ?? sm.gameObject.name).ToLowerInvariant();
            if (name.Contains("kiln")) return ModConfig.FireOutputKiln.Value;
            if (name.Contains("blast")) return ModConfig.FireOutputBlastFurnace.Value;
            return ModConfig.FireOutputSmelter.Value;
        }

        public static bool IsFireplaceLit(Fireplace fp)
        {
            if (fp == null || fp.gameObject == null) return false;
            if (fp.IsBurning()) return true;

            // If transiently blocked by smoke (m_blocked), check if it has fuel and is in burning state
            ZNetView nview = fp.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid())
            {
                ZDO zdo = nview.GetZDO();
                if (zdo != null)
                {
                    bool isStateOn = zdo.GetInt(ZDOVars.s_state, 0) == 1;
                    float fuel = zdo.GetFloat(ZDOVars.s_fuel, 0f);
                    if (isStateOn && (fuel > 0f || fp.m_infiniteFuel))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool IsSmelterActive(Smelter sm)
        {
            if (sm == null || sm.gameObject == null || sm.m_windmill != null) return false;
            if (sm.IsActive()) return true;

            ZNetView nview = sm.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid())
            {
                ZDO zdo = nview.GetZDO();
                if (zdo != null)
                {
                    float fuel = zdo.GetFloat(ZDOVars.s_fuel, 0f);
                    int queued = zdo.GetInt(ZDOVars.s_queued, 0);
                    bool hasFuel = sm.m_maxFuel == 0 || fuel > 0f;
                    bool hasOre = sm.m_maxOre == 0 || queued > 0;
                    return hasFuel && hasOre;
                }
            }
            return false;
        }

        public static bool IsStructuralPiece(Piece piece)
        {
            if (piece == null || piece.gameObject == null) return false;

            string name = piece.gameObject.name.ToLowerInvariant();
            bool isStoneFence = name.Contains("stone") && name.Contains("fence");

            // 1. Rejection: Active heat sources
            if (piece.GetComponent<Fireplace>() != null || piece.GetComponent<Smelter>() != null)
            {
                return false;
            }

            // 2. Component-based Blacklist
            if (piece.GetComponent<CraftingStation>() != null ||
                piece.GetComponent<StationExtension>() != null ||
                piece.GetComponent<Bed>() != null ||
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

            // 3. Name-based Blacklist
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
            if (isStoneFence) return true;

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

            // 5. Category-based Whitelist
            if (piece.m_category == Piece.PieceCategory.BuildingWorkbench ||
                piece.m_category == Piece.PieceCategory.BuildingStonecutter)
            {
                return true;
            }

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

        public static Bounds CalculatePieceBounds(Piece piece)
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

        public static bool IsRoofPiece(Piece piece)
        {
            if (piece == null || piece.gameObject == null) return false;
            string name = piece.gameObject.name.ToLowerInvariant();
            if (name.Contains("roof")) return true;
            if (!string.IsNullOrEmpty(piece.m_name) && piece.m_name.ToLowerInvariant().Contains("roof")) return true;
            return false;
        }
    }
}
