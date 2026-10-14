using hamburbur.Libs;
using hamburbur.Managers;
using hamburbur.Mod_Backend;
using hamburbur.Plugins;
using Photon.Pun;
using SeralythCounter.Backend;
using UnityEngine;

namespace SeralythCounter.Mods.OP;

[HamburburPluginMod("OP")]
[hamburburmod("Force Close Gun", "Aim and shoot to send a force-close event to that player's Seralyth menu.",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 0)]
public class ForceCloseGun : hamburburmod
{
    private readonly GunLib _gun      = new() { ShouldFollow = true };
    private          float  _nextShot;

    protected override void Start()     => _gun.Start();
    protected override void OnDisable() => _gun.OnDisable();

    protected override void LateUpdate()
    {
        _gun.LateUpdate();
        if (!PhotonNetwork.InRoom || !_gun.IsShooting || _gun.ChosenRig == null) return;
        if (Time.time < _nextShot) return;

        SeralythNetworkManager.Instance?.SendForceClose(_gun.ChosenRig.Creator.ActorNumber);

        NotificationManager.SendNotification(
            "SeralythCounter",
            $"Force-closed Seralyth on {_gun.ChosenRig.Creator.NickName}",
            3f, true, false);

        _nextShot = Time.time + 1f;
    }
}

[HamburburPluginMod("OP")]
[hamburburmod("Seralyth Flood Gun", "Aim and shoot to flood a player's Seralyth menu with close events.",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 0)]
public class SeralythFloodGun : hamburburmod
{
    private readonly GunLib _gun      = new() { ShouldFollow = true };
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
            "SeralythCounter",
            $"Flooded Seralyth on {_gun.ChosenRig.Creator.NickName}",
            3f, true, false);

        _nextShot = Time.time + 1f;
    }
}

[HamburburPluginMod("OP")]
[hamburburmod("Ghost Position Gun", "Aim and shoot to spawn a red cube on that player — everyone can see it.",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 0)]
public class GhostPositionGun : hamburburmod
{
    private readonly GunLib _gun      = new() { ShouldFollow = true };
    private          float  _nextShot;

    protected override void Start()     => _gun.Start();
    protected override void OnDisable() => _gun.OnDisable();

    protected override void LateUpdate()
    {
        _gun.LateUpdate();
        if (!PhotonNetwork.InRoom || !_gun.IsShooting || _gun.ChosenRig == null) return;
        if (Time.time < _nextShot) return;

        SeralythNetworkManager.Instance?.SpawnNetworkedCubeForActor(_gun.ChosenRig.Creator.ActorNumber);

        NotificationManager.SendNotification(
            "SeralythCounter",
            $"Spawned networked cube on {_gun.ChosenRig.Creator.NickName}",
            3f, true, false);

        _nextShot = Time.time + 1f;
    }
}

[HamburburPluginMod("OP")]
[hamburburmod("Seralyth Scan Gun", "Aim and shoot to check if that player is a detected Seralyth user.",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 0)]
public class SeralythScanGun : hamburburmod
{
    private readonly GunLib _gun      = new() { ShouldFollow = true };
    private          float  _nextShot;

    protected override void Start()     => _gun.Start();
    protected override void OnDisable() => _gun.OnDisable();

    protected override void LateUpdate()
    {
        _gun.LateUpdate();
        if (!PhotonNetwork.InRoom || !_gun.IsShooting || _gun.ChosenRig == null) return;
        if (Time.time < _nextShot) return;

        int    actor = _gun.ChosenRig.Creator.ActorNumber;
        string nick  = _gun.ChosenRig.Creator.NickName;
        bool   found = SeralythNetworkManager.DetectedCheaters.ContainsKey(actor);

        NotificationManager.SendNotification(
            "SeralythCounter",
            found ? $"{nick} IS a Seralyth user!" : $"{nick} is NOT detected.",
            4f, true, false);

        _nextShot = Time.time + 1f;
    }
}

[HamburburPluginMod("OP")]
[hamburburmod("Remove Obstacles Gun", "Shoot to remove all spawned obstacle cubes.",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 0)]
public class RemoveObstaclesGun : hamburburmod
{
    private readonly GunLib _gun      = new() { ShouldFollow = true };
    private          float  _nextShot;

    protected override void Start()     => _gun.Start();
    protected override void OnDisable() => _gun.OnDisable();

    protected override void LateUpdate()
    {
        _gun.LateUpdate();
        if (!PhotonNetwork.InRoom || !_gun.IsShooting || _gun.ChosenRig == null) return;
        if (Time.time < _nextShot) return;

        SeralythNetworkManager.Instance?.RemoveAllObstacles();

        NotificationManager.SendNotification(
            "SeralythCounter",
            "Removed all obstacle cubes.",
            3f, true, false);

        _nextShot = Time.time + 1f;
    }
}

[HamburburPluginMod("OP")]
[hamburburmod("Destroy View Gun", "Aim and shoot to send Photon event 204, ghosting that player's rig for everyone.",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 0)]
public class DestroyViewGun : hamburburmod
{
    private readonly GunLib _gun      = new() { ShouldFollow = true };
    private          float  _nextShot;

    protected override void Start()     => _gun.Start();
    protected override void OnDisable() => _gun.OnDisable();

    protected override void LateUpdate()
    {
        _gun.LateUpdate();
        if (!PhotonNetwork.InRoom || !_gun.IsShooting || _gun.ChosenRig == null) return;
        if (Time.time < _nextShot) return;

        SeralythNetworkManager.Instance?.DestroyPhotonView(_gun.ChosenRig.Creator.ActorNumber);

        NotificationManager.SendNotification(
            "SeralythCounter",
            $"Sent event 204 to {_gun.ChosenRig.Creator.NickName}",
            3f, true, false);

        _nextShot = Time.time + 1f;
    }
}

[HamburburPluginMod("OP")]
[hamburburmod("Force Rename Gun", "Aim and shoot to blank that player's display name.",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 0)]
public class ForceRenameGun : hamburburmod
{
    private readonly GunLib _gun      = new() { ShouldFollow = true };
    private          float  _nextShot;

    protected override void Start()     => _gun.Start();
    protected override void OnDisable() => _gun.OnDisable();

    protected override void LateUpdate()
    {
        _gun.LateUpdate();
        if (!PhotonNetwork.InRoom || !_gun.IsShooting || _gun.ChosenRig == null) return;
        if (Time.time < _nextShot) return;

        string nick = _gun.ChosenRig.Creator.NickName;
        SeralythNetworkManager.Instance?.ForceRename(_gun.ChosenRig.Creator.ActorNumber, "");

        NotificationManager.SendNotification(
            "SeralythCounter",
            $"Force-renamed {nick}",
            3f, true, false);

        _nextShot = Time.time + 1f;
    }
}
