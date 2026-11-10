using System;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using VRConTrain.Forklift;

namespace VRConTrain.Interaction
{
    /// <summary>
    /// Switches the player between walking on foot and sitting in the forklift. On foot, the enter control (A on
    /// the right controller) seats the player when they are close to the cab. Seated, the exit control (left stick
    /// click) puts them on the floor beside the cab, but only once the forklift has stopped.
    /// </summary>
    /// <remarks>
    /// On foot: the rig's body capsule, move, turn, gravity and teleport are on, and the forklift ignores the
    /// controllers and holds its brake. Seated: the rig rides on the seat anchor, on-foot locomotion is off, and the
    /// driver controls are live.
    /// </remarks>
    public sealed class ForkliftBoarding : MonoBehaviour
    {
        const string k_MapName = "Player";
        const string k_ExitHint = "Click the left stick to get out";
        const string k_StopHint = "Stop the forklift before getting out";
        const string k_NoRoomHint = "No room to get out here";
        const float k_MaxExitSlope = 30f;
        const float k_MaxExitStep = 0.3f;

        [Header("Rig")]
        [SerializeField] private XROrigin _origin;
        [SerializeField] private CharacterController _body;

        [Tooltip("The rig's on-foot locomotion: move, turn, gravity and the teleportation provider.")]
        [SerializeField] private GameObject _locomotion;

        [SerializeField] private TriggerTeleporter _teleporter;

        [Header("Forklift")]
        [SerializeField] private Transform _seatAnchor;
        [SerializeField] private SeatedRigRecenter _seatedRecenter;
        [SerializeField] private ForkliftController _forklift;
        [SerializeField] private ForkliftInputRouter _router;

        [Tooltip("The driver input component. It is enabled only while seated.")]
        [SerializeField] private Behaviour _driverInput;

        [Header("Input")]
        [SerializeField] private InputActionAsset _controls;

        [Header("Rules")]
        [Tooltip("How close the player's head must be to the seat to get in, in metres, ignoring height.")]
        [SerializeField, Min(0.5f)] private float _boardingRange = 2.5f;

        [Tooltip("Fastest the forklift may be moving for the driver to get out, in metres per second.")]
        [SerializeField, Min(0f)] private float _maxExitSpeed = 0.3f;

        [Tooltip("Where to stand after getting out, in forklift space at floor level. Tried in order; the first " +
                 "with room to stand is used.")]
        [SerializeField] private Vector3[] _exitPoints =
        {
            new Vector3(-1.3f, 0f, -0.6f), // driver's left, beside the seat
            new Vector3(1.3f, 0f, -0.6f), // driver's right
            new Vector3(0f, 0f, -2.9f), // behind the counterweight
        };

        [SerializeField, Min(0.05f)] private float _bodyRadius = 0.25f;
        [SerializeField, Min(0.5f)] private float _bodyHeight = 1.8f;

        [Tooltip("Layers that count as floor and as obstacles when finding a place to stand.")]
        [SerializeField] private LayerMask _collisionMask = ~(1 << 2);

        [SerializeField] private bool _startSeated;

        [Header("Prompts")]
        [Tooltip("Shown above the forklift while the player is on foot and close enough to get in.")]
        [SerializeField] private TMP_Text _boardPrompt;

        [Tooltip("Short messages for the seated driver, such as how to get out.")]
        [SerializeField] private TMP_Text _cabHint;

        [SerializeField, Min(0f)] private float _hintDuration = 4f;

        private InputAction _enter;
        private InputAction _exit;
        private bool _enterWasPressed;
        private bool _exitWasPressed;
        private float _hintHideTime;

        /// <summary>True while the player is sitting in the forklift.</summary>
        public bool IsSeated { get; private set; }

        /// <summary>True when the player's head is close enough to the seat to get in.</summary>
        public bool IsInBoardingRange =>
            LocomotionMath.HorizontalDistance(_origin.Camera.transform.position, _seatAnchor.position) <= _boardingRange;

        /// <summary>Raised with true when the player gets in, and false when they get out.</summary>
        public event Action<bool> SeatedChanged;

        private void Awake()
        {
            if (_origin == null || _body == null || _locomotion == null || _teleporter == null || _seatAnchor == null
                || _seatedRecenter == null || _forklift == null || _router == null || _driverInput == null || _controls == null)
            {
                Debug.LogError($"{nameof(ForkliftBoarding)} on {name} is missing a reference.", this);
                enabled = false;
                return;
            }

            var map = _controls.FindActionMap(k_MapName, true);
            _enter = map.FindAction("EnterForklift", true);
            _exit = map.FindAction("ExitForklift", true);
        }

        private void OnEnable()
        {
            _enter?.Enable();
            _exit?.Enable();
        }

        private void OnDisable()
        {
            _enter?.Disable();
            _exit?.Disable();
        }

        private void Start()
        {
            if (_startSeated)
                SitDown();
            else
                ApplyMode(false);
        }

        private void Update()
        {
            // Presses are found by comparing IsPressed with last frame. WasPressedThisFrame misses XR controller
            // presses that the Input System processes outside the frame's main input update.
            var enterPressed = _enter.IsPressed();
            var exitPressed = _exit.IsPressed();
            var enterDown = enterPressed && !_enterWasPressed;
            var exitDown = exitPressed && !_exitWasPressed;
            _enterWasPressed = enterPressed;
            _exitWasPressed = exitPressed;

            // One transition per frame, so a key bound to both actions can't get in and straight back out.
            if (IsSeated)
            {
                if (exitDown)
                    TryExit();
            }
            else if (enterDown)
            {
                TryEnter();
            }

            UpdatePrompts();
        }

        /// <summary>Seats the player if they are on foot and close enough. Returns true if they got in.</summary>
        public bool TryEnter()
        {
            if (IsSeated || !IsInBoardingRange)
                return false;

            SitDown();
            return true;
        }

        /// <summary>
        /// Puts the seated player on the floor beside the forklift if it has stopped and there is room. Returns true
        /// if they got out; otherwise shows the driver why not.
        /// </summary>
        public bool TryExit()
        {
            if (!IsSeated)
                return false;

            if (!LocomotionMath.IsStoppedForExit(_forklift.ForwardSpeed, _maxExitSpeed))
            {
                ShowHint(k_StopHint);
                return false;
            }

            if (!TryFindExitPoint(out var feet))
            {
                ShowHint(k_NoRoomHint);
                return false;
            }

            StandUp(feet);
            return true;
        }

        private void SitDown()
        {
            // Turn the body capsule and locomotion off before moving, so they can't push the rig back out.
            ApplyMode(true);

            var rig = _origin.transform;
            rig.SetParent(_seatAnchor, false);
            rig.localPosition = Vector3.zero;
            rig.localRotation = Quaternion.identity;
            _seatedRecenter.Recenter();
            ShowHint(k_ExitHint);
        }

        private void StandUp(Vector3 feet)
        {
            var rig = _origin.transform;
            rig.SetParent(null, true);
            _origin.MatchOriginUp(Vector3.up);

            // Move the rig so the point under the player's head lands on the exit point; real-world position in
            // the play area and heading are kept.
            var head = _origin.Camera.transform.position;
            var underHead = new Vector3(head.x, rig.position.y, head.z);
            rig.position += feet - underHead;

            ApplyMode(false);
        }

        private void ApplyMode(bool seated)
        {
            IsSeated = seated;
            _body.enabled = !seated;
            _locomotion.SetActive(!seated);
            _teleporter.enabled = !seated;
            _seatedRecenter.enabled = seated;
            _driverInput.enabled = seated;
            _router.DriverPresent = seated;

            if (!seated)
                _hintHideTime = 0f;

            SeatedChanged?.Invoke(seated);
        }

        private bool TryFindExitPoint(out Vector3 feet)
        {
            var forklift = _forklift.transform;
            foreach (var exitPoint in _exitPoints)
            {
                var ground = forklift.TransformPoint(exitPoint);
                if (!Physics.Raycast(ground + Vector3.up * 1.5f, Vector3.down, out var floor, 3f, _collisionMask, QueryTriggerInteraction.Ignore))
                    continue;

                // The spot must be floor at the forklift's own level, not the top of a box or rack beside it.
                if (!LocomotionMath.IsWalkable(floor.normal, k_MaxExitSlope)
                    || !LocomotionMath.IsWithinStep(floor.point.y, ground.y, k_MaxExitStep))
                    continue;

                var bottom = floor.point + Vector3.up * (_bodyRadius + 0.05f);
                var top = floor.point + Vector3.up * (_bodyHeight - _bodyRadius);
                if (Physics.CheckCapsule(bottom, top, _bodyRadius, _collisionMask, QueryTriggerInteraction.Ignore))
                    continue;

                feet = floor.point;
                return true;
            }

            feet = default;
            return false;
        }

        private void ShowHint(string message)
        {
            if (_cabHint == null)
                return;

            _cabHint.text = message;
            _hintHideTime = Time.time + _hintDuration;
        }

        private void UpdatePrompts()
        {
            if (_boardPrompt != null)
            {
                var showPrompt = !IsSeated && IsInBoardingRange;
                if (_boardPrompt.gameObject.activeSelf != showPrompt)
                    _boardPrompt.gameObject.SetActive(showPrompt);

                // Turn the prompt to face the player; text reads correctly when its forward points away from them.
                if (showPrompt)
                {
                    var prompt = _boardPrompt.transform;
                    var away = prompt.position - _origin.Camera.transform.position;
                    away.y = 0f;
                    if (away.sqrMagnitude > 0.0001f)
                        prompt.rotation = Quaternion.LookRotation(away, Vector3.up);
                }
            }

            if (_cabHint != null)
            {
                var showHint = IsSeated && Time.time < _hintHideTime;
                if (_cabHint.gameObject.activeSelf != showHint)
                    _cabHint.gameObject.SetActive(showHint);
            }
        }
    }
}
