using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KingdomResolution
{
    public class Labels
    {
        public static string EventTimeFactorLabel = "Event Time Factor";
        public static string EventTimeFactorTooltip = "Adjust the duration of kingdom events from 1 day to 100% (normal duration)";
        public static string ProjectTimeFactorLabel = "Project Time Factor";
        public static string ProjectTimeFactorTooltip = "Adjust the duration of kingdom projects excluding projects that require player assistance from 1 day to 100% (normal duration)";
        public static string RulerTimeFactorLabel = "Ruler Managed Project Time Factor";
        public static string RulerTimeFactorTooltip = "Adjust the duration of kingdom projects that require projects that require player assistance from 1 day to 100% (normal duration)";
        public static string EventPriceFactorLabel = "Event BP Price Factor";
        public static string EventPriceFactorTooltip = "Adjust the BP price of kingdom projects and events from 0% (free) to 100% (full price)";
        public static string EasyEventsLabel = "Enable Easy Events";
        public static string EasyEventsTooltip = "Reduce the DC of kingdom events to -100 resulting in trimumph event results";
        public static string AlwaysManageKingdomLabel = "Enable Manage Kingdom Everywhere";
        public static string AlwaysManageKingdomTooltip = "Allows the Kingdom Management screen to be accessed even while in unclaimed regions.";
        public static string AlwaysAdvanceTimeLabel = "Enable Skip Day/Claim Region Everywhere";
        public static string AlwaysAdvanceTimeTooltip = "Enable the ability to claim regions and skip one day from the kingdom management screen while outside of a settlement.";
        public static string SkipPlayerTimeLabel = "Disable Skip Player Time";
        public static string SkipPlayerTimeTooltip = "Prevents the game from auto-advancing time the instant you start a project requiring player assistance (claim territory or advance an adviser/realm skill).";
        public static string AlwaysBaronProcurementLabel = "Enable Ruler Procure Rations Everywhere (DLC Only)";
        public static string AlwaysBaronProcurementTooltip = "Enables the player to use their special camping ability to procure rations even while outside of the player's territory or in a dungeon.";
        public static string OverrideIgnoreEventsLabel = "Disable End of Month Failed Events";
        public static string OverrideIgnoreEventsTooltip = "Prevents automatically failing ignored events at the end of the month.";
        public static string DisableAutoAssignLeadersLabel = "Disable Auto Assign Leaders";
        public static string DisableAutoAssignLeadersTooltip = "Prevents leaders from been automatically assigned a task at the start of the month.";
        public static string CurrencyFallbackLabel = "Enable Currency Fallback";
        public static string CurrencyFallbackTooltip = "Automatically purchase BP with gold when starting kindom projects, building settlements or claiming resources with insufficent BP";
        public static string CurrencyFallbackExchangeRateLabel = "Currency Fallback Exchange Rate";
        public static string CurrencyFallbackExchangeRateTooltip = "Exchange rate for purching BP with gold (default 80 gold per BP)";
        public static string PauseKingdomTimelineLabel = "Pause Kingdom Timeline";
        public static string PauseKingdomTimelineTooltip = "Prevents kingdom timeline events. kingdom events, kingdom projects, kingdom buffs, building settlements, artisans, BP gain from progressing. Does not effect timed quests, or other instances of game time. Works by increasing the kingdom founding day by one every day.";
        public static string EnablePausedKingdomManagementLabel = "Enable Paused Kingdom Management";
        public static string EnablePausedKingdomManagementTooltip = "Allows kingdom tasks, kingdom events, kingdom projects, artisans, kingdom buffs, building settlements, artisans, weekly BP gain to progress normally while preventing kingdom timeline events from starting.";
        public static string EnablePausedRandomEventsLabel = "Enable Paused Random Events";
        public static string EnablePausedRandomEventsTooltip = "Every month, a set of random kingdom events are schedules to begin at random intervals across the month.Enable Paused Random Events allows these random events to occur while the kingdom timeline is paused.";
        public static string PreviewEventResultsLabel = "Preview Event Results";
        public static string PreviewEventResultsTooltip = "Shows the final stat gains associated with an event. Event results are based on the leader type and leader alignment selected. The Best Result preview only takes into account stat gains and not BP bonuses. Does not show action triggers associated with event results";
        public static string PreviewDialogResultsLabel = "Preview Dialog Results";
        public static string PreviewDialogResultsTooltip = "Preview actions that will trigger as a result of a dialog choice below the option";
        public static string PreviewAlignmentRestrictedDialogLabel = "Preview Alignment Restricted Dialog";
        public static string PreviewAlignmentRestrictedDialogTooltip = "Preview dialog options text that are normally hidden by \"Specific alignment required\" message.";
        public static string PreviewRandomEncountersLabel = "Preview Random Encounters";
        public static string PreviewRandomEncountersTooltip = "Shows Random Encounter name, type and challenge rating in the random encounter popup on the global map.";
    }
}
