using Kingmaker.UI.SettingsUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public bool alwaysManageKingdom = false;
        public bool alwaysBaronProcurement = false;
        public bool previewEventResults = false;
        public bool previewDialogResults = false;
        public bool previewAlignmentRestrictedDialog = false;
        public BindingKeysData kingdomStashHotkey;
        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }
}
