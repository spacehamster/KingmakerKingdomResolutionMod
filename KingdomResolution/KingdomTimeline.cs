using Harmony12;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
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
using UnityEngine;

namespace KingdomResolution
{
    class KingdomTimeline
    {
        static FastSetter<KingdomTask, int> StartedOnSetter;
        static Harmony12.AccessTools.FieldRef<KingdomEvent, int> m_StartedOnRef;
        static FastInvoker<KingdomTimelineManager, bool> UpdateTimelineOneDay;
        /*
         * Pause Kingdom works by keeping current day constant, and increasing kingdom start day to compensate
         * To manage kingdom while paused, event start and finish days are moved backwards to simulate currentday moving forwards
         * any events that depend on CurrentDay need to be manually triggered
         */
        public static void FixTimeline(int delta)
        {
            if (delta < 1) return;
            if (!Main.settings.enablePausedKingdomManagement) return;
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
                    foreach (var building in settlement.Buildings)
                    {
                        if (building.IsFinished) continue;
                        building.FinishedOn = building.FinishedOn - delta;
                    }
                }
            }
            foreach (RegionState regionState2 in KingdomState.Instance.Regions)
            {
                foreach (Artisan artisan in regionState2.Artisans)
                {
                    if (artisan.HasHelpProject) continue;
                    artisan.ProductionStartedOn = artisan.ProductionStartedOn - delta;
                    artisan.ProductionEndsOn = artisan.ProductionEndsOn - delta;
                }
            }
            foreach (KingdomTask task in KingdomState.Instance.ActiveTasks)
            {
                if (!(task is KingdomTaskEvent kte)) continue;
                if (!kte.IsInProgress) continue;
                if (kte.Event.EventBlueprint is BlueprintKingdomProject bkp)
                {
                    StartedOnSetter(task, task.StartedOn - delta);
                }
            }
            foreach (var kingdomEvent in KingdomState.Instance.ActiveEvents)
            {
                if (kingdomEvent.IsFinished) continue;
                m_StartedOnRef(kingdomEvent) = m_StartedOnRef(kingdomEvent) - delta;
            }
            for (int i = 0; i < delta; i++)
            {
                var totalDays = (int)Game.Instance.TimeController.GameTime.TotalDays;
                if ((totalDays - delta) % 7 == 0)
                {
                    KingdomState.Instance.BPPerTurnTotal = Rulebook.Trigger<RuleCalculateBPGain>(new RuleCalculateBPGain()).BPToAdd;
                    KingdomState.Instance.BuildPoints += KingdomState.Instance.BPPerTurnTotal;
                    KingdomState.Instance.CurrentTurn++;
                    EventBus.RaiseEvent(delegate (IKingdomLogHandler h)
                    {
                        h.OnBPGained(KingdomState.Instance.BPPerTurnTotal);
                    });
                }
            }
            KingdomState.Instance.LastRavenVisitDay -= delta;
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
            static bool Prepare()
            {
                StartedOnSetter = Accessors.CreateSetter<KingdomTask, int>("StartedOn");
                m_StartedOnRef = Accessors.CreateFieldRef<KingdomEvent, int>("m_StartedOn");
                UpdateTimelineOneDay = Accessors.CreateInvoker<KingdomTimelineManager, bool>("UpdateTimelineOneDay");
                return true;
            }
            static bool Prefix(KingdomTimelineManager __instance)
            {
                try
                {
                    if (!Main.enabled) return true;
                    if (!Main.settings.pauseKingdomTimeline) return true;
                    //Unpause kingdom timeline if there is ever a game over triggered
                    if (KingdomState.Instance.ActiveEvents.Any(e => Util.CausesGameOver(e.EventBlueprint)))
                    {
                        Main.settings.pauseKingdomTimeline = false;
                        return true;
                    }
                    int delta = (int)Game.Instance.TimeController.GameTime.TotalDays - KingdomState.Instance.CurrentDay - KingdomState.Instance.StartDay;
                    KingdomState.Instance.StartDay = (int)Game.Instance.TimeController.GameTime.TotalDays - KingdomState.Instance.CurrentDay;
                    bool changed = false;
                    for (int i = 0; i < delta; i++)
                    {
                        FixTimeline(1);
                        changed = changed || UpdateTimelineOneDay(__instance);
                    }
                    EventBus.RaiseEvent(delegate (IKingdomTimeChanged h)
                    {
                        h.OnTimeChanged(changed);
                    });
                }
                catch (Exception ex)
                {
                    Main.DebugError(ex);
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(KingdomTimelineManager), "RollRandomEvents")]
        static class KingdomTimelineManager_RollRandomEvents_Patch
        {
            static bool Prefix()
            {
                if (!Main.enabled) return true;
                if (Main.settings.pauseKingdomTimeline)
                {
                    if (Main.settings.enablePausedKingdomManagement && Main.settings.enablePausedRandomEvents) return true;
                    return false;
                }
                return true;
            }
        }
    }
}
