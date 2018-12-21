using Harmony12;
using Kingmaker;
using Kingmaker.ElementsSystem;
using Kingmaker.Kingdom;
using Kingmaker.Kingdom.Blueprints;
using Kingmaker.Kingdom.Tasks;
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
        /*
         * CurrentDay is used in a large amound of places, and is updated when UpdateTimeline is ran
         * StartDat is used to update CurrentDay, and used to calculate KingdomState.Date,
         * KingdomState.DaysTillNextMonth, KingdomEvent.RecurIfNeeded
         * KingdomState.Date is defined as GameStart + StartDay + CurrentDay;
         * 

         */
        [HarmonyPatch(typeof(KingdomTimelineManager), "UpdateTimeline")]
        static class KingdomTimelineManager_UpdateTimeline_Patch
        {
            static bool Prefix()
            {
                if (!Main.enabled) return true;
                if (!Main.settings.stopKingdomTimeline) return true;
                KingdomState.Instance.StartDay = (int)Game.Instance.TimeController.GameTime.TotalDays - KingdomState.Instance.CurrentDay;
                return true;
            }
        }
        [HarmonyPatch(typeof(KingdomTimelineManager), "MaybeUpdateTimeline")]
        static class KingdomState_MaybeUpdateTimeline_Patch
        {
            static bool Prefix()
            {
                if (!Main.enabled) return true;
                if (!Main.settings.stopKingdomTimeline) return true;
                KingdomState.Instance.StartDay = (int)Game.Instance.TimeController.GameTime.TotalDays - KingdomState.Instance.CurrentDay;
                return true;
            }
        }
        [HarmonyPatch(typeof(KingdomState), "DaysTillNextMonth", MethodType.Getter)]
        static class KingdomState_DaysTillNextMonth_Patch
        {
            static bool Prefix(ref int __result)
            {
                if (!Main.enabled) return true;
                if (!Main.settings.stopKingdomTimeline) return true;
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
                if (!Main.settings.stopKingdomTimeline) return true;
                var date = KingdomState.Instance.ToDate(KingdomState.Instance.CurrentDay);
                if (date.Day == 1)
                {
                    date = KingdomState.Instance.ToDate(KingdomState.Instance.CurrentDay - 1);
                }
                __result = date;
                return true;
            }
        }
        static BlueprintKingdomEventTimeline.Entry openEntry;
        public static void PreviewTimeline()
        {
            var timeline = Game.Instance.BlueprintRoot.Kingdom.Timeline;
            GUILayout.Label($"Start Day {KingdomState.Instance.StartDay} Current Day {KingdomState.Instance.CurrentDay}");
            foreach (var entry in timeline.Entries.Entries)
            {
                if (entry.Day < KingdomState.Instance.CurrentDay) continue;
                GUILayout.BeginHorizontal();
                var timeString = entry.Event.ResolutionTime;
                GUILayout.Label($"In {entry.Day - KingdomState.Instance.CurrentDay} days, {entry.Event.name}: {entry.Event.LocalizedName}", "box");
                if (openEntry == entry && GUILayout.Button("Less"))
                {
                    openEntry = null;
                }
                if (openEntry != entry && GUILayout.Button("More"))
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
        public static void ShowBlueprintEvent(BlueprintKingdomEventBase blueprint)
        {
            GUILayout.Label($"Description: {blueprint.LocalizedDescription}");
            GUILayout.Label($"ResolutionTime: {blueprint.ResolutionTime} days");
            GUILayout.Label($"ResolveAutomatically: {blueprint.ResolveAutomatically}");
            GUILayout.Label($"NeedToVistTheThroneRoom: {blueprint.NeedToVisitTheThroneRoom}");
            if (blueprint is BlueprintKingdomEvent bke)
            {
                var actionText = bke.OnTrigger.Actions.Join((action) => Main.FormatAction(action).Join());
                GUILayout.Label($"OnTrigger: {actionText}");
            }
            if (blueprint is BlueprintKingdomProject bkp)
            {
                GUILayout.Label($"ProjectType: {bkp.ProjectType}");
            }
            var actions = new HashSet<GameAction>();
            foreach (var solution in blueprint.Solutions.Entries)
            {
                foreach (var resolution in solution.Resolutions)
                {
                    foreach (var action in resolution.Actions.Actions)
                    {
                        actions.Add(action);
                    }
                }
            }
            var possibleActionText = actions.Join((action) => Main.FormatAction(action).Join());
            GUILayout.Label($"PossibleResolutions: {possibleActionText}");
        }
        public static void ShowEvent(KingdomEvent activeEvent)
        {
            var task = activeEvent.AssociatedTask;
            if (task != null) GUILayout.Label($"Task {task.Name}");
            GUILayout.Label($"IsRecurrent {activeEvent.IsRecurrent}");
            ShowBlueprintEvent(activeEvent.EventBlueprint);


        }
        static KingdomEvent openEvent = null;
        public static void ShowActiveEvents()
        {
            //KingdomEvent
            foreach (var activeEvent in KingdomState.Instance.ActiveEvents)
            {
                GUILayout.BeginHorizontal();
                var timeString = activeEvent.IsPlanned ?
                    $"Starts in {activeEvent.StartedOn} days" :
                    activeEvent.IsFinished ?
                    $"Finished ago" :
                    "Active";
                GUILayout.Label($"{activeEvent.FullName}, {timeString}", "box");
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
                    ShowEvent(activeEvent);
                }
            }
        }
        static KingdomTask openTask = null;
        public static void ShowActiveTasks()
        {
            //KingdomTask
            foreach (var activeTask in KingdomState.Instance.ActiveTasks)
            {
                GUILayout.BeginHorizontal();
                var timeString = activeTask.IsInProgress ?
                    $"Ends in {activeTask.EndsOn - KingdomState.Instance.CurrentDay} days" :
                    activeTask.IsFinished ?
                    $"Finished {activeTask.EndsOn - KingdomState.Instance.CurrentDay} days ago" :
                    "Pending";
                GUILayout.Label($"{activeTask.Name}, {timeString}", "box");
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
                    GUILayout.Label($"Name: {activeTask.Name}");
                    GUILayout.Label($"Description: {activeTask.Description}");
                    if(activeTask.Region != null) GUILayout.BeginHorizontal($"Event: {activeTask.Region.Blueprint.LocalizedName}");
                    if (activeTask is KingdomTaskEvent kte)
                    {
                        GUILayout.Label($"Event: {kte.Event.FullName}");
                        ShowEvent(kte.Event);
                    }
                }
            }
        }

    }
}
