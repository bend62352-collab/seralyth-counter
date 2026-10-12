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

namespace SeralythCounter.Mods.Multiplayer;

// ════════════════════════════════════════════════════════════════════════════
//  CLOSE GUN
//  Aim at a Seralyth user and pull trigger → sends their close event,
//  wiping their menu from every other player's screen.
// ════════════════════════════════════════════════════════════════════════════
[HamburburPluginMod("Multiplayer")]
[hamburburmod("Seralyth Close Gun",
    "Aim at a Seralyth user and shoot to force-close their mod menu for everyone",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 10)]
public class SeralythCloseGun : hamburburmod
{
    private readonly GunLib _gun = new() { ShouldFollow = true };
    private          float  _lastFire;

    protected override void Start()   => _gun.Start();
    protected override void OnDisable() => _gun.OnDisable();

    protected override void LateUpdate()
    {
        _gun.LateUpdate();

        if (!PhotonNetwork.InRoom || !_gun.IsShooting || _gun.ChosenRig == null)
            return;

        int actor = _gun.ChosenRig.GetNetPlayer().ActorNumber;

        // Only fire against confirmed Seralyth users (or ignore the check
        // by removing the if-guard if you want it to fire on anyone)
        if (!SeralythNetworkManager.DetectedCheaters.ContainsKey(actor))
        {
            Notify($"{_gun.ChosenRig.OwningNetPlayer().SanitizedNickName} is not running Seralyth.");
            return;
        }

        if (Time.time - _lastFire < 0.5f) return;
        _lastFire = Time.time;

        // Raise Seralyth close event — their NetworkMenuManager drops the entry
        object[]           payload = { "close" };
        RaiseEventOptions  opts    = new() { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(71, payload, opts, SendOptions.SendReliable);

        Notify($"Sent close to Seralyth user: {_gun.ChosenRig.OwningNetPlayer().SanitizedNickName}");
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  HEARTBEAT KILL GUN
//  Hold aim on a Seralyth user → floods them with close events rapidly
//  (Seralyth times out a remote menu after 3.5 s of silence / on close msg)
// ════════════════════════════════════════════════════════════════════════════
[HamburburPluginMod("Multiplayer")]
[hamburburmod("Seralyth Heartbeat Kill Gun",
    "Hold aim on a Seralyth user to continuously spam close events until their menu stays gone",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 11)]
public class SeralythHeartbeatKillGun : hamburburmod
{
    private readonly GunLib   _gun = new() { ShouldFollow = true };
    private          float    _lastFire;
    private          int      _lockedActor = -1;
    private          Coroutine _floodRoutine;

    protected override void Start()   => _gun.Start();

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
                _lockedActor  = actor;
                _floodRoutine = PluginContext.Current?.StartCoroutine(FloodLoop(actor))
                             ?? StartCoroutine(FloodLoop(actor));
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
            TargetActors  = null,          // send to everyone
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
            StopCoroutine(_floodRoutine);
            _floodRoutine = null;
        }
        _lockedActor = -1;
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  WALL GUN
//  Aim and shoot → spawns a solid red wall that follows the target.
//  Useful on confirmed Seralyth users to block their movement locally.
// ════════════════════════════════════════════════════════════════════════════
[HamburburPluginMod("Multiplayer")]
[hamburburmod("Seralyth Wall Gun",
    "Shoot at a Seralyth user to place a wall in front of them (local, only you see it)",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 12)]
public class SeralythWallGun : hamburburmod
{
    private readonly GunLib _gun = new() { ShouldFollow = true };
    private          float  _lastShot;

    protected override void Start()   => _gun.Start();
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
        Notify($"Wall placed on {_gun.ChosenRig.OwningNetPlayer().SanitizedNickName}");
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  THEME CORRUPT GUN
//  Aim and shoot → sends a malformed Seralyth theme packet to the target.
//  Seralyth validates colors, but sending NaN / out-of-range pack values
//  causes their colour unpacker to either throw or display garbage.
// ════════════════════════════════════════════════════════════════════════════
[HamburburPluginMod("Multiplayer")]
[hamburburmod("Seralyth Theme Corrupt Gun",
    "Send malformed theme packets to a Seralyth user to corrupt their menu display",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 13)]
public class SeralythThemeCorruptGun : hamburburmod
{
    private readonly GunLib _gun = new() { ShouldFollow = true };
    private          float  _lastFire;

    protected override void Start()   => _gun.Start();
    protected override void OnDisable() => _gun.OnDisable();

    protected override void LateUpdate()
    {
        _gun.LateUpdate();

        if (!PhotonNetwork.InRoom || !_gun.IsShooting || _gun.ChosenRig == null) return;
        if (Time.time - _lastFire < 0.3f) return;
        _lastFire = Time.time;

        int actor = _gun.ChosenRig.GetNetPlayer().ActorNumber;

        // Seralyth theme packet: "theme", packedMainColor (int), packedGradient (int),
        // rainbowFlag, pastelRainbow, epileptic, copyRigColor, transparent
        // Sending int.MinValue / int.MaxValue for the colour packs triggers
        // edge cases in their colour unpacker (divide-by-zero on alpha channel decode)
        object[] payload =
        {
            "theme",
            int.MinValue,   // packed main colour (NaN-equivalent for their int packer)
            int.MaxValue,   // packed gradient
            true,           // rainbow
            true,           // pastel rainbow
            true,           // epileptic (rapid strobing in their UI)
            false,
            false,
        };

        // Send only to that specific actor so only they see the corrupt theme
        RaiseEventOptions opts = new()
        {
            TargetActors  = [actor],
            CachingOption = EventCaching.DoNotCache,
        };

        PhotonNetwork.RaiseEvent(71, payload, opts, SendOptions.SendReliable);
        Notify($"Corrupt theme sent to {_gun.ChosenRig.OwningNetPlayer().SanitizedNickName}");
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  DETECTOR GUN  (passive, no trigger needed)
//  Keep enabled to mark detected Seralyth users with a nametag overlay.
//  Works alongside the base Detector mod but adds a visual beam that turns
//  red when your crosshair lands on a detected cheater.
// ════════════════════════════════════════════════════════════════════════════
[HamburburPluginMod("Multiplayer")]
[hamburburmod("Seralyth ESP Gun",
    "Highlights detected Seralyth users — beam turns red when aiming at one",
    ButtonType.Togglable, AccessSetting.Public, EnabledType.Disabled, 14)]
public class SeralythEspGun : hamburburmod
{
    private readonly GunLib _gun = new() { ShouldFollow = false };

    protected override void Start()   => _gun.Start();
    protected override void OnDisable() => _gun.OnDisable();

    protected override void LateUpdate()
    {
        _gun.LateUpdate();

        if (_gun.ChosenRig == null) return;

        int actor = _gun.ChosenRig.GetNetPlayer().ActorNumber;
        bool isCheat = SeralythNetworkManager.DetectedCheaters.ContainsKey(actor);

        // The GunLib beam colour is controlled by the user's theme setting,
        // but we can send a notification callout when we're aiming at a cheater
        if (isCheat)
            Notify($"⚠ SERALYTH: {_gun.ChosenRig.OwningNetPlayer().SanitizedNickName}");
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  SHARED HELPER
// ════════════════════════════════════════════════════════════════════════════
internal static class GunNotify
{
    internal static void Notify(string msg)
    {
        Debug.Log($"[SeralythCounter] {msg}");
        NotificationManager.SendNotification(
            "<color=red>SeralythCounter</color>", msg, 3f, true, false);
    }
}
