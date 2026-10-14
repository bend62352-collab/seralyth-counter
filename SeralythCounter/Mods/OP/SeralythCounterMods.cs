using hamburbur.Managers;
using hamburbur.Mod_Backend;
using hamburbur.Plugins;
using Photon.Pun;
using SeralythCounter.Backend;

namespace SeralythCounter.Mods.OP;

// ── Detection (always-on notification) ───────────────────────────────────────
[HamburburPluginMod("OP")]
[hamburburmod("Seralyth Detector", "Shows a notification whenever a Seralyth 4.8.5 user is detected via Photon byte 71.",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Enabled, 0)]
public class SeralythDetector : hamburburmod
{
    protected override void OnEnable()  => SeralythNetworkManager.NotificationsEnabled = true;
    protected override void OnDisable() => SeralythNetworkManager.NotificationsEnabled = false;
}

// ── Event block ───────────────────────────────────────────────────────────────
[HamburburPluginMod("OP")]
[hamburburmod("Block Seralyth Events", "Ignores payload from Seralyth byte-71 events so their mods cannot affect you.",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Enabled, 0)]
public class BlockSeralythEvents : hamburburmod
{
    protected override void OnEnable()  => SeralythNetworkManager.EventBlockEnabled = true;
    protected override void OnDisable() => SeralythNetworkManager.EventBlockEnabled = false;
}

// ── Heartbeat flood ───────────────────────────────────────────────────────────
[HamburburPluginMod("OP")]
[hamburburmod("Heartbeat Flood", "Spams close events at every detected Seralyth user to crash their mod menu.",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 0)]
public class HeartbeatFlood : hamburburmod
{
    protected override void OnEnable()  => SeralythNetworkManager.HeartbeatFloodEnabled = true;
    protected override void OnDisable() => SeralythNetworkManager.HeartbeatFloodEnabled = false;
}

// ── Ghost position (networked) ────────────────────────────────────────────────
[HamburburPluginMod("OP")]
[hamburburmod("Ghost Position", "Spawns a red cube on every detected Seralyth user — visible to ALL players.",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 0)]
public class GhostPosition : hamburburmod
{
    protected override void OnEnable()  => SeralythNetworkManager.GhostPositionEnabled = true;
    protected override void OnDisable()
    {
        SeralythNetworkManager.GhostPositionEnabled = false;
        SeralythNetworkManager.Instance?.RemoveAllObstacles();
    }
}

// ── Auto Destroy View (event 204) ────────────────────────────────────────────
[HamburburPluginMod("OP")]
[hamburburmod("Auto Destroy View", "Auto-sends Photon event 204 to ghost every newly detected Seralyth user.",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 0)]
public class AutoDestroyView : hamburburmod
{
    protected override void OnEnable()  => SeralythNetworkManager.AutoDestroyViewEnabled = true;
    protected override void OnDisable() => SeralythNetworkManager.AutoDestroyViewEnabled = false;
}

// ── Spawn Cubes on All ────────────────────────────────────────────────────────
[HamburburPluginMod("OP")]
[hamburburmod("Spawn Cubes on All", "Sends networked cubes to all detected Seralyth users — they and everyone can see them.",
    ButtonType.Fixed, AccessSetting.Public, EnabledType.Disabled, 0)]
public class SpawnCubesAll : hamburburmod
{
    protected override void Pressed()
    {
        if (!PhotonNetwork.InRoom) return;

        int count = SeralythNetworkManager.DetectedCheaters.Count;
        if (count == 0)
        {
            NotificationManager.SendNotification(
                "SeralythCounter", "No Seralyth users detected yet.", 3f, false, false);
            return;
        }

        foreach (int actor in SeralythNetworkManager.DetectedCheaters.Keys)
            SeralythNetworkManager.Instance?.SpawnNetworkedCubeForActor(actor);

        NotificationManager.SendNotification(
            "SeralythCounter", $"Spawned cubes on {count} Seralyth user(s).", 3f, false, false);
    }
}

// ── Remove all spawned cubes ──────────────────────────────────────────────────
[HamburburPluginMod("OP")]
[hamburburmod("Remove All Cubes", "Removes all locally visible spawned obstacle cubes.",
    ButtonType.Fixed, AccessSetting.Public, EnabledType.Disabled, 0)]
public class RemoveAllCubes : hamburburmod
{
    protected override void Pressed()
    {
        SeralythNetworkManager.Instance?.RemoveAllObstacles();
        NotificationManager.SendNotification(
            "SeralythCounter", "Removed all obstacle cubes.", 3f, false, false);
    }
}

// ── Force close all ───────────────────────────────────────────────────────────
[HamburburPluginMod("OP")]
[hamburburmod("Force Close All", "Sends a single force-close to every currently detected Seralyth user.",
    ButtonType.Fixed, AccessSetting.Public, EnabledType.Disabled, 0)]
public class ForceCloseAll : hamburburmod
{
    protected override void Pressed()
    {
        if (!PhotonNetwork.InRoom) return;

        int count = SeralythNetworkManager.DetectedCheaters.Count;
        if (count == 0)
        {
            NotificationManager.SendNotification(
                "SeralythCounter", "No Seralyth users detected yet.", 3f, false, false);
            return;
        }

        foreach (int actor in SeralythNetworkManager.DetectedCheaters.Keys)
            SeralythNetworkManager.Instance?.SendForceClose(actor);

        NotificationManager.SendNotification(
            "SeralythCounter", $"Force-closed {count} Seralyth user(s).", 3f, false, false);
    }
}

// ── Destroy View All ──────────────────────────────────────────────────────────
[HamburburPluginMod("OP")]
[hamburburmod("Destroy View All", "Sends Photon event 204 to all currently detected Seralyth users.",
    ButtonType.Fixed, AccessSetting.Public, EnabledType.Disabled, 0)]
public class DestroyViewAll : hamburburmod
{
    protected override void Pressed()
    {
        if (!PhotonNetwork.InRoom) return;

        int count = SeralythNetworkManager.DetectedCheaters.Count;
        if (count == 0)
        {
            NotificationManager.SendNotification(
                "SeralythCounter", "No Seralyth users detected yet.", 3f, false, false);
            return;
        }

        foreach (int actor in SeralythNetworkManager.DetectedCheaters.Keys)
            SeralythNetworkManager.Instance?.DestroyPhotonView(actor);

        NotificationManager.SendNotification(
            "SeralythCounter", $"Sent event 204 to {count} Seralyth user(s).", 3f, false, false);
    }
}

// ── Force rename all ──────────────────────────────────────────────────────────
[HamburburPluginMod("OP")]
[hamburburmod("Force Rename All", "Blanks all detected Seralyth users' names.",
    ButtonType.Fixed, AccessSetting.Public, EnabledType.Disabled, 0)]
public class ForceRenameAll : hamburburmod
{
    protected override void Pressed()
    {
        if (!PhotonNetwork.InRoom) return;

        int count = SeralythNetworkManager.DetectedCheaters.Count;
        if (count == 0)
        {
            NotificationManager.SendNotification(
                "SeralythCounter", "No Seralyth users detected yet.", 3f, false, false);
            return;
        }

        foreach (int actor in SeralythNetworkManager.DetectedCheaters.Keys)
            SeralythNetworkManager.Instance?.ForceRename(actor, "");

        NotificationManager.SendNotification(
            "SeralythCounter", $"Force-renamed {count} Seralyth user(s).", 3f, false, false);
    }
}
