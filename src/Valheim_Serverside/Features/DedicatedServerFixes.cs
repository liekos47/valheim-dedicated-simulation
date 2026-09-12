using FeaturesLib;
using HarmonyLib;
using System;
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
			Player.m_localPlayer is always null on a dedicated server. Vanilla gets away with
			dereferencing it in owner-side code because a client always owns the object; this mod
			makes the server the owner, so the server runs that code and throws. Seen live on 1.0.12:

			  - Pickable.RPC_Pick: Valheim 1.0 passes Player.m_localPlayer.GetZDOID() to
			    EffectList.Create (a new gamepad-rumble argument). The client shows "+1" and
			    nothing reaches the inventory, because the throw happens before the item drops.
			  - Ship.UpdateSailSize: Player.m_localPlayer.GetPlayerID() compared against the
			    ship's current user, every physics tick, for any server-owned ship.

			This transpiler replaces `Player.m_localPlayer.X()` for the zero-argument calls below
			with a call that returns a neutral value when there is no local player and is otherwise
			identical. Targets are the methods in assembly_valheim that contain such a pair and can
			run on a server (UI classes are left alone). Regenerate the list after a game update:

			    HookCheck assembly_valheim.dll com.rlabrecque.steamworks.net.dll --pattern m_localPlayer GetZDOID
			    HookCheck ... --pattern m_localPlayer GetPlayerID
			    HookCheck ... --pattern m_localPlayer GetPlayerName
		*/
		[HarmonyPatch]
		public static class NullLocalPlayer_Patch
		{
			static readonly (Type type, string method)[] targets =
			{
				// m_localPlayer.GetZDOID()
				(typeof(Pickable), "RPC_Pick"),
				(typeof(Trap), "RPC_OnStateChanged"),
				(typeof(Container), "SetInUse"),
				(typeof(Ship), "UpdateSailSize"),
				(typeof(Sadle), "IsLocalUser"),
				(typeof(ZSFX), "IsPlayerCreator"),
				// m_localPlayer.GetPlayerID() and/or GetPlayerName()
				(typeof(CookingStation), "SpawnItem"),
				(typeof(Piece), "CheckClusteredBuildPieceStats"),
				(typeof(PrivateArea), "HaveLocalAccess"),
			};

			// callee name -> null-safe replacement on this class
			static readonly Dictionary<string, string> safeCalls = new Dictionary<string, string>
			{
				{ "GetZDOID", nameof(LocalPlayerZDOID) },
				{ "GetPlayerID", nameof(LocalPlayerID) },
				{ "GetPlayerName", nameof(LocalPlayerName) },
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

			public static long LocalPlayerID()
			{
				Player localPlayer = Player.m_localPlayer;
				return localPlayer ? localPlayer.GetPlayerID() : 0L;
			}

			public static string LocalPlayerName()
			{
				Player localPlayer = Player.m_localPlayer;
				return localPlayer ? localPlayer.GetPlayerName() : "";
			}

			static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
			{
				FieldInfo localPlayer = AccessTools.Field(typeof(Player), nameof(Player.m_localPlayer));
				List<CodeInstruction> code = instructions.ToList();
				int replaced = 0;
				for (int i = 0; i < code.Count; i++)
				{
					if (i + 1 < code.Count
						&& code[i].LoadsField(localPlayer)
						&& (code[i + 1].opcode == OpCodes.Callvirt || code[i + 1].opcode == OpCodes.Call)
						&& code[i + 1].operand is MethodInfo callee
						&& callee.GetParameters().Length == 0
						&& safeCalls.TryGetValue(callee.Name, out string helperName))
					{
						MethodInfo helper = AccessTools.Method(typeof(NullLocalPlayer_Patch), helperName);
						// Keep any jump labels that pointed at the field load.
						yield return new CodeInstruction(OpCodes.Call, helper).WithLabels(code[i].labels);
						i++;
						replaced++;
						continue;
					}
					yield return code[i];
				}

				string where = $"{original.DeclaringType?.Name}.{original.Name}";
				if (replaced == 0)
				{
					ServersidePlugin.logger.LogWarning($"NullLocalPlayer fix: pattern not found in {where}; it may crash on the server");
				}
				else
				{
					ServersidePlugin.logger.LogInfo($"NullLocalPlayer fix: {replaced} call(s) made null-safe in {where}");
				}
			}
		}

		/*
			Ship.UpdateSailSize animates the sail furling, drives the sail cloth and plays the furl
			effect. It is purely visual: m_sailPosition, the value it maintains, is read by nothing
			else, and the sail force in the physics step comes from m_speed. Clients run it for
			themselves, so the server has no reason to, and skipping it also avoids the cloth
			component, which has no business on a headless build.
		*/
		[HarmonyPatch(typeof(Ship), "UpdateSailSize")]
		public static class Ship_UpdateSailSize_SkipOnServer_Patch
		{
			static bool Prefix()
			{
				return !(ZNet.instance && ZNet.instance.IsDedicated());
			}
		}
	}
}
