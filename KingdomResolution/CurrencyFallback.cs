// Copyright (c) 2018 fireundubh <fireundubh@gmail.com>
// This code is licensed under MIT license (see LICENSE for details)

using Harmony12;
using Kingmaker.Controllers.Rest;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Globalmap.State;
using Kingmaker.Kingdom;
using Kingmaker.Kingdom.Blueprints;
using Kingmaker.Kingdom.Settlements;
using Kingmaker.Kingdom.Tasks;
using Kingmaker.Localization;
using Kingmaker.PubSubSystem;
using Kingmaker.UI.Kingdom;
using Kingmaker.Utility;
using Kingmaker.UI.Settlement;
using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Kingmaker.Globalmap;
using static Kingmaker.Kingdom.Settlements.SettlementState;
using static Kingmaker.Globalmap.State.LocationData;
using Kingmaker.Achievements;
using Kingmaker.Kingdom.UI;
using static Kingmaker.Kingdom.KingdomUIRoot;
using Kingmaker.UI.GlobalMap;
using Kingmaker;
using Kingmaker.Blueprints.Root.Strings;

namespace KingdomResolution
{
    class KingdomCurrencyFallback
    {
        public static string costFormat = "<size=-2>{0}</size>";
        public static string costSplitFormat = "<size=-2>{0}, {1} GP</size>";
        public static string resourceCostSplitFormat = "{1} GP, {0}";
        public static bool CanSpend(int pointCost)
        {
            if (KingdomState.Instance.BP - pointCost >= 0)
            {
                return true;
            }

            int currencyMult = 80; //Math.Max((int)(1f / Main.settings.eventPriceFactor), 1);

            return KingdomState.Instance.BP * currencyMult + Kingmaker.Game.Instance.Player.Money - pointCost * currencyMult >= 0;
        }

        public static bool SpendPoints(int pointCost)
        {
            int pointDebt = KingdomState.Instance.BP - pointCost;

            if (pointDebt >= 0)
            {
                KingdomState.Instance.BP -= pointCost;
                return true;
            }

            int pointCostNew = pointCost - Mathf.Abs(pointDebt);

            int goldCost = Mathf.Abs(pointDebt) * 80; //KingmakerPatchSettings.CurrencyFallback.CurrencyMultiplier;

            if (!Kingmaker.Game.Instance.Player.SpendMoney(goldCost))
            {
                return false;
            }

            KingdomState.Instance.BP -= pointCostNew;
            return true;
        }
        public static Tuple<int, int> SplitCost(int pointCost)
        {
            int pointDebt = KingdomState.Instance.BP - pointCost;

            if (pointDebt >= 0)
            {
                return new Tuple<int, int>(pointCost, 0);
            }

            int pointCostNew = pointCost - Mathf.Abs(pointDebt);

            int goldCost = Mathf.Abs(pointDebt) * 80; // KingmakerPatchSettings.CurrencyFallback.CurrencyMultiplier;

            return new Tuple<int, int>(pointCostNew, goldCost);
        }
        [HarmonyPatch()]
        static class RequiredStaff_Initialize_Patch
        {
            static FastInvoker SetColor;
            static Type TargetType() => AccessTools.Inner(typeof(BuildingItem), "RequiredStaff");
            static bool Prepare()
            {
                SetColor = Accessors.CreateInvoker(TargetType(), "SetColor", typeof(object), typeof(BlueprintSettlementBuilding), typeof(SettlementBuilding), typeof(SettlementState));
                return true;
            }
            static MethodBase TargetMethod()
            {
                return TargetType().GetMethod("Initialize");
            }
            static bool Prefix(object __instance, BlueprintSettlementBuilding building, SettlementBuilding settlementBuilding, SettlementState settlement,
                    ref Image ___Slots, ref TextMeshProUGUI ___Cost, ref Image ___DiscountLayer)
            {
                try
                {
                    if (!Main.enabled) return true;
                    if (!Main.settings.currencyFallback) return true;

                    string costFormat = KingdomCurrencyFallback.costFormat;
                    string costSplitFormat = KingdomCurrencyFallback.costSplitFormat;

                    ___Slots.sprite = KingdomUIRoot.Instance.Settlement.GetSlotSprite(building.SlotCount);

                    if (settlementBuilding == null)
                    {
                        int actualCost = settlement.GetActualCost(building, out bool isDiscounted);
                        ___DiscountLayer.gameObject.SetActive(actualCost == 0 || isDiscounted);

                        if (actualCost == 0)
                        {
                            ___Cost.text = string.Format(costFormat, KingdomUIRoot.Instance.Texts.BuildFreeCost);
                        }
                        else
                        {
                            Tuple<int, int> costSplit = KingdomCurrencyFallback.SplitCost(actualCost);

                            LocalizedString format = isDiscounted ? KingdomUIRoot.Instance.Texts.BuildPointsDiscountFormat : KingdomUIRoot.Instance.Texts.BuildPointsFormat;

                            if (costSplit.Item2 == 0)
                            {
                                ___Cost.text = string.Format(costFormat, string.Format(format, costSplit.Item1));
                            }
                            else
                            {
                                ___Cost.text = string.Format(costSplitFormat, string.Format(format, costSplit.Item1), costSplit.Item2);
                            }
                        }
                    }
                    else
                    {
                        ___DiscountLayer.gameObject.SetActive(false);
                        ___Cost.text = string.Format(costFormat, string.Format(KingdomUIRoot.Instance.Texts.BuildPointsFormat, settlementBuilding.Owner.GetSellPrice(building)));
                    }

                    SetColor(__instance, building, settlementBuilding, settlement);
                    return false;
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                    return true;
                }
            }
        }
        [HarmonyPatch(typeof(KingdomUIEventWindowFooter), "CanStart")]
        static class KingdomUIEventWindowFooter_CanStart_Patch
        {
            static FastInvoker<KingdomUIEventWindowFooter, bool> IsSpendRulerTimeDays;
            static bool Prepare()
            {
                IsSpendRulerTimeDays = Accessors.CreateInvoker<KingdomUIEventWindowFooter, bool>("IsSpendRulerTimeDays");
                return true;
            }
            static bool Prefix(KingdomUIEventWindowFooter __instance, ref bool __result, KingdomEventUIView kingdomEventView, ref bool enable)
            {
                try
                {
                    if (!Main.enabled) return true;
                    if (!Main.settings.currencyFallback) return true;
                    enable = false;
                    if (kingdomEventView.Task == null)
                    {
                        __result = false;
                        return false;
                    }
                    if (kingdomEventView.Task.IsStarted)
                    {
                        __result = false;
                        return false;
                    }
                    enable = (kingdomEventView.Task.AssignedLeader != null);
                    if (enable && kingdomEventView.CostInBP > 0)
                    {
                        enable = KingdomCurrencyFallback.CanSpend(kingdomEventView.CostInBP);
                    }
                    if (enable && !KingdomTimelineManager.CanAdvanceTime())
                    {
                        enable = !IsSpendRulerTimeDays(__instance);
                    }
                    __result = true;
                    return false;
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                    return true;
                }
            }
        }
        [HarmonyPatch(typeof(KingdomUITexts), "GetProjectCostFormat")]
        static class KingdomUITexts_GetProjectCostFormat_Patch
        {
            static void Postfix(int cost, bool format, ref string __result)
            {
                try
                {
                    if (!Main.enabled) return;
                    if (!Main.settings.currencyFallback) return;
                    if (!KingdomCurrencyFallback.CanSpend(cost)) return;
                    string costFormat = KingdomCurrencyFallback.costFormat;
                    string costSplitFormat = KingdomCurrencyFallback.costSplitFormat;
                    var originalFormat = (!format) ? KingdomUIRoot.Instance.Texts.KingdomProjectStartBPCostStartLabelFormat : KingdomUIRoot.Instance.Texts.KingdomProjectStartBPCostFormat;
                    Tuple<int, int> costSplit = KingdomCurrencyFallback.SplitCost(cost);
                    if (costSplit.Item2 == 0)
                    {
                        __result = string.Format(costFormat, string.Format(originalFormat, costSplit.Item1));
                    }
                    else
                    {
                        __result = string.Format(costSplitFormat, string.Format(originalFormat, costSplit.Item1), costSplit.Item2);
                    }
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                    return;
                }
            }
        }
        [HarmonyPatch(typeof(KingdomTask), "Start")]
        static class KingdomTask_Start_Patch
        {
            static FastSetter<KingdomTask, bool> IsStartedSetter;
            static FastSetter<KingdomTask, int> StartedOnSetter;
            static FastInvoker<KingdomTask, object> OnTaskChanged;
            static bool Prepare()
            {
                IsStartedSetter = Accessors.CreateSetter<KingdomTask, bool>("IsStarted");
                StartedOnSetter = Accessors.CreateSetter<KingdomTask, int>("StartedOn");
                OnTaskChanged = Accessors.CreateInvoker<KingdomTask, object>("OnTaskChanged");
                return true;
            }
            static bool Prefix(KingdomTask __instance, bool raiseEvent)
            {
                try
                {
                    if (!Main.enabled) return true;
                    if (!Main.settings.currencyFallback) return true;
                    IsStartedSetter(__instance, true);
                    StartedOnSetter(__instance, KingdomState.Instance.CurrentDay);

                    KingdomCurrencyFallback.SpendPoints(__instance.OneTimeBPCost);

                    if (raiseEvent)
                    {
                        OnTaskChanged(__instance);
                    }

                    EventBus.RaiseEvent((IKingdomTaskEventsHandler h) => h.OnTaskStarted(__instance));

                    if (__instance.SkipPlayerTime <= 0)
                    {
                        return false;
                    }

                    Kingmaker.Game.Instance.AdvanceGameTime(TimeSpan.FromDays(__instance.SkipPlayerTime));

                    foreach (UnitEntityData unitEntityData in Kingmaker.Game.Instance.Player.AllCharacters)
                    {
                        RestController.ApplyRest(unitEntityData.Descriptor);
                    }

                    new KingdomTimelineManager().UpdateTimeline();

                    return false;
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                    return true;
                }
            }
        }
        [HarmonyPatch(typeof(KingdomUISettlementWindow), "UpdateBuildEnabled")]
        static class KingdomUISettlementWindow_UpdateBuildEnabled_Patch
        {
            static bool Prefix(ref TMP_InputField ___m_InputNameField, ref Button ___m_Build)
            {
                if (!Main.enabled) return true;
                if (!Main.settings.currencyFallback) return true;
                try
                {
                    bool canAfford = KingdomCurrencyFallback.CanSpend(KingdomRoot.Instance.SettlementBPCost);

                    if (canAfford)
                    {
                        canAfford = !___m_InputNameField.text.Empty();
                    }

                    ___m_Build.interactable = canAfford;

                    return false;
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                    return true;
                }
            }
        }
        [HarmonyPatch(typeof(LocationData), "ClaimResource")]
        static class LocationData_ClaimResource_Patch
        {
            static bool Prefix(LocationData __instance)
            {
                try
                {
                    if (!Main.enabled) return true;
                    if (!Main.settings.currencyFallback) return true;
                    if (__instance.Resource != ResourceStateType.CanClaim)
                    {
                        return false;
                    }

                    KingdomCurrencyFallback.SpendPoints(KingdomRoot.Instance.DefaultMapResourceCost);

                    KingdomState.Instance.Resources.Add(__instance.Blueprint);

                    __instance.Blueprint.ResourceStats.Apply();

                    if (GlobalMapRules.Instance && GlobalMapRules.Instance.ClaimedResourceVisual)
                    {
                        GlobalMapLocation locationObject = GlobalMapRules.Instance.GetLocationObject(__instance.Blueprint);

                        if (locationObject)
                        {
                            UnityEngine.Object.Instantiate(GlobalMapRules.Instance.ClaimedResourceVisual, locationObject.transform, false);
                        }
                    }

                    if (KingdomRoot.Instance.Locations.Count(l => l.HasKingdomResource) == KingdomState.Instance.Resources.Count)
                    {
                        Kingmaker.Game.Instance.Player.Achievements.Unlock(AchievementType.IntensiveDevelopment);
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                    return true;
                }
            }
        }
        [HarmonyPatch(typeof(GlobalMapMessageBox), "FillDialogInfoResource")]
        static class GlobalMapMessageBox_FillDialogInfoResource_Patch
        {
            static void Postfix(TextMeshProUGUI ___m_DescText, GlobalMapLocation ___m_Location,
                        GameObject ___m_StandartControllers, GameObject ___m_OkControllers)
            {
                try
                {
                    if (!Main.enabled) return;
                    if (!Main.settings.currencyFallback) return;
                    LocationData.ResourceStateType resource = ___m_Location.Data.Resource;
                    if (resource == ResourceStateType.CantClaimWrongRegion) return;
                    if (resource != ResourceStateType.CanClaim) return;
                    if (!KingdomCurrencyFallback.CanSpend(KingdomRoot.Instance.DefaultMapResourceCost)) return;
                    string resourceCostSplitFormat = KingdomCurrencyFallback.resourceCostSplitFormat;
                    UITextGlobalMap globalMap = Game.Instance.BlueprintRoot.LocalizedTexts.UserInterfacesText.GlobalMap;
                    Tuple<int, int> costSplit = KingdomCurrencyFallback.SplitCost(KingdomRoot.Instance.DefaultMapResourceCost);
                    if (costSplit.Item2 == 0) return;
                    var bpText = string.Format(resourceCostSplitFormat, costSplit.Item1, costSplit.Item2);
                    ___m_DescText.text = string.Format(globalMap.ResourceAvailable, bpText, KingdomState.Instance.BP);
                    ___m_StandartControllers.SetActive(true);
                    ___m_OkControllers.SetActive(false);
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                }
            }
        }
        [HarmonyPatch(typeof(RegionState), "FoundSettlement")]
        static class RegionState_FoundSettlement_Patch
        {
            static bool Prefix(RegionState __instance, RegionSettlementLocation settlementLocation, string name)
            {
                try
                {
                    if (!Main.enabled) return true;
                    if (!Main.settings.currencyFallback) return true;
                    if (!__instance.Blueprint.SettlementBuildArea)
                    {
                        UberDebug.LogError("Cannot found a settlement in {0}: no building area set up", settlementLocation);
                        return false;
                    }

                    if (__instance.Settlement != null)
                    {
                        UberDebug.LogError("Cannot found a settlement in {0}: already built", settlementLocation);
                        return false;
                    }

                    if (settlementLocation != null && settlementLocation.AssociatedLocation == null)
                    {
                        UberDebug.LogError("Cannot found a settlement in {0}: no globalmap location associated", settlementLocation);
                        return false;
                    }

                    if (settlementLocation == null && __instance.Blueprint.SettlementGlobalmapLocations.Length == 0)
                    {
                        UberDebug.LogError("Cannot found a settlement in {0}: no location specified and no default found", __instance.Blueprint);
                        return false;
                    }

                    KingdomCurrencyFallback.SpendPoints(KingdomRoot.Instance.SettlementBPCost);

                    var settlementState = new SettlementState(SettlementState.LevelType.Village)
                    {
                        Region = __instance
                    };

                    SettlementState settlementState2 = settlementState;

                    settlementState2.HasWaterSlot = settlementLocation?.HasWaterSlot == true;

                    settlementState.Name = name ?? __instance.Blueprint.DefaultSettlementName;

                    settlementState.Location = settlementLocation?.AssociatedLocation ?? __instance.Blueprint.SettlementGlobalmapLocations.FirstOrDefault();

                    settlementState.SettlementLocation = settlementLocation;

                    __instance.Settlement = settlementState;

                    __instance.SetSettlementUIMarkers();

                    EventBus.RaiseEvent((ISettlementFoundingHandler h) => h.OnSettlementFounded(__instance.Settlement));
                    return false;
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                    return true;
                }
            }
        }
        [HarmonyPatch(typeof(SettlementState), "Build")]
        static class SettlementState_Build_Patch
        {
            static FastGetter<SettlementState, BlueprintSettlementBuilding> SellDiscountedBuildingGetter;
            static FastSetter<SettlementState, BlueprintSettlementBuilding> SellDiscountedBuildingSetter;
            static bool Prepare()
            {
                SellDiscountedBuildingGetter = Accessors.CreateGetter<SettlementState, BlueprintSettlementBuilding>("SellDiscountedBuilding");
                SellDiscountedBuildingSetter = Accessors.CreateSetter<SettlementState, BlueprintSettlementBuilding>("SellDiscountedBuilding");
                return true;
            }
            static bool Prefix(SettlementState __instance, ref SettlementBuilding __result,
                BlueprintSettlementBuilding building, SettlementGridTopology.Slot slot, bool force,
                ref int ___m_SlotsLeft, BuildingsCollection ___m_Buildings)
            {
                try
                {
                    if (!Main.enabled) return true;
                    if (!Main.settings.currencyFallback) return true;

                    var removedBuilding = true;

                    if (!force)
                    {
                        if (!__instance.CanBuild(building))
                        {
                            __result = null;
                            return false;
                        }

                        BuildingSlot slotObject = slot.GetSlotObject();

                        if (slotObject?.CanBuildHere(building) != true)
                        {
                            return false;
                        }

                        KingdomCurrencyFallback.SpendPoints(__instance.GetActualCost(building));

                        removedBuilding = __instance.FreeBuildings.Remove(building) || KingdomState.Instance.FreeBuildings.Remove(building);
                    }

                    SettlementBuilding settlementBuilding = ___m_Buildings.Build(building);
                    settlementBuilding.BuildOnSlot(slot);

                    if (building.SpecialSlot == SpecialSlotType.None)
                    {
                        ___m_SlotsLeft -= building.SlotCount;
                    }

                    if (!force && !removedBuilding || SellDiscountedBuildingGetter(__instance) != building)
                    {
                        SellDiscountedBuildingSetter(__instance, null);
                    }

                    __instance.Update();

                    EventBus.RaiseEvent((ISettlementBuildingHandler h) => h.OnBuildingStarted(__instance, settlementBuilding));

                    __result = settlementBuilding;
                    return false;
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                    return true;
                }
            }
        }
        [HarmonyPatch(typeof(SettlementState), "CanBuild")]
        static class SettlementState_CanBuild_Patch
        {
            static FastInvoker<SettlementState, BlueprintSettlementBuilding, bool> CanBuildByLevel;
            static bool Prepare()
            {
                CanBuildByLevel = Accessors.CreateInvoker<SettlementState, BlueprintSettlementBuilding, bool>("CanBuildByLevel");
                return true;
            }
            static bool Prefix(SettlementState __instance, ref bool __result, BlueprintSettlementBuilding building, int ___m_SlotsLeft)
            {
                try
                {
                    if (!Main.enabled) return true;
                    if (!Main.settings.currencyFallback) return true;

                    if (!KingdomCurrencyFallback.CanSpend(__instance.GetActualCost(building)))
                    {
                        UberDebug.LogSystem("[KingdomResolution] SettlementState_CanBuild_Patch: Cannot spend");
                        __result = false;
                        return false;
                    }

                    SpecialSlotType specialSlot = building.SpecialSlot;

                    if (specialSlot != SpecialSlotType.None)
                    {
                        if (__instance.IsSpecialSlotFilled(specialSlot))
                        {
                            return false;
                        }
                    }
                    else if (___m_SlotsLeft < building.SlotCount)
                    {
                        return false;
                    }

                    __result = CanBuildByLevel(__instance, building);
                    return false;
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                    return true;
                }
            }
        }
        [HarmonyPatch(typeof(SettlementState), "CanBuildUprgade")]
        static class SettlementState_CanBuildUprgade_Patch
        {
            static bool Prefix(SettlementState __instance, ref bool __result, BlueprintSettlementBuilding building)
            {
                try
                {
                    if (!Main.enabled) return true;
                    if (!Main.settings.currencyFallback) return true;

                    __result = KingdomCurrencyFallback.CanSpend(__instance.GetActualCost(building)) && building.MinLevel <= __instance.Level && __instance.Buildings.Any(b => b.Blueprint.UpgradesTo == building);
                    return false;
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                    return true;
                }
            }
        }
        [HarmonyPatch(typeof(SettlementState), "UpgradeBuilding")]
        static class SettlementState_UpgradeBuilding_Patch
        {
            static bool Prefix(SettlementState __instance, SettlementBuilding building, ref SettlementBuilding __result,
                BuildingsCollection ___m_Buildings)
            {
                try
                {
                    if (!Main.enabled) return true;
                    if (!Main.settings.currencyFallback) return true;

                    if (!building.IsFinished || !___m_Buildings.HasFact(building) || !building.Blueprint.UpgradesTo)
                    {
                        return false;
                    }

                    if (!KingdomCurrencyFallback.CanSpend(__instance.GetActualCost(building.Blueprint.UpgradesTo)))
                    {
                        UberDebug.LogWarning("Cannot upgrade " + building.Blueprint + ": not enough BP");
                        return false;
                    }

                    KingdomCurrencyFallback.SpendPoints(__instance.GetActualCost(building.Blueprint));

                    SettlementBuilding result = ___m_Buildings.Upgrade(building);

                    __instance.Update();

                    EventBus.RaiseEvent((ISettlementBuildUpdate h) => h.OnBuildUpdate(building));

                    __result = result;
                    return false;
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                    return true;
                }
            }
        }
    }
}
