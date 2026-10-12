using System.Collections;
using ExitGames.Client.Photon;
using hamburbur.Libs;
using hamburbur.Managers;
using hamburbur.Mod_Backend;
using hamburbur.Plugins;
using Photon.Pun;
using Photon.Realtime;
using SeralythCounter.Backend;
using UnityEngine;

namespace SeralythCounter.Mods.OP;

// ════════════════════════════════════════════════════════════════════════════
//  CLOSE GUN
//  Aim at a Seralyth user and pull trigger → sends their close event,
//  wiping their menu from every other player's screen.
// ════════════════════════════════════════════════════════════════════════════
[HamburburPluginMod("OP")]
[hamburburmod("Seralyth Close Gun",
    "Aim at a Seralyth user and shoot to force-close their mod menu for everyone",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 10)]
public class SeralythCloseGun : hamburburmod
{
    private readonly GunLib _gun = new() { ShouldFollow = true };
    private          float  _lastFire;

    protected override void Start()     => _gun.Start();
    protected override void OnDisable() => _gun.OnDisable();

    protected override void LateUpdate()
    {
        _gun.LateUpdate();

        if (!PhotonNetwork.InRoom || !_gun.IsShooting || _gun.ChosenRig == null)
            return;

        int actor = _gun.ChosenRig.GetNetPlayer().ActorNumber;

        if (!SeralythNetworkManager.DetectedCheaters.ContainsKey(actor))
        {
            NotificationManager.SendNotification(
                "<color=red>SeralythCounter</color>",
                $"{_gun.ChosenRig.OwningNetPlayer().SanitizedNickName} is not a detected Seralyth user.",
                3f, true, false);
            return;
        }

        if (Time.time - _lastFire < 0.5f) return;
        _lastFire = Time.time;

        object[]          payload = { "close" };
        RaiseEventOptions opts    = new() { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(71, payload, opts, SendOptions.SendReliable);

        NotificationManager.SendNotification(
            "<color=red>SeralythCounter</color>",
            $"Sent close to {_gun.ChosenRig.OwningNetPlayer().SanitizedNickName}",
            3f, true, false);
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  HEARTBEAT KILL GUN
//  Hold aim on a Seralyth user → floods them with close events rapidly.
//  Seralyth times out a remote menu after 3.5 s of silence / on close msg.
// ════════════════════════════════════════════════════════════════════════════
[HamburburPluginMod("OP")]
[hamburburmod("Seralyth Heartbeat Kill Gun",
    "Hold aim on a Seralyth user to continuously spam close events until their menu stays gone",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 11)]
public class SeralythHeartbeatKillGun : hamburburmod
{
    private readonly GunLib   _gun = new() { ShouldFollow = true };
    private          int      _lockedActor = -1;
    private          Coroutine _floodRoutine;

    protected override void Start()     => _gun.Start();

    protected override void OnDisable()
    {
        StopFlood();
        _gun.OnDisable();
    }

    protected override void LateUpdate()
    {
        _gun.LateUpdate();

        if (!PhotonNetwork.InRoom) { StopFlood(); return; }

        if (_gun.IsShooting && _gun.ChosenRig != null)
        {
            int actor = _gun.ChosenRig.GetNetPlayer().ActorNumber;
            if (actor != _lockedActor)
            {
                StopFlood();
                _lockedActor   = actor;
                // Route coroutine through the NetworkManager MonoBehaviour
                _floodRoutine  = SeralythNetworkManager.Instance.StartCoroutine(FloodLoop(actor));
            }
        }
        else
        {
            StopFlood();
        }
    }

    private IEnumerator FloodLoop(int actor)
    {
        object[]          payload = { "close" };
        RaiseEventOptions opts    = new()
        {
            Receivers     = ReceiverGroup.All,
            CachingOption = EventCaching.DoNotCache,
        };

        while (_lockedActor == actor && PhotonNetwork.InRoom)
        {
            PhotonNetwork.RaiseEvent(71, payload, opts, SendOptions.SendReliable);
            yield return new WaitForSeconds(0.25f);
        }
    }

    private void StopFlood()
    {
        if (_floodRoutine != null)
        {
            SeralythNetworkManager.Instance?.StopCoroutine(_floodRoutine);
            _floodRoutine = null;
        }
        _lockedActor = -1;
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  WALL GUN
//  Aim and shoot → spawns a solid red wall that follows the target.
// ════════════════════════════════════════════════════════════════════════════
[HamburburPluginMod("OP")]
[hamburburmod("Seralyth Wall Gun",
    "Shoot at a Seralyth user to place a wall in front of them (local, only you see it)",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 12)]
public class SeralythWallGun : hamburburmod
{
    private readonly GunLib _gun = new() { ShouldFollow = true };
    private          float  _lastShot;

    protected override void Start()     => _gun.Start();
    protected override void OnDisable()
    {
        SeralythNetworkManager.RemoveAllObstacles();
        _gun.OnDisable();
    }

    protected override void LateUpdate()
    {
        _gun.LateUpdate();

        if (!_gun.IsShooting || _gun.ChosenRig == null) return;
        if (Time.time - _lastShot < 1f) return;
        _lastShot = Time.time;

        int actor = _gun.ChosenRig.GetNetPlayer().ActorNumber;
        SeralythNetworkManager.SpawnObstacleForActor(actor);

        NotificationManager.SendNotification(
            "<color=red>SeralythCounter</color>",
            $"Wall placed on {_gun.ChosenRig.OwningNetPlayer().SanitizedNickName}",
            3f, true, false);
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  THEME CORRUPT GUN
//  Sends a malformed Seralyth theme packet — int.MinValue/MaxValue colour
//  packs trigger edge cases in their colour unpacker (divide-by-zero on
//  alpha decode), and epileptic=true causes rapid strobing in their UI.
// ════════════════════════════════════════════════════════════════════════════
[HamburburPluginMod("OP")]
[hamburburmod("Seralyth Theme Corrupt Gun",
    "Send malformed theme packets to a Seralyth user to corrupt their menu display",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 13)]
public class SeralythThemeCorruptGun : hamburburmod
{
    private readonly GunLib _gun = new() { ShouldFollow = true };
    private          float  _lastFire;

    protected override void Start()     => _gun.Start();
    protected override void OnDisable() => _gun.OnDisable();

    protected override void LateUpdate()
    {
        _gun.LateUpdate();

        if (!PhotonNetwork.InRoom || !_gun.IsShooting || _gun.ChosenRig == null) return;
        if (Time.time - _lastFire < 0.3f) return;
        _lastFire = Time.time;

        int actor = _gun.ChosenRig.GetNetPlayer().ActorNumber;

        object[] payload =
        {
            "theme",
            int.MinValue,   // packed main colour — triggers divide-by-zero in their alpha decoder
            int.MaxValue,   // packed gradient
            true,           // rainbow
            true,           // pastel rainbow
            true,           // epileptic (rapid strobing)
            false,
            false,
        };

        RaiseEventOptions opts = new()
        {
            TargetActors  = [actor],
            CachingOption = EventCaching.DoNotCache,
        };

        PhotonNetwork.RaiseEvent(71, payload, opts, SendOptions.SendReliable);

        NotificationManager.SendNotification(
            "<color=red>SeralythCounter</color>",
            $"Corrupt theme sent to {_gun.ChosenRig.OwningNetPlayer().SanitizedNickName}",
            3f, true, false);
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  ESP GUN  (passive — no trigger needed)
//  Keep enabled to get a callout whenever your crosshair lands on a
//  detected Seralyth user.
// ════════════════════════════════════════════════════════════════════════════
[HamburburPluginMod("OP")]
[hamburburmod("Seralyth ESP Gun",
    "Highlights detected Seralyth users — notifies when you aim at one",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 14)]
public class SeralythEspGun : hamburburmod
{
    private readonly GunLib _gun = new() { ShouldFollow = false };

    protected override void Start()     => _gun.Start();
    protected override void OnDisable() => _gun.OnDisable();

    protected override void LateUpdate()
    {
        _gun.LateUpdate();

        if (_gun.ChosenRig == null) return;

        int  actor   = _gun.ChosenRig.GetNetPlayer().ActorNumber;
        bool isCheat = SeralythNetworkManager.DetectedCheaters.ContainsKey(actor);

        if (isCheat)
            NotificationManager.SendNotification(
                "<color=red>SeralythCounter</color>",
                $"⚠ SERALYTH: {_gun.ChosenRig.OwningNetPlayer().SanitizedNickName}",
                1f, true, false);
    }
}
