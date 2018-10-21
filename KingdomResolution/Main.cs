using Harmony12;
using UnityModManagerNet;
using System.Reflection;
using System;
using Debug = System.Diagnostics.Debug;
using System.Diagnostics;
using Kingmaker.Kingdom.Tasks;

namespace KingdomResolution
{

    public class Main
    {
        [System.Diagnostics.Conditional("DEBUG")]
        private static void DebugLog(string msg)
    => Debug.WriteLine(nameof(KingdomResolution) + ": " + msg);

        public static bool enabled;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            Debug.Listeners.Add(new TextWriterTraceListener("Mods/KingdomResolution/KingdomResolution.log"));
            Debug.AutoFlush = true;

            var harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            modEntry.OnToggle = OnToggle;
            return true;
        }
        // Called when the mod is turned to on/off.
        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value /* active or inactive */)
        {
            enabled = value;
            return true; // Permit or not.
        }
        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
        }
        [HarmonyPatch(typeof(KingdomEvent), "CalculateRulerTime")]
        static class KingdomEvent_CalculateRulerTime_Patch
        {
            static bool Prefix(ref int __result)
            {
                if (!enabled) return true;
                __result = 0;
                return false;
            }
        }
        [HarmonyPatch(typeof(KingdomEvent), "CalculateResolutionTime")]
        static class KingdomEvent_CalculateResolutionTime_Patch
        {
            static bool Prefix(KingdomEvent __instance, ref int __result)
            {
                if (!enabled) return true;
                if (__instance.EventBlueprint.NeedToVisitTheThroneRoom) return true;
                __result = 1;
                return false;
            }
        }
    }
}
