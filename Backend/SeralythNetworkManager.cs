using System.Collections.Generic;
using ExitGames.Client.Photon;
using hamburbur.Tools;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace SeralythCounter.Backend;

public class SeralythNetworkManager : MonoBehaviour
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
    public static bool EventBlockEnabled     = false;
    public static bool HeartbeatFloodEnabled = false;
    public static bool GhostPositionEnabled  = false;

    private const byte SeralythByte = 71;

    private readonly Dictionary<int, GameObject> _obstacles = new();

    private void Awake() => Instance = this;

    // ──────────────────────────────────────────────────────────────────────
    // Force close
    // ──────────────────────────────────────────────────────────────────────
    public void SendForceClose(int actorNumber)
    {
        if (!PhotonNetwork.InRoom) return;

        RaiseEventOptions opts = new()
        {
            TargetActors  = new[] { actorNumber },
            CachingOption = EventCaching.DoNotCache,
        };

        PhotonNetwork.RaiseEvent(SeralythByte,
            new object[] { "close" },
            opts,
            SendOptions.SendReliable);
    }

    public void FloodClose(int actorNumber)
    {
        if (!PhotonNetwork.InRoom) return;

        RaiseEventOptions opts = new()
        {
            TargetActors  = new[] { actorNumber },
            CachingOption = EventCaching.DoNotCache,
        };

        for (int i = 0; i < 50; i++)
            PhotonNetwork.RaiseEvent(SeralythByte,
                new object[] { "close" },
                opts,
                SendOptions.SendUnreliable);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Obstacle spawning
    // ──────────────────────────────────────────────────────────────────────
    public void SpawnObstacleForActor(int actorNumber)
    {
        if (_obstacles.ContainsKey(actorNumber)) return;
        if (!PhotonNetwork.InRoom) return;

        VRRig? rig = actorNumber.Rig();
        if (rig == null) return;

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.position   = rig.headConstraint.position;
        cube.transform.localScale = Vector3.one * 0.5f;
        DontDestroyOnLoad(cube);

        var rend = cube.GetComponent<Renderer>();
        if (rend != null) rend.material.color = Color.red;

        _obstacles[actorNumber] = cube;
    }

    public void RemoveObstacle(int actorNumber)
    {
        if (!_obstacles.TryGetValue(actorNumber, out var obj)) return;
        if (obj != null) Destroy(obj);
        _obstacles.Remove(actorNumber);
    }

    public void RemoveAllObstacles()
    {
        foreach (var obj in _obstacles.Values)
            if (obj != null) Destroy(obj);
        _obstacles.Clear();
    }
}
