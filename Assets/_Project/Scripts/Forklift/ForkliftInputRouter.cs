using UnityEngine;

namespace VRConTrain.Forklift
{
    /// <summary>
    /// Passes the input source and steering control through to the <see cref="ForkliftController"/>, and the cab
    /// levers through to the <see cref="MastController"/>.
    /// </summary>
    public sealed class ForkliftInputRouter : MonoBehaviour
    {
        [SerializeField] private ForkliftController _controller;

        [Tooltip("A component implementing IForkliftInputSource.")]
        [SerializeField] private MonoBehaviour _inputSource;

        [Tooltip("A component implementing IForkliftSteering.")]
        [SerializeField] private MonoBehaviour _steering;

        [Tooltip("Optional. Moves the mast and forks from the cab levers.")]
        [SerializeField] private MastController _mast;

        [Tooltip("A component implementing IForkliftLever: pull to lift, push to lower.")]
        [SerializeField] private MonoBehaviour _liftLever;

        [Tooltip("A component implementing IForkliftLever: pull to tilt back, push to tilt forward.")]
        [SerializeField] private MonoBehaviour _tiltLever;

        [Tooltip("A component implementing IForkliftLever: positive shifts the forks right.")]
        [SerializeField] private MonoBehaviour _sideShiftLever;

        [Tooltip("How fast keyboard steering turns the steering wheel, in degrees per second.")]
        [SerializeField, Min(0f)] private float _debugSteerRate = 240f;

        /// <summary>
        /// True while someone is in the driver's seat. With no driver the forklift holds its brake and ignores the
        /// accelerator and mast levers.
        /// </summary>
        public bool DriverPresent { get; set; } = true;

        private IForkliftInputSource _input;
        private IForkliftSteering _steeringControl;
        private IForkliftLever _lift;
        private IForkliftLever _tilt;
        private IForkliftLever _sideShift;

        private void Awake()
        {
            _input = _inputSource as IForkliftInputSource;
            _steeringControl = _steering as IForkliftSteering;
            _lift = _liftLever as IForkliftLever;
            _tilt = _tiltLever as IForkliftLever;
            _sideShift = _sideShiftLever as IForkliftLever;

            if (_controller == null || _input == null || _steeringControl == null)
            {
                Debug.LogError($"{nameof(ForkliftInputRouter)} on {name} is missing a reference.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_input == null || _controller == null)
                return;

            _input.ShiftUpPressed += _controller.ShiftUp;
            _input.ShiftDownPressed += _controller.ShiftDown;
        }

        private void OnDisable()
        {
            if (_input == null || _controller == null)
                return;

            _input.ShiftUpPressed -= _controller.ShiftUp;
            _input.ShiftDownPressed -= _controller.ShiftDown;
        }

        private void Update()
        {
            if (!DriverPresent)
            {
                _controller.SetDriverInput(0f, 1f, _steeringControl.Steer);
                if (_mast != null)
                    _mast.SetInput(0f, 0f, 0f);

                return;
            }

            var debugSteer = _input.DebugSteer;
            if (!Mathf.Approximately(debugSteer, 0f))
                _steeringControl.Turn(debugSteer * _debugSteerRate * Time.deltaTime);

            _controller.SetDriverInput(_input.Accelerator, _input.Brake, _steeringControl.Steer);

            if (_mast != null)
                _mast.SetInput(LeverValue(_lift), LeverValue(_tilt), LeverValue(_sideShift));
        }

        private static float LeverValue(IForkliftLever lever) => lever != null ? lever.Value : 0f;
    }
}
