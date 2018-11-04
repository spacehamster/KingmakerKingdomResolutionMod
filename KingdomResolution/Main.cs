using Harmony12;
using UnityModManagerNet;
using System.Reflection;
using System;
using Debug = System.Diagnostics.Debug;
using System.Diagnostics;
using Kingmaker.Kingdom.Tasks;
using UnityEngine;
using Kingmaker.Kingdom;
using Kingmaker.Kingdom.Blueprints;

namespace KingdomResolution
{

    public class Main
    {
        public static UnityModManagerNet.UnityModManager.ModEntry.ModLogger logger;
        [System.Diagnostics.Conditional("DEBUG")]
        public static void DebugLog(string msg)
        {
            Debug.WriteLine(nameof(KingdomResolution) + ": " + msg);
            if (logger != null) logger.Log(msg);
        }

        public static bool enabled;
        static Settings settings;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            Debug.Listeners.Add(new TextWriterTraceListener("Mods/KingdomResolution/KingdomResolution.log"));
            Debug.AutoFlush = true;
            settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            var harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            logger = modEntry.Logger;
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
                Func<float, string> percentFormatter = (value) => Math.Round(value * 100, 0) == 0 ? " 1 day" : Math.Round(value * 100, 0) + " %";
                ChooseFactor("Task Time Factor ", settings.eventTimeFactor, 1, (value) => settings.eventTimeFactor = (float)Math.Round(value, 2), percentFormatter);
                ChooseFactor("Project Time Factor ", settings.projectTimeFactor, 1, (value) => settings.projectTimeFactor = (float)Math.Round(value, 2), percentFormatter);
                ChooseFactor("Baron Project Time Factor ", settings.baronTimeFactor, 1, (value) => settings.baronTimeFactor = (float)Math.Round(value, 2), percentFormatter);
                ChooseFactor("Event BP Price Factor ", settings.eventPriceFactor, 1,
                    (value) => settings.eventPriceFactor = (float)Math.Round(value, 2), (value) => Math.Round(Math.Round(value, 2) * 100, 0) + " %");
                KingdomState instance = KingdomState.Instance;
                if (instance != null)
                {
                    ChooseFactor("Kingdom Unrest ", (float)instance.Unrest, 5,
                        (unrest) => instance.SetUnrest((KingdomStatusType)unrest),
                        (unrest) => (KingdomStatusType)unrest == KingdomStatusType.Metastable ? " Serene" : " " + (KingdomStatusType)unrest
                        );
                }

                settings.skipBaron = GUILayout.Toggle(settings.skipBaron, "Disable baron skip time ", GUILayout.ExpandWidth(false));
                settings.alwaysInsideKingdom = GUILayout.Toggle(settings.alwaysInsideKingdom, "Always Inside Kingdom  ", GUILayout.ExpandWidth(false));
                settings.overrideIgnoreEvents = GUILayout.Toggle(settings.overrideIgnoreEvents, "Disable End of Month Failed Events  ", GUILayout.ExpandWidth(false));
                settings.easyEvents = GUILayout.Toggle(settings.easyEvents, "Enable Easy Events  ", GUILayout.ExpandWidth(false));


            } catch(Exception ex)
            {
                DebugLog(ex.ToString() + "\n" + ex.StackTrace);
                throw ex;
            }
        }
        static void ChooseFactor(string label, float value, float maxValue, Action<float> setter, Func<float, string> formatter)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(200));
            var newValue = GUILayout.HorizontalSlider(value, 0, maxValue, GUILayout.Width(300));
            GUILayout.Label(formatter(newValue));
            GUILayout.EndHorizontal();
            if (newValue != value)
            {
                setter(newValue);
            }
            ;
        }
        /*
         * Type of KingdomTask, Manages KingdomEvent
         */
        [HarmonyPatch(typeof(KingdomTaskEvent), "SkipPlayerTime", MethodType.Getter)]
        static class KingdomTaskEvent_SkipPlayerTime_Patch
        {
            static bool Prefix(KingdomTaskEvent __instance, ref int __result)
            {
                if (!enabled) return true;
                if (!settings.skipBaron) return true;
                __result = 0;
                return false;
            }
        }
        /*
         * Represents BlueprintKingdomEventBase
         * BlueprintKingdomEventBase has Concrete Types BlueprintKingdomEvent, BlueprintKingdomProject and BlueprintKingdomClaim
         */
        [HarmonyPatch(typeof(KingdomEvent), "CalculateResolutionTime")]
        static class KingdomEvent_CalculateResolutionTime_Patch
        {
            static bool Prefix(KingdomEvent __instance, ref int __result)
            {
                if (!enabled) return true;
                //Refer KingdomUIEventWindowFooter.CanGoThroneRoom
                if (__instance.EventBlueprint.NeedToVisitTheThroneRoom && __instance.AssociatedTask == null) return true;
                int resolutionTime = __instance.EventBlueprint.ResolutionTime;
                var timeModifier = Traverse.Create(__instance).Method("GetTimeModifier").GetValue<float>();
                if (__instance.EventBlueprint is BlueprintKingdomEvent)
                {
                    __result = Mathf.RoundToInt((float)resolutionTime * (1f + timeModifier) * settings.eventTimeFactor);
                    __result = __result < 1 ? 1 : __result;
                    return false;
                }
                if (__instance.EventBlueprint is BlueprintKingdomProject)
                {
                    __result = Mathf.RoundToInt((float)resolutionTime * (1f + timeModifier) * settings.eventTimeFactor);
                    __result = __result < 1 ? 1 : __result;
                    return false;
                }
                if (__instance.EventBlueprint is BlueprintKingdomProject && __instance.CalculateRulerTime() > 0)
                {
                    __result = Mathf.RoundToInt((float)resolutionTime * (1f + timeModifier) * settings.eventTimeFactor);
                    __result = __result < 1 ? 1 : __result;
                    return false;
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(KingdomTaskEvent), "GetDC")]
        static class KingdomTaskEvent_GetDC_Patch
        {
            static bool Prefix(ref int __result)
            {
                if (!enabled) return true;
                if (!settings.easyEvents) return true;
                __result = -100;
                return false;
            }
        }
        [HarmonyPatch(typeof(KingdomState), "IsPartyInsideKingdom", MethodType.Getter)]
        static class KingdomState_IsPartyInsideKingdom_Patch
        {
            static bool Prefix(ref bool __result)
            {
                if (!enabled) return true;
                if (!settings.alwaysInsideKingdom) return true;
                __result = true;
                return false;
            }
        }
        [HarmonyPatch(typeof(KingdomTimelineManager), "FailIgnoredEvents")]
        static class KingdomTimelineManager_FailIgnoredEvents_Patch
        {
            static bool Prefix()
            {
                if (!enabled) return true;
                if (!settings.overrideIgnoreEvents) return true;
                return false;
            }
        }
        [HarmonyPatch(typeof(KingdomEvent), "CalculateBPCost")]
        static class KingdomTimelineManager_CalculateBPCost_Patch
        {
            static bool Prefix(KingdomEvent __instance, ref int __result)
            {
                if (!enabled) return true;
                BlueprintKingdomProject blueprintKingdomProject = __instance.EventBlueprint as BlueprintKingdomProject;
                int bpCost = blueprintKingdomProject != null ? blueprintKingdomProject.ProjectStartBPCost : 0;
                if (__instance.EventBlueprint is BlueprintKingdomClaim || __instance.EventBlueprint is BlueprintKingdomUpgrade)
                {
                    bpCost = Mathf.RoundToInt(bpCost * (1f + KingdomState.Instance.ClaimCostModifier) * settings.eventPriceFactor);
                }
                __result = bpCost;
                return false;
            }
        }
    }
}
