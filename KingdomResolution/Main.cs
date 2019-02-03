using Harmony12;
using UnityModManagerNet;
using System;
using System.Reflection;
using Kingmaker.Kingdom.Tasks;
using UnityEngine;
using Kingmaker.Kingdom;
using Kingmaker.Kingdom.Blueprints;
using Kingmaker.UI.SettingsUI;
using Kingmaker.Blueprints;

namespace KingdomResolution
{
    public class Main
    {
        public static UnityModManagerNet.UnityModManager.ModEntry.ModLogger logger;
        [System.Diagnostics.Conditional("DEBUG")]
        public static void DebugLog(string msg)
        {
            if (logger != null) logger.Log(msg);
        }
        public static void DebugError(Exception ex)
        {
            if (logger != null) logger.Log(ex.ToString() + "\n" + ex.StackTrace);
        }
        public static bool enabled;
        public static Settings settings;
        static string modId;
        static bool Load(UnityModManager.ModEntry modEntry)
        {
            try
            {
                logger = modEntry.Logger;
                modId = modEntry.Info.Id;
                settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
                var harmony = HarmonyInstance.Create(modEntry.Info.Id);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                modEntry.OnToggle = OnToggle;
                modEntry.OnGUI = OnGUI;
                modEntry.OnSaveGUI = OnSaveGUI;
                KingdomStash.Init();
            } catch(Exception ex)
            {
                DebugError(ex);
                throw ex;   
            }
            return true;
        }
        // Called when the mod is turned to on/off.
        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value /* active or inactive */)
        {
            enabled = value;
            return true; // Permit or not.
        }
        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }
        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            if (!enabled) return;
            try
            {
                string percentFormatter(float value) => Math.Round(value * 100, 0) == 0 ? " 1 day" : Math.Round(value * 100, 0) + " %";
                GUILayout.Label("Kingdom Options", Util.BoldLabel);
                GUIHelper.ChooseFactor(Labels.EventTimeFactorLabel, Labels.EventTimeFactorTooltip, settings.eventTimeFactor, 1, 
                    (value) => settings.eventTimeFactor = (float)Math.Round(value, 2), percentFormatter);
                GUIHelper.ChooseFactor(Labels.ProjectTimeFactorLabel, Labels.ProjectTimeFactorTooltip, settings.projectTimeFactor, 1, 
                    (value) => settings.projectTimeFactor = (float)Math.Round(value, 2), percentFormatter);
                GUIHelper.ChooseFactor(Labels.RulerTimeFactorLabel, Labels.RulerTimeFactorTooltip, settings.baronTimeFactor, 1, 
                    (value) => settings.baronTimeFactor = (float)Math.Round(value, 2), percentFormatter);
                GUIHelper.ChooseFactor(Labels.EventPriceFactorLabel, Labels.EventPriceFactorTooltip, settings.eventPriceFactor, 1,
                    (value) => settings.eventPriceFactor = (float)Math.Round(value, 2), (value) => " " + Math.Round(Math.Round(value, 2) * 100, 0) + " %");
                GUIHelper.Toggle(ref settings.easyEvents, Labels.EasyEventsLabel, Labels.EasyEventsTooltip);
                GUIHelper.Toggle(ref settings.alwaysManageKingdom, Labels.AlwaysManageKingdomLabel, Labels.AlwaysManageKingdomTooltip);
                GUIHelper.Toggle(ref settings.alwaysAdvanceTime, Labels.AlwaysAdvanceTimeLabel, Labels.AlwaysAdvanceTimeTooltip);
                GUIHelper.Toggle(ref settings.skipPlayerTime, Labels.SkipPlayerTimeLabel, Labels.SkipPlayerTimeTooltip);
                GUIHelper.Toggle(ref settings.alwaysBaronProcurement, Labels.AlwaysBaronProcurementLabel, Labels.AlwaysBaronProcurementTooltip);
                GUIHelper.Toggle(ref settings.overrideIgnoreEvents, Labels.OverrideIgnoreEventsLabel, Labels.OverrideIgnoreEventsTooltip);
                GUIHelper.Toggle(ref settings.disableAutoAssignLeaders, Labels.DisableAutoAssignLeadersLabel, Labels.DisableAutoAssignLeadersTooltip);
                GUIHelper.Toggle(ref settings.currencyFallback, Labels.CurrencyFallbackLabel, Labels.CurrencyFallbackTooltip);
                GUIHelper.ChooseInt(ref settings.currencyFallbackExchangeRate, Labels.CurrencyFallbackExchangeRateLabel, Labels.CurrencyFallbackExchangeRateTooltip);
                GUILayout.BeginHorizontal();
                GUIHelper.Toggle(ref settings.pauseKingdomTimeline, Labels.PauseKingdomTimelineLabel, Labels.PauseKingdomTimelineTooltip);
                if (settings.pauseKingdomTimeline)
                {
                    GUIHelper.Toggle(ref settings.enablePausedKingdomManagement, Labels.EnablePausedKingdomManagementLabel, Labels.EnablePausedKingdomManagementTooltip);
                    if (settings.enablePausedKingdomManagement)
                    {
                        GUIHelper.Toggle(ref settings.enablePausedRandomEvents, Labels.EnablePausedRandomEventsLabel, Labels.EnablePausedRandomEventsTooltip);
                    }
                }
                GUILayout.EndHorizontal();
                if (ResourcesLibrary.LibraryObject != null && SettingsRoot.Instance.KingdomManagementMode.CurrentValue == KingdomDifficulty.Auto)
                {
                    if (GUILayout.Button("Disable Auto Kingdom Management Mode"))
                    {
                        SettingsRoot.Instance.KingdomManagementMode.CurrentValue = KingdomDifficulty.Easy;
                        SettingsRoot.Instance.KingdomDifficulty.CurrentValue = KingdomDifficulty.Easy;
                    }
                }
                ChooseKingdomUnreset();
                GUILayout.Label("Preview Options", Util.BoldLabel);
                GUIHelper.Toggle(ref settings.previewEventResults, Labels.PreviewEventResultsLabel, Labels.PreviewEventResultsTooltip);
                GUIHelper.Toggle(ref settings.previewDialogResults, Labels.PreviewDialogResultsLabel, Labels.PreviewDialogResultsTooltip);
                GUIHelper.Toggle(ref settings.previewAlignmentRestrictedDialog, Labels.PreviewAlignmentRestrictedDialogLabel, Labels.PreviewAlignmentRestrictedDialogTooltip);
                GUIHelper.Toggle(ref settings.previewRandomEncounters, Labels.PreviewRandomEncountersLabel, Labels.PreviewRandomEncountersTooltip);
                GUILayout.Label("Misc Options", Util.BoldLabel);
                GUIHelper.Toggle(ref settings.highlightObjectsToggle, Labels.HighlightObjectToggleLabel, Labels.HighLightObjectToggleTooltip);
                KingdomStash.OnGUI();
                KingdomInfo.OnGUI();
                GUIHelper.ShowTooltip();
            }
            catch (Exception ex)
            {
                DebugError(ex);
                throw ex;
            }
        }
        static void ChooseKingdomUnreset()
        {
            KingdomState instance = KingdomState.Instance;
            if (instance == null) return;
            var kingdomUnrestName = instance.Unrest == KingdomStatusType.Metastable ? " Serene" : " " + instance.Unrest;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Kingdom Unrest: " + kingdomUnrestName, GUILayout.Width(300));
            if (GUILayout.Button("More Unrest"))
            {
                if (instance.Unrest != KingdomStatusType.Crumbling) {
                    instance.SetUnrest(instance.Unrest - 1, KingdomStatusChangeReason.None, modId);
                }
            }
            if (GUILayout.Button("Less Unrest"))
            {
                if (instance.Unrest == KingdomStatusType.Metastable) return;
                instance.SetUnrest(instance.Unrest + 1, KingdomStatusChangeReason.None, modId);
            }
            GUILayout.EndHorizontal();
        }
        /*
         * Type of KingdomTask, Manages KingdomEvent
         */
        [HarmonyPatch(typeof(KingdomTaskEvent), "SkipPlayerTime", MethodType.Getter)]
        static class KingdomTaskEvent_SkipPlayerTime_Patch
        {
            static void Postfix(ref int __result)
            {
                try
                {
                    if (!enabled) return;
                    if (__result < 1) return;
                    if (settings.skipPlayerTime)
                    {
                        __result = 0;
                    }
                    else
                    {
                        __result = Mathf.RoundToInt(__result * settings.baronTimeFactor);
                        __result = __result < 1 ? 1 : __result;
                    }
                } catch(Exception ex)
                {
                    DebugError(ex);
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
                    if (!enabled) return;
                    if (__instance.EventBlueprint.IsResolveByBaron) return;
                    if (__instance.EventBlueprint is BlueprintKingdomEvent)
                    {
                        __result = Mathf.RoundToInt(__result * settings.eventTimeFactor);
                        __result = __result < 1 ? 1 : __result;
                    }
                    var projectBlueprint = __instance.EventBlueprint as BlueprintKingdomProject;
                    if (projectBlueprint != null && projectBlueprint.SpendRulerTimeDays > 0)
                    {
                        __result = Mathf.RoundToInt(__result * settings.baronTimeFactor);
                        __result = __result < 1 ? 1 : __result;
                    }
                    if (projectBlueprint != null && projectBlueprint.SpendRulerTimeDays <= 0)
                    {
                        __result = Mathf.RoundToInt(__result * settings.projectTimeFactor);
                        __result = __result < 1 ? 1 : __result;
                    }
                }
                catch (Exception ex)
                {
                    DebugError(ex);
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
                    if (!enabled) return;
                    __result = Mathf.RoundToInt(__result * settings.eventPriceFactor);
                } catch(Exception ex)
                {
                    DebugError(ex);
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
                    if (!enabled) return;
                    if (!settings.easyEvents) return;
                    __result = -100;
                } catch(Exception ex)
                {
                    DebugError(ex);
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
                    if (!enabled) return;
                    if (!settings.alwaysManageKingdom) return;
                    __result = true;
                }
                catch (Exception ex)
                {
                    DebugError(ex);
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
                    if (!enabled) return;
                    if (!settings.alwaysAdvanceTime) return;
                    __result = true;
                }
                catch (Exception ex)
                {
                    DebugError(ex);
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
                    if (!enabled) return;
                    if (!settings.alwaysBaronProcurement) return;
                    __result = true;
                }
                catch (Exception ex)
                {
                    DebugError(ex);
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
                    if (!enabled) return true;
                    if (!settings.overrideIgnoreEvents) return true;
                    return false;
                } catch(Exception ex)
                {
                    DebugError(ex);
                    return true;
                }
            }
        }
        [HarmonyPatch(typeof(KingdomTimelineManager), "AutoAssignLeaders")]
        static class KingdomTimelineManager_AutoAssignLeaders_Patch
        {
            static bool Prefix()
            {
                if (!enabled) return true;
                if (!settings.disableAutoAssignLeaders) return true;
                return false;
            }
        }
    }
}
