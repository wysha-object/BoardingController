using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;

namespace BoardingController
{
    [FileLocation($"ModsSettings/{nameof(BoardingController)}/{nameof(BoardingController)}")]
    public class Settings : ModSetting
    {
        public const string kTabGeneral = "TabGeneral";
        public const string kGroupVersion = "GroupVersion";

        [SettingsUISection(kTabGeneral, kGroupVersion)]
        public string ReleaseChannel => Mod.ReleaseChannel();

        [SettingsUISection(kTabGeneral, kGroupVersion)]
        public string Version => Mod.s_InformationalVersion;

        public Settings(IMod mod)
            : base(mod)
        {
            SetDefaults();
            AssetDatabase.global.LoadSettings(nameof(BoardingController), this);
            RegisterInOptionsUI();
            RegisterKeyBindings();
        }

        public override void SetDefaults() { }

        public override void Apply()
        {
            base.Apply();
            RegisterInOptionsUI();
            RegisterKeyBindings();
        }
    }
}
