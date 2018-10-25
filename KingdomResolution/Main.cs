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
        [System.Diagnostics.Conditional("DEBUG")]
        private static void DebugLog(string msg)
    => Debug.WriteLine(nameof(KingdomResolution) + ": " + msg);

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
            settings.skipTasks = GUILayout.Toggle(settings.skipTasks, "Enable 1 Day Tasks ", GUILayout.ExpandWidth(false));
            settings.skipProjects = GUILayout.Toggle(settings.skipProjects, "Enable 1 Day Projects ", GUILayout.ExpandWidth(false));
            settings.skipBaron = GUILayout.Toggle(settings.skipBaron, "Enable 1 Day Baron Projects ", GUILayout.ExpandWidth(false));
            settings.alwaysInsideKingdom = GUILayout.Toggle(settings.alwaysInsideKingdom, "Always Inside Kingdom  ", GUILayout.ExpandWidth(false));
            settings.overrideIgnoreEvents = GUILayout.Toggle(settings.overrideIgnoreEvents, "Disable End of Month Failed Events  ", GUILayout.ExpandWidth(false));
            GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            GUILayout.Label("Event DC Modifier ", GUILayout.ExpandWidth(false));
            settings.DCModifier = (int)GUILayout.HorizontalSlider(settings.DCModifier, -100, 100, GUILayout.Width(300f));
            GUILayout.Label(" " + settings.DCModifier, GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
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
                if (__instance.EventBlueprint.NeedToVisitTheThroneRoom) return true;
                if (settings.skipTasks && __instance.EventBlueprint is BlueprintKingdomEvent)
                {
                    __result = 1;
                    return false;
                }
                if (settings.skipProjects && __instance.EventBlueprint is BlueprintKingdomProject)
                {
                    __result = 1;
                    return false;
                }
                if (settings.skipBaron && __instance.EventBlueprint is BlueprintKingdomProject && __instance.CalculateRulerTime() > 0)
                {
                    __result = 1;
                    return false;
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(KingdomEvent), "DCModifier", MethodType.Getter)]
        static class KingdomEvent_DCModifier_Patch
        {
            static bool Prefix(ref int __result)
            {
                if (!enabled) return true;
                __result = settings.DCModifier;
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
    }
}
