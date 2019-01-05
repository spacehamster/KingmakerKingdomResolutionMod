using Harmony12;
using Kingmaker;
using Kingmaker.Blueprints.Root;
using Kingmaker.Blueprints.Root.Strings;
using Kingmaker.Controllers.GlobalMap;
using Kingmaker.DialogSystem;
using Kingmaker.DialogSystem.Blueprints;
using Kingmaker.ElementsSystem;
using Kingmaker.Enums;
using Kingmaker.Kingdom;
using Kingmaker.Kingdom.Blueprints;
using Kingmaker.Kingdom.UI;
using Kingmaker.UI.Common;
using Kingmaker.UI.Dialog;
using Kingmaker.UI.GlobalMap;
using Kingmaker.UI.Kingdom;
using Kingmaker.UI.SettingsUI;
using Kingmaker.UI.Tooltip;
using Kingmaker.UnitLogic.Alignments;
using Kingmaker.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;

namespace KingdomResolution
{
    class PreviewManager
    {
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
                        if (page.Answers[0] == answer.ParentAsset)
                        {
                            isRecursive = true;
                            break;
                        }
                        if (page.Answers[0] is BlueprintAnswersList) break;
                    }
                    if (page.Cues.Count > 0)
                    {
                        foreach (var c in page.Cues) if (c.CanShow()) toCheck.Enqueue(new Tuple<BlueprintCueBase, int>(c, currentDepth + 1));
                    }
                }
                else if (cueBase is BlueprintCheck check)
                {
                    toCheck.Enqueue(new Tuple<BlueprintCueBase, int>(check.Success, currentDepth + 1));
                    toCheck.Enqueue(new Tuple<BlueprintCueBase, int>(check.Fail, currentDepth + 1));
                }
                else if (cueBase is BlueprintCueSequence sequence)
                {
                    foreach (var c in sequence.Cues) if (c.CanShow()) toCheck.Enqueue(new Tuple<BlueprintCueBase, int>(c, currentDepth + 1));
                    if (sequence.Exit != null)
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
                text + ((!text.Empty()) ? " " : string.Empty) + answer.DisplayText);
        }
        [HarmonyPatch(typeof(UIConsts), "GetAnswerString")]
        static class UIConsts_GetAnswerString_Patch
        {
            static void Postfix(ref string __result, BlueprintAnswer answer, string bind, int index)
            {
                try
                {
                    if (!Main.enabled) return;
                    if (Main.settings.previewAlignmentRestrictedDialog && !answer.IsAlignmentRequirementSatisfied)
                    {
                        __result = GetFixedAnswerString(answer, bind, index);
                    }
                    if (!Main.settings.previewDialogResults) return;
                    var answerData = CollateAnswerData(answer, out bool isRecursive);
                    if (isRecursive)
                    {
                        __result += $" <size=75%>[Repeats]</size>";
                    }
                    var results = new List<string>();
                    foreach (var data in answerData)
                    {
                        var cue = data.Item1;
                        var depth = data.Item2;
                        var actions = data.Item3;
                        var alignment = data.Item4;
                        var line = new List<string>();
                        if (actions.Length > 0)
                        {
                            line.AddRange(actions.
                                SelectMany(action => Util.FormatActionAsList(action)
                                .Select(actionText => actionText == "" ? "EmptyAction" : actionText)));
                        }
                        if (alignment != null && alignment.Value > 0)
                        {
                            line.Add($"AlignmentShift({alignment.Direction}, {alignment.Value}, {alignment.Description})");
                        }
                        if (cue is BlueprintCheck check)
                        {
                            line.Add($"Check({check.Type}, DC {check.DC}, hidden {check.Hidden})");
                        }
                        if (line.Count > 0) results.Add($"{depth}: {line.Join()}");
                    }
                    if (results.Count > 0) __result += $" \n<size=75%>[{results.Join()}]</size>";
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
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
                    if (!Main.enabled) return;
                    if (!Main.settings.previewDialogResults) return;
                    var cue = Game.Instance.DialogController.CurrentCue;
                    var actions = cue.OnShow.Actions.Concat(cue.OnStop.Actions).ToArray();
                    var alignment = cue.AlignmentShift;
                    var text = "";
                    if (actions.Length > 0)
                    {
                        var result = Util.FormatActions(actions);
                        if (result == "") result = "EmptyAction";
                        text += $" \n<size=75%>[{result}]</size>";
                    }
                    if (alignment != null && alignment.Value > 0)
                    {
                        text += $" \n<size=75%>[AlignmentShift {alignment.Direction} by {alignment.Value} - {alignment.Description}]";
                    }
                    __instance.DialogPhrase.text += text;
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
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
                    if (!Main.enabled) return;
                    if (!Main.settings.previewEventResults) return;
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
                    bool isValid(EventResult result) => (alignmentMask & result.LeaderAlignment) != AlignmentMaskType.None;
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
                    Main.DebugError(ex);
                }
            }
        }
        [HarmonyPatch(typeof(GlobalMapRandomEncounterController), "OnRandomEncounterStarted")]
        static class GlobalMapRandomEncounterController_OnRandomEncounterStarted_Patch
        {
            static void Postfix(GlobalMapRandomEncounterController __instance, ref RandomEncounterData encounter)
            {
                try
                {
                    if (!Main.enabled) return;
                    if (Main.settings.previewRandomEncounters)
                    {
                        var blueprint = encounter.Blueprint;
                        var text = $"\n<size=70%>Name: {blueprint.name}\nType: {blueprint.Type}\nCR: {encounter.CR}</size>";
                        Traverse.Create(__instance).Field("m_Description").GetValue<TextMeshProUGUI>().text += text;
                    }
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                }
            }
        }
    }
}
