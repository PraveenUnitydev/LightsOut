using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace LightsOut
{
    /// <summary>
    /// The networked player. Movement is owner-authoritative (each phone moves its own blob and NetworkTransform
    /// sends it to everyone), which keeps controls instant on Wi-Fi. The host assigns each player a colour, and the
    /// colour index also picks the spawn point. Remote players are only drawn while the local player can see them.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(NetworkTransform))]
    public class PlayerAvatar : NetworkBehaviour
    {
        public static PlayerAvatar Local { get; private set; }
        public static readonly List<PlayerAvatar> All = new();

        /// <summary>Raised when a player joins, leaves, or changes name/colour.</summary>
        public static event Action RosterChanged;

        [SerializeField] float _speed = 4.2f;
        [SerializeField] float _acceleration = 35f;

        public readonly NetworkVariable<int> ColorIndex = new(-1);
        public readonly NetworkVariable<FixedString32Bytes> PlayerName = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>0..1 how visible this player currently is to the local player (used by name tags).</summary>
        public float VisibleAlpha { get; private set; }

        public string DisplayName => PlayerName.Value.Length > 0 ? PlayerName.Value.ToString() : "Player " + OwnerClientId;
        public Color Color => Palette.Player(ColorIndex.Value);

        Rigidbody2D _rb;
        NetworkTransform _netTransform;
        BlobVisual _visual;
        Vector2 _input, _lastPos, _velocity;
        bool _placed;

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _netTransform = GetComponent<NetworkTransform>();
            GetComponent<Collider2D>().sharedMaterial = new PhysicsMaterial2D("Slippery") { friction = 0f, bounciness = 0f };

            _visual = new GameObject("Visual").AddComponent<BlobVisual>();
            _visual.Build(transform);
            _visual.SetAlpha(0f);
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            ColorIndex.OnValueChanged += OnColorChanged;
            PlayerName.OnValueChanged += OnNameChanged;

            if (IsServer) ColorIndex.Value = PickFreeColor();

            if (IsOwner)
            {
                Local = this;
                var name = new FixedString32Bytes();
                name.CopyFromTruncated(GameRoot.LocalPlayerName ?? "");
                PlayerName.Value = name;
                if (VisionMask.Instance != null) VisionMask.Instance.Target = transform;
            }
            else
            {
                // Remote players are moved by NetworkTransform only.
                _rb.simulated = false;
            }

            _lastPos = transform.position;
            _visual.SetColor(Color);
            Debug.Log($"[LightsOut] Player spawned: client {OwnerClientId} owner={IsOwner} colour={ColorIndex.Value}");
            RosterChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            ColorIndex.OnValueChanged -= OnColorChanged;
            PlayerName.OnValueChanged -= OnNameChanged;
            if (Local == this)
            {
                Local = null;
                if (VisionMask.Instance != null) VisionMask.Instance.Target = null;
            }
            RosterChanged?.Invoke();
        }

        void OnColorChanged(int previous, int current)
        {
            _visual.SetColor(Palette.Player(current));
            RosterChanged?.Invoke();
        }

        void OnNameChanged(FixedString32Bytes previous, FixedString32Bytes current) => RosterChanged?.Invoke();

        int PickFreeColor()
        {
            var used = new HashSet<int>();
            foreach (var p in All)
                if (p != this && p.ColorIndex.Value >= 0) used.Add(p.ColorIndex.Value);
            for (int i = 0; i < Palette.Players.Length; i++)
                if (!used.Contains(i)) return i;
            return (int)(OwnerClientId % (ulong)Palette.Players.Length);
        }

        void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner)
            {
                _input = GameInput.Move;
                if (!_placed && ColorIndex.Value >= 0) PlaceAtSpawn();
            }

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector2 pos = transform.position;
            _velocity = Vector2.Lerp(_velocity, (pos - _lastPos) / dt, 0.5f);
            _lastPos = pos;
            _visual.Tick(_velocity, dt);

            float target = IsOwner ? 1f : (VisionMask.Instance != null && VisionMask.Instance.CanSee(pos, 0.3f) ? 1f : 0f);
            if (IsOwner && !_placed) target = 0f;
            VisibleAlpha = Mathf.MoveTowards(VisibleAlpha, target, dt * 6f);
            _visual.SetAlpha(VisibleAlpha);
        }

        void FixedUpdate()
        {
            if (!IsSpawned || !IsOwner || !_placed) return;
            _rb.linearVelocity = Vector2.MoveTowards(_rb.linearVelocity, _input * _speed, _acceleration * Time.fixedDeltaTime);
        }

        void PlaceAtSpawn()
        {
            var spawns = GameMap.SpawnPoints;
            Vector3 pos = spawns.Count > 0 ? spawns[ColorIndex.Value % spawns.Count] : Vector2.zero;
            _rb.position = pos;
            _rb.linearVelocity = Vector2.zero;
            _netTransform.Teleport(pos, Quaternion.identity, transform.localScale);
            _lastPos = pos;
            _placed = true;
        }
    }
}
