using FeaturesLib;
using HarmonyLib;
using System;

namespace Valheim_Serverside.Features
{
	/*
		When a routed RPC handler throws, vanilla logs "Exception in ZRpc::HandlePackage" and the
		stack trace - but not which peer sent it or which RPC it was. Under serverside simulation
		the server receives every RPC aimed at an object it owns, so a client-side mod sending a
		malformed message (wrong parameter count for this game version) shows up here with no way
		to tell whose it is.

		This records the sender and method hash of the routed RPC being handled, and a Harmony
		finalizer logs them when the handler throws. The hash is decoded with
		HookCheck --findhash <hash>, or matched against the RPC list from HookCheck --rpcsigs.
	*/
	public class RpcDiagnostics : IFeature
	{
		public bool FeatureEnabled()
		{
			return true;
		}

		[HarmonyPatch(typeof(ZRoutedRpc), "HandleRoutedRPC")]
		public static class ZRoutedRpc_HandleRoutedRPC_Patch
		{
			static void Prefix(ZRoutedRpc.RoutedRPCData data, out ZRoutedRpc.RoutedRPCData __state)
			{
				__state = data;
			}

			static Exception Finalizer(Exception __exception, ZRoutedRpc.RoutedRPCData __state)
			{
				if (__exception == null)
				{
					return null;
				}
				string sender = __state.m_senderPeerID.ToString();
				ZNetPeer peer = ZNet.instance ? ZNet.instance.GetPeer(__state.m_senderPeerID) : null;
				if (peer != null)
				{
					sender = $"{peer.m_playerName} [{peer.m_uid}]";
				}
				Exception inner = __exception;
				while (inner.InnerException != null)
				{
					inner = inner.InnerException;
				}
				ServersidePlugin.logger.LogWarning($"Routed RPC hash {__state.m_methodHash} from {sender} to ZDO {__state.m_targetZDO} threw {inner.GetType().Name}: {inner.Message}");
				// Rethrow so vanilla's own handling and logging are unchanged.
				return __exception;
			}
		}
	}
}
