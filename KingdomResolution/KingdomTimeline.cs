using Harmony12;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.ElementsSystem;
using Kingmaker.Kingdom;
using Kingmaker.Kingdom.Artisans;
using Kingmaker.Kingdom.Blueprints;
using Kingmaker.Kingdom.Rules;
using Kingmaker.Kingdom.Settlements;
using Kingmaker.Kingdom.Tasks;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KingdomResolution
{
    class KingdomTimeline
    {

        public static void FixTimeline()
        {
            int delta = (int)Game.Instance.TimeController.GameTime.TotalDays - KingdomState.Instance.CurrentDay - KingdomState.Instance.StartDay;
            KingdomState.Instance.StartDay = (int)Game.Instance.TimeController.GameTime.TotalDays - KingdomState.Instance.CurrentDay;
            if (delta < 1) return;
            if (Main.settings.enableKingomManagement)
            {
                foreach (KingdomBuff kingdomBuff in KingdomState.Instance.ActiveBuffs.Enumerable)
                {
                    if (kingdomBuff.EndsOnDay > 0 && KingdomState.Instance.CurrentDay >= kingdomBuff.EndsOnDay)
                    {
                        kingdomBuff.EndsOnDay = (int)Math.Max(1, kingdomBuff.EndsOnDay - delta);
                    }
                }
                foreach (RegionState regionState in KingdomState.Instance.Regions)
                {
                    SettlementState settlement = regionState.Settlement;
                    if (settlement != null)
                    {
                        foreach(var building in settlement.Buildings)
                        {
                            if (building.IsFinished) continue;
                            building.FinishedOn = (int)Math.Max(1, building.FinishedOn - delta);
                        }
                    }
                }
                foreach (RegionState regionState2 in KingdomState.Instance.Regions)
                {
                    foreach (Artisan artisan in regionState2.Artisans)
                    {

                    }
                }
                if (Main.settings.enablePausedProjects)
                {
                    foreach (KingdomTask task in KingdomState.Instance.ActiveTasks)
                    {
                        var kte = task as KingdomTaskEvent;
                        if (kte == null) continue;
                        if (!kte.IsInProgress) continue;
                        if (kte.Event.EventBlueprint is BlueprintKingdomProject bkp)
                        {
                            //Traverse.Create(task).Property("StartedOn").SetValue(task.StartedOn - delta);
                            typeof(KingdomTask).GetProperty("StartedOn").SetValue(task, task.StartedOn - delta, null);
                            Traverse.Create(kte.Event).Field("m_StartedOn").SetValue(kte.Event.StartedOn - delta);
                        }
                    }
                }
                for (int i = 0; i < delta; i++)
                {
                    if ((Game.Instance.TimeController.GameTime.TotalDays - delta) % 7 == 0)
                    {
                        KingdomState.Instance.BPPerTurnTotal = Rulebook.Trigger<RuleCalculateBPGain>(new RuleCalculateBPGain()).BPToAdd;
                        KingdomState.Instance.BP += KingdomState.Instance.BPPerTurnTotal;
                        KingdomState.Instance.CurrentTurn++;
                        EventBus.RaiseEvent<IKingdomLogHandler>(delegate (IKingdomLogHandler h)
                        {
                            h.OnBPGained(KingdomState.Instance.BPPerTurnTotal);
                        });
                    }
                }
            }

        }
        /*
         * CurrentDay is used in a large amound of places, and is updated when UpdateTimeline is ran
         * StartDat is used to update CurrentDay, and used to calculate KingdomState.Date,
         * KingdomState.DaysTillNextMonth, KingdomEvent.RecurIfNeeded
         * KingdomState.Date is defined as GameStart + StartDay + CurrentDay;
         */
        [HarmonyPatch(typeof(KingdomTimelineManager), "UpdateTimeline")]
        static class KingdomTimelineManager_UpdateTimeline_Patch
        {
            static bool Prefix()
            {
                try
                {
                    if (!Main.enabled) return true;
                    if (!Main.settings.pauseKingdomTimeline) return true;
                    FixTimeline();
                } catch(Exception ex)
                {
                    Main.DebugError(ex);
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(KingdomTimelineManager), "MaybeUpdateTimeline")]
        static class KingdomState_MaybeUpdateTimeline_Patch
        {
            static bool Prefix()
            {
                try { 
                    if (!Main.enabled) return true;
                    if (!Main.settings.pauseKingdomTimeline) return true;
                    int delta = (int)Game.Instance.TimeController.GameTime.TotalDays - KingdomState.Instance.CurrentDay - KingdomState.Instance.StartDay;
                    FixTimeline();
                    if (Main.settings.enableMabyEvents && delta > 0)
                    {
                        KingdomState.Instance.TimelineManager.UpdateTimeline();
                        return false;
                    }
                } catch(Exception ex)
                {
                    Main.DebugError(ex);
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(KingdomState), "DaysTillNextMonth", MethodType.Getter)]
        static class KingdomState_DaysTillNextMonth_Patch
        {
            static bool Prefix(ref int __result)
            {
                if (!Main.enabled) return true;
                if (!Main.settings.pauseKingdomTimeline) return true;
                return true;
            }
        }
        /*
         * KingdomState.Date is used by 
         * RollRandomEvents to effect the delay random events have
         * and by KingdomTimelineManager.UpdateTImelineOneDay to check if start of the month
         * and RollRandomEvents
         */
        [HarmonyPatch(typeof(KingdomState), "Date", MethodType.Getter)]
        static class KingdomTimelineManager_Date_Patch
        {
            static bool Prefix(ref DateTime __result)
            {
                if (!Main.enabled) return true;
                if (!Main.settings.pauseKingdomTimeline) return true;
                if (Main.settings.enableRandomEvents) return true;
                var date = KingdomState.Instance.ToDate(KingdomState.Instance.CurrentDay);
                if (date.Day == 1)
                {
                    date = KingdomState.Instance.ToDate(KingdomState.Instance.CurrentDay - 1);
                }
                __result = date;
                return true;
            }
        }
        static bool CausesGameOver(BlueprintKingdomEvent blueprint){
            var results = blueprint.GetComponent<EventFinalResults>();
            if (results == null) return false;
            foreach(var result in results.Results)
            {
                foreach(var action in result.Actions.Actions)
                {
                    if (action is GameOver) return true;
                }
            }
            return false;
        }
        static BlueprintKingdomEventTimeline.Entry openEntry;
        public static void PreviewTimeline()
        {
            var timeline = Game.Instance.BlueprintRoot.Kingdom.Timeline;
            var currentDay = KingdomState.Instance != null ? KingdomState.Instance.CurrentDay : 0;
            if (KingdomState.Instance != null)
            {
                var nextBP = 7 - KingdomState.Instance.CurrentDay % 7;
                GUILayout.Label($"GameTime {Game.Instance.TimeController.GameTime.TotalDays:0.#} StartDay {KingdomState.Instance.StartDay} Current Day {KingdomState.Instance.CurrentDay} D.O.M {KingdomState.Instance.Date.Day} NextBP {nextBP}");
                foreach(var entry in timeline.Entries.Entries)
                {
                    if(currentDay < entry.Day && CausesGameOver(entry.Event) && entry.Event.TriggerCondition.Check())
                    {
                        GUILayout.Label($"GameOver in {entry.Day - currentDay} days from {entry.Event.LocalizedName}");
                        break;
                    }
                }
            } 
            else
            {
                GUILayout.Label($"GameTime {Game.Instance.TimeController.GameTime.TotalDays:0.#} GameOver in {90 - Game.Instance.TimeController.GameTime.TotalDays:0.#} days from Stolen Lands");
            }

            foreach (var entry in timeline.Entries.Entries)
            {
                if (entry.Day < currentDay) continue;
                GUILayout.BeginHorizontal();
                var timeString = entry.Event.ResolutionTime;
                GUILayout.Label($"In {entry.Day - currentDay} days, {entry.Event.name}: {entry.Event.LocalizedName}", BoxLabel);
                if (openEntry == entry && GUILayout.Button("Less", GUILayout.Width(45)))
                {
                    openEntry = null;
                }
                if (openEntry != entry && GUILayout.Button("More", GUILayout.Width(45)))
                {
                    openEntry = entry;
                }
                GUILayout.EndHorizontal();
                if (openEntry == entry)
                {
                    ShowBlueprintEvent(entry.Event);
                }

            }
        }
        static GUIStyle m_BoldLabel;
        static GUIStyle BoldLabel
        {
            get
            {
                if (m_BoldLabel == null)
                {
                    m_BoldLabel = new GUIStyle(GUI.skin.label)
                    {
                        fontStyle = FontStyle.Bold
                    };
                }
                return m_BoldLabel;
            }
        }
        static GUIStyle m_BoxLabel;
        static GUIStyle BoxLabel
        {
            get
            {
                if (m_BoxLabel == null)
                {
                    m_BoxLabel = new GUIStyle(GUI.skin.box)
                    {
                        alignment = TextAnchor.LowerLeft
                    };
                }
                return m_BoxLabel;
            }
        }
        static GUIStyle m_YellowBoxLabel;
        static GUIStyle YellowBoxLabel
        {
            get
            {
                if (m_YellowBoxLabel == null)
                {
                    m_YellowBoxLabel = new GUIStyle(GUI.skin.box)
                    {
                        alignment = TextAnchor.LowerLeft,
                        normal = new GUIStyleState() { textColor = Color.yellow },
                        active = new GUIStyleState(){ textColor = Color.cyan },
                        focused = new GUIStyleState() { textColor = Color.magenta },
                        hover = new GUIStyleState() { textColor = Color.green },
                    };
                }
                return m_YellowBoxLabel;
            }
        }
        public static bool IsResultEmpty(EventResult result)
        {
            return result.LocalizedDescription == "" &&
                result.SuccessCount == 0 &&
                result.StatChanges.IsEmpty &&
                !result.Actions.HasActions;
        }
        public static void ShowBlueprintEvent(BlueprintKingdomEventBase blueprint)
        {
            GUILayout.Label($"Description: {blueprint.LocalizedDescription}");
            GUILayout.Label($"ResolutionTime: {blueprint.ResolutionTime} days");
            GUILayout.Label($"ResolveAutomatically: {blueprint.ResolveAutomatically}");
            GUILayout.Label($"NeedToVistTheThroneRoom: {blueprint.NeedToVisitTheThroneRoom}");
            GUILayout.Label($"BlueprintType: {blueprint.GetType().Name}");
            if (blueprint is BlueprintKingdomEvent bke)
            {
                var actionText = bke.OnTrigger.Actions
                    .SelectMany(action => Main.FormatAction(action))
                    .Select(text => text == "" ? "EmptyAction" : text)
                    .Join();
                var statChangesText = bke.StatsOnTrigger.ToStringWithPrefix(" ");
                if (actionText != "") GUILayout.Label($"OnTrigger: {actionText}");
                if(!bke.StatsOnTrigger.IsEmpty) GUILayout.Label($"StatsOnTrigger: {statChangesText}"); 
            }
            if (blueprint is BlueprintKingdomProject bkp)
            {
                GUILayout.Label($"ProjectType: {bkp.ProjectType}");
            }
            foreach (var solution in blueprint.Solutions.Entries)
            {
                foreach (var result in solution.Resolutions)
                {
                    if (IsResultEmpty(result)) continue;
                    var statChangesText = result.StatChanges.ToStringWithPrefix(" ");
                    var actionText = result.Actions.Actions
                        .SelectMany(action => Main.FormatAction(action))
                        .Select(text => text == "" ? "EmptyAction" : text)
                        .Join();
                    GUILayout.Label($"PossibleSolution: {result.Margin}, Leader {solution.Leader}, DC {solution.DCModifier}", BoldLabel);

                    if(result.LeaderAlignment != Kingmaker.UnitLogic.Alignments.AlignmentMaskType.Any) GUILayout.Label($"Alignment: {result.LeaderAlignment}");
                    if (actionText != "") GUILayout.Label($"Actions: {actionText}");
                    if(!result.StatChanges.IsEmpty) GUILayout.Label($"StatChanges: {statChangesText}");
                    if(result.SuccessCount != 0) GUILayout.Label($"SuccessCount: {result.SuccessCount}");
                    if(result.LocalizedDescription != "") GUILayout.Label($"Description: {result.LocalizedDescription}");
                }
            }
            var finalResults = blueprint.GetComponent<EventFinalResults>();
            if(finalResults != null) foreach(var result in finalResults.Results)
            {
                var statChangesText = result.StatChanges.ToStringWithPrefix(" ");
                var actionText = result.Actions.Actions
                    .SelectMany(action => Main.FormatAction(action))
                    .Select(text => text == "" ? "EmptyAction" : text)
                    .Join();
                GUILayout.Label($"FinalResult {result.Margin}", BoldLabel);
                if (result.LeaderAlignment != Kingmaker.UnitLogic.Alignments.AlignmentMaskType.Any) GUILayout.Label($"Alignment: {result.LeaderAlignment}");
                if (actionText != "") GUILayout.Label($"Actions: {actionText}");
                if (!result.StatChanges.IsEmpty) GUILayout.Label($"StatChanges: {statChangesText}");
                if (result.SuccessCount != 0) GUILayout.Label($"SuccessCount: {result.SuccessCount}");
                if (result.LocalizedDescription != "") GUILayout.Label($"Description: {result.LocalizedDescription}");
            }
        }
        public static void ShowEvent(KingdomEvent activeEvent)
        {
            GUILayout.Label($"IsRecurrent {activeEvent.IsRecurrent}");
            GUILayout.Label($"DCModifier {activeEvent.DCModifier}");
            GUILayout.Label($"StartedOn {activeEvent.StartedOn}");
            ShowBlueprintEvent(activeEvent.EventBlueprint);
        }
        static KingdomEvent openEvent = null;
        public static void ShowActiveEvents()
        {
            if (KingdomState.Instance == null)
            {
                GUILayout.Label($"Kingdom not founded");
                return;
            }
            //KingdomEvent
            foreach (var activeEvent in KingdomState.Instance.ActiveEvents)
            {
                GUILayout.BeginHorizontal();
                var timeString = activeEvent.IsPlanned ?
                    $"Starts in {activeEvent.StartedOn - KingdomState.Instance.CurrentDay} days" :
                    activeEvent.IsFinished ?
                    $"Finished" :
                    "Active";
                var labelStyle = BoxLabel;
                GUILayout.Label($"{activeEvent.FullName}, {timeString}", labelStyle);
                if(activeEvent != openEvent && GUILayout.Button("More", GUILayout.Width(45)))
                {
                    openEvent = activeEvent;
                } else if(activeEvent == openEvent && GUILayout.Button("Less", GUILayout.Width(45)))
                {
                    openEvent = null;
                }
                GUILayout.EndHorizontal();
                if (activeEvent == openEvent)
                {
                    var task = activeEvent.AssociatedTask;
                    if (task != null) GUILayout.Label($"Task: {task.Name}");
                    ShowEvent(activeEvent);
                }
            }
        }
        public static int GetExpireDays(KingdomEvent @event)
        {
            int month = KingdomState.Instance.ToDate(@event.StartedOn).Month;
            int month2 = KingdomState.Instance.ToDate(@event.StartedOn + 10).Month;
            DateTime d = KingdomState.Instance.Date.AddMonths((month != month2) ? 2 : 1);
            d = new DateTime(d.Year, d.Month, 1);
            return KingdomState.Instance.ToDay(d) - KingdomState.Instance.CurrentDay;
        }
        static KingdomTask openTask = null;
        public static void ShowActiveTasks()
        {
            if (KingdomState.Instance == null)
            {
                GUILayout.Label($"Kingdom not founded");
                return;
            }
            //KingdomTask
            foreach (var activeTask in KingdomState.Instance.ActiveTasks)
            {
                GUILayout.BeginHorizontal();
                var kte = activeTask as KingdomTaskEvent;
                var bke = kte != null ? kte.Event.EventBlueprint as BlueprintKingdomEvent : null;
                var timeString = activeTask.IsInProgress ?
                    $"Ends in {activeTask.EndsOn - KingdomState.Instance.CurrentDay} days" :
                    activeTask.IsFinished ?
                    $"Finished {activeTask.EndsOn - KingdomState.Instance.CurrentDay} days ago" :
                    bke != null ? 
                    $"Should attend by {GetExpireDays(kte.Event)} days"
                    : "Pending";
                var labelStyle = BoxLabel;
                GUILayout.Label($"{activeTask.Name}, {timeString}", labelStyle);
                if (activeTask != openTask && GUILayout.Button("More", GUILayout.Width(45)))
                {
                    openTask = activeTask;
                }
                else if (activeTask == openTask && GUILayout.Button("Less", GUILayout.Width(45)))
                {
                    openTask = null;
                }
                GUILayout.EndHorizontal();
                if (activeTask == openTask)
                {
                    GUILayout.Label($"Description: {activeTask.Description}");
                    GUILayout.Label($"TaskStartedOn: {activeTask.StartedOn}");
                    GUILayout.Label($"Duration: {activeTask.Duration}");
                    GUILayout.Label($"OneTimeBPCost: {activeTask.OneTimeBPCost}");
                    GUILayout.Label($"SkipPlayerTime: {activeTask.SkipPlayerTime}");
                    GUILayout.Label($"NeedsIgnore: {activeTask.NeedsIgnore}");
                    if (activeTask.Region != null) GUILayout.Label($"Region: {activeTask.Region.Blueprint.LocalizedName}");
                    if (kte != null)
                    {
                        var eventStatus = kte.Event.IsPlanned ?
                            $"Starts in {kte.Event.StartedOn - KingdomState.Instance.CurrentDay} days" :
                            kte.Event.IsFinished ?
                            $"Finished" :
                            "Active";
                        GUILayout.Label($"Event: {kte.Event.FullName} - {eventStatus}");
                        ShowEvent(kte.Event);
                    }
                }
            }
        }

    }
}
