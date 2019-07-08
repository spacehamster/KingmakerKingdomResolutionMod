using Harmony12;
using Kingmaker.Kingdom;
using Kingmaker.Kingdom.Blueprints;
using Kingmaker.Kingdom.Tasks;
using Kingmaker.UI.Tooltip;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KingdomResolution
{
    class Patches
    {      /*
         * Type of KingdomTask, Manages KingdomEvent
         */
        [HarmonyPatch(typeof(KingdomTaskEvent), "SkipPlayerTime", MethodType.Getter)]
        static class KingdomTaskEvent_SkipPlayerTime_Patch
        {
            static void Postfix(ref int __result)
            {
                try
                {
                    if (!Main.enabled) return;
                    if (__result < 1) return;
                    if (Main.settings.skipPlayerTime)
                    {
                        __result = 0;
                    }
                    else
                    {
                        __result = Mathf.RoundToInt(__result * Main.settings.baronTimeFactor);
                        __result = __result < 1 ? 1 : __result;
                    }
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                }
            }
        }
        /*
         * Represents BlueprintKingdomEventBase
         * BlueprintKingdomEventBase has Concrete Types BlueprintKingdomEvent, BlueprintKingdomProject and BlueprintKingdomClaim
         */
        [HarmonyPatch(typeof(KingdomEvent), "CalculateResolutionTime")]
        static class KingdomEvent_CalculateResolutionTime_Patch
        {
            static void Postfix(KingdomEvent __instance, ref int __result)
            {
                try
                {
                    if (!Main.enabled) return;
                    if (__instance.EventBlueprint.IsResolveByBaron) return;
                    if (__instance.EventBlueprint is BlueprintKingdomEvent)
                    {
                        __result = Mathf.RoundToInt(__result * Main.settings.eventTimeFactor);
                        __result = __result < 1 ? 1 : __result;
                    }
                    var projectBlueprint = __instance.EventBlueprint as BlueprintKingdomProject;
                    if (projectBlueprint != null && projectBlueprint.SpendRulerTimeDays > 0)
                    {
                        __result = Mathf.RoundToInt(__result * Main.settings.baronTimeFactor);
                        __result = __result < 1 ? 1 : __result;
                    }
                    if (projectBlueprint != null && projectBlueprint.SpendRulerTimeDays <= 0)
                    {
                        __result = Mathf.RoundToInt(__result * Main.settings.projectTimeFactor);
                        __result = __result < 1 ? 1 : __result;
                    }
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                }
            }
        }
        [HarmonyPatch(typeof(KingdomEvent), "CalculateBPCost")]
        static class KingdomEvent_CalculateBPCost_Patch
        {
            static void Postfix(KingdomEvent __instance, ref int __result)
            {
                try
                {
                    if (!Main.enabled) return;
                    __result = Mathf.RoundToInt(__result * Main.settings.eventPriceFactor);
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                }
            }
        }
        [HarmonyPatch(typeof(KingdomTaskEvent), "GetDC")]
        static class KingdomTaskEvent_GetDC_Patch
        {
            static void Postfix(ref int __result)
            {
                try
                {
                    if (!Main.enabled) return;
                    if (!Main.settings.easyEvents) return;
                    __result = -100;
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                }
            }
        }
        [HarmonyPatch(typeof(KingdomState), "CanSeeKingdomFromGlobalMap", MethodType.Getter)]
        static class KingdomState_CanSeeKingdomFromGlobalMap_Patch
        {
            static void Postfix(ref bool __result)
            {
                try
                {
                    if (!Main.enabled) return;
                    if (!Main.settings.alwaysManageKingdom) return;
                    __result = true;
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                }
            }
        }
        [HarmonyPatch(typeof(KingdomTimelineManager), "CanAdvanceTime")]
        static class KingdomTimelineManager_CanAdvanceTime_Patch
        {
            static void Postfix(ref bool __result)
            {
                try
                {
                    if (!Main.enabled) return;
                    if (!Main.settings.alwaysAdvanceTime) return;
                    __result = true;
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                }
            }
        }
        [HarmonyPatch(typeof(KingdomState), "PartyIsInKingdomBorders", MethodType.Getter)]
        static class KingdomState_PartyIsInKingdomBorders_Patch
        {
            static void Postfix(ref bool __result)
            {
                try
                {
                    if (!Main.enabled) return;
                    if (!Main.settings.alwaysBaronProcurement) return;
                    __result = true;
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                }
            }
        }
        [HarmonyPatch(typeof(KingdomTimelineManager), "FailIgnoredEvents")]
        static class KingdomTimelineManager_FailIgnoredEvents_Patch
        {
            static bool Prefix()
            {
                try
                {
                    if (!Main.enabled) return true;
                    if (!Main.settings.overrideIgnoreEvents) return true;
                    return false;
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                    return true;
                }
            }
        }
        [HarmonyPatch(typeof(KingdomTimelineManager), "AutoAssignLeaders")]
        static class KingdomTimelineManager_AutoAssignLeaders_Patch
        {
            static bool Prefix()
            {
                if (!Main.enabled) return true;
                if (!Main.settings.disableAutoAssignLeaders) return true;
                return false;
            }
        }
        [HarmonyPatch(typeof(LeaderState), "GetCharacterStat")]
        static class LeaderState_GetCharacterStat_Patch
        {
            static bool Prefix(ref bool withPenalty)
            {
                if (!Main.enabled) return true;
                if (!Main.settings.disableMercenaryPenalty) return true;
                withPenalty = false;
                return true;
            }
        }
        [HarmonyPatch(typeof(DescriptionTemplatesKingdom), "KingdomLeaderStatDescription",
            new Type[] { typeof(LeaderState), typeof(LeaderState.Leader), typeof(DescriptionBricksBox) })]
        static class DescriptionTemplatesKingdom_KingdomLeaderStatDescription_Patch
        {
            static void Prefix(ref int __state)
            {
                if (!Main.enabled) return;
                if (!Main.settings.disableMercenaryPenalty) return;
                if (KingdomRoot.Instance != null)
                {
                    __state = KingdomRoot.Instance.CustomLeaderPenalty;
                    KingdomRoot.Instance.CustomLeaderPenalty = 0;
                }
            }
            static void Postfix(ref int __state)
            {
                if (!Main.enabled) return;
                if (!Main.settings.disableMercenaryPenalty) return;
                if (KingdomRoot.Instance != null)
                {
                    KingdomRoot.Instance.CustomLeaderPenalty = __state;
                }
                return;
            }
        }
    }
}
