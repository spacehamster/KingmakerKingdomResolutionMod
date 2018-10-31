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
        public static UnityModManager.ModEntry.ModLogger logger;
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
            try
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
            } catch(Exception e)
            {
                modEntry.Logger.Log(e.ToString() + "\n" + e.StackTrace);
                throw e;
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
            settings.skipTasks = GUILayout.Toggle(settings.skipTasks, "Enable 1 Day Tasks ", GUILayout.ExpandWidth(false));
            settings.skipProjects = GUILayout.Toggle(settings.skipProjects, "Enable 1 Day Projects ", GUILayout.ExpandWidth(false));
            settings.skipBaron = GUILayout.Toggle(settings.skipBaron, "Enable 1 Day Baron Projects ", GUILayout.ExpandWidth(false));
            settings.alwaysInsideKingdom = GUILayout.Toggle(settings.alwaysInsideKingdom, "Always Inside Kingdom  ", GUILayout.ExpandWidth(false));
            settings.overrideIgnoreEvents = GUILayout.Toggle(settings.overrideIgnoreEvents, "Disable End of Month Failed Events  ", GUILayout.ExpandWidth(false));
            settings.easyEvents = GUILayout.Toggle(settings.easyEvents, "Enable Easy Events  ", GUILayout.ExpandWidth(false));
            settings.previewResults = GUILayout.Toggle(settings.previewResults, "Preview Event Results  ", GUILayout.ExpandWidth(false));
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
