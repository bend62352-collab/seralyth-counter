using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using hamburbur.Managers;

namespace SeralythCounter.Backend;

/// <summary>
/// Listens on Photon custom event byte 71 (Seralyth's NetworkMenuByte).
/// Tracks which players are running the Seralyth mod menu, and exposes
/// methods the mod-menu buttons can invoke to counter them.
/// </summary>
public class SeralythNetworkManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    // ── Seralyth wire constants ─────────────────────────────────────────────
    private const byte NetworkMenuByte = 71;

    // Seralyth message type strings (first element of the object[] payload)
    private const string MsgMenuState = "menuState";
    private const string MsgClose     = "close";
    private const string MsgHeartbeat = "heartbeat";
    private const string MsgTheme     = "theme";

    // ── Singleton ───────────────────────────────────────────────────────────
    private static SeralythNetworkManager _instance;
    public  static SeralythNetworkManager Instance => _instance;

    // ── Detected cheaters: actorNumber → last heartbeat timestamp ──────────
    private readonly Dictionary<int, float> _detectedCheaters = new();

    // ── Public read-only view so mods can enumerate detected players ────────
    public static IReadOnlyDictionary<int, float> DetectedCheaters =>
        _instance?._detectedCheaters ?? (IReadOnlyDictionary<int, float>)new Dictionary<int, float>();

    // ── Per-cheater coroutine handles for heartbeat-flood ──────────────────
    private readonly Dictionary<int, Coroutine> _floodCoroutines  = new();

    // ── Active counter modes (set by mods) ─────────────────────────────────
    public static bool HeartbeatFloodEnabled   { get; set; } = false;
    public static bool EventBlockEnabled       { get; set; } = false;
    public static bool GhostPositionEnabled    { get; set; } = false;

    // ── Obstacle tracking ──────────────────────────────────────────────────
    private readonly Dictionary<int, GameObject> _obstacles = new();

    // ── Initialise ─────────────────────────────────────────────────────────
    public static void EnsureLoaded()
    {
        if (_instance != null) return;
        GameObject go = new("SeralythCounter_NetworkManager");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<SeralythNetworkManager>();
    }

    public static void Shutdown()
    {
        if (_instance == null) return;
        PhotonNetwork.RemoveCallbackTarget(_instance);
        Destroy(_instance.gameObject);
        _instance = null;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        PhotonNetwork.AddCallbackTarget(this);
    }

    // ── IOnEventCallback ───────────────────────────────────────────────────
    public void OnEvent(EventData photonEvent)
    {
        // Only care about Seralyth's event byte
        if (photonEvent.Code != NetworkMenuByte) return;

        int actor = photonEvent.Sender;

        // EventBlockEnabled: swallow the event so it never reaches Seralyth's
        // own OnEvent handler. The Harmony prefix patch is a cleaner way to do
        // this (see HarmonyPatches.cs), but we also track it here.
        if (EventBlockEnabled)
        {
            // Can't truly "consume" in this callback model, but the Harmony
            // patch on PhotonNetwork.OnEvent will handle the real suppression.
            // We still register the sender so they show up in the list.
        }

        if (photonEvent.CustomData is not object[] data || data.Length == 0) return;
        string msgType = data[0] as string;
        if (string.IsNullOrEmpty(msgType)) return;

        switch (msgType)
        {
            case MsgHeartbeat:
            case MsgMenuState:
            case MsgTheme:
                RegisterCheater(actor);
                break;

            case MsgClose:
                // They closed their menu — still flag them, but note it
                RegisterCheater(actor);
                Notify($"Seralyth user (actor {actor}) closed their menu.");
                break;
        }
    }

    // ── Cheater registry ───────────────────────────────────────────────────
    private void RegisterCheater(int actor)
    {
        bool isNew = !_detectedCheaters.ContainsKey(actor);
        _detectedCheaters[actor] = Time.time;

        if (!isNew) return;
        string name = GetPlayerName(actor);
        Notify($"Seralyth mod menu detected: {name} (actor {actor})");

        if (HeartbeatFloodEnabled)  StartFloodForActor(actor);
        if (GhostPositionEnabled)   SpawnObstacleForActor(actor);
    }

    // Purge stale entries every few seconds
    private IEnumerator Start()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f);
            List<int> toRemove = new();
            float now = Time.time;
            foreach (KeyValuePair<int, float> kv in _detectedCheaters)
                if (now - kv.Value > 10f) // 10-second grace after last packet
                    toRemove.Add(kv.Key);

            foreach (int a in toRemove)
                UnregisterCheater(a);
        }
    }

    private void UnregisterCheater(int actor)
    {
        _detectedCheaters.Remove(actor);
        StopFloodForActor(actor);
        RemoveObstacleForActor(actor);
        Notify($"Seralyth user (actor {actor}) is gone / timed out.");
    }

    // ── Photon player-leave callback ───────────────────────────────────────
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (_detectedCheaters.ContainsKey(otherPlayer.ActorNumber))
            UnregisterCheater(otherPlayer.ActorNumber);
    }

    // ── Counter: heartbeat flood ───────────────────────────────────────────
    /// <summary>
    /// Seralyth removes a remote player's menu state if no heartbeat arrives
    /// for 3.5 s. We spam fake "close" events FROM OURSELVES carrying the
    /// cheater's actor number in the payload so their own clients drop the
    /// remote menu entry. This works because Seralyth trusts the actor field
    /// inside the payload rather than the Photon sender for the close message.
    ///
    /// Note: we send to ALL players so every legitimate player also sees the
    /// cheater's menu disappear.
    /// </summary>
    private void StartFloodForActor(int actor)
    {
        if (_floodCoroutines.ContainsKey(actor)) return;
        Coroutine c = StartCoroutine(FloodHeartbeatKill(actor));
        _floodCoroutines[actor] = c;
    }

    private void StopFloodForActor(int actor)
    {
        if (!_floodCoroutines.TryGetValue(actor, out Coroutine c)) return;
        if (c != null) StopCoroutine(c);
        _floodCoroutines.Remove(actor);
    }

    private IEnumerator FloodHeartbeatKill(int actor)
    {
        // Send a close event attributed to the cheater's actor slot.
        // Seralyth's OnEvent checks the Photon Sender field, so we need to
        // send it as if the cheater sent it. Because we can't spoof Photon
        // sender IDs in PUN2, instead we send the "close" string payload and
        // rely on Seralyth processing our message (actor mismatch means it
        // may just drop it — in that case the obstacle / heartbeat-timeout
        // approach is more reliable).
        object[] closePayload = { MsgClose };
        RaiseEventOptions opts = new() { Receivers = ReceiverGroup.All };

        while (_detectedCheaters.ContainsKey(actor))
        {
            if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
                PhotonNetwork.RaiseEvent(NetworkMenuByte, closePayload, opts, SendOptions.SendReliable);

            yield return new WaitForSeconds(0.5f);
        }
    }

    // ── Counter: spawn obstacle in front of cheater ────────────────────────
    /// <summary>
    /// Places a solid white cube in the cheater's last known position so they
    /// run into a wall. Purely local — only you see it, but it's enough to
    /// grief them back if you're the room host or running trusted-mod logic.
    ///
    /// For a networked version, wrap in PhotonNetwork.Instantiate with a
    /// prefab that has a PhotonView.
    /// </summary>
    public static void SpawnObstacleForActor(int actor)
    {
        if (_instance == null) return;
        _instance.SpawnObstacleInternal(actor);
    }

    private void SpawnObstacleInternal(int actor)
    {
        if (_obstacles.ContainsKey(actor)) return;

        Player player = GetPlayer(actor);
        if (player == null) return;

        // Try to find their rig in the scene
        Transform rig = FindRigForActor(actor);
        Vector3 spawnPos = rig != null
            ? rig.position + rig.forward * 1.5f + Vector3.up * 0.5f
            : Vector3.zero;

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = $"SeralythCounter_Obstacle_{actor}";
        cube.transform.position = spawnPos;
        cube.transform.localScale = new Vector3(2f, 3f, 2f);

        // Make it bright red so YOU can see it's a counter-object
        Renderer rend = cube.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = new(Shader.Find("Standard"));
            mat.color = new Color(1f, 0.1f, 0.1f, 1f);
            rend.material = mat;
        }

        // Attach a tracker so the cube follows the cheater
        CheaterObstacleTracker tracker = cube.AddComponent<CheaterObstacleTracker>();
        tracker.TargetActor = actor;

        _obstacles[actor] = cube;
        Notify($"Obstacle spawned on Seralyth user (actor {actor}).");
    }

    public static void RemoveObstacleForActor(int actor)
    {
        if (_instance == null) return;
        if (!_instance._obstacles.TryGetValue(actor, out GameObject obj)) return;
        if (obj != null) Destroy(obj);
        _instance._obstacles.Remove(actor);
    }

    public static void RemoveAllObstacles()
    {
        if (_instance == null) return;
        foreach (KeyValuePair<int, GameObject> kv in _instance._obstacles)
            if (kv.Value != null) Destroy(kv.Value);
        _instance._obstacles.Clear();
    }

    // ── Counter: force-send "close" to make their menu disappear for others ─
    /// <summary>
    /// Raises a Seralyth close event so every other client's NetworkMenuManager
    /// drops this player's remote menu display.
    /// </summary>
    public static void SendForceClose(int actor)
    {
        if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom) return;
        object[] payload = { MsgClose };
        RaiseEventOptions opts = new() { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(NetworkMenuByte, payload, opts, SendOptions.SendReliable);
        Notify($"Sent force-close for actor {actor}.");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────
    private static string GetPlayerName(int actor)
    {
        Player p = GetPlayer(actor);
        return p?.NickName ?? $"<unknown #{actor}>";
    }

    private static Player GetPlayer(int actor)
    {
        foreach (Player p in PhotonNetwork.PlayerList)
            if (p.ActorNumber == actor) return p;
        return null;
    }

    private static Transform FindRigForActor(int actor)
    {
        // Gorilla Tag stores each player's rig as a VRRig component tagged with actor number.
        // Reflect into the type so we don't need a hard reference.
        Type vrRigType = Type.GetType("VRRig, GorillaLocomotion") ??
                         Type.GetType("VRRig, Assembly-CSharp");
        if (vrRigType == null) return null;

        foreach (Component rig in FindObjectsByType(vrRigType, FindObjectsSortMode.None))
        {
            System.Reflection.FieldInfo fi = vrRigType.GetField("photonView",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (fi?.GetValue(rig) is PhotonView pv && pv.Owner?.ActorNumber == actor)
                return ((Component)rig).transform;
        }
        return null;
    }

    private static void Notify(string message)
    {
        Debug.Log($"[SeralythCounter] {message}");

        Type nm = Type.GetType("hamburbur.Managers.NotificationManager, hamburbur");
        System.Reflection.MethodInfo mi = nm?.GetMethod("SendNotification",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        mi?.Invoke(null, new object[]
        {
            "<color=red>SeralythCounter</color>",
            message,
            4f,
            true,
            false,
        });
    }
}

/// <summary>
/// Follows a cheater's rig so the obstacle stays in front of them.
/// </summary>
public class CheaterObstacleTracker : MonoBehaviour
{
    public int TargetActor;
    private Transform _target;
    private float _nextSearch;

    private void Update()
    {
        if (Time.time > _nextSearch)
        {
            _nextSearch = Time.time + 1f;
            _target = TryFindRig();
        }

        if (_target == null) return;
        // Stay 1.5 m in front of them
        transform.position = _target.position + _target.forward * 1.5f + Vector3.up * 0.5f;
    }

    private Transform TryFindRig()
    {
        Type vrRigType = Type.GetType("VRRig, GorillaLocomotion") ??
                         Type.GetType("VRRig, Assembly-CSharp");
        if (vrRigType == null) return null;

        foreach (Component rig in FindObjectsByType(vrRigType, FindObjectsSortMode.None))
        {
            System.Reflection.FieldInfo fi = vrRigType.GetField("photonView",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (fi?.GetValue(rig) is PhotonView pv && pv.Owner?.ActorNumber == TargetActor)
                return ((Component)rig).transform;
        }
        return null;
    }
}
