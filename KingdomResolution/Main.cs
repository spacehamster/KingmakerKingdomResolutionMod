using UnityModManagerNet;
using System;
using System.Reflection;
using UnityEngine;
using Kingmaker.Kingdom;
using System.Linq;
using Kingmaker.UI.SettingsUI;
using Kingmaker.Blueprints;
using Kingmaker.UI.Tooltip;
using HarmonyLib;

namespace KingdomResolution
{
#if DEBUG
    [EnableReloading]
#endif
    public class Main
    {
        public static UnityModManagerNet.UnityModManager.ModEntry.ModLogger logger;
        [System.Diagnostics.Conditional("DEBUG")]
        public static void Log(string msg)
        {
            if (logger != null) logger.Log(msg);
        }
        public static void Error(Exception ex)
        {
            if (logger != null) logger.Log(ex.ToString() + "\n" + ex.StackTrace);
        }
        public static bool enabled;
        public static Settings settings;
        static string modId;
        static int SavedCustomLeaderPenalty;
        static Harmony HarmonyInstance;
        static bool Load(UnityModManager.ModEntry modEntry)
        {
            try
            {
                logger = modEntry.Logger;
                modId = modEntry.Info.Id;
                settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
                HarmonyInstance = new Harmony(modEntry.Info.Id);
                HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
                modEntry.OnToggle = OnToggle;
                modEntry.OnGUI = OnGUI;
                modEntry.OnSaveGUI = OnSaveGUI;
#if DEBUG
                modEntry.OnUnload = Unload;
#endif
                KingdomStash.Init();
            }
            catch (Exception ex)
            {
                Error(ex);
                throw ex;
            }
            return true;
        }
        static bool Unload(UnityModManager.ModEntry modEntry)
        {
            HarmonyInstance.UnpatchAll(modId);
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
                GUIHelper.Toggle(ref settings.disableMercenaryPenalty, Labels.DisableMercenaryPenaltyLabel, Labels.DisableMercenaryPenaltyTooltip);
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
                Error(ex);
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
                if (instance.Unrest != KingdomStatusType.Crumbling)
                {
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
    }
}
