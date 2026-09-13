using FeaturesLib;
using HarmonyLib;
using PluginConfiguration;

namespace Valheim_Serverside.Features
{
	public class ServerSimulationDistance : IFeature
	{
		public bool FeatureEnabled()
		{
			return Configuration.serverSimulationDistanceEnabled.Value;
		}

		/*
			Valheim 1.0 replaced the fixed 3x3 active area with a SimulationDistance chosen per client
			(a graphics setting, levels 0-5). The server keeps its own value and clamps every client's
			request down to it (ZNet.RPC_RequestValidSimulationDistance), but never raises its own.

			Under serverside simulation that value decides how much world the server simulates around
			EACH player, so it is the single biggest lever on server CPU. The original mod was written
			for the classic ring: 3x3 zones fully simulated, 5x5 loaded. This pins the server to a
			configured level at startup, before any client connects, so joining clients are capped to
			it as well. Level -1 only logs what the server would otherwise have used.

			Applied from a postfix on ZNet.Awake, which is where vanilla runs its own
			SimulationDistanceServerHandshake; ZNet.ApplySimulationDistance also pushes the value into
			ZoneSystem, Water and Heightmap so every consumer agrees.
		*/
		[HarmonyPatch(typeof(ZNet), "Awake")]
		public static class ZNet_Awake_Patch
		{
			static void Postfix(ZNet __instance)
			{
				SimulationDistance current = __instance.GetDesiredSimulationDistance();
				ServersidePlugin.logger.LogInfo($"Simulation distance: server default is near={current.NearSimulationDistance} far={current.FarSimulationDistance} classic={current.IsClassic}");

				int level = Configuration.serverSimulationDistanceLevel.Value;
				if (level < 0)
				{
					ServersidePlugin.logger.LogInfo("Simulation distance: Level is -1, leaving the server's own value");
					return;
				}

				SimulationDistance wanted = SimulationDistance.GetSimulationDistance(level);
				if (wanted.Equals(current))
				{
					ServersidePlugin.logger.LogInfo($"Simulation distance: already at level {level}, nothing to change");
					return;
				}

				__instance.ApplySimulationDistance(wanted);
				ServersidePlugin.logger.LogInfo($"Simulation distance: applied level {level} (near={wanted.NearSimulationDistance} far={wanted.FarSimulationDistance} classic={wanted.IsClassic}); clients will be capped to it");
			}
		}
	}
}
