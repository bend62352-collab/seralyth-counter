using hamburbur.Mod_Backend;
using hamburbur.Plugins;
using SeralythCounter.Backend;

namespace SeralythCounter.Mods.OP;

[HamburburPluginMod("OP")]
[hamburburmod("Seralyth Detector", "Listen for Seralyth mod-menu network packets and flag users",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 0)]
public class SeralythDetector : hamburburmod
{
    protected override void OnEnable()  => SeralythNetworkManager.EnsureLoaded();
    protected override void OnDisable() => SeralythNetworkManager.Shutdown();
}

[HamburburPluginMod("OP")]
[hamburburmod("Block Seralyth Events", "Silently drop all Seralyth byte-71 Photon events (requires Detector)",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 1)]
public class BlockSeralythEvents : hamburburmod
{
    protected override void OnEnable()  => SeralythNetworkManager.EventBlockEnabled = true;
    protected override void OnDisable() => SeralythNetworkManager.EventBlockEnabled = false;
}

[HamburburPluginMod("OP")]
[hamburburmod("Flood Seralyth Close", "Spam close-events to crash their mod menu for other players (requires Detector)",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 2)]
public class FloodSeralythClose : hamburburmod
{
    protected override void OnEnable()  => SeralythNetworkManager.HeartbeatFloodEnabled = true;
    protected override void OnDisable() => SeralythNetworkManager.HeartbeatFloodEnabled = false;
}

[HamburburPluginMod("OP")]
[hamburburmod("Force Close All", "Instantly send close-event for every detected Seralyth user",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 3)]
public class ForceCloseAll : hamburburmod
{
    protected override void OnEnable()
    {
        foreach (int actor in SeralythNetworkManager.DetectedCheaters.Keys)
            SeralythNetworkManager.SendForceClose(actor);
    }
}

[HamburburPluginMod("OP")]
[hamburburmod("Wall Cheaters", "Spawn a wall in front of each detected Seralyth user (local)",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 4)]
public class WallCheaters : hamburburmod
{
    protected override void OnEnable()
    {
        SeralythNetworkManager.GhostPositionEnabled = true;
        foreach (int actor in SeralythNetworkManager.DetectedCheaters.Keys)
            SeralythNetworkManager.SpawnObstacleForActor(actor);
    }

    protected override void OnDisable()
    {
        SeralythNetworkManager.GhostPositionEnabled = false;
        SeralythNetworkManager.RemoveAllObstacles();
    }
}
