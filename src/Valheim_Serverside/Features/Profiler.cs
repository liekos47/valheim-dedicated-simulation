using FeaturesLib;
using HarmonyLib;
using PluginConfiguration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Valheim_Serverside.Features
{
	/*
		A headless Valheim server has no Unity profiler, and under serverside simulation the main
		thread is the resource that runs out. This times a fixed set of methods with Stopwatch
		and logs, every Profiler.IntervalSeconds, how the interval's wall time was spent.

		Umbrella methods (MonoUpdaters.*, ZNet.Update) contain the detail methods, so the
		percentages are not meant to add up; read umbrellas for "where does the frame go" and
		details for "what inside it is expensive". Per-call cost and call counts are logged too,
		so a method that is cheap but called ten thousand times a second is visible.

		Off by default. Overhead when on is two Stopwatch reads per call of each target.
	*/
	public class Profiler : IFeature
	{
		public bool FeatureEnabled()
		{
			return Configuration.profilerEnabled.Value;
		}

		[HarmonyPatch]
		public static class Timing_Patch
		{
			static readonly (Type type, string method, bool umbrella)[] targets =
			{
				(typeof(MonoUpdaters), "Update", true),
				(typeof(MonoUpdaters), "FixedUpdate", true),
				(typeof(MonoUpdaters), "LateUpdate", true),
				(typeof(ZNet), "Update", true),
				(typeof(ZDOMan), "Update", false),
				(typeof(ZDOMan), "SendZDOToPeers2", false),
				(typeof(ZDOMan), "CreateSyncList", false),
				(typeof(ZDOMan), "ReleaseNearbyZDOS", false),
				(typeof(ZNetScene), "CreateDestroyObjects", false),
				(typeof(ZNetScene), "CreateObjects", false),
				(typeof(ZNetScene), "RemoveObjects", false),
				(typeof(ZoneSystem), "Update", false),
				(typeof(ZoneSystem), "CreateLocalZones", false),
				(typeof(ZoneSystem), "SpawnZone", false),
				(typeof(MonsterAI), "UpdateAI", false),
				(typeof(AnimalAI), "UpdateAI", false),
				(typeof(Character), "CustomFixedUpdate", false),
				(typeof(SpawnSystem), "UpdateSpawning", false),
				(typeof(RandEventSystem), "FixedUpdate", false),
			};

			class Stat
			{
				public string name;
				public bool umbrella;
				public long ticks;
				public long calls;
				public int depth;
				public long started;
			}

			static readonly Dictionary<MethodBase, Stat> stats = new Dictionary<MethodBase, Stat>();
			static MethodBase frameAnchor;
			static long intervalStart;
			static long frames;

			static IEnumerable<MethodBase> TargetMethods()
			{
				foreach (var (type, method, umbrella) in targets)
				{
					MethodBase m = AccessTools.Method(type, method);
					if (m == null)
					{
						ServersidePlugin.logger.LogWarning($"Profiler: {type.Name}.{method} not found, skipping");
						continue;
					}
					stats[m] = new Stat { name = $"{type.Name}.{method}", umbrella = umbrella };
					// ZNetScene.CreateDestroyObjects runs once per frame; use it to count frames and to report.
					if (type == typeof(ZNetScene) && method == "CreateDestroyObjects") frameAnchor = m;
					yield return m;
				}
				intervalStart = Stopwatch.GetTimestamp();
			}

			[HarmonyPriority(Priority.First)]
			static void Prefix(MethodBase __originalMethod)
			{
				if (!stats.TryGetValue(__originalMethod, out Stat s)) return;
				if (s.depth++ == 0) s.started = Stopwatch.GetTimestamp();
			}

			[HarmonyPriority(Priority.Last)]
			static void Postfix(MethodBase __originalMethod)
			{
				if (!stats.TryGetValue(__originalMethod, out Stat s)) return;
				if (--s.depth == 0)
				{
					s.ticks += Stopwatch.GetTimestamp() - s.started;
					s.calls++;
				}
				if (__originalMethod == frameAnchor) OnFrame();
			}

			static void OnFrame()
			{
				frames++;
				long now = Stopwatch.GetTimestamp();
				double elapsedMs = (now - intervalStart) * 1000.0 / Stopwatch.Frequency;
				if (elapsedMs < Configuration.profilerIntervalSeconds.Value * 1000.0) return;

				var sb = new StringBuilder();
				sb.Append($"Profiler: {elapsedMs / 1000.0:F0}s, {frames} frames ({elapsedMs / frames:F1} ms/frame), players={ZNet.instance?.GetNrOfPlayers() ?? 0}");
				foreach (var s in stats.Values.Where(x => x.calls > 0).OrderByDescending(x => x.umbrella).ThenByDescending(x => x.ticks))
				{
					double ms = s.ticks * 1000.0 / Stopwatch.Frequency;
					sb.Append($"\n  {(s.umbrella ? "[frame] " : "        ")}{s.name,-34} {ms / elapsedMs * 100.0,5:F1}%  {ms,8:F0} ms  {s.calls,8} calls  {ms / s.calls,7:F3} ms/call");
					s.ticks = 0; s.calls = 0;
				}
				ServersidePlugin.logger.LogInfo(sb.ToString());
				frames = 0;
				intervalStart = now;
			}
		}
	}
}
