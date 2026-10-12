using hamburbur.Managers;
using hamburbur.Mod_Backend;
using hamburbur.Plugins;
using SeralythCounter.Backend;

namespace SeralythCounter.Mods.OP;

// ════════════════════════════════════════════════════════════════════════════
//  Detection
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Must be enabled first — starts listening on Seralyth's Photon channel.
/// </summary>
[HamburburPluginMod("OP")]
[hamburburmod("Seralyth Detector", "Listen for Seralyth mod-menu network packets and flag users",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 0)]
public class SeralythDetector : hamburburmod
{
    protected override void OnEnable()  => SeralythNetworkManager.EnsureLoaded();
    protected override void OnDisable() => SeralythNetworkManager.Shutdown();
}

// ════════════════════════════════════════════════════════════════════════════
//  Counter: block their event stream
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Harmony-patches PUN2's OnEvent so Seralyth's byte-71 packets are dropped
/// before their NetworkMenuManager can process them. Their remote mod-menu
/// display never appears on anyone else's screen.
/// </summary>
[HamburburPluginMod("OP")]
[hamburburmod("Block Seralyth Events", "Silently drop all Seralyth byte-71 Photon events (requires Detector)",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 1)]
public class BlockSeralythEvents : hamburburmod
{
    protected override void OnEnable()  => SeralythNetworkManager.EventBlockEnabled = true;
    protected override void OnDisable() => SeralythNetworkManager.EventBlockEnabled = false;
}

// ════════════════════════════════════════════════════════════════════════════
//  Counter: heartbeat flood / force-close
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Continuously sends Seralyth "close" events so every other client's
/// NetworkMenuManager removes the cheater's remote menu entry.
/// Seralyth removes a player's menu if no heartbeat is seen for 3.5 s —
/// and a close message triggers instant removal.
/// </summary>
[HamburburPluginMod("OP")]
[hamburburmod("Flood Seralyth Close", "Spam close-events to crash their mod menu for other players (requires Detector)",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 2)]
public class FloodSeralythClose : hamburburmod
{
    protected override void OnEnable()  => SeralythNetworkManager.HeartbeatFloodEnabled = true;
    protected override void OnDisable() => SeralythNetworkManager.HeartbeatFloodEnabled = false;
}

// ════════════════════════════════════════════════════════════════════════════
//  Counter: one-shot force close for all detected cheaters
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Single-fire button. Sends one Seralyth close event for every currently
/// detected cheater actor — instant menu-removal for everyone in the room.
/// </summary>
[HamburburPluginMod("OP")]
[hamburburmod("Force Close All", "Instantly send close-event for every detected Seralyth user",
    ButtonType.Button, AccessSetting.Public, EnabledType.Disabled, 3)]
public class ForceCloseAll : hamburburmod
{
    protected override void OnEnable()
    {
        foreach (int actor in SeralythNetworkManager.DetectedCheaters.Keys)
            SeralythNetworkManager.SendForceClose(actor);
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  Counter: ghost obstacle wall
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Spawns a solid obstacle (local only) that tracks in front of each detected
/// cheater, blocking their movement from your perspective.
/// Toggle off to clean up all obstacles.
/// </summary>
[HamburburPluginMod("OP")]
[hamburburmod("Wall Cheaters", "Spawn a wall in front of each detected Seralyth user (local)",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 4)]
public class WallCheaters : hamburburmod
{
    protected override void OnEnable()
    {
        SeralythNetworkManager.GhostPositionEnabled = true;
        // Immediately wall anyone already detected
        foreach (int actor in SeralythNetworkManager.DetectedCheaters.Keys)
            SeralythNetworkManager.SpawnObstacleForActor(actor);
    }

    protected override void OnDisable()
    {
        SeralythNetworkManager.GhostPositionEnabled = false;
        SeralythNetworkManager.RemoveAllObstacles();
    }
}
