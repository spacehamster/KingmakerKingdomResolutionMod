using Harmony12;
using UnityModManagerNet;
using System.Reflection;
using System;
using System.Linq;
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
using UnityEngine.UI;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.Controllers.Dialog;
using static UnityModManagerNet.UnityModManager;
using Kingmaker.DialogSystem.Blueprints;
using System.Collections.Generic;
using Kingmaker.Utility;
using Kingmaker.ElementsSystem;

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
            try
            {
                logger = modEntry.Logger;
                settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
                var harmony = HarmonyInstance.Create(modEntry.Info.Id);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                modEntry.OnToggle = OnToggle;
                modEntry.OnGUI = OnGUI;
                modEntry.OnSaveGUI = OnSaveGUI;
            } catch(Exception ex)
            {
                DebugLog(ex.ToString() + "\n" + ex.StackTrace);
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
                Func<float, string> percentFormatter = (value) => Math.Round(value * 100, 0) == 0 ? " 1 day" : Math.Round(value * 100, 0) + " %";
                ChooseFactor("Event Time Factor ", settings.eventTimeFactor, 1, (value) => settings.eventTimeFactor = (float)Math.Round(value, 2), percentFormatter);
                ChooseFactor("Project Time Factor ", settings.projectTimeFactor, 1, (value) => settings.projectTimeFactor = (float)Math.Round(value, 2), percentFormatter);
                ChooseFactor("Baron Project Time Factor ", settings.baronTimeFactor, 1, (value) => settings.baronTimeFactor = (float)Math.Round(value, 2), percentFormatter);
                ChooseFactor("Event BP Price Factor ", settings.eventPriceFactor, 1,
                    (value) => settings.eventPriceFactor = (float)Math.Round(value, 2), (value) => Math.Round(Math.Round(value, 2) * 100, 0) + " %");
                settings.skipBaron = GUILayout.Toggle(settings.skipBaron, "Disable baron skip time ", GUILayout.ExpandWidth(false));
                settings.alwaysInsideKingdom = GUILayout.Toggle(settings.alwaysInsideKingdom, "Always Inside Kingdom  ", GUILayout.ExpandWidth(false));
                settings.overrideIgnoreEvents = GUILayout.Toggle(settings.overrideIgnoreEvents, "Disable End of Month Failed Events  ", GUILayout.ExpandWidth(false));
                settings.easyEvents = GUILayout.Toggle(settings.easyEvents, "Enable Easy Events  ", GUILayout.ExpandWidth(false));
                settings.previewEventResults = GUILayout.Toggle(settings.previewEventResults, "Preview Event Results  ", GUILayout.ExpandWidth(false));
                settings.previewDialogResults = GUILayout.Toggle(settings.previewDialogResults, "Preview Dialog Results  ", GUILayout.ExpandWidth(false));
                ChooseKingdomUnreset();
            }
            catch (Exception ex)
            {
                DebugLog(ex.ToString() + "\n" + ex.StackTrace);
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
                    instance.SetUnrest(instance.Unrest - 1);
                }
            }
            if (GUILayout.Button("Less Unrest"))
            {
                if (instance.Unrest == KingdomStatusType.Metastable) return;
                instance.SetUnrest(instance.Unrest + 1);
            }
            GUILayout.EndHorizontal();
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
            static void Postfix(KingdomTaskEvent __instance, ref int __result)
            {
                if (!enabled) return;
                if (settings.skipBaron)
                {
                    __result = 0;
                }
                else
                {
                    __result = Mathf.RoundToInt(__result * settings.baronTimeFactor);
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
                if (!enabled) return;
                if (__instance.EventBlueprint.IsResolveByBaron) return;
                int resolutionTime = __instance.EventBlueprint.ResolutionTime;
                var timeModifier = Traverse.Create(__instance).Method("GetTimeModifier").GetValue<float>();
                if (__instance.EventBlueprint is BlueprintKingdomEvent)
                {
                    __result = Mathf.RoundToInt(__result * settings.eventTimeFactor);
                    __result = __result < 1 ? 1 : __result;
                }
                if (__instance.EventBlueprint is BlueprintKingdomProject && __instance.CalculateRulerTime() > 0)
                {
                    __result = Mathf.RoundToInt(__result * settings.baronTimeFactor);
                    __result = __result < 1 ? 1 : __result;
                }
                if (__instance.EventBlueprint is BlueprintKingdomProject)
                {
                    __result = Mathf.RoundToInt(__result * settings.projectTimeFactor);
                    __result = __result < 1 ? 1 : __result;
                }
            }
        }
        [HarmonyPatch(typeof(KingdomTaskEvent), "GetDC")]
        static class KingdomTaskEvent_GetDC_Patch
        {
            static void Postfix(ref int __result)
            {
                if (!enabled) return;
                if (!settings.easyEvents) return;
                __result = -100;
            }
        }
        [HarmonyPatch(typeof(KingdomState), "IsPartyInsideKingdom", MethodType.Getter)]
        static class KingdomState_IsPartyInsideKingdom_Patch
        {
            static void Postfix(ref bool __result)
            {
                if (!enabled) return;
                if (!settings.alwaysInsideKingdom) return;
                __result = true;
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
            static void Postfix(KingdomEvent __instance, ref int __result)
            {
                if (!enabled) return;
                __result = Mathf.RoundToInt(__result * settings.eventPriceFactor);
                return;
            }
        }
        static string FormatResult(EventResult eventResult, EventResult[] eventResults, BlueprintKingdomEvent eventBlueprint = null)
        {
            string text = "";
            //var statChangesText = CalculateStatChanges(eventResults, eventResult, eventBlueprint).ToStringWithPrefix(" ");
            var statChangesText = eventResult.StatChanges.ToStringWithPrefix(" ");
            text += string.Format("{0}:{1}",
                eventResult.Margin,
                statChangesText == "" ? " No Change" : statChangesText);
            //TODO: Solution for presenting actions
            //var actions = eventResult.Actions.Actions.Where((action) => action.GetType() != typeof(Conditional)).Join((action) => action.GetType().Name, ", ");
            //if (actions != "") text += ". Actions: " + actions;
            text += "\n";
            return text;
        }
        static List<String> ResolveConditional(Conditional conditional)
        {
            var actionList = conditional.ConditionsChecker.Check(null) ? conditional.IfTrue : conditional.IfFalse;
            var result = new List<String>();
            foreach(var action in actionList.Actions)
            {
                result.AddRange(FormatAction(action));
            }
            return result;
        }
        static List<string> FormatAction(GameAction action)
        {
            if(action is Conditional)
            {
                return ResolveConditional(action as Conditional);
            }
            var result = new List<string>();
            result.Add(action.GetCaption());
            return result;
        }
        /*
         * Note: answer.NextCue can have actions associated with it, should those be shown?
         */ 
        [HarmonyPatch(typeof(UIConsts), "GetAnswerString")]
        static class UIConsts_GetAnswerString_Patch
        {
            static void Postfix(ref string __result, BlueprintAnswer answer)
            {
                if (!settings.previewDialogResults) return;

                if (answer.OnSelect.HasActions)
                {
                    __result += " \n<size=75%>[" + answer.OnSelect.Actions.Join((action) => FormatAction(action).Join()) + "]</size>"; 
                }
            }
        }
#if (DEBUG)
        [HarmonyPatch(typeof(Kingmaker.UI.Dialog.DialogController), "HandleOnCueShow")]
        static class DialogController_HandleOnCueShow_Patch
        {
            static void Postfix(DialogController __instance, CueShowData data)
            {
                try
                {
                    DebugLog("Showing Cue " + data?.Cue?.name ?? "NULL");
                    DebugLog(data?.Cue?.DisplayText ?? "No Display Text");
                    if (data?.Cue?.Answers?.Count != null && data?.Cue?.Answers?.Count > 0)
                    {
                        DebugLog($"Answers {data?.Cue?.Answers?.Count}");
                    }
                    foreach (var answerBase in data.Cue.Answers)
                    {
                        var answers = new List<BlueprintAnswer>();
                        if(answerBase is BlueprintAnswersList)
                        {
                            var answersList = answerBase as BlueprintAnswersList;
                            foreach(var answer in answersList.Answers)
                            {
                                if (answer is BlueprintAnswer) answers.Add(answer as BlueprintAnswer);
                                else DebugLog($" Found {answer.GetType()} in AnswersList");
                            }
                        }
                        if (answerBase is BlueprintAnswer)
                        {
                            answers.Add(answerBase as BlueprintAnswer);         
                        }
                        foreach(var answer in answers)
                        {
                            DebugLog($" {answer?.name} - {answerBase?.ParentAsset?.name}");
                            DebugLog($" Text: {answer.Text}");
                            DebugLog($" Cues: {answer.NextCue.Cues.Count}");
                            foreach (var cue in answer.NextCue.Cues)
                            {
                                //DebugLog($"  Cue {cue.name}");
                            }
                        }
                    }
                    DebugLog($"Continue {data.Cue.Continue?.Cues?.Count}");
                    foreach (var cue in data.Cue.Continue.Cues)
                    {
                        DebugLog($" Continue: {cue.name} - {cue.GetType()}");
                        DebugLog($" {cue.ToString()}");
                    }
                    DebugLog($"OnShow {data?.Cue?.OnShow?.Actions?.Length}");
                    foreach (var action in data.Cue.OnShow.Actions)
                    {
                        DebugLog($" {action?.GetType()?.Name} - {action?.GetCaption()}");
                    }
                    DebugLog($"OnStop {data?.Cue?.OnShow?.Actions?.Length}");
                    foreach (var action in data?.Cue?.OnShow?.Actions)
                    {
                        DebugLog($" {action?.GetType()?.Name} - {action?.GetCaption()}");
                    }
                    DebugLog("");
                } catch(Exception ex)
                {
                    DebugLog(ex.ToString() + "\n" + ex.ToString());
                }
            }
        }
#endif
        [HarmonyPatch(typeof(KingdomUIEventWindow), "SetHeader")]
        static class KingdomUIEventWindow_SetHeader_Patch
        {
            static void Postfix(KingdomUIEventWindow __instance, KingdomEventUIView kingdomEventView)
            {
                if (!enabled) return;
                if (!settings.previewEventResults) return;
                if (kingdomEventView.Task == null)
                {
                    return; //Task is null on event results;
                }
                var solutionText = Traverse.Create(__instance).Field("m_Description").GetValue<TextMeshProUGUI>();
                //MakeTextScrollable(solutionText.transform.parent.GetComponent<RectTransform>());
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

                var alignmentMask = leader.LeaderSelection.Alignment.ToMask();
                Func<EventResult, bool> isValid = (result) => (alignmentMask & result.LeaderAlignment) != AlignmentMaskType.None;
                var validResults = from resolution in resolutions
                                   where isValid(resolution)
                                   select resolution;
                solutionText.text += "Leader " + leader.LeaderSelection.CharacterName + " - Alignment " + alignmentMask + "\n";
                foreach (var eventResult in validResults)
                {
                    solutionText.text += FormatResult(eventResult, resolutions, kingdomEventView.EventBlueprint);
                }
                int bestResult = 0;
                EventResult bestEventResult = null;
                LeaderType bestLeader = 0;
                foreach (var solution in solutions.Entries)
                {
                    foreach (var eventResult in solution.Resolutions)
                    {
                        int sum = 0;
                        for (int i = 0; i < 10; i++) sum += eventResult.StatChanges[(KingdomStats.Type)i];
                        if (sum > bestResult)
                        {
                            bestResult = sum;
                            bestLeader = solution.Leader;
                            bestEventResult = eventResult;
                        }
                    }
                }

                if (bestEventResult != null)
                {
                    solutionText.text += "<size=50%>\n<size=75%>";
                    solutionText.text += "Best Result: Leader " + bestLeader + " - Alignment " + bestEventResult.LeaderAlignment + "\n";
                    if (isValid(bestEventResult) && bestLeader == leader.Type)
                    {
                        solutionText.text += "<color=#308014>";
                    } else
                    {
                        solutionText.text += "<color=#808080>";
                    }

                    solutionText.text += FormatResult(bestEventResult, solutions.GetResolutions(bestLeader), kingdomEventView.EventBlueprint);
                    if (!isValid(bestEventResult))
                    {
                        solutionText.text += "</color>";
                    }
                }
                solutionText.text += "</size>";
            }
        }
    }
}
