using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using hamburbur.Tools;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace SeralythCounter.Backend;

public class SeralythNetworkManager : MonoBehaviour, IOnEventCallback
{
    public static SeralythNetworkManager? Instance { get; private set; }

    public static void EnsureLoaded()
    {
        if (Instance != null) return;
        var go = new GameObject("[SeralythNetworkManager]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<SeralythNetworkManager>();
    }

    public static void Shutdown()
    {
        if (Instance == null) return;
        Destroy(Instance.gameObject);
        Instance = null;
    }

    // ── Feature flags set by toggle mods ──────────────────────────────────
    public static bool NotificationsEnabled   = true;
    public static bool EventBlockEnabled      = false;
    public static bool HeartbeatFloodEnabled  = false;
    public static bool GhostPositionEnabled   = false;
    public static bool AutoDestroyViewEnabled = false;
    public static bool NetworkedSpawnEnabled  = false;

    private const byte SeralythByte  = 71;
    private const byte SpawnCubeByte = 72;   // our own event — broadcasts cube position to all

    // Exact Seralyth 4.8.5 command strings (from NetworkMenuManager.cs)
    private static readonly HashSet<string> SeralythActiveCommands = new()
    {
        "seralyth_netmenu_state",
        "seralyth_netmenu_heartbeat",
        "seralyth_netmenu_theme_v2",
        "seralyth_netmenu_theme",
        "seralyth_netmenu_gundata",
    };
    private const string SeralythCloseCommand = "seralyth_netmenu_close";

    private const float HeartbeatTimeout = 5f;
    private const float NotifyCooldown   = 5f;

    public  static readonly Dictionary<int, float> DetectedCheaters = new();
    private static readonly Dictionary<int, float> LastNotified     = new();

    // Local cubes keyed by actor number
    private readonly Dictionary<int, List<GameObject>> _obstacles = new();

    private void Awake()
    {
        Instance = this;
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDestroy()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
        RemoveAllObstacles();
    }

    // ── IOnEventCallback ─────────────────────────────────────────────────
    public void OnEvent(EventData photonEvent)
    {
        // Handle our own networked-spawn event from any sender
        if (photonEvent.Code == SpawnCubeByte)
        {
            HandleSpawnCubeEvent(photonEvent.CustomData);
            return;
        }

        if (photonEvent.Code != SeralythByte) return;

        int    sender    = photonEvent.Sender;
        string eventType = ParseEventType(photonEvent.CustomData);

        if (eventType == SeralythCloseCommand)
        {
            DetectedCheaters.Remove(sender);
            LastNotified.Remove(sender);
            RemoveObstacle(sender);
            return;
        }

        if (!SeralythActiveCommands.Contains(eventType)) return;

        DetectedCheaters[sender] = Time.time;

        // Notification (respects cooldown, only when mod is toggled on)
        if (NotificationsEnabled)
        {
            float now = Time.time;
            if (!LastNotified.TryGetValue(sender, out float last) || now - last > NotifyCooldown)
            {
                LastNotified[sender] = now;
                string nick = GetNick(sender);
                hamburbur.Managers.NotificationManager.SendNotification(
                    "SeralythCounter",
                    $"Detected Seralyth: {nick}",
                    5f, true, true);
            }
        }

        // Countermeasures
        if (HeartbeatFloodEnabled)  FloodClose(sender);
        if (GhostPositionEnabled)   SpawnNetworkedCubeForActor(sender);
        if (AutoDestroyViewEnabled) DestroyPhotonView(sender);
    }

    // ── Timeout cleanup ───────────────────────────────────────────────────
    private void Update()
    {
        float now    = Time.time;
        var   remove = new List<int>();

        foreach (var kv in DetectedCheaters)
            if (now - kv.Value > HeartbeatTimeout)
                remove.Add(kv.Key);

        foreach (int id in remove)
        {
            DetectedCheaters.Remove(id);
            LastNotified.Remove(id);
            RemoveObstacle(id);
        }
    }

    // ── Force close ───────────────────────────────────────────────────────
    public void SendForceClose(int actorNumber)
    {
        if (!PhotonNetwork.InRoom) return;

        PhotonNetwork.RaiseEvent(SeralythByte,
            new object[] { SeralythCloseCommand },
            new RaiseEventOptions
            {
                TargetActors  = new[] { actorNumber },
                CachingOption = EventCaching.DoNotCache,
            },
            SendOptions.SendReliable);
    }

    public void FloodClose(int actorNumber)
    {
        if (!PhotonNetwork.InRoom) return;

        var opts = new RaiseEventOptions
        {
            TargetActors  = new[] { actorNumber },
            CachingOption = EventCaching.DoNotCache,
        };

        for (int i = 0; i < 50; i++)
            PhotonNetwork.RaiseEvent(SeralythByte,
                new object[] { SeralythCloseCommand },
                opts,
                SendOptions.SendUnreliable);
    }

    // ── Networked cube spawning ───────────────────────────────────────────
    // Sends byte-72 event to ALL players (including the target).
    // Every client spawns a local cube at the given world position.
    // Result: everyone including the Seralyth user sees the cube.
    public void SpawnNetworkedCubeForActor(int actorNumber)
    {
        if (!PhotonNetwork.InRoom) return;

        VRRig? rig = actorNumber.Rig();
        if (rig == null) return;

        Vector3 pos = rig.headConstraint.position;

        // Pack: [actorNumber, x, y, z]
        var data = new object[] { actorNumber, pos.x, pos.y, pos.z };

        PhotonNetwork.RaiseEvent(SpawnCubeByte, data,
            new RaiseEventOptions
            {
                Receivers     = ReceiverGroup.All,
                CachingOption = EventCaching.DoNotCache,
            },
            SendOptions.SendReliable);
    }

    private void HandleSpawnCubeEvent(object customData)
    {
        if (customData is not object[] arr || arr.Length < 4) return;

        int     actor = System.Convert.ToInt32(arr[0]);
        float   x     = System.Convert.ToSingle(arr[1]);
        float   y     = System.Convert.ToSingle(arr[2]);
        float   z     = System.Convert.ToSingle(arr[3]);
        Vector3 pos   = new Vector3(x, y, z);

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.position   = pos;
        cube.transform.localScale = Vector3.one * 0.5f;
        DontDestroyOnLoad(cube);

        var rend = cube.GetComponent<Renderer>();
        if (rend != null) rend.material.color = Color.red;

        if (!_obstacles.ContainsKey(actor))
            _obstacles[actor] = new List<GameObject>();

        _obstacles[actor].Add(cube);
    }

    public void RemoveObstacle(int actorNumber)
    {
        if (!_obstacles.TryGetValue(actorNumber, out var list)) return;
        foreach (var obj in list)
            if (obj != null) Destroy(obj);
        _obstacles.Remove(actorNumber);
    }

    public void RemoveAllObstacles()
    {
        foreach (var list in _obstacles.Values)
            foreach (var obj in list)
                if (obj != null) Destroy(obj);
        _obstacles.Clear();
    }

    // Legacy local-only obstacle (kept for guns that call SpawnObstacleForActor)
    public void SpawnObstacleForActor(int actorNumber)
        => SpawnNetworkedCubeForActor(actorNumber);

    // ── Photon event 204 — destroy PhotonView (ghosts rig) ───────────────
    public void DestroyPhotonView(int actorNumber)
    {
        if (!PhotonNetwork.InRoom) return;

        foreach (PhotonView view in PhotonNetwork.PhotonViewCollection)
        {
            if (view.Owner?.ActorNumber != actorNumber) continue;

            var ht   = new Hashtable { { 0, view.ViewID } };
            var opts = new RaiseEventOptions
            {
                TargetActors  = new[] { actorNumber },
                CachingOption = EventCaching.DoNotCache,
            };
            PhotonNetwork.NetworkingClient.OpRaiseEvent(204, ht, opts, SendOptions.SendReliable);
        }
    }

    // ── Photon event 207 — destroy Player (lags/disconnects) ─────────────
    public void DestroyPlayer(int actorNumber)
    {
        if (!PhotonNetwork.InRoom) return;

        var ht   = new Hashtable { { 0, actorNumber } };
        var opts = new RaiseEventOptions
        {
            TargetActors  = new[] { actorNumber },
            CachingOption = EventCaching.DoNotCache,
        };
        PhotonNetwork.NetworkingClient.OpRaiseEvent(207, ht, opts, SendOptions.SendReliable);
    }

    // ── Force rename via actor custom properties ──────────────────────────
    public void ForceRename(int actorNumber, string newName)
    {
        if (!PhotonNetwork.InRoom) return;

        Player? target = PhotonNetwork.PlayerList.FirstOrDefault(p => p.ActorNumber == actorNumber);
        if (target == null) return;

        var ht = new Hashtable { [(byte)255] = newName };
        target.SetCustomProperties(ht);
    }

    // ── MasterClient crash cycle ──────────────────────────────────────────
    public void CrashMasterCycle(int actorNumber)
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;

        Player? target = PhotonNetwork.PlayerList.FirstOrDefault(p => p.ActorNumber == actorNumber);
        if (target == null) return;

        PhotonNetwork.SetMasterClient(target);
        PhotonNetwork.SetMasterClient(PhotonNetwork.LocalPlayer);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static string ParseEventType(object customData)
    {
        if (customData == null) return "";
        try
        {
            if (customData is object[] arr && arr.Length > 0)
                return arr[0]?.ToString() ?? "";

            if (customData is Hashtable ht)
            {
                if (ht.ContainsKey("type"))    return ht["type"]?.ToString()    ?? "";
                if (ht.ContainsKey("command")) return ht["command"]?.ToString() ?? "";
                if (ht.ContainsKey(0))         return ht[0]?.ToString()         ?? "";
                if (ht.ContainsKey("0"))       return ht["0"]?.ToString()       ?? "";
            }

            if (customData is string s) return s;
        }
        catch { }
        return "";
    }

    private static string GetNick(int actorNumber)
    {
        try { return actorNumber.Rig()?.Creator?.NickName ?? actorNumber.ToString(); }
        catch { return actorNumber.ToString(); }
    }
}
