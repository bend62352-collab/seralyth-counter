using System.Reflection;
using ExitGames.Client.Photon;
using HarmonyLib;
using SeralythCounter.Backend;

namespace SeralythCounter;

public abstract class HarmonyPatches
{
    private static Harmony _instance;
    public static bool IsPatched { get; private set; }
    public const string InstanceId = Constants.Guid;

    internal static void ApplyHarmonyPatches()
    {
        if (IsPatched) return;
        _instance ??= new Harmony(InstanceId);
        _instance.PatchAll(Assembly.GetExecutingAssembly());
        IsPatched = true;
    }

    internal static void RemoveHarmonyPatches()
    {
        if (_instance == null || !IsPatched) return;
        _instance.UnpatchSelf();
        IsPatched = false;
    }
}

/// <summary>
/// Harmony prefix on IPhotonPeerListener.OnOperationResponse (or the PUN2
/// EventData dispatch path). In PUN2 the low-level entry point for custom
/// events is LoadBalancingPeer.OnEvent → DispatchCallback. We patch
/// NetworkingClient.OnEvent so we can silently drop Seralyth event 71
/// when EventBlockEnabled is true.
///
/// Adjust the target type/method to match your exact PUN2 assembly if
/// the harmony log reports it not found.
/// </summary>
[HarmonyPatch]
public static class PhotonEventBlockPatch
{
    private const byte SeralythByte = 71;

    // Target: Photon.Realtime.LoadBalancingClient.OnEvent(EventData)
    // PUN2 ships this in Photon.Realtime.dll; fall back to the PUN assembly.
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        // Try Photon.Realtime assembly first
        System.Type t =
            System.Type.GetType("Photon.Realtime.LoadBalancingClient, Photon.Realtime") ??
            System.Type.GetType("Photon.Realtime.LoadBalancingClient, PhotonRealtime") ??
            System.Type.GetType("Photon.Realtime.LoadBalancingClient, Assembly-CSharp");

        return t?.GetMethod("OnEvent",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[] { typeof(EventData) },
            null);
    }

    // Return false (skip original) to block the event entirely
    [HarmonyPrefix]
    public static bool Prefix(EventData photonEvent)
    {
        if (!SeralythNetworkManager.EventBlockEnabled) return true; // pass through
        if (photonEvent.Code != SeralythByte)         return true; // not our concern
        // Drop it — Seralyth's own OnEvent never fires for this packet.
        return false;
    }
}
