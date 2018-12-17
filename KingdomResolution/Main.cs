using Harmony12;
using UnityModManagerNet;
using System.Reflection;
using System;
using System.Linq;
using Kingmaker.Kingdom.Tasks;
using UnityEngine;
using Kingmaker.Kingdom;
using Kingmaker.Kingdom.Blueprints;
using Kingmaker.Kingdom.UI;
using TMPro;
using Kingmaker.UI.Kingdom;
using Kingmaker.UnitLogic.Alignments;
using Kingmaker.Enums;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.DialogSystem.Blueprints;
using System.Collections.Generic;
using Kingmaker.Utility;
using Kingmaker.ElementsSystem;
using Kingmaker;
using Kingmaker.Blueprints.Root.Strings;
using Kingmaker.UI.SettingsUI;
using Kingmaker.DialogSystem;
using Kingmaker.UI.Common;
using Kingmaker.Blueprints.Root;
using Kingmaker.UI.Tooltip;
using Kingmaker.UI.Dialog;

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
        static Settings settings;
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
                Func<float, string> percentFormatter = (value) => Math.Round(value * 100, 0) == 0 ? " 1 day" : Math.Round(value * 100, 0) + " %";
                ChooseFactor("Event Time Factor ", settings.eventTimeFactor, 1, (value) => settings.eventTimeFactor = (float)Math.Round(value, 2), percentFormatter);
                ChooseFactor("Project Time Factor ", settings.projectTimeFactor, 1, (value) => settings.projectTimeFactor = (float)Math.Round(value, 2), percentFormatter);
                ChooseFactor("Ruler Managed Project Time Factor ", settings.baronTimeFactor, 1, (value) => settings.baronTimeFactor = (float)Math.Round(value, 2), percentFormatter);
                ChooseFactor("Event BP Price Factor ", settings.eventPriceFactor, 1,
                    (value) => settings.eventPriceFactor = (float)Math.Round(value, 2), (value) => Math.Round(Math.Round(value, 2) * 100, 0) + " %");
                settings.skipPlayerTime = GUILayout.Toggle(settings.skipPlayerTime, "Disable Skip Player Time ", GUILayout.ExpandWidth(false));
                settings.alwaysManageKingdom = GUILayout.Toggle(settings.alwaysManageKingdom, "Enable Manage Kingdom Everywhere ", GUILayout.ExpandWidth(false));
                settings.alwaysBaronProcurement = GUILayout.Toggle(settings.alwaysBaronProcurement, "Enable Ruler Procure Rations Everywhere (DLC Only) ", GUILayout.ExpandWidth(false));
                settings.overrideIgnoreEvents = GUILayout.Toggle(settings.overrideIgnoreEvents, "Disable End of Month Failed Events  ", GUILayout.ExpandWidth(false));
                settings.easyEvents = GUILayout.Toggle(settings.easyEvents, "Enable Easy Events  ", GUILayout.ExpandWidth(false));
                settings.previewEventResults = GUILayout.Toggle(settings.previewEventResults, "Preview Event Results  ", GUILayout.ExpandWidth(false));
                settings.previewDialogResults = GUILayout.Toggle(settings.previewDialogResults, "Preview Dialog Results  ", GUILayout.ExpandWidth(false));
                settings.previewAlignmentRestrictedDialog = GUILayout.Toggle(settings.previewAlignmentRestrictedDialog, "Preview Alignment Restricted Dialog  ", GUILayout.ExpandWidth(false));
                ChooseKingdomUnreset();
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
                try
                {
                    if (!enabled) return;
                    if (settings.skipPlayerTime)
                    {
                        __result = 0;
                    }
                    else
                    {
                        __result = Mathf.RoundToInt(__result * settings.baronTimeFactor);
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
                    else if (__instance.EventBlueprint is BlueprintKingdomProject && __instance.CalculateRulerTime() > 0)
                    {
                        __result = Mathf.RoundToInt(__result * settings.baronTimeFactor);
                        __result = __result < 1 ? 1 : __result;
                    }
                    else if (__instance.EventBlueprint is BlueprintKingdomProject)
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
            var caption = action.GetCaption();
            caption = caption == "" || caption == null ? action.GetType().Name : caption;
            result.Add(caption);
            return result;
        }
        static List<Tuple<BlueprintCueBase, int, GameAction[], AlignmentShift>> CollateAnswerData(BlueprintAnswer answer, out bool isRecursive)
        {
            var cueResults = new List<Tuple<BlueprintCueBase, int, GameAction[], AlignmentShift>>();
            var toCheck = new Queue<Tuple<BlueprintCueBase, int>>();
            isRecursive = false;
            if (answer.NextCue.Cues.Count > 0)
            {
                toCheck.Enqueue(new Tuple<BlueprintCueBase, int>(answer.NextCue.Cues[0], 1));
            }
            cueResults.Add(new Tuple<BlueprintCueBase, int, GameAction[], AlignmentShift>(
                answer.ParentAsset as BlueprintCueBase,
                0,
                answer.OnSelect.Actions,
                answer.AlignmentShift
                ));
            while (toCheck.Count > 0)
            {
                var item = toCheck.Dequeue();
                var cueBase = item.Item1;
                int currentDepth = item.Item2;
                if (currentDepth > 20) break;
                if (cueBase is BlueprintCue cue)
                {
                    cueResults.Add(new Tuple<BlueprintCueBase, int, GameAction[], AlignmentShift>(
                        cue, 
                        currentDepth,
                        cue.OnShow.Actions.Concat(cue.OnStop.Actions).ToArray(),
                        cue.AlignmentShift
                        ));
                    if (cue.Answers.Count > 0)
                    {
                        if (cue.Answers[0] == answer.ParentAsset) isRecursive = true;
                        break;
                    }
                    if (cue.Continue.Cues.Count > 0)
                    {
                        toCheck.Enqueue(new Tuple<BlueprintCueBase, int>(cue.Continue.Cues[0], currentDepth + 1));
                    }
                }
                else if (cueBase is BlueprintBookPage page)
                {
                    cueResults.Add(new Tuple<BlueprintCueBase, int, GameAction[], AlignmentShift>(
                        page,
                        currentDepth,
                        page.OnShow.Actions,
                        null
                        ));
                    if (page.Answers.Count > 0)
                    {
                        if (page.Answers[0] == answer.ParentAsset) isRecursive = true;
                        break;
                    }
                    if (page.Cues.Count > 0)
                    {
                        foreach (var c in page.Cues) toCheck.Enqueue(new Tuple<BlueprintCueBase, int>(c, currentDepth + 1));
                    }
                }
                else if (cueBase is BlueprintCheck check)
                {
                    toCheck.Enqueue(new Tuple<BlueprintCueBase, int>(check.Success, currentDepth + 1));
                    toCheck.Enqueue(new Tuple<BlueprintCueBase, int>(check.Fail, currentDepth + 1));
                }
                else if (cueBase is BlueprintCueSequence sequence)
                {
                    foreach (var c in sequence.Cues) toCheck.Enqueue(new Tuple<BlueprintCueBase, int>(c, currentDepth + 1));
                    if(sequence.Exit != null)
                    {
                        var exit = sequence.Exit;
                        if (exit.Answers.Count > 0)
                        {
                            if (exit.Answers[0] == answer.ParentAsset) isRecursive = true;
                            break;
                        }
                        if (exit.Continue.Cues.Count > 0)
                        {
                            toCheck.Enqueue(new Tuple<BlueprintCueBase, int>(exit.Continue.Cues[0], currentDepth + 1));
                        }
                    }
                }
                else
                {
                    break;
                }
            }
            return cueResults;
        }
        public static string GetFixedAnswerString(BlueprintAnswer answer, string bind, int index)
        {
            bool flag = Game.Instance.DialogController.Dialog.Type == DialogType.Book;
            string checkFormat = (!flag) ? UIDialog.Instance.AnswerStringWithCheckFormat : UIDialog.Instance.AnswerStringWithCheckBeFormat;
            string text = string.Empty;
            if (SettingsRoot.Instance.ShowSkillcheksDC.CurrentValue)
            {
                text = answer.SkillChecks.Aggregate(string.Empty, (string current, CheckData skillCheck) => current + string.Format(checkFormat, UIUtility.PackKeys(new object[]
                {
                    TooltipType.SkillcheckDC,
                    skillCheck.Type
                }), LocalizedTexts.Instance.Stats.GetText(skillCheck.Type), skillCheck.DC));
            }
            if (SettingsRoot.Instance.ShowAliggnmentRequirements.CurrentValue && answer.AlignmentRequirement != AlignmentComponent.None)
            {
                text = string.Format(UIDialog.Instance.AlignmentRequirementFormat, UIUtility.GetAlignmentRequirementText(answer.AlignmentRequirement)) + text;
            }
            if (answer.HasShowCheck)
            {
                text = string.Format(UIDialog.Instance.AnswerShowCheckFormat, LocalizedTexts.Instance.Stats.GetText(answer.ShowCheck.Type), text);
            }
            if (SettingsRoot.Instance.ShowAlignmentShiftsInAnswer.CurrentValue && answer.AlignmentRequirement == AlignmentComponent.None && answer.AlignmentShift.Value > 0 && SettingsRoot.Instance.ShowAlignmentShiftsInAnswer.CurrentValue)
            {
                text = string.Format(UIDialog.Instance.AligmentShiftedFormat, UIUtility.GetAlignmentShiftDirectionText(answer.AlignmentShift.Direction)) + text;
            }
            string stringByBinding = UIKeyboardTexts.Instance.GetStringByBinding(Game.Instance.Keyboard.GetBindingByName(bind));
            return string.Format(UIDialog.Instance.AnswerDialogueFormat, 
                (!stringByBinding.Empty()) ? stringByBinding : index.ToString(), 
                text + ((!text.Empty<char>()) ? " " : string.Empty) + answer.DisplayText);
        }
        [HarmonyPatch(typeof(UIConsts), "GetAnswerString")]
        static class UIConsts_GetAnswerString_Patch
        {
            static void Postfix(ref string __result, BlueprintAnswer answer, string bind, int index)
            {
                try
                {
                    if (settings.previewAlignmentRestrictedDialog && !answer.IsAlignmentRequirementSatisfied)
                    {
                        __result = GetFixedAnswerString(answer, bind, index);
                    }
                    if (!settings.previewDialogResults) return;
                    var answerData = CollateAnswerData(answer, out bool isRecursive);
                    if (isRecursive)
                    {
                        __result += $" \n<size=75%>[Repeats]</size>";
                    }
                    foreach (var data in answerData)
                    {
                        var cue = data.Item1;
                        var depth = data.Item2;
                        var actions = data.Item3;
                        var alignment = data.Item4;
                        if (actions.Length > 0)
                        {
                            var actionText = actions.Join((action) => FormatAction(action).Join());
                            if (actionText == "") actionText = "EmptyAction";
                            __result += $" \n<size=75%>[{depth}: {actionText}]</size>";
                        }
                        if(alignment != null && alignment.Value > 0)
                        {
                            __result += $" \n<size=75%>[{depth}: AlignmentShift({alignment.Direction}, {alignment.Value}, {alignment.Description})]";
                        }
                        if (cue is BlueprintCheck check)
                        {
                            __result += $" \n<size=75%>[{depth}: {check.Type} check, DC {check.DC}, hidden {check.Hidden}]</size>";
                        }
                    }
                } catch(Exception ex)
                {
                    DebugError(ex);
                }
            }
        }
        [HarmonyPatch(typeof(DialogCurrentPart), "Fill")]
        static class DialogCurrentPart_Fill_Patch
        {
            static void Postfix(DialogCurrentPart __instance)
            {
                try
                {
                    var cue = Game.Instance.DialogController.CurrentCue;
                    var actions = cue.OnShow.Actions.Concat(cue.OnStop.Actions).ToArray();
                    var alignment = cue.AlignmentShift;
                    var text = "";
                    if (actions.Length > 0)
                    {
                        var actionText = actions.Join((action) => FormatAction(action).Join());
                        if (actionText == "") actionText = "EmptyAction";
                        text += $" \n<size=75%>[ {actionText}]</size>";
                    }
                    if (alignment != null && alignment.Value > 0)
                    {
                        text += $" \n<size=75%>[ AlignmentShift {alignment.Direction} by {alignment.Value} - {alignment.Description}]";
                    }
                    __instance.DialogPhrase.text += text;
                }
                catch (Exception ex)
                {
                    DebugError(ex);
                }
            }
        }
        [HarmonyPatch(typeof(KingdomUIEventWindow), "SetHeader")]
        static class KingdomUIEventWindow_SetHeader_Patch
        {
            static void Postfix(KingdomUIEventWindow __instance, KingdomEventUIView kingdomEventView)
            {
                try
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
                    var validResults = resolutions.Where(isValid);
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
                        }
                        else
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
                catch (Exception ex)
                {
                    DebugError(ex);
                }
            }
        }
    }
}
