using ExitGames.Client.Photon;
using hamburbur.Libs;
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
// Force Close Gun
// ────────────────────────────────────────────────────────────────────────────
[HamburburPluginMod("OP")]
[hamburburmod("Force Close Gun", "Aim at any player and shoot to force-close their Seralyth menu.",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 0)]
public class ForceCloseGun : hamburburmod
{
    private readonly GunLib _gun = new() { ShouldFollow = true };

    protected override void Start()     => _gun.Start();
    protected override void OnDisable() => _gun.OnDisable();

    protected override void LateUpdate()
    {
        _gun.LateUpdate();
        if (!PhotonNetwork.InRoom || !_gun.IsShooting || _gun.ChosenRig == null) return;

        SeralythNetworkManager.Instance?.SendForceClose(_gun.ChosenRig.Creator.ActorNumber);

        NotificationManager.SendNotification(
            "<color=red>SeralythCounter</color>",
            $"Sent Force Close to {_gun.ChosenRig.Creator.NickName}",
            3f, true, false);
    }
}

// ────────────────────────────────────────────────────────────────────────────
// Seralyth Flood Gun
// ────────────────────────────────────────────────────────────────────────────
[HamburburPluginMod("OP")]
[hamburburmod("Seralyth Flood Gun", "Aim at any player and shoot to flood their Seralyth menu with close events.",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 0)]
public class SeralythFloodGun : hamburburmod
{
    private readonly GunLib _gun     = new() { ShouldFollow = true };
    private          float  _nextShot;

    protected override void Start()     => _gun.Start();
    protected override void OnDisable() => _gun.OnDisable();

    protected override void LateUpdate()
    {
        _gun.LateUpdate();
        if (!PhotonNetwork.InRoom || !_gun.IsShooting || _gun.ChosenRig == null) return;
        if (Time.time < _nextShot) return;

        SeralythNetworkManager.Instance?.FloodClose(_gun.ChosenRig.Creator.ActorNumber);

        NotificationManager.SendNotification(
            "<color=red>SeralythCounter</color>",
            $"Flooded Seralyth on {_gun.ChosenRig.Creator.NickName}",
            3f, true, false);

        _nextShot = Time.time + 1f;
    }
}

// ────────────────────────────────────────────────────────────────────────────
// Ghost Position Gun
// ────────────────────────────────────────────────────────────────────────────
[HamburburPluginMod("OP")]
[hamburburmod("Ghost Position Gun", "Aim at any player and shoot to spawn a red obstacle cube on them.",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 0)]
public class GhostPositionGun : hamburburmod
{
    private readonly GunLib _gun = new() { ShouldFollow = true };

    protected override void Start()     => _gun.Start();
    protected override void OnDisable() => _gun.OnDisable();

    protected override void LateUpdate()
    {
        _gun.LateUpdate();
        if (!PhotonNetwork.InRoom || !_gun.IsShooting || _gun.ChosenRig == null) return;

        SeralythNetworkManager.Instance?.SpawnObstacleForActor(_gun.ChosenRig.Creator.ActorNumber);

        NotificationManager.SendNotification(
            "<color=red>SeralythCounter</color>",
            $"Spawned obstacle on {_gun.ChosenRig.Creator.NickName}",
            3f, true, false);
    }
}

// ────────────────────────────────────────────────────────────────────────────
// Seralyth Scan Gun
// ────────────────────────────────────────────────────────────────────────────
[HamburburPluginMod("OP")]
[hamburburmod("Seralyth Scan Gun", "Aim at any player and shoot to check if they are a detected Seralyth user.",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 0)]
public class SeralythScanGun : hamburburmod
{
    private readonly GunLib _gun = new() { ShouldFollow = true };

    protected override void Start()     => _gun.Start();
    protected override void OnDisable() => _gun.OnDisable();

    protected override void LateUpdate()
    {
        _gun.LateUpdate();
        if (!PhotonNetwork.InRoom || !_gun.IsShooting || _gun.ChosenRig == null) return;

        int    actor = _gun.ChosenRig.Creator.ActorNumber;
        string nick  = _gun.ChosenRig.Creator.NickName;
        bool   found = SeralythDetector.DetectedCheaters.ContainsKey(actor);

        NotificationManager.SendNotification(
            "<color=red>SeralythCounter</color>",
            found ? $"{nick} IS a Seralyth user!" : $"{nick} is NOT a detected Seralyth user.",
            4f, true, false);
    }
}

// ────────────────────────────────────────────────────────────────────────────
// Remove Obstacles Gun
// ────────────────────────────────────────────────────────────────────────────
[HamburburPluginMod("OP")]
[hamburburmod("Remove Obstacles Gun", "Shoot to remove all spawned obstacle cubes.",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 0)]
public class RemoveObstaclesGun : hamburburmod
{
    private readonly GunLib _gun = new() { ShouldFollow = true };

    protected override void Start()     => _gun.Start();
    protected override void OnDisable() => _gun.OnDisable();

    protected override void LateUpdate()
    {
        _gun.LateUpdate();
        if (!PhotonNetwork.InRoom || !_gun.IsShooting || _gun.ChosenRig == null) return;

        SeralythNetworkManager.Instance?.RemoveAllObstacles();

        NotificationManager.SendNotification(
            "<color=red>SeralythCounter</color>",
            "Removed all obstacle cubes.",
            3f, true, false);
    }
}
