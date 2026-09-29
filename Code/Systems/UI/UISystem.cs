using Game.UI;
using Game.UI.InGame;

namespace BoardingController.Systems.UI
{
    public partial class UISystem : UISystemBase
    {
        private SelectedInfoUISystem m_SelectedInfoUISystem;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_SelectedInfoUISystem = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();

            AddUIBindings();
        }
    }
}
