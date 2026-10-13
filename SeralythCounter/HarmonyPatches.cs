using System.Reflection;
using HarmonyLib;
using ExitGames.Client.Photon;
using SeralythCounter.Backend;

namespace SeralythCounter
{
    public abstract class HarmonyPatches
    {
        private static Harmony instance;

        public static bool IsPatched { get; private set; }
        public const string InstanceId = Constants.Guid;

        internal static void ApplyHarmonyPatches()
        {
            if (!IsPatched)
            {
                if (instance == null)
                    instance = new Harmony(InstanceId);

                instance.PatchAll(Assembly.GetExecutingAssembly());
                IsPatched = true;
            }
        }

        internal static void RemoveHarmonyPatches()
        {
            if (instance != null && IsPatched)
            {
                instance.UnpatchSelf();
                IsPatched = false;
            }
        }
    }

    [HarmonyPatch(typeof(PhotonPeer), "OnEvent")]
    internal static class BlockSeralythEventPatch
    {
        private const byte SeralythByte = 71;

        [HarmonyPrefix]
        public static bool Prefix(EventData photonEvent)
        {
            if (!SeralythNetworkManager.EventBlockEnabled) return true;
            if (photonEvent.Code != SeralythByte)         return true;
            return false;
        }
    }
}
