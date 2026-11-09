using UnityEngine;

namespace VRConTrain.Forklift
{
    /// <summary>
    /// Moves the mast and fork carriage: lift and lower, tilt forward and back, and side-shift left and right. Each
    /// input sets a speed, like a hydraulic valve, and every movement stops at its limit. The parts are moved as
    /// kinematic transforms relative to their rest pose, so the model's pivots don't need to be at the hinges.
    /// </summary>
    public sealed class MastController : MonoBehaviour
    {
        [Tooltip("The mast. It tilts around the hinge. Its parent must be the upright forklift root.")]
        [SerializeField] private Transform _mast;

        [Tooltip("The fork carriage, a child of the mast. It lifts and side-shifts along the mast.")]
        [SerializeField] private Transform _carriage;

        [Tooltip("The mast's tilt hinge, in the mast parent's space.")]
        [SerializeField] private Vector3 _tiltHinge = new Vector3(0f, 0.318f, 1.023f);

        [Tooltip("The tilt axis in the mast parent's space. A positive angle tilts the mast back towards the driver.")]
        [SerializeField] private Vector3 _tiltAxis = Vector3.left;

        [Header("Lift")]
        [Tooltip("Lift speed at full lever, in m/s.")]
        [SerializeField, Min(0f)] private float _liftSpeed = 0.35f;

        [Tooltip("How far the carriage can lower below its modelled position, in metres (negative).")]
        [SerializeField] private float _minLift = -0.55f;

        [Tooltip("How far the carriage can rise above its modelled position, in metres.")]
        [SerializeField] private float _maxLift = 0.9f;

        [Header("Tilt")]
        [Tooltip("Tilt speed at full lever, in degrees per second.")]
        [SerializeField, Min(0f)] private float _tiltSpeed = 4f;

        [Tooltip("Furthest forward tilt, in degrees (negative).")]
        [SerializeField] private float _minTilt = -6f;

        [Tooltip("Furthest back tilt, in degrees.")]
        [SerializeField] private float _maxTilt = 10f;

        [Header("Side-shift")]
        [Tooltip("Side-shift speed at full lever, in m/s.")]
        [SerializeField, Min(0f)] private float _shiftSpeed = 0.1f;

        [Tooltip("Side-shift travel each way from center, in metres.")]
        [SerializeField, Min(0f)] private float _maxShift = 0.1f;

        private Vector3 _mastRestPosition;
        private Quaternion _mastRestRotation;
        private Vector3 _carriageRestPosition;
        private Vector3 _liftDirectionLocal;
        private Vector3 _shiftDirectionLocal;
        private float _liftInput;
        private float _tiltInput;
        private float _shiftInput;

        /// <summary>Carriage height relative to its modelled position, in metres.</summary>
        public float Lift { get; private set; }

        /// <summary>Mast tilt in degrees; positive is back towards the driver.</summary>
        public float Tilt { get; private set; }

        /// <summary>Carriage side-shift in metres; positive is to the driver's right.</summary>
        public float SideShift { get; private set; }

        /// <summary>Sets the control inputs, each from -1 to 1.</summary>
        /// <param name="lift">Positive lifts, negative lowers.</param>
        /// <param name="tilt">Positive tilts back, negative tilts forward.</param>
        /// <param name="sideShift">Positive shifts right, negative shifts left.</param>
        public void SetInput(float lift, float tilt, float sideShift)
        {
            _liftInput = lift;
            _tiltInput = tilt;
            _shiftInput = sideShift;
        }

        private void Awake()
        {
            if (_mast == null || _carriage == null || _carriage.parent != _mast)
            {
                Debug.LogError($"{nameof(MastController)} on {name} needs a mast and a carriage that is its child.", this);
                enabled = false;
                return;
            }

            _tiltAxis = _tiltAxis.normalized;
            _mastRestPosition = _mast.localPosition;
            _mastRestRotation = _mast.localRotation;
            _carriageRestPosition = _carriage.localPosition;

            // One metre up and right in the forklift's frame, expressed in the mast's local space (scale included).
            // Captured before any tilt, so the carriage then follows the mast as it tilts.
            var frame = _mast.parent;
            _liftDirectionLocal = _mast.InverseTransformVector(frame.up);
            _shiftDirectionLocal = _mast.InverseTransformVector(frame.right);
        }

        private void Update()
        {
            var deltaTime = Time.deltaTime;
            Lift = ForkliftMastMath.Integrate(Lift, _liftInput, _liftSpeed, deltaTime, _minLift, _maxLift);
            Tilt = ForkliftMastMath.Integrate(Tilt, _tiltInput, _tiltSpeed, deltaTime, _minTilt, _maxTilt);
            SideShift = ForkliftMastMath.Integrate(SideShift, _shiftInput, _shiftSpeed, deltaTime, -_maxShift, _maxShift);

            var tilt = Quaternion.AngleAxis(Tilt, _tiltAxis);
            _mast.localPosition = _tiltHinge + tilt * (_mastRestPosition - _tiltHinge);
            _mast.localRotation = tilt * _mastRestRotation;
            _carriage.localPosition = _carriageRestPosition + _liftDirectionLocal * Lift + _shiftDirectionLocal * SideShift;
        }
    }
}
