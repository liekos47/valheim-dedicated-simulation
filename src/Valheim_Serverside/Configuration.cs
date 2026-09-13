using BepInEx.Configuration;

namespace PluginConfiguration
{
	public class Configuration
	{
		public static ConfigEntry<bool> modEnabled;

		public static ConfigEntry<bool> maxObjectsPerFrameEnabled;
		public static ConfigEntry<int> maxObjectsPerFrame;

		public static ConfigEntry<bool> serverSimulationDistanceEnabled;
		public static ConfigEntry<int> serverSimulationDistanceLevel;

		public static void Load(ConfigFile config)
		{
			modEnabled = config.Bind<bool>("General", "Enabled", true, "Enable or disable the mod");

			maxObjectsPerFrameEnabled = config.Bind<bool>("MaxObjectsPerFrame", "Enabled", true, "Enable or disable the feature");
			maxObjectsPerFrame = config.Bind<int>("MaxObjectsPerFrame", "MaxObjects", 100, "Maximum number of objects the server can create per frame.");

			serverSimulationDistanceEnabled = config.Bind<bool>("ServerSimulationDistance", "Enabled", true,
				"Pin the server's simulation distance at startup. With serverside simulation the server does the work every client would have done around itself, so a large distance multiplies server CPU.");
			serverSimulationDistanceLevel = config.Bind<int>("ServerSimulationDistance", "Level", 0,
				"Valheim 1.0 simulation distance level the server simulates around each player, and caps joining clients to. 0 = classic (3x3 zones fully simulated, 5x5 loaded), the distance this mod was designed for. 1-5 = larger rings, much more CPU. -1 = leave the server's own value and only log it.");
		}
	}
}
