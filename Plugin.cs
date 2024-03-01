using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Muse.Oasis.TerrainGeneration;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using static WandererAnimalAgent;
using System.Xml;

namespace WildBender;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private readonly Harmony harmony = new(MyPluginInfo.PLUGIN_GUID);
    internal static ManualLogSource Log;
    public static ConfigEntry<float> medsSlopeAngleLimit { get; private set; }
    public static ConfigEntry<bool> medsChiselFaster { get; private set; }
    public static ConfigEntry<float> medsChiselRangeMultiplier { get; private set; }
    public static ConfigEntry<int> medsFrogs { get; private set; }
    public static ConfigEntry<int> medsPangolins { get; private set; }
    public static ConfigEntry<int> medsSprites { get; private set; }
    public static ConfigEntry<float> medsManaCostMultiplier { get; private set; }
    public static ConfigEntry<int> medsAddFrogsOnLoad { get; private set; }
    public static ConfigEntry<int> medsAddPangolinsOnLoad { get; private set; }
    public static ConfigEntry<int> medsAddSpritesOnLoad { get; private set; }
    public static ConfigEntry<bool> medsFreeSpringRecipes { get; private set; }
    public static ConfigEntry<float> medsFlowLimit1 { get; private set; }
    public static ConfigEntry<float> medsFlowLimit2 { get; private set; }
    public static ConfigEntry<float> medsFlowLimit3 { get; private set; }
    public static ConfigEntry<float> medsSpringLinkRange { get; private set; }
    public static ConfigEntry<int> medsLinkedWellstoneCap { get; private set; }
    public static ConfigEntry<bool> medsStorageCapacityIncrease { get; private set; }
    public static ConfigEntry<bool> medsStackSize { get; private set; }
    public static ConfigEntry<float> medsAirDashSpeedMultiplier { get; private set; }
    public static ConfigEntry<float> medsClimbSpeedMultiplier { get; private set; }
    public static ConfigEntry<float> medsWalkSpeedMultiplier { get; private set; }

    public static int medsAddedFrogs = 0;
    public static int medsAddedPangolins = 0;
    public static int medsAddedSprites = 0;
    private void Awake()
    {
        // Plugin startup logic
        medsFrogs = Config.Bind(new ConfigDefinition("World Generation", "Frogs"), 10, new ConfigDescription("Multiplier for number of frogs (3*x) spawned on world creation."));
        medsSprites = Config.Bind(new ConfigDefinition("World Generation", "Spirit Darters"), 3, new ConfigDescription("Multiplier for number of spirit darters (18*x) spawned on world creation."));
        medsAddFrogsOnLoad = Config.Bind(new ConfigDefinition("World Editing", "Add Frogs On Load"), 0, new ConfigDescription("Clone this number of frogs when loading an existing save. Note that this copies an existing frog."));
        medsAddPangolinsOnLoad = Config.Bind(new ConfigDefinition("World Editing", "Add Pangolins On Load"), 0, new ConfigDescription("Clone this number of pangolins when loading an existing save. Note that this copies an existing pangolin."));
        medsAddSpritesOnLoad = Config.Bind(new ConfigDefinition("World Editing", "Add Spirit Darters On Load"), 0, new ConfigDescription("Clone this number of spirit darters when loading an existing save. Note that this copies an existing spirit darter."));
        medsFlowLimit1 = Config.Bind(new ConfigDefinition("World Editing", "Flow Limit 1"), 0.1f, new ConfigDescription("Low water flow multiplier for wellstones."));
        medsFlowLimit2 = Config.Bind(new ConfigDefinition("World Editing", "Flow Limit 2"), 0.5f, new ConfigDescription("Medium water flow multiplier for wellstones."));
        medsFlowLimit3 = Config.Bind(new ConfigDefinition("World Editing", "Flow Limit 3"), 1f, new ConfigDescription("High water flow multiplier for wellstones."));
        medsSpringLinkRange = Config.Bind(new ConfigDefinition("World Editing", "Spring Link Range"), 100f, new ConfigDescription("Maximum distance from wellstones to springs."));
        medsLinkedWellstoneCap = Config.Bind(new ConfigDefinition("World Editing", "Linked Wellstone Cap"), 100, new ConfigDescription("Maximum number of wellstones per spring."));
        medsSlopeAngleLimit = Config.Bind(new ConfigDefinition("Player", "Chisel/Shovel - Maximum Slope Angle"), 90f, new ConfigDescription("Maximum allowed slope angle when digging/chiselling."));
        medsChiselFaster = Config.Bind(new ConfigDefinition("Player", "Chisel - Reduce Cooldown"), true, new ConfigDescription("Decreases chiselling cooldown to 1 second."));
        medsChiselRangeMultiplier = Config.Bind(new ConfigDefinition("Player", "Chisel - Range Multiplier"), 5f, new ConfigDescription("Multiplies the maximum length of chiselling actions."));
        medsAirDashSpeedMultiplier = Config.Bind(new ConfigDefinition("Player", "Speed Multiplier - Air Dashing"), 1.2f, new ConfigDescription("Makes you air dash faster."));
        medsClimbSpeedMultiplier = Config.Bind(new ConfigDefinition("Player", "Speed Multiplier - Climbing"), 1.1f, new ConfigDescription("Makes you climb faster."));
        medsWalkSpeedMultiplier = Config.Bind(new ConfigDefinition("Player", "Speed Multiplier - Walking"), 1.1f, new ConfigDescription("Makes you walk faster."));
        medsManaCostMultiplier = Config.Bind(new ConfigDefinition("Player", "Mana Cost Multiplier"), 1f, new ConfigDescription("Multiplier for mana costs (lower = cheaper)."));
        medsFreeSpringRecipes = Config.Bind(new ConfigDefinition("Other", "Free Spring Recipes"), false, new ConfigDescription("Are spring recipes free?"));
        medsStorageCapacityIncrease = Config.Bind(new ConfigDefinition("Other", "Storage Capacity Increase"), true, new ConfigDescription("Increase storage capacity of upgraded storage baskets?"));
        medsStackSize = Config.Bind(new ConfigDefinition("Other", "Stack Size Increase"), true, new ConfigDescription("Increase stack size to 999 for all items."));
        Log = Logger;
        harmony.PatchAll();
        Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} has loaded!");
    }
    [HarmonyPatch]
    internal class Patches
    {
        internal static bool singleUnique = false;
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UITitleScreen), "Awake")]
        public static void UITitleScreenAwakePrefix()
        {

            FeatureSetPlacementRule[] medsFSPRs = Resources.FindObjectsOfTypeAll<FeatureSetPlacementRule>();
            foreach (FeatureSetPlacementRule _fspr in medsFSPRs)
            {
                //Log.LogInfo("_fspr: " + _fspr.name);
                foreach (FeatureSetPlacementRule.SubFeature feature in _fspr.features)
                {
                    //Log.LogInfo("_fsrp.feature: " + feature.feature.name);
                    //Log.LogInfo("_fsrp.featuretype: " + feature.feature.GetType().Name);
                    if (feature.feature.GetType() == typeof(PrefabPlacementRule))
                    {
                        PrefabPlacementRule ppr = (PrefabPlacementRule)feature.feature;
                        if (ppr.prefabName == "TreeSpirit")
                            feature.meanCount = medsSprites.Value;
                        else if (ppr.prefabName == "FrogAnimal")
                            feature.meanCount = medsFrogs.Value;
                        //Log.LogInfo("_fsrp.feature.prefabName: " + ppr.prefabName);
                    }
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CombatModelBase), "UseMana")]
        public static void CombatModelBaseUseManaPrefix(ref CombatModelBase __instance, ref float amount)
        {
            //Log.LogInfo("CombatModelBaseUseMana: " + __instance.GetType());
            if (__instance.GetType() == typeof(PlayerCombatModel))
                amount *= medsManaCostMultiplier.Value;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MagicSpringCraftingStation), "UpdateAvailableUpgrades")]
        public static void MagicSpringCraftingStationUpdateAvailableUpgradesPostfix(ref MagicSpringCraftingStation __instance)
        {
            if (medsFreeSpringRecipes.Value)
            {
                foreach (IRecipe _r in __instance.Recipes)
                {
                    _r.Ingredients.Clear();
                    if (_r.GetType() == typeof(RecipeData))
                    {
                        RecipeData _rd = (RecipeData)_r;
                        if (_rd.EssenceCost > 1)
                            _rd.essenceCost = 1;
                        _rd.foodCost = 0;
                        _rd.waterCost = 0;
                    }
                }
            }
            if (__instance.name.StartsWith("Wellstone"))
            {
                MagicSpringCraftingStation.FlowLimit1 = medsFlowLimit1.Value;
                MagicSpringCraftingStation.FlowLimit2 = medsFlowLimit2.Value;
                MagicSpringCraftingStation.FlowLimit3 = medsFlowLimit3.Value;
            }
            //MagicSpringCraftingStation.FlowLimit2 = 2f;
            //MagicSpringCraftingStation.FlowLimit1 = 5f;
        }

        /*[HarmonyPostfix]
        [HarmonyPatch(typeof(TotemBase), "Awake")]
        public static void TotemBaseAwake(ref TotemBase __instance)
        {
            Log.LogInfo("AWAKENED " + __instance.name + ": " + __instance.LinkRange.ToString());
            //__instance.LinkRange = 30f;
            //Traverse.Create(__instance).Field("slopeAngleLimit").SetValue(90f);
        }*/

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TotemBase), "Awake")]
        public static void TotemBaseAwake(ref TotemBase __instance)
        {
            if (__instance.LinkRange < 1f)
                return;
            if (__instance.name.StartsWith("Wellstone") || __instance.name.StartsWith("LargeSpring")) //__instance.name.StartsWith("SmallSpring") ||
            {
                __instance.LinkRange = medsSpringLinkRange.Value;
            }
            /*else if (__instance.name.StartsWith("SigilStone"))
            {
                __instance.LinkRange = 40f;
            }*/
            //Log.LogInfo("STARTED " + __instance.name + ": " + __instance.LinkRange.ToString());
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldSource), "GetChunkData")]
        public static void WorldSourceGetChunkDataPostfix(int x, int y, ref WorldChunk __result)
        {
            if (medsAddedFrogs < medsAddFrogsOnLoad.Value || medsAddedPangolins < medsAddPangolinsOnLoad.Value || medsAddedSprites < medsAddSpritesOnLoad.Value)
            {
                for (int l = 0; l < __result.Features.Count; l++)
                {
                    if (__result.Features[l].Prefab == "FrogAnimal" && medsAddedFrogs < medsAddFrogsOnLoad.Value)
                    {
                        for (int b = 0; b < medsAddFrogsOnLoad.Value; b++)
                        {
                            __result.Features.Add(__result.Features[l]);
                            medsAddedFrogs++;
                        }
                        medsAddFrogsOnLoad.Value = 0;
                    }
                    if (__result.Features[l].Prefab == "PangolinAnimal" && medsAddedPangolins < medsAddPangolinsOnLoad.Value)
                    {
                        for (int b = 0; b < medsAddPangolinsOnLoad.Value; b++)
                        {
                            __result.Features.Add(__result.Features[l]);
                            medsAddedPangolins++;
                        }
                        medsAddPangolinsOnLoad.Value = 0;
                    }
                    if (__result.Features[l].Prefab == "TreeSpirit" && medsAddedSprites < medsAddSpritesOnLoad.Value)
                    {
                        for (int b = 0; b < medsAddSpritesOnLoad.Value; b++)
                        {
                            __result.Features.Add(__result.Features[l]);
                            medsAddedSprites++;
                        }
                        medsAddSpritesOnLoad.Value = 0;
                    }
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DigShapeProjectileBehaviour), "Awake")]
        public static void DigShapeProjectileBehaviourAwakePrefix(ref DigShapeProjectileBehaviour __instance)
        {
            //Log.LogInfo("DigShapeProjectileBehaviourAwakePrefix");
            Traverse.Create(__instance).Field("slopeAngleLimit").SetValue(medsSlopeAngleLimit.Value);
            Traverse.Create(__instance).Field("shapeSearchRange").SetValue(15f * medsChiselRangeMultiplier.Value);
            if (medsChiselFaster.Value)
                Traverse.Create(__instance).Field("shapeCompleteCooldown").SetValue(1f);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DigShapeProjectileBehaviour), "OnProjectileImpact")]
        public static void DigShapeOnProjectileImpactPrefix(ref DigShapeProjectileBehaviour __instance)
        {
            Traverse.Create(__instance).Field("slopeAngleLimit").SetValue(medsSlopeAngleLimit.Value);
            Traverse.Create(__instance).Field("shapeSearchRange").SetValue(15f * medsChiselRangeMultiplier.Value);
            if (medsChiselFaster.Value)
                Traverse.Create(__instance).Field("shapeCompleteCooldown").SetValue(1f);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TrenchingProjectileBehaviour), "Awake")]
        public static void TrenchingProjectileBehaviourAwakePrefix(ref TrenchingProjectileBehaviour __instance)
        {
            Traverse.Create(__instance).Field("slopeAngleLimit").SetValue(medsSlopeAngleLimit.Value);
            Traverse.Create(__instance).Field("secondMarkerSearchRange").SetValue(70f * medsChiselRangeMultiplier.Value);
            if (medsChiselFaster.Value)
                Traverse.Create(__instance).Field("trenchCompleteCooldown").SetValue(1f);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TrenchingProjectileBehaviour), "OnProjectileImpact")]
        public static void OnProjectileImpactPrefix(ref TrenchingProjectileBehaviour __instance)
        {
            Traverse.Create(__instance).Field("slopeAngleLimit").SetValue(medsSlopeAngleLimit.Value);
            Traverse.Create(__instance).Field("secondMarkerSearchRange").SetValue(70f * medsChiselRangeMultiplier.Value);
            if (medsChiselFaster.Value)
                Traverse.Create(__instance).Field("trenchCompleteCooldown").SetValue(1f);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StorageEntity), "Start")]
        public static void StorageEntityStartPrefix(ref StorageEntity __instance)
        {
            if (__instance.name == "StorageBasket" && __instance.storageSizeUpgraded && medsStorageCapacityIncrease.Value)
            {
                __instance.MaxHealth = 500;
                __instance.Health = 500;
                Traverse.Create(__instance).Field("capacity").SetValue(40);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StorageEntity), "InitializeItemWrappers")]
        public static void StorageEntityInitializeItemWrappersPrefix(ref StorageEntity __instance)
        {
            if (__instance.name == "StorageBasket" && __instance.storageSizeUpgraded && medsStorageCapacityIncrease.Value)
            {
                __instance.MaxHealth = 500;
                __instance.Health = 500;
                Traverse.Create(__instance).Field("capacity").SetValue(40);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StorageEntity), "IncreaseCapacity")]
        public static void StorageEntityIncreaseCapacityPostfix(ref StorageEntity __instance)
        {
            if (__instance.name == "StorageBasket" && __instance.storageSizeUpgraded && medsStorageCapacityIncrease.Value)
            {
                __instance.MaxHealth = 500;
                __instance.Health = 500;
                Traverse.Create(__instance).Field("capacity").SetValue(40);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MagicSpring), "Start")]
        public static void MagicSpringStartPrefix(ref MagicSpring __instance)
        {
            //Log.LogInfo("MagicSpringStartPrefix");
            Traverse.Create(__instance).Field("_linkedWellstoneCap").SetValue(medsLinkedWellstoneCap.Value);
            Traverse.Create(__instance).Field("_canHostWellstone").SetValue(true);
            foreach (MagicSpring linkedSpring in __instance.LinkedSprings)
            {
                linkedSpring.CanHostNewWellstone = true;
                linkedSpring.LinkedWellstoneCap = medsLinkedWellstoneCap.Value;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MagicSpring), "Awake")]
        public static void MagicSpringAwakePrefix(ref MagicSpring __instance)
        {
            //Log.LogInfo("MagicSpringAwakePrefix");
            Traverse.Create(__instance).Field("_linkedWellstoneCap").SetValue(medsLinkedWellstoneCap.Value);
            Traverse.Create(__instance).Field("_canHostWellstone").SetValue(true);
            foreach (MagicSpring linkedSpring in __instance.LinkedSprings)
            {
                linkedSpring.CanHostNewWellstone = true;
                linkedSpring.LinkedWellstoneCap = medsLinkedWellstoneCap.Value;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MagicSpring), "SimulationUpdate")]
        public static void MagicSpringSimulationUpdatePostfix(ref MagicSpring __instance)
        {
            //Log.LogInfo("MagicSpringSimulationUpdatePostfix");
            Traverse.Create(__instance).Field("_linkedWellstoneCap").SetValue(medsLinkedWellstoneCap.Value);
            Traverse.Create(__instance).Field("_canHostWellstone").SetValue(true);
            foreach (MagicSpring linkedSpring in __instance.LinkedSprings)
            {
                linkedSpring.CanHostNewWellstone = true;
                linkedSpring.LinkedWellstoneCap = medsLinkedWellstoneCap.Value;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MagicSpring), "UpdateLinkedSprings")]
        public static void MagicSpringUpdateLinkedSpringsPostfix(ref MagicSpring __instance)
        {
            //Log.LogInfo("MagicSpringUpdateLinkedSpringsPostfix");
            Traverse.Create(__instance).Field("_linkedWellstoneCap").SetValue(medsLinkedWellstoneCap.Value);
            Traverse.Create(__instance).Field("_canHostWellstone").SetValue(true);
            foreach (MagicSpring linkedSpring in __instance.LinkedSprings)
            {
                linkedSpring.CanHostNewWellstone = true;
                linkedSpring.LinkedWellstoneCap = medsLinkedWellstoneCap.Value;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DiggingAction), "OnExecutionFinished")]
        public static void DiggingActionOnExecutionFinishedPrefix(ref DiggingAction __instance)
        {
            Traverse.Create(__instance).Field("slopeAngleLimit").SetValue(medsSlopeAngleLimit.Value);
            Traverse.Create(__instance).Field("digRadius").SetValue(2f);
        }

        /*[HarmonyPrefix]
        [HarmonyPatch(typeof(UIChatPanel), "PlayerLoaded")]
        public static void PlayerLoadedPrefix(ref OasisPlayer player)
        {
            CheatModule.Instance.ModifyClimbSpeed(player, medsClimbSpeed.Value);
        }*/

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ItemDatabase), "Awake")]
        public static void ItemDatabaseAwake(ref ItemDatabase __instance)
        {
            if (!medsStackSize.Value)
                return;
            for (int index = 0; index < __instance.itemKinds.Count; ++index)
                if (__instance.itemKinds[index].stackLimit > 1)
                    __instance.itemKinds[index].stackLimit = 999;
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Muse.Oasis.CharacterMotion.WindwalkState), "UpdateMovement")]
        public static void WindwalkStateUpdateMovementPrefix(ref Muse.Oasis.CharacterMotion.WindwalkState __instance)
        {
            __instance.sprintSpeed = 15f * medsAirDashSpeedMultiplier.Value;
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Muse.Oasis.CharacterMotion.GroundedState), "UpdateMovement")]
        public static void WindwalkStateUpdateMovementPrefix(ref Muse.Oasis.CharacterMotion.GroundedState __instance)
        {
            __instance.walkSpeed = 6f * medsWalkSpeedMultiplier.Value;
            __instance.strafeSpeed = 4f * medsWalkSpeedMultiplier.Value;
            __instance.backSpeed = 3f * medsWalkSpeedMultiplier.Value;
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Muse.Oasis.CharacterMotion.ClimbingState), "UpdateMovement")]
        public static void ClimbingStateUpdateMovementPrefix(ref Muse.Oasis.CharacterMotion.ClimbingState __instance)
        {
            __instance.climbSpeed = 1.5f * medsClimbSpeedMultiplier.Value;
        }

        /*[HarmonyPrefix]
        [HarmonyPatch(typeof(Muse.Oasis.TerrainGeneration.WorldGenerator), "IncrementPlacement")]
        public static void IncrementPlacementPrefix(string rule, int count = 1)
        {
            //Log.LogInfo("IncrementPlacement prefix, rule: " + rule + " count: " + (count).ToString());
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Muse.Oasis.TerrainGeneration.WorldGenerator), "PlaceChunkFeatures")]
        public static void PlaceChunkFeaturesPrefix(ref WorldChunk chunk, ref WorldSource world, ref Muse.Oasis.TerrainGeneration.WorldGenerator __instance)
        {
            /*Log.LogInfo("PlaceChunkFeatures prefix, chunk: " + chunk.Filename + ", world: " + world.Name);
            FeaturePlacementRule.ActiveGenerator = __instance;
            foreach (FeaturePlacementRule feature in __instance.features)
            {
                Log.LogInfo("PlaceChunkFeatures prefix, chunk: " + chunk.Filename + ", world: " + world.Name + " LIST " + feature.path);
                //WorldGenerator.IncrementPlacement(feature.path, feature.Place(chunk, world));
            }
            FeaturePlacementRule.ActiveGenerator = (WorldGenerator)null;
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Muse.Oasis.TerrainGeneration.WorldGenerator), "PlaceChunkUniqueFeatures")]
        public static void PlaceChunkUniqueFeaturesPrefix(ref WorldChunk chunk, ref WorldSource world, ref Muse.Oasis.TerrainGeneration.WorldGenerator __instance)
        {
            /*Log.LogInfo("PlaceChunkUniqueFeatures prefix, chunk: " + chunk.Filename + ", world: " + world.Name);
            if (!singleUnique)
            {
                foreach (WorldMarker _wm in world.Metadata.UniqueFeatures)
                {
                    Log.LogInfo("PlaceChunkUniqueFeatures WorldMarker: " + _wm.Name + " type: " + _wm.FeatureType);
                }
                foreach (WorldEntity _em in world.Metadata.Entities)
                {
                    Log.LogInfo("PlaceChunkUniqueFeatures WorldEntity: " + _em.ToString());
                }
                foreach (UniqueFeature _uf in __instance.uniqueFeatures)
                {
                    Log.LogInfo("PlaceChunkUniqueFeatures UniqueFeature: " + _uf.feature.path);
                }
                singleUnique = true;
            }
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Muse.Oasis.TerrainGeneration.WorldGenerator), "GenerateRandomUniqueFeaturePosition")]
        public static void GenerateRandomUniqueFeaturePositionPrefix(ref UniqueFeature unique, ref Muse.Oasis.TerrainGeneration.WorldGenerator __instance)
        {
            //Log.LogInfo("GenerateRandomUniqueFeaturePosition prefix, path: " + unique.feature.name + ", globalCount: " + unique.globalCount.ToString());
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Muse.Oasis.TerrainGeneration.WorldGenerator), "PlaceChunkUniqueFeatures")]
        public static void PlaceChunkUniqueFeaturesPostfix(ref WorldChunk chunk, ref WorldSource world, ref Muse.Oasis.TerrainGeneration.WorldGenerator __instance)
        {
            //Log.LogInfo("PlaceChunkUniqueFeatures postfix, chunk: " + chunk.Filename + ", world: " + world.Name);
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Muse.Oasis.TerrainGeneration.WorldGenerator), "PlaceChunkEarlyFeatures")]
        public static void PlaceChunkEarlyFeaturesPrefix(ref WorldChunk chunk, ref WorldSource world, ref Muse.Oasis.TerrainGeneration.WorldGenerator __instance)
        {
            //Log.LogInfo("PlaceChunkEarlyFeatures prefix");
        }*/
    }
}
