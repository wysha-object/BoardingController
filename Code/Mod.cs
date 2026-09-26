using System.Reflection;
using BoardingController.Systems.Simulation;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.Pathfind;
using Game.SceneFlow;
using Game.Simulation;
using GameBoardingController.Systems.Pathfind;
using Unity.Entities;

namespace BoardingController
{
    public class Mod : IMod
    {
        public static readonly string s_InformationalVersion = (
            (AssemblyInformationalVersionAttribute)System.Attribute.GetCustomAttribute(Assembly.GetAssembly(typeof(Mod)), typeof(AssemblyInformationalVersionAttribute))
        ).InformationalVersion;

        public static ILog s_Log = LogManager.GetLogger(nameof(BoardingController)).SetShowsErrorsInUI(false);

        public static Settings s_Settings;

        public static string ReleaseChannel()
        {
#if STABLE
            return "Stable";
#elif BETA
            return "Beta";
#else
            return "UNKNOWN";
#endif
        }

        public void OnLoad(UpdateSystem updateSystem)
        {
            s_Log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                s_Log.Info($"Current mod asset at {asset.path}");

            s_Settings = new Settings(this);

            SetupSystem(updateSystem);
        }

        private void SetupSystem(UpdateSystem updateSystem)
        {
            //updateSystem.World.GetOrCreateSystemManaged<RoutesModifiedSystem>().Enabled = false;
            updateSystem.World.GetOrCreateSystemManaged<TransportCarAISystem>().Enabled = false;

            //updateSystem.UpdateAt<PatchedRoutesModifiedSystem>(SystemUpdatePhase.ModificationEnd);
            updateSystem.UpdateAt<PatchedTransportCarAISystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<PatchedTransportCarAISystem>(SystemUpdatePhase.LoadSimulation);
            updateSystem.UpdateAt<Systems.Tool.ToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<Systems.UI.UISystem>(SystemUpdatePhase.UIUpdate);
        }

        public void OnDispose()
        {
            s_Log.Info(nameof(OnDispose));
        }
    }
}
