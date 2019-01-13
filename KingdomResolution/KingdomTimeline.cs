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
                    //Traverse.Create(task).Property<int>("StartedOn").Value = task.StartedOn - delta;
                    typeof(KingdomTask).GetProperty("StartedOn").SetValue(task, task.StartedOn - delta, null);
                }
            }
            foreach (var kingdomEvent in KingdomState.Instance.ActiveEvents)
            {
                if (kingdomEvent.IsFinished) continue;
                var m_StartedOn = Traverse.Create(kingdomEvent).Field("m_StartedOn").GetValue<int>();
                Traverse.Create(kingdomEvent).Field("m_StartedOn").SetValue(m_StartedOn - delta);
            }
            for (int i = 0; i < delta; i++)
            {
                var totalDays = (int)Game.Instance.TimeController.GameTime.TotalDays;
                if ((totalDays - delta) % 7 == 0)
                {
                    KingdomState.Instance.BPPerTurnTotal = Rulebook.Trigger<RuleCalculateBPGain>(new RuleCalculateBPGain()).BPToAdd;
                    KingdomState.Instance.BP += KingdomState.Instance.BPPerTurnTotal;
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
            static bool Prefix(KingdomTimelineManager __instance)
            {
                try
                {
                    if (!Main.enabled) return true;
                    if (!Main.settings.pauseKingdomTimeline) return true;

                    int delta = (int)Game.Instance.TimeController.GameTime.TotalDays - KingdomState.Instance.CurrentDay - KingdomState.Instance.StartDay;
                    KingdomState.Instance.StartDay = (int)Game.Instance.TimeController.GameTime.TotalDays - KingdomState.Instance.CurrentDay;
                    bool changed = false;
                    for (int i = 0; i < delta; i++)
                    {
                        FixTimeline(1);
                        changed = changed || Traverse.Create(__instance).Method("UpdateTimelineOneDay").GetValue<bool>();
                    }
                    EventBus.RaiseEvent(delegate (IKingdomTimeChanged h)
                    {
                        h.OnTimeChanged(changed);
                    });
                } catch (Exception ex)
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
                if (Main.settings.pauseKingdomTimeline) {
                    if (Main.settings.enablePausedKingdomManagement && Main.settings.enablePausedRandomEvents) return true;
                    return false;
                }
                return true;
            }
        }
        static bool CausesGameOver(BlueprintKingdomEvent blueprint) {
            var results = blueprint.GetComponent<EventFinalResults>();
            if (results == null) return false;
            foreach (var result in results.Results)
            {
                foreach (var action in result.Actions.Actions)
                {
                    if (action is GameOver) return true;
                }
            }
            return false;
        }
        enum KingdomInfoUI
        {
            None,
            Timeline,
            Events,
            Tasks,
            Artisan,
            History
        }
        static KingdomInfoUI OpenUI = KingdomInfoUI.None;
        static WeakReference<Artisan> openArtisanRef = new WeakReference<Artisan>(null);
        static WeakReference<KingdomTask> openTaskRef = new WeakReference<KingdomTask>(null);
        static WeakReference<KingdomEvent> openEventRef = new WeakReference<KingdomEvent>(null);
        static WeakReference<KingdomEventHistoryEntry> openHistoryRef = new WeakReference<KingdomEventHistoryEntry>(null);
        static WeakReference<BlueprintKingdomEventTimeline.Entry> openEntryRef = new WeakReference<BlueprintKingdomEventTimeline.Entry>(null);
        public static void ShowTimeline()
        {
            var timeline = Game.Instance.BlueprintRoot.Kingdom.Timeline;
            var currentDay = KingdomState.Instance != null ? KingdomState.Instance.CurrentDay : 0;
            if (KingdomState.Instance != null)
            {
                var nextBP = 7 - KingdomState.Instance.CurrentDay % 7;
                GUILayout.Label($"GameTime {Game.Instance.TimeController.GameTime.TotalDays:0.#} StartDay {KingdomState.Instance.StartDay} Current Day {KingdomState.Instance.CurrentDay} Day of Month {KingdomState.Instance.Date.Day} NextBP {nextBP}");
                foreach (var entry in timeline.Entries.Entries)
                {
                    if (currentDay < entry.Day && CausesGameOver(entry.Event) && entry.Event.TriggerCondition.Check())
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
            openEntryRef.TryGetTarget(out BlueprintKingdomEventTimeline.Entry openEntry);
            foreach (var entry in timeline.Entries.Entries)
            {
                if (entry.Day < currentDay) continue;
                GUILayout.BeginHorizontal();
                var timeString = entry.Event.ResolutionTime;
                GUILayout.Label($"In {entry.Day - currentDay} days, {entry.Event.name}: {entry.Event.LocalizedName}", Util.BoxLabel);
                if (openEntry == entry && GUILayout.Button("Less", GUILayout.Width(45)))
                {
                    openEntryRef.SetTarget(null);
                    openEntry = null;
                }
                if (openEntry != entry && GUILayout.Button("More", GUILayout.Width(45)))
                {
                    openEntryRef.SetTarget(entry);
                    openEntry = entry;
                }
                GUILayout.EndHorizontal();
                if (openEntry == entry)
                {
                    ShowBlueprintEvent(entry.Event);
                }

            }
        }
        public static bool IsResultEmpty(EventResult result)
        {
            return result.LocalizedDescription == "" &&
                result.SuccessCount == 0 &&
                result.StatChanges.IsEmpty &&
                !result.Actions.HasActions;
        }
        public static void OnGUI()
        {
            GUILayout.BeginHorizontal();
            foreach (KingdomInfoUI value in Enum.GetValues(typeof(KingdomInfoUI)))
            {
                if (value == KingdomInfoUI.None) continue;
                if (OpenUI == value && GUILayout.Button($"Hide {value}"))
                {
                    OpenUI = KingdomInfoUI.None;
                } else if (OpenUI != value && GUILayout.Button($"Show {value}"))
                {
                    OpenUI = value;
                }
            }
            GUILayout.EndHorizontal();
            if (OpenUI == KingdomInfoUI.Timeline)
            {
                ShowTimeline();
            }
            if (OpenUI == KingdomInfoUI.Events)
            {
                ShowActiveEvents();
            }
            if (OpenUI == KingdomInfoUI.Tasks)
            {
                ShowActiveTasks();
            }
            if (OpenUI == KingdomInfoUI.Artisan)
            {
                ShowArtisan();
            }
            if (OpenUI == KingdomInfoUI.History)
            {
                ShowHistory();
            }
        }
        public static void ShowArtisan()
        {
            if (KingdomState.Instance == null)
            {
                GUILayout.Label($"Kingdom not founded");
                return;
            }
            foreach (var region in KingdomState.Instance.Regions)
            {
                openArtisanRef.TryGetTarget(out Artisan openArtisan);
                foreach (var artisan in region.Artisans)
                {
                    GUILayout.BeginHorizontal();
                    var productionString = "";
                    if (artisan.CurrentProduction.Count > 0)
                    {
                        var timeString = KingdomState.Instance.CurrentDay >= artisan.ProductionEndsOn ?
                                "ready" :
                                $"due in {artisan.ProductionEndsOn - KingdomState.Instance.CurrentDay} days";
                        productionString = $" - {artisan.CurrentProduction.Join(cp => cp.Name)} {timeString}";
                    }
                    var artisanHelpProjectString = "";
                    if (artisan.HasHelpProject)
                    {
                        var helpProject = Traverse.Create(artisan).Field("m_HelpProjectEvent").GetValue<KingdomEvent>();
                        var dueIn = helpProject.StartedOn + helpProject.CalculateResolutionTime() - KingdomState.Instance.CurrentDay;
                        artisanHelpProjectString += $" - Help Project {helpProject.EventBlueprint.DisplayName} finishes in {dueIn}";
                    }
                    GUILayout.Label($"{artisan.Blueprint.name} - {region.Blueprint.LocalizedName}{productionString}{artisanHelpProjectString}", Util.BoxLabel);
                    if (artisan.CurrentProduction.Count > 0 && !artisan.HasHelpProject && KingdomState.Instance.CurrentDay < artisan.ProductionEndsOn && GUILayout.Button("Finish crafting", GUILayout.ExpandWidth(false)))
                    {
                        artisan.ProductionEndsOn = KingdomState.Instance.CurrentDay;
                        artisan.Update();
                        artisan.ProductionEndsOn--;
                        artisan.ProductionStartedOn = artisan.ProductionEndsOn - 1;
                    }
                    if (artisan.HasGift && GUILayout.Button("Collect Gifts", GUILayout.ExpandWidth(false))) {
                        artisan.CollectGift(KingdomState.Instance.KingdomLoot);
                    }
                    if (artisan.CurrentProduction.Count == 0 && artisan.BuildingUnlocked)
                    {
                        var building = region.Settlement?.Buildings.FirstOrDefault((SettlementBuilding s) => s.Blueprint.CountsAs(artisan.Blueprint.ShopBlueprint) && s.Active);
                        if (building != null && !building.IsDisabled && GUILayout.Button("Schedule Production", GUILayout.ExpandWidth(false)))
                        {
                            artisan.Update();
                        }
                    }
                    if (artisan != openArtisan && GUILayout.Button("More", GUILayout.Width(45)))
                    {
                        openArtisanRef.SetTarget(artisan);
                        openArtisan = artisan;
                    }
                    else if (artisan == openArtisan && GUILayout.Button("Less", GUILayout.Width(45)))
                    {
                        openArtisanRef.SetTarget(null);
                        openArtisan = null;
                    }
                    GUILayout.EndHorizontal();
                    if (artisan == openArtisan)
                    {
                        var TiersUnlocked = Traverse.Create(artisan).Field("TiersUnlocked").GetValue<bool[]>();
                        var blueprint = artisan.Blueprint;
                        var currentProductionString = artisan.CurrentProduction.Count > 0 ?
                                artisan.CurrentProduction.Join(bp => bp.Name)
                                : "None";
                        var onProductionStartedString = blueprint.OnProductionStarted.Actions.Length == 0 ? "None" : Util.FormatActions(blueprint.OnProductionStarted);
                        var OnGiftReadyString = blueprint.OnGiftReady.Actions.Length == 0 ? "None" : Util.FormatActions(blueprint.OnGiftReady);
                        var OnGiftCollectedString = blueprint.OnGiftCollected.Actions.Length == 0 ? "None" : Util.FormatActions(blueprint.OnGiftCollected);
                        var unlockMasterpiece = blueprint.MasterpieceUnlock.Conditions.Length == 0 ? "None" : Util.FormatConditions(blueprint.MasterpieceUnlock.Conditions);
                        var canCollectGift = blueprint.CanCollectGift.Conditions.Length == 0 ? "None" : Util.FormatConditions(blueprint.CanCollectGift.Conditions);
                        GUILayout.Label($"BuildingUnlocked: {artisan.BuildingUnlocked}");
                        GUILayout.Label($"MasterpieceUnlocked: {artisan.MasterpieceUnlocked}");
                        GUILayout.Label($"MasterpieceProduced: {artisan.MasterpieceProduced}");
                        GUILayout.Label($"TurnsWithoutMasterpiece: {artisan.TurnsWithoutMasterpiece}");
                        GUILayout.Label($"ProductionStartedOn: {artisan.ProductionStartedOn}");
                        GUILayout.Label($"ProductionEndsOn: {artisan.ProductionEndsOn}");
                        GUILayout.Label($"HasGift: {artisan.HasGift}");
                        GUILayout.Label($"HasHelpProject: {artisan.HasHelpProject}");
                        GUILayout.Label($"Region: {region.Blueprint.LocalizedName}");
                        GUILayout.Label($"TiersUnlocked: {TiersUnlocked?.Join() ?? "None"}");
                        GUILayout.Label($"CurrentProduction: {currentProductionString}");
                        GUILayout.Label($"Blueprint", Util.BoldLabel);
                        GUILayout.Label($"Name: {blueprint.name}");
                        GUILayout.Label($"Shop: {blueprint.ShopBlueprint.Name}");
                        GUILayout.Label($"Masterpiece: {blueprint.Masterpiece.Name}");
                        GUILayout.Label($"MasterpieceUnlock: {unlockMasterpiece} - {blueprint.MasterpieceUnlock?.Check()}");
                        GUILayout.Label($"HelpProject: {blueprint.HelpProject?.LocalizedName ?? "None"}");
                        GUILayout.Label($"OnProductionStarted: {onProductionStartedString}");
                        GUILayout.Label($"OnGiftReady: {OnGiftReadyString}");
                        GUILayout.Label($"OnGiftCollected: {OnGiftCollectedString}");
                        GUILayout.Label($"CanCollectGift: {canCollectGift}");
                        var tierProductionTimeString = blueprint.Tiers.Join(t => t.ProductionTime.ToString());
                        GUILayout.Label($"Tiers: {blueprint.Tiers.Length} ProductionTimes: {tierProductionTimeString}");
                        foreach (var itemDeck in blueprint.ItemDecks)
                        {
                            GUILayout.Label($"ItemDeck: {itemDeck.name}");
                            if (!string.IsNullOrEmpty(itemDeck.TypeName)) GUILayout.Label($"  TypeName: {itemDeck.TypeName}");
                            int tierIndex = 0;
                            foreach (var tier in itemDeck.Tiers)
                            {
                                var tierItemsString = tier.Packs.Join(pack => pack.Items.Join(item => item.Name));
                                if (tierItemsString == "") tierItemsString = "None";
                                GUILayout.Label($"  Tier {tierIndex++} Items: {tierItemsString}");
                            }
                        }
                    }
                }
            }
        }
        public static void ShowHistoryEntry(KingdomEventHistoryEntry eventHistory)
        {
            GUILayout.Label($"TriggeredOn: {eventHistory.TriggeredOn}");
            GUILayout.Label($"Solver: {eventHistory.Solver?.LocalizedName.ToString() ?? "None"}");
            GUILayout.Label($"SolverLeader: {eventHistory.SolverLeader}");
            GUILayout.Label($"SolutionCheck: {eventHistory.SolutionCheck}");
            GUILayout.Label($"SolvedInDays: {eventHistory.SolvedInDays}");
            GUILayout.Label($"Region: {eventHistory.Region?.LocalizedName ?? "None"}");
            GUILayout.Label($"IsShow: {eventHistory.IsShow}");
            GUILayout.Label($"IsUserClick: {eventHistory.IsUserClick}");
            GUILayout.Label($"SolveResults: {eventHistory.SolveResults.Join()}");
            GUILayout.Label($"SolveResultsFinal: {eventHistory.SolveResultsFinal.Join()}");
            GUILayout.Label($"TotalChanges: {eventHistory.TotalChanges.ToStringWithPrefix(" ")}");
            GUILayout.Label($"ResolutionChanges: {eventHistory.ResolutionChanges?.ToStringWithPrefix(" ") ?? "None"}");
            GUILayout.Label($"Type: {eventHistory.Type}");
            GUILayout.Label($"WasIgnored: {eventHistory.WasIgnored}");
            GUILayout.Label($"Blueprint", Util.BoldLabel);
            ShowBlueprintEvent(eventHistory.Event);
        }
        public static void ShowHistory()
        {
            if (KingdomState.Instance == null)
            {
                GUILayout.Label($"Kingdom not founded");
                return;
            }
            GUILayout.Label($"Event History", Util.BoldLabel);
            //KingdomEvent
            openHistoryRef.TryGetTarget(out KingdomEventHistoryEntry openHistory);
            foreach (var eventHistory in KingdomState.Instance.EventHistory)
            {
                GUILayout.BeginHorizontal();
                var labelStyle = Util.BoxLabel;
                GUILayout.Label($"{eventHistory.Event.DisplayName}", labelStyle);
                if (eventHistory != openHistory && GUILayout.Button("More", GUILayout.Width(45)))
                {
                    openHistoryRef.SetTarget(eventHistory);
                    openHistory = eventHistory;
                }
                else if (eventHistory == openHistory && GUILayout.Button("Less", GUILayout.Width(45)))
                {
                    openHistoryRef.SetTarget(null);
                    openHistory = null;
                }
                GUILayout.EndHorizontal();
                if (eventHistory == openHistory)
                {
                    ShowHistoryEntry(eventHistory);
                }
            }

            GUILayout.Label($"Finished Events", Util.BoldLabel);
            foreach (var eventHistory in KingdomState.Instance.FinishedEvents)
            {
                GUILayout.BeginHorizontal();
                var labelStyle = Util.BoxLabel;
                GUILayout.Label($"{eventHistory.Event.DisplayName}", labelStyle);
                if (eventHistory != openHistory && GUILayout.Button("More", GUILayout.Width(45)))
                {
                    openHistoryRef.SetTarget(eventHistory);
                    openHistory = eventHistory;
                }
                else if (eventHistory == openHistory && GUILayout.Button("Less", GUILayout.Width(45)))
                {
                    openHistoryRef.SetTarget(null);
                    openHistory = null;
                }
                GUILayout.EndHorizontal();
                if (eventHistory == openHistory)
                {
                    ShowHistoryEntry(eventHistory);
                }
            }
        }
        public static void ShowBlueprintEvent(BlueprintKingdomEventBase blueprint)
        {
            GUILayout.Label($"Bluerpint: {blueprint.DisplayName} ({blueprint.name})", Util.BoldLabel);
            GUILayout.Label($"Description: {blueprint.LocalizedDescription}");
            GUILayout.Label($"ResolutionTime: {blueprint.ResolutionTime} days");
            GUILayout.Label($"NeedToVistTheThroneRoom: {blueprint.NeedToVisitTheThroneRoom}");
            GUILayout.Label($"TriggerCondition: {Util.FormatConditions(blueprint.TriggerCondition)}");
            if (blueprint.HasDC) GUILayout.Label($"ResolutionDC: {blueprint.ResolutionDC}");
            if (!blueprint.HasDC) GUILayout.Label($"AutoResolveResult: {blueprint.AutoResolveResult}");
            if (blueprint is BlueprintKingdomEvent bke)
            {
                var actionText = Util.FormatActions(bke.OnTrigger);
                var statChangesText = bke.StatsOnTrigger.ToStringWithPrefix(" ");
                if (actionText != "") GUILayout.Label($"OnTrigger: {actionText}");
                if (!bke.StatsOnTrigger.IsEmpty) GUILayout.Label($"StatsOnTrigger: {statChangesText}");
            }
            if (blueprint is BlueprintKingdomProject bkp)
            {
                GUILayout.Label($"ProjectType: {bkp.ProjectType}");
            }
            if (blueprint is BlueprintKingdomClaim bkc)
            {
                if (bkc.KnownCondition != null && bkc.KnownCondition.Conditions.Length > 0) GUILayout.Label($"KnownConditions: {Util.FormatConditions(bkc.KnownCondition)}");
                if (bkc.FailCondition != null && bkc.FailCondition.Conditions.Length > 0) GUILayout.Label($"FailCondition: {Util.FormatConditions(bkc.FailCondition)}");
                if (string.IsNullOrEmpty(bkc.UnknownDescription)) GUILayout.Label($"UnknownDescription: {bkc.UnknownDescription}");
                if (string.IsNullOrEmpty(bkc.KnownDescription)) GUILayout.Label($"KnownDescription: {bkc.KnownDescription}");
                if (string.IsNullOrEmpty(bkc.FailedDescription)) GUILayout.Label($"FailedDescription: {bkc.FailedDescription}");
                if (string.IsNullOrEmpty(bkc.FulfilledDescription)) GUILayout.Label($"FulfilledDescription: {bkc.FulfilledDescription}");
            }
            GUILayout.Label($"ResolveAutomatically: {blueprint.ResolveAutomatically}");
            if (!blueprint.ResolveAutomatically)
            {
                foreach (var solution in blueprint.Solutions.Entries)
                {
                    foreach (var result in solution.Resolutions)
                    {
                        if (IsResultEmpty(result)) continue;
                        var statChangesText = result.StatChanges.ToStringWithPrefix(" ");
                        var actionText = Util.FormatActions(result.Actions);
                        GUILayout.Label($"PossibleSolution: {result.Margin}, Leader {solution.Leader}, DC {solution.DCModifier}", Util.BoldLabel);
                        if (result.LeaderAlignment != Kingmaker.UnitLogic.Alignments.AlignmentMaskType.Any) GUILayout.Label($"Alignment: {result.LeaderAlignment}");
                        if (actionText != "") GUILayout.Label($"Actions: {actionText}");
                        if (!result.StatChanges.IsEmpty) GUILayout.Label($"StatChanges: {statChangesText}");
                        if (result.SuccessCount != 0) GUILayout.Label($"SuccessCount: {result.SuccessCount}");
                        if (result.LocalizedDescription != "") GUILayout.Label($"Description: {result.LocalizedDescription}");
                        if (result.Condition != null && result.Condition.Conditions.Length > 0) GUILayout.Label($"Conditions: {Util.FormatConditions(result.Condition)}");
                    }
                }
            }
            var finalResults = blueprint.GetComponent<EventFinalResults>();
            if (finalResults != null) foreach (var result in finalResults.Results)
                {
                    var statChangesText = result.StatChanges.ToStringWithPrefix(" ");
                    var actionText = Util.FormatActions(result.Actions);
                    GUILayout.Label($"FinalResult {result.Margin}", Util.BoldLabel);
                    if (result.LeaderAlignment != Kingmaker.UnitLogic.Alignments.AlignmentMaskType.Any) GUILayout.Label($"Alignment: {result.LeaderAlignment}");
                    if (actionText != "") GUILayout.Label($"Actions: {actionText}");
                    if (!result.StatChanges.IsEmpty) GUILayout.Label($"StatChanges: {statChangesText}");
                    if (result.SuccessCount != 0) GUILayout.Label($"SuccessCount: {result.SuccessCount}");
                    if (result.LocalizedDescription != "") GUILayout.Label($"Description: {result.LocalizedDescription}");
                    if (result.Condition != null && result.Condition.Conditions.Length > 0) GUILayout.Label($"Conditions: {Util.FormatConditions(result.Condition)}");
                }
        }
        public static void ShowEvent(KingdomEvent activeEvent)
        {
            GUILayout.Label($"FullName {activeEvent.FullName}");
            GUILayout.Label($"CheckTriggerOnStart {activeEvent.CheckTriggerOnStart}");
            GUILayout.Label($"ContinueRecurrentSolution {activeEvent.ContinueRecurrentSolution}");
            GUILayout.Label($"DCModifier {activeEvent.DCModifier}");
            GUILayout.Label($"IgnoredOn {activeEvent.IgnoredOn}");
            GUILayout.Label($"IsFinished {activeEvent.IsFinished}");
            GUILayout.Label($"IsPlanned {activeEvent.IsPlanned}");
            GUILayout.Label($"IsRecurrent {activeEvent.IsRecurrent}");
            GUILayout.Label($"NeedsTask {activeEvent.NeedsTask}");
            GUILayout.Label($"StartedOn {activeEvent.StartedOn}");
            ShowBlueprintEvent(activeEvent.EventBlueprint);
        }
        public static void ShowActiveEvents()
        {
            if (KingdomState.Instance == null)
            {
                GUILayout.Label($"Kingdom not founded");
                return;
            }
            //KingdomEvent
            openEventRef.TryGetTarget(out KingdomEvent openEvent);
            foreach (var activeEvent in KingdomState.Instance.ActiveEvents)
            {
                GUILayout.BeginHorizontal();
                var timeString = "";
                if (activeEvent.IsPlanned)
                {
                    timeString = $"Starts in {activeEvent.StartedOn - KingdomState.Instance.CurrentDay} days";
                }
                else if (activeEvent.IsFinished)
                {
                    timeString = $"Finished";
                }
                else if (activeEvent.EventBlueprint.NeedToVisitTheThroneRoom && activeEvent.EventBlueprint.ResolveAutomatically)
                {
                    timeString = $"Should visit throneroom by {activeEvent.StartedOn + activeEvent.CalculateResolutionTime() - KingdomState.Instance.CurrentDay} days";
                }
                else
                {
                    timeString = "Active";
                }
                var labelStyle = Util.BoxLabel;
                GUILayout.Label($"{activeEvent.FullName}, {timeString}", labelStyle);
                if (activeEvent != openEvent && GUILayout.Button("More", GUILayout.Width(45)))
                {
                    openEventRef.SetTarget(activeEvent);
                    openEvent = activeEvent;
                }
                else if (activeEvent == openEvent && GUILayout.Button("Less", GUILayout.Width(45)))
                {
                    openEventRef.SetTarget(null);
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
        static List<string> m_TaskInfo;
        static List<string> GetTaskInfo(KingdomTask activeTask)
        {
            if(m_TaskInfo == null)
            {
                m_TaskInfo = new List<string>();
                m_TaskInfo.Add($"Description: {activeTask.Description}");
                m_TaskInfo.Add($"StartedOn: {activeTask.StartedOn}");
                m_TaskInfo.Add($"EndsOn: {activeTask.EndsOn}");
                m_TaskInfo.Add($"Duration: {activeTask.Duration}");
                m_TaskInfo.Add($"OneTimeBPCost: {activeTask.OneTimeBPCost}");
                m_TaskInfo.Add($"SkipPlayerTime: {activeTask.SkipPlayerTime}");
                m_TaskInfo.Add($"NeedsIgnore: {activeTask.NeedsIgnore}");
                m_TaskInfo.Add($"IsValid: {activeTask.IsValid}");
                m_TaskInfo.Add($"IsStarted: {activeTask.IsStarted}");
                m_TaskInfo.Add($"IsFinished: {activeTask.IsFinished}");
                m_TaskInfo.Add($"IsInProgress: {activeTask.IsInProgress}");
            }
            return m_TaskInfo;
        }
        public static void ShowActiveTasks()
        {
            if (KingdomState.Instance == null)
            {
                GUILayout.Label($"Kingdom not founded");
                return;
            }
            //KingdomTask
            openTaskRef.TryGetTarget(out KingdomTask openTask);
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
                var labelStyle = Util.BoxLabel;
                GUILayout.Label($"{activeTask.Name}, {timeString}", labelStyle);
                if (activeTask != openTask && GUILayout.Button("More", GUILayout.Width(45)))
                {
                    openTaskRef.SetTarget(activeTask);
                    openTask = activeTask;
                    m_TaskInfo = null;
                }
                else if (activeTask == openTask && GUILayout.Button("Less", GUILayout.Width(45)))
                {
                    openTaskRef.SetTarget(null);
                    openTask = null;
                    m_TaskInfo = null;
                }
                GUILayout.EndHorizontal();
                if (activeTask == openTask)
                {
                    if (activeTask.Region != null) GUILayout.Label($"Region: {activeTask.Region.Blueprint.LocalizedName}");
                    if (kte != null)
                    {
                        var taskInfo = GetTaskInfo(activeTask);
                        foreach (var text in taskInfo) GUILayout.Label(text);
                        var eventStatus = kte.Event.IsPlanned ?
                            $"Starts in {kte.Event.StartedOn - KingdomState.Instance.CurrentDay} days" :
                            kte.Event.IsFinished ?
                            $"Finished" :
                            "Active";
                        GUILayout.Label($"Event: {kte.Event.FullName} - {eventStatus}", Util.BoldLabel);
                        ShowEvent(kte.Event);
                    }
                }
            }
        }

    }
}
