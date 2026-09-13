using BepInEx;
using BepInEx.Logging;
using FeaturesLib;
using HarmonyLib;
using PatchingLib;
using PluginConfiguration;
using Requirements;

namespace Valheim_Serverside
{

	[Harmony]
	[BepInPlugin(PluginGuid, "Dedicated Simulation", "1.1.9")]
	[BepInDependency(ValheimPlusPluginId, BepInDependency.DependencyFlags.SoftDependency)]

	public class ServersidePlugin : BaseUnityPlugin
	{

		private static ServersidePlugin context;

		public static Configuration configuration;

		public static Harmony harmony;

		public const string PluginGuid = "liekos47.DedicatedSimulation";

		public const string ValheimPlusPluginId = "org.bepinex.plugins.valheim_plus";

		public static ManualLogSource logger;

		private void Awake()
		{
			context = this;
			logger = Logger;

			Configuration.Load(Config);

			if (!ModIsEnabled())
			{
				Logger.LogInfo("Dedicated Simulation is disabled. (configuration)");
				return;
			}
			else if (!IsDedicated())
			{
				Logger.LogInfo("Dedicated Simulation is disabled. (not a dedicated server)");
				return;
			}
			Logger.LogInfo("Installing Dedicated Simulation");

			harmony = new Harmony(PluginGuid);

			AvailableFeatures availableFeatures = new AvailableFeatures();
			availableFeatures.AddFeature(new Features.Core());
			availableFeatures.AddFeature(new Features.DedicatedServerFixes());
			availableFeatures.AddFeature(new Features.ServerSimulationDistance());
			availableFeatures.AddFeature(new Features.Profiler());
			availableFeatures.AddFeature(new Features.RpcDiagnostics());
			availableFeatures.AddFeature(new Features.MaxObjectsPerFrame());
			availableFeatures.AddFeature(new Features.Debugging());
			availableFeatures.AddFeature(new Features.Compat_ValheimPlus());

			PatchRequirements patchRequirements = new PatchRequirements();
			patchRequirements.AddRequirement(new PatchRequirement.DebugBuild());

			new HarmonyFeaturesPatcher(patchRequirements).PatchAll(availableFeatures.GetAllNestedTypes(), harmony);

			Logger.LogInfo("Dedicated Simulation installed");
		}

		public bool ModIsEnabled()
		{
			return Configuration.modEnabled.Value;
		}

		public static bool IsDedicated()
		{
			return new ZNet().IsDedicated();
		}
	}

}
