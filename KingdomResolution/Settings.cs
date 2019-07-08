using Kingmaker.UI.SettingsUI;
using UnityModManagerNet;

namespace KingdomResolution
{
    public class Settings : UnityModManager.ModSettings
    {
        public float eventTimeFactor = 1;
        public float projectTimeFactor = 1;
        public float baronTimeFactor = 1;
        public float eventPriceFactor = 1;
        public bool skipPlayerTime = false;
        public bool overrideIgnoreEvents = false;
        public bool easyEvents = false;
        public bool pauseKingdomTimeline = false;
        public bool enablePausedKingdomManagement = false;
        public bool enablePausedRandomEvents = false;
        public bool disableAutoAssignLeaders = false;
        public bool alwaysManageKingdom = false;
        public bool alwaysAdvanceTime = false;
        public bool alwaysBaronProcurement = false;
        public bool previewEventResults = false;
        public bool previewDialogResults = false;
        public bool previewAlignmentRestrictedDialog = false;
        public bool previewRandomEncounters = false;
        public bool disableMercenaryPenalty = false;
        public bool currencyFallback = false;
        public int currencyFallbackExchangeRate = 80;
        public BindingKeysData kingdomStashHotkey;
        public bool highlightObjectsToggle;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }
}
