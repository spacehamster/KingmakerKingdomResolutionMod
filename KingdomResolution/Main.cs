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
using Kingmaker.Kingdom.UI;
using TMPro;
using Kingmaker.UI.Kingdom;
using Kingmaker.UnitLogic.Alignments;
using Kingmaker.Enums;

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

        public static bool enabled;
        static Settings settings;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
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
                ChooseFactor("Event Time Factor ", settings.eventTimeFactor, 1, (value) => settings.eventTimeFactor = (float)Math.Round(value, 2), percentFormatter);
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
                if (settings.skipBaron)
                {
                    __result = 0;
                }
                else
                {
                    __result = Mathf.RoundToInt(__instance.Event.CalculateRulerTime() * settings.baronTimeFactor);
                }                
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
                if (__instance.EventBlueprint.IsResolveByBaron) return true;
                int resolutionTime = __instance.EventBlueprint.ResolutionTime;
                var timeModifier = Traverse.Create(__instance).Method("GetTimeModifier").GetValue<float>();
                if (__instance.EventBlueprint is BlueprintKingdomEvent)
                {
                    __result = Mathf.RoundToInt((float)resolutionTime * (1f + timeModifier) * settings.eventTimeFactor);
                    __result = __result < 1 ? 1 : __result;
                    return false;
                }
                if (__instance.EventBlueprint is BlueprintKingdomProject && __instance.CalculateRulerTime() > 0)
                {
                    __result = Mathf.RoundToInt((float)resolutionTime * (1f + timeModifier) * settings.baronTimeFactor);
                    __result = __result < 1 ? 1 : __result;
                    return false;
                }
                if (__instance.EventBlueprint is BlueprintKingdomProject)
                {
                    __result = Mathf.RoundToInt((float)resolutionTime * (1f + timeModifier) * settings.projectTimeFactor);
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
        [HarmonyPatch(typeof(KingdomUIEventWindow), "SetHeader")]
        static class KingdomUIEventWindow_SetHeader_Patch
        {
            static void Postfix(KingdomUIEventWindow __instance, KingdomEventUIView kingdomEventView)
            {
                if (!enabled) return;
                if (!settings.previewResults) return;
                if(kingdomEventView.Task == null)
                {
                    return; //Task is null on event results;
                }
                var solutionText = Traverse.Create(__instance).Field("m_Description").GetValue<TextMeshProUGUI>();
                solutionText.text += "\n";
                var leader = kingdomEventView.Task.AssignedLeader;
                if (leader == null)
                {
                    solutionText.text += "<size=75%>Select a leader to preview results</size>";
                    return;
                }
                var blueprint = kingdomEventView.Blueprint;
                var solutions = blueprint.Solutions;
                var resolutions = solutions.GetResolutions(leader.Type);
                solutionText.text += "<size=75%>";
                foreach (var eventResult in resolutions)
                {
                    var alignmentMask = leader.LeaderSelection.Alignment.ToMask();
                    var text = "";
                    bool invalid = (alignmentMask & eventResult.LeaderAlignment) == AlignmentMaskType.None;
                    //invalid |= !(eventResult.Condition == null && eventResult.Condition.Check(blueprint));
                    if(invalid) text += "<color=#808080>";
                    var statChanges = eventResult.StatChanges.ToStringWithPrefix(" ");
                    text += string.Format("{0}:{1}",
                        eventResult.Margin,
                        statChanges == "" ? " No Change" : statChanges);
                    //TODO: Human readable action names
                    var actions = eventResult.Actions.Actions.Join((action) => action.name, ", ");
                    if (actions != "") text += ". Actions: " + actions; 
                    if (invalid) text += "</color>";
                    text += "\n";
                    DebugLog(text);
                    solutionText.text += text;
                }
                solutionText.text += "</size>";
            }
        }
    }
}
