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

		public static ConfigEntry<bool> profilerEnabled;
		public static ConfigEntry<int> profilerIntervalSeconds;

		public static ConfigEntry<bool> skipDistantObjects;
		public static ConfigEntry<int> createDestroyIntervalMs;

		public static void Load(ConfigFile config)
		{
			modEnabled = config.Bind<bool>("General", "Enabled", true, "Enable or disable the mod");

			maxObjectsPerFrameEnabled = config.Bind<bool>("MaxObjectsPerFrame", "Enabled", true, "Enable or disable the feature");
			maxObjectsPerFrame = config.Bind<int>("MaxObjectsPerFrame", "MaxObjects", 100, "Maximum number of objects the server can create per frame.");

			serverSimulationDistanceEnabled = config.Bind<bool>("ServerSimulationDistance", "Enabled", true,
				"Pin the server's simulation distance at startup. With serverside simulation the server does the work every client would have done around itself, so a large distance multiplies server CPU.");
			serverSimulationDistanceLevel = config.Bind<int>("ServerSimulationDistance", "Level", 0,
				"Valheim 1.0 simulation distance level the server simulates around each player, and caps joining clients to. 0 = classic (3x3 zones fully simulated, 5x5 loaded), the distance this mod was designed for. 1-5 = larger rings, much more CPU. -1 = leave the server's own value and only log it.");

			profilerEnabled = config.Bind<bool>("Profiler", "Enabled", false,
				"Log, every IntervalSeconds, how the server's main thread time is split between zone loading, object creation, ZDO sync, AI and character updates. Diagnostic; small overhead while on.");
			profilerIntervalSeconds = config.Bind<int>("Profiler", "IntervalSeconds", 60, "Seconds between profiler reports.");

			skipDistantObjects = config.Bind<bool>("Performance", "SkipDistantObjects", false,
				"Do not instantiate the distant (far ring) objects on the server. Clients instantiate them for themselves from the ZDOs; the server only needs the near ring it simulates. Saves memory and load-time CPU. Experimental: test with players before leaving it on.");
			createDestroyIntervalMs = config.Bind<int>("Performance", "CreateDestroyIntervalMs", 0,
				"Minimum milliseconds between runs of the per-player object create/destroy sweep. 0 = every frame like vanilla. Clients are unaffected (they load objects from ZDOs themselves); a small value such as 50 spreads server load.");
		}
	}
}
