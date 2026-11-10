using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace VRConTrain.Interaction
{
    /// <summary>
    /// Teleports the player on foot. While the teleport control (right trigger) is held, an arc is drawn from the
    /// right hand and a marker shows the landing spot. Releasing the trigger teleports there and keeps the player's
    /// heading. The arc turns red, and no marker shows, where the player can't land: slopes, shelves, the forklift,
    /// or anywhere without room to stand.
    /// </summary>
    public sealed class TriggerTeleporter : MonoBehaviour
    {
        const string k_MapName = "Player";
        const string k_ActionName = "Teleport";
        const int k_MaxSegments = 64;

        [SerializeField] private XROrigin _origin;
        [SerializeField] private TeleportationProvider _provider;

        [Tooltip("Where the arc starts; its forward is the aim direction.")]
        [SerializeField] private Transform _aimOrigin;

        [SerializeField] private InputActionAsset _controls;

        [Header("Visuals")]
        [SerializeField] private LineRenderer _line;
        [SerializeField] private Material _validMaterial;
        [SerializeField] private Material _invalidMaterial;

        [Tooltip("Shown at the landing spot. Its position is the spot itself.")]
        [SerializeField] private Transform _marker;

        [Tooltip("How far above the floor the marker's tip sits, in metres.")]
        [SerializeField, Min(0f)] private float _markerLift = 0.05f;

        [Header("Arc")]
        [Tooltip("Layers the arc can hit and that block the player's body.")]
        [SerializeField] private LayerMask _collisionMask = ~(1 << 2);

        [Tooltip("Launch speed of the arc, in metres per second. Higher reaches further.")]
        [SerializeField, Min(0.1f)] private float _launchSpeed = 7f;

        [SerializeField, Min(0.1f)] private float _gravity = 9.81f;

        [Tooltip("Time between arc points, in seconds.")]
        [SerializeField, Range(0.01f, 0.2f)] private float _timeStep = 0.05f;

        [SerializeField, Range(2, k_MaxSegments)] private int _maxSegments = 40;

        [Header("Landing")]
        [Tooltip("Steepest surface the player can land on, in degrees.")]
        [SerializeField, Range(0f, 60f)] private float _maxSlope = 30f;

        [Tooltip("How far above or below the player's feet the landing spot may be, in metres.")]
        [SerializeField, Min(0f)] private float _maxStepHeight = 0.5f;

        [SerializeField, Min(0.05f)] private float _bodyRadius = 0.25f;
        [SerializeField, Min(0.5f)] private float _bodyHeight = 1.8f;

        private readonly Vector3[] _points = new Vector3[k_MaxSegments + 1];
        private InputAction _teleport;
        private bool _wasPressed;
        private bool _aiming;
        private bool _hasTarget;
        private Vector3 _target;

        /// <summary>True while the teleport control is held and the arc is showing.</summary>
        public bool IsAiming => _aiming;

        /// <summary>True while aiming at a spot the player can land on.</summary>
        public bool HasValidTarget => _aiming && _hasTarget;

        /// <summary>The spot the arc last landed on, valid or not.</summary>
        public Vector3 Target => _target;

        /// <summary>Raised with the destination when a teleport is requested.</summary>
        public event Action<Vector3> Teleported;

        private void Awake()
        {
            if (_origin == null || _provider == null || _aimOrigin == null || _controls == null || _line == null)
            {
                Debug.LogError($"{nameof(TriggerTeleporter)} on {name} is missing a reference.", this);
                enabled = false;
                return;
            }

            _teleport = _controls.FindActionMap(k_MapName, true).FindAction(k_ActionName, true);
        }

        private void OnEnable()
        {
            _teleport?.Enable();

            // A trigger already held (for example the accelerator when getting out) must be released first.
            _wasPressed = _teleport != null && _teleport.IsPressed();
            HideVisuals();
        }

        private void OnDisable()
        {
            // Only this action is turned off; the rest of the Player map belongs to other components.
            _teleport?.Disable();
            _aiming = false;
            HideVisuals();
        }

        private void Update()
        {
            // Presses are found by comparing IsPressed with last frame. WasPressedThisFrame misses XR controller
            // presses that the Input System processes outside the frame's main input update.
            var pressed = _teleport.IsPressed();
            if (pressed && !_wasPressed)
                _aiming = true;

            _wasPressed = pressed;
            if (!_aiming)
                return;

            if (pressed)
            {
                UpdateAim();
                return;
            }

            TryTeleport();
            CancelAim();
        }

        /// <summary>Recalculates the arc and landing spot from the aim origin, and updates the visuals.</summary>
        public void UpdateAim()
        {
            _aiming = true;
            var start = _aimOrigin.position;
            var velocity = _aimOrigin.forward * _launchSpeed;
            var feetHeight = _origin.transform.position.y;

            _points[0] = start;
            var count = 1;
            var hitSomething = false;
            _hasTarget = false;

            for (var i = 1; i <= _maxSegments; i++)
            {
                var previous = _points[count - 1];
                var next = LocomotionMath.ArcPoint(start, velocity, _gravity, i * _timeStep);
                var step = next - previous;
                var length = step.magnitude;

                if (Physics.Raycast(previous, step / length, out var hit, length, _collisionMask, QueryTriggerInteraction.Ignore))
                {
                    _points[count++] = hit.point;
                    _target = hit.point;
                    hitSomething = true;
                    _hasTarget = LocomotionMath.IsWalkable(hit.normal, _maxSlope)
                        && LocomotionMath.IsWithinStep(hit.point.y, feetHeight, _maxStepHeight)
                        && HasRoomToStand(hit.point);
                    break;
                }

                _points[count++] = next;
            }

            if (!hitSomething)
                _target = _points[count - 1];

            _line.enabled = true;
            _line.positionCount = count;
            _line.SetPositions(_points);
            _line.sharedMaterial = _hasTarget ? _validMaterial : _invalidMaterial;

            if (_marker != null)
            {
                if (_marker.gameObject.activeSelf != _hasTarget)
                    _marker.gameObject.SetActive(_hasTarget);

                if (_hasTarget)
                    _marker.SetPositionAndRotation(_target + Vector3.up * _markerLift, Quaternion.identity);
            }
        }

        /// <summary>Teleports to the current landing spot if it is valid. Returns true if a teleport was requested.</summary>
        public bool TryTeleport()
        {
            if (!HasValidTarget)
                return false;

            _provider.QueueTeleportRequest(new TeleportRequest
            {
                destinationPosition = _target,
                destinationRotation = Quaternion.identity,
                matchOrientation = MatchOrientation.None,
                requestTime = Time.time,
            });

            Teleported?.Invoke(_target);
            return true;
        }

        /// <summary>Stops aiming without teleporting and hides the arc.</summary>
        public void CancelAim()
        {
            _aiming = false;
            _hasTarget = false;
            HideVisuals();
        }

        private bool HasRoomToStand(Vector3 feet)
        {
            // The bottom sphere starts just above the floor so the floor itself doesn't count as blocking.
            var bottom = feet + Vector3.up * (_bodyRadius + 0.05f);
            var top = feet + Vector3.up * (_bodyHeight - _bodyRadius);
            return !Physics.CheckCapsule(bottom, top, _bodyRadius, _collisionMask, QueryTriggerInteraction.Ignore);
        }

        private void HideVisuals()
        {
            if (_line != null)
                _line.enabled = false;

            if (_marker != null && _marker.gameObject.activeSelf)
                _marker.gameObject.SetActive(false);
        }
    }
}
