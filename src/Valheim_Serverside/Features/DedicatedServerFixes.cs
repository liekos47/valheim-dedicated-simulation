using FeaturesLib;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Valheim_Serverside.Features
{
	public class DedicatedServerFixes : IFeature
	{
		public bool FeatureEnabled()
		{
			return true;
		}

		/*
			Valheim 1.0 added a `gamepadEffectsExclusiveToPlayer` argument to EffectList.Create and
			fills it with `Player.m_localPlayer.GetZDOID()`. That is fine on a client, but
			m_localPlayer is always null on a dedicated server, so every owner-side method that does
			it throws NullReferenceException once this mod makes the server the owner. The visible
			symptom is picking a berry bush: the client shows "+1" and nothing reaches the inventory,
			because Pickable.RPC_Pick dies before it drops the item.

			This transpiler replaces the `Player.m_localPlayer.GetZDOID()` pair with a call that
			returns ZDOID.None when there is no local player and is otherwise identical.

			The target list is every method in assembly_valheim with that instruction pair. Regenerate
			it after a game update with:
			    HookCheck assembly_valheim.dll com.rlabrecque.steamworks.net.dll --pattern m_localPlayer GetZDOID
		*/
		[HarmonyPatch]
		public static class NullLocalPlayer_GetZDOID_Patch
		{
			static readonly (System.Type type, string method)[] targets =
			{
				(typeof(Pickable), "RPC_Pick"),
				(typeof(Trap), "RPC_OnStateChanged"),
				(typeof(Container), "SetInUse"),
				(typeof(Ship), "UpdateSailSize"),
				(typeof(Sadle), "IsLocalUser"),
				(typeof(ZSFX), "IsPlayerCreator"),
			};

			static IEnumerable<MethodBase> TargetMethods()
			{
				foreach (var (type, method) in targets)
				{
					MethodBase m = AccessTools.Method(type, method);
					if (m == null)
					{
						ServersidePlugin.logger.LogWarning($"NullLocalPlayer fix: {type.Name}.{method} not found, skipping");
						continue;
					}
					yield return m;
				}
			}

			public static ZDOID LocalPlayerZDOID()
			{
				Player localPlayer = Player.m_localPlayer;
				return localPlayer ? localPlayer.GetZDOID() : ZDOID.None;
			}

			static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
			{
				FieldInfo localPlayer = AccessTools.Field(typeof(Player), nameof(Player.m_localPlayer));
				MethodInfo getZdoid = AccessTools.Method(typeof(Character), nameof(Character.GetZDOID));
				MethodInfo safe = AccessTools.Method(typeof(NullLocalPlayer_GetZDOID_Patch), nameof(LocalPlayerZDOID));

				List<CodeInstruction> code = instructions.ToList();
				int replaced = 0;
				for (int i = 0; i < code.Count; i++)
				{
					if (i + 1 < code.Count && code[i].LoadsField(localPlayer) && code[i + 1].Calls(getZdoid))
					{
						// Keep any jump labels that pointed at the field load.
						yield return new CodeInstruction(OpCodes.Call, safe).WithLabels(code[i].labels);
						i++;
						replaced++;
						continue;
					}
					yield return code[i];
				}

				if (replaced == 0)
				{
					ServersidePlugin.logger.LogWarning($"NullLocalPlayer fix: pattern not found in {original.DeclaringType?.Name}.{original.Name}; it may crash on the server");
				}
			}
		}
	}
}
