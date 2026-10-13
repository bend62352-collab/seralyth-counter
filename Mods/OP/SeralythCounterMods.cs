using System.Collections.Generic;
using ExitGames.Client.Photon;
using hamburbur.Managers;
using hamburbur.Mod_Backend;
using hamburbur.Plugins;
using hamburbur.Tools;
using Photon.Pun;
using Photon.Realtime;
using SeralythCounter.Backend;
using UnityEngine;

namespace SeralythCounter.Mods.OP;

// ────────────────────────────────────────────────────────────────────────────
// Seralyth Detector  (auto-on)
// Subscribes to ALL Photon events the moment the mod loads, always listening.
// Uses isEnabled flag (like hamburbur's own EventLogger) to gate notifications.
// ────────────────────────────────────────────────────────────────────────────
[HamburburPluginMod("OP")]
[hamburburmod("Seralyth Detector", "Detects players running the Seralyth mod menu via Photon event byte 71.",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Enabled, 0)]
public class SeralythDetector : hamburburmod
{
    private const byte  SeralythByte     = 71;
    private const float HeartbeatTimeout = 3.5f;
    private const float NotifyCooldown   = 5f;

    // Shared detection state — other mods read this
    public static readonly Dictionary<int, float> DetectedCheaters = new();
    private static readonly Dictionary<int, float> LastNotified    = new();

    private bool _isEnabled;

    protected override void Start()
    {
        // Subscribe once when the mod first loads — stays subscribed forever
        PhotonNetwork.NetworkingClient.EventReceived += OnPhotonEvent;
        SeralythNetworkManager.EnsureLoaded();
    }

    protected override void OnEnable()  => _isEnabled = true;
    protected override void OnDisable() => _isEnabled = false;

    private void Update()
    {
        // Prune timed-out entries
        var now     = Time.time;
        var remove  = new List<int>();
        foreach (var kv in DetectedCheaters)
            if (now - kv.Value > HeartbeatTimeout)
                remove.Add(kv.Key);
        foreach (int id in remove)
        {
            DetectedCheaters.Remove(id);
            LastNotified.Remove(id);
            SeralythNetworkManager.Instance?.RemoveObstacle(id);
        }
    }

    private void OnPhotonEvent(EventData photonEvent)
    {
        if (photonEvent.Code != SeralythByte) return;

        int    sender    = photonEvent.Sender;
        string eventType = "";

        try
        {
            if (photonEvent.CustomData is object[] data && data.Length > 0)
                eventType = data[0]?.ToString() ?? "";
        }
        catch { /* malformed */ }

        switch (eventType)
        {
            case "menuState":
            case "heartbeat":
                DetectedCheaters[sender] = Time.time;

                // Notify (with cooldown so it doesn't spam)
                if (_isEnabled)
                {
                    float now = Time.time;
                    if (!LastNotified.TryGetValue(sender, out float last) || now - last > NotifyCooldown)
                    {
                        LastNotified[sender] = now;
                        string nick = GetNick(sender);
                        NotificationManager.SendNotification(
                            "<color=red>SeralythCounter</color>",
                            $"Detected Seralyth user: {nick}",
                            5f, true, true);
                    }
                }

                // Trigger counter effects
                if (SeralythNetworkManager.HeartbeatFloodEnabled)
                    SeralythNetworkManager.Instance?.FloodClose(sender);
                if (SeralythNetworkManager.GhostPositionEnabled)
                    SeralythNetworkManager.Instance?.SpawnObstacleForActor(sender);
                break;

            case "close":
                DetectedCheaters.Remove(sender);
                LastNotified.Remove(sender);
                SeralythNetworkManager.Instance?.RemoveObstacle(sender);
                break;
        }
    }

    private static string GetNick(int actorNumber)
    {
        try { return actorNumber.Rig()?.Creator?.NickName ?? actorNumber.ToString(); }
        catch { return actorNumber.ToString(); }
    }
}

// ────────────────────────────────────────────────────────────────────────────
// Block Seralyth Events  (auto-on)
// ────────────────────────────────────────────────────────────────────────────
[HamburburPluginMod("OP")]
[hamburburmod("Block Seralyth Events", "Drops incoming Seralyth byte-71 Photon packets so their mod menu cannot affect you.",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Enabled, 0)]
public class BlockSeralythEvents : hamburburmod
{
    protected override void OnEnable()  => SeralythNetworkManager.EventBlockEnabled = true;
    protected override void OnDisable() => SeralythNetworkManager.EventBlockEnabled = false;
}

// ────────────────────────────────────────────────────────────────────────────
// Heartbeat Flood  (off by default)
// ────────────────────────────────────────────────────────────────────────────
[HamburburPluginMod("OP")]
[hamburburmod("Heartbeat Flood", "Spams close events at any detected Seralyth user to crash their mod menu.",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 0)]
public class HeartbeatFlood : hamburburmod
{
    protected override void OnEnable()  => SeralythNetworkManager.HeartbeatFloodEnabled = true;
    protected override void OnDisable() => SeralythNetworkManager.HeartbeatFloodEnabled = false;
}

// ────────────────────────────────────────────────────────────────────────────
// Force Close All  (off by default)
// ────────────────────────────────────────────────────────────────────────────
[HamburburPluginMod("OP")]
[hamburburmod("Force Close All", "Sends a force-close event to every currently detected Seralyth user.",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 0)]
public class ForceCloseAll : hamburburmod
{
    protected override void OnEnable()
    {
        if (!PhotonNetwork.InRoom) return;
        foreach (int actor in SeralythDetector.DetectedCheaters.Keys)
            SeralythNetworkManager.Instance?.SendForceClose(actor);
    }
}

// ────────────────────────────────────────────────────────────────────────────
// Ghost Position  (off by default)
// ────────────────────────────────────────────────────────────────────────────
[HamburburPluginMod("OP")]
[hamburburmod("Ghost Position", "Spawns a red obstacle cube on detected Seralyth users.",
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
