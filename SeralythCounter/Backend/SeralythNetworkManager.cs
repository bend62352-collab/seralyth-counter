using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using hamburbur.Managers;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace SeralythCounter.Backend
{
    /// <summary>
    /// Singleton MonoBehaviour that lives for the session.
    /// Listens for Seralyth's byte-71 Photon events and tracks detected cheaters.
    /// </summary>
    public class SeralythNetworkManager : MonoBehaviour, IOnEventCallback
    {
        public static SeralythNetworkManager Instance { get; private set; }

        // ── flags toggled by mods ──────────────────────────────────────────
        public static bool EventBlockEnabled      { get; set; } = false;
        public static bool HeartbeatFloodEnabled  { get; set; } = false;
        public static bool GhostPositionEnabled   { get; set; } = false;

        // actorNumber → last-seen timestamp
        public static Dictionary<int, float> DetectedCheaters { get; } = new();

        private const byte   SeralythByte    = 71;
        private const float  TimeoutSeconds  = 3.5f;
        private const float  FloodInterval   = 0.25f;

        private readonly Dictionary<int, GameObject> _obstacles = new();
        private          Coroutine                   _floodCoroutine;
        private          Coroutine                   _timeoutCoroutine;

        // ── lifecycle ─────────────────────────────────────────────────────

        public static void EnsureLoaded()
        {
            if (Instance != null) return;
            var go = new GameObject("SeralythNetworkManager");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<SeralythNetworkManager>();
        }

        public static void Shutdown()
        {
            if (Instance == null) return;
            Destroy(Instance.gameObject);
            Instance = null;
            DetectedCheaters.Clear();
        }

        private void OnEnable()
        {
            PhotonNetwork.AddCallbackTarget(this);
            _timeoutCoroutine = StartCoroutine(TimeoutLoop());
        }

        private void OnDisable()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
            if (_timeoutCoroutine  != null) StopCoroutine(_timeoutCoroutine);
            if (_floodCoroutine    != null) StopCoroutine(_floodCoroutine);
            RemoveAllObstacles();
        }

        // ── IOnEventCallback ──────────────────────────────────────────────

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code != SeralythByte) return;

            int    sender = photonEvent.Sender;
            object[] data = photonEvent.CustomData as object[];
            if (data == null || data.Length == 0) return;

            string msgType = data[0] as string;

            switch (msgType)
            {
                case "menuState":
                case "heartbeat":
                case "theme":
                    // Sender is running Seralyth — record/refresh them
                    DetectedCheaters[sender] = Time.time;
                    NotificationManager.SendNotification(
                        "<color=red>SeralythCounter</color>",
                        $"Seralyth detected: actor {sender}", 3f, true, false);

                    if (GhostPositionEnabled)
                        SpawnObstacleForActor(sender);

                    if (HeartbeatFloodEnabled && _floodCoroutine == null)
                        _floodCoroutine = StartCoroutine(FloodClose());
                    break;

                case "close":
                    DetectedCheaters.Remove(sender);
                    RemoveObstacleForActor(sender);
                    break;
            }
        }

        // ── helpers called by mods ────────────────────────────────────────

        public static void SendForceClose(int targetActor)
        {
            if (!PhotonNetwork.InRoom) return;
            object[]          payload = { "close" };
            RaiseEventOptions opts    = new()
            {
                TargetActors  = new[] { targetActor },
                CachingOption = EventCaching.DoNotCache,
            };
            PhotonNetwork.RaiseEvent(SeralythByte, payload, opts, SendOptions.SendReliable);
        }

        public static void SpawnObstacleForActor(int actor)
        {
            if (Instance == null) return;
            if (Instance._obstacles.ContainsKey(actor)) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.localScale = new Vector3(2f, 3f, 0.2f);
            var rig = GetRigForActor(actor);
            if (rig != null)
                go.transform.position = rig.transform.position + rig.transform.forward * 1.5f;

            var mat = go.GetComponent<Renderer>().material;
            mat.color = new Color(1f, 0f, 0f, 0.5f);

            DontDestroyOnLoad(go);
            Instance._obstacles[actor] = go;
            Instance.StartCoroutine(Instance.TrackObstacle(actor, go));
        }

        public static void RemoveAllObstacles()
        {
            if (Instance == null) return;
            foreach (var kv in Instance._obstacles)
                if (kv.Value != null) Destroy(kv.Value);
            Instance._obstacles.Clear();
        }

        private static void RemoveObstacleForActor(int actor)
        {
            if (Instance == null) return;
            if (Instance._obstacles.TryGetValue(actor, out var go))
            {
                if (go != null) Destroy(go);
                Instance._obstacles.Remove(actor);
            }
        }

        // ── coroutines ────────────────────────────────────────────────────

        private IEnumerator TrackObstacle(int actor, GameObject wall)
        {
            while (wall != null && DetectedCheaters.ContainsKey(actor))
            {
                var rig = GetRigForActor(actor);
                if (rig != null)
                    wall.transform.position = rig.transform.position + rig.transform.forward * 1.5f;
                yield return new WaitForSeconds(0.1f);
            }
            if (wall != null) Destroy(wall);
            _obstacles.Remove(actor);
        }

        private IEnumerator FloodClose()
        {
            object[]          payload = { "close" };
            RaiseEventOptions opts    = new()
            {
                Receivers     = ReceiverGroup.All,
                CachingOption = EventCaching.DoNotCache,
            };

            while (HeartbeatFloodEnabled && PhotonNetwork.InRoom)
            {
                PhotonNetwork.RaiseEvent(SeralythByte, payload, opts, SendOptions.SendReliable);
                yield return new WaitForSeconds(FloodInterval);
            }
            _floodCoroutine = null;
        }

        private IEnumerator TimeoutLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);
                float now     = Time.time;
                var   toRemove = new List<int>();
                foreach (var kv in DetectedCheaters)
                    if (now - kv.Value > TimeoutSeconds)
                        toRemove.Add(kv.Key);
                foreach (int a in toRemove)
                {
                    DetectedCheaters.Remove(a);
                    RemoveObstacleForActor(a);
                }
            }
        }

        // ── utility ───────────────────────────────────────────────────────

        private static VRRig GetRigForActor(int actor)
        {
            foreach (var rig in GameObject.FindObjectsOfType<VRRig>())
                if (rig.Creator != null && rig.Creator.ActorNumber == actor)
                    return rig;
            return null;
        }
    }
}
