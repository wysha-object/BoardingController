using System.Reflection;
using BoardingController.Systems.Routes;
using BoardingController.Systems.Simulation;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.Pathfind;
using Game.Routes;
using Game.SceneFlow;
using Game.Simulation;
using Game.Tools;
using GameBoardingController.Systems.Pathfind;

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
            updateSystem.World.GetOrCreateSystemManaged<SegmentCurveSystem>().Enabled = false;
            //updateSystem.World.GetOrCreateSystemManaged<RoutesModifiedSystem>().Enabled = false;
            updateSystem.World.GetOrCreateSystemManaged<TransportCarAISystem>().Enabled = false;
            updateSystem.World.GetOrCreateSystemManaged<ResidentAISystem>().Enabled = false;

            updateSystem.UpdateAt<PatchedSegmentCurveSystem>(SystemUpdatePhase.Modification5);
            //updateSystem.UpdateAt<PatchedRoutesModifiedSystem>(SystemUpdatePhase.ModificationEnd);
            updateSystem.UpdateAt<PatchedTransportCarAISystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateBefore<PatchedResidentAISystem, ResidentAISystem.Actions>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<PatchedTransportCarAISystem>(SystemUpdatePhase.LoadSimulation);
            updateSystem.UpdateAt<Systems.UI.UISystem>(SystemUpdatePhase.UIUpdate);
        }

        public void OnDispose()
        {
            s_Log.Info(nameof(OnDispose));
        }
    }
}
