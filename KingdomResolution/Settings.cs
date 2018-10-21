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
        public bool skipEvents = true;
        public int DCModifier = 0;
        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }
}
