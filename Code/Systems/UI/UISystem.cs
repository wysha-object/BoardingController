using Game.UI;
using Game.UI.InGame;

namespace BoardingController.Systems.UI
{
    public partial class UISystem : UISystemBase
    {
        public enum ToolState
        {
            Disabled = 0,
            Enabled = 1,
        }

        private SelectedInfoUISystem m_SelectedInfoUISystem;

        private BoardingController.Systems.Tool.ToolSystem m_BoardingControllerToolSystem;

        private Game.Tools.ToolSystem m_GameToolSystem;

        public ToolState GetToolState()
        {
            return (ToolState)m_GetToolStateBinding.value;
        }

        public void SetToolState(ToolState toolState)
        {
            if (toolState == GetToolState())
            {
                return;
            }
            m_GetToolStateBinding.Update((int)toolState);
            switch (toolState)
            {
                case ToolState.Disabled:
                    m_BoardingControllerToolSystem.Disable();
                    break;
                case ToolState.Enabled:
                    m_BoardingControllerToolSystem.Enable();
                    break;
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_SelectedInfoUISystem = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();
            m_BoardingControllerToolSystem = World.GetOrCreateSystemManaged<Tool.ToolSystem>();
            m_GameToolSystem = World.GetOrCreateSystemManaged<Game.Tools.ToolSystem>();

            m_GameToolSystem.EventToolChanged += (system) =>
            {
                if (system != m_BoardingControllerToolSystem)
                {
                    SetToolState(ToolState.Disabled);
                }
            };

            AddUIBindings();
        }
    }
}
