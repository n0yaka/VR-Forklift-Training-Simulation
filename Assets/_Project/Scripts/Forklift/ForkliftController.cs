using System;
using UnityEngine;

namespace VRConTrain.Forklift
{
    /// <summary>
    /// Drives the forklift with WheelColliders: the front wheels are driven and always point straight, and the rear
    /// wheels do all the steering, so the tail swings out in a turn.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ForkliftController : MonoBehaviour
    {
        const float k_RpmToDegreesPerSecond = 6f;

        [Header("Wheel colliders")]
        [SerializeField] private WheelCollider _frontLeft;
        [SerializeField] private WheelCollider _frontRight;
        [SerializeField] private WheelCollider _rearLeft;
        [SerializeField] private WheelCollider _rearRight;

        [Header("Wheel visuals")]
        [Tooltip("The single mesh holding both front wheels.")]
        [SerializeField] private Transform _frontWheelsVisual;
        [SerializeField] private Transform _rearLeftVisual;
        [SerializeField] private Transform _rearRightVisual;

        [Header("Speed")]
        [SerializeField, Min(0.1f)] private float _forwardTopSpeedKmh = 8f;
        [SerializeField, Min(0.1f)] private float _reverseTopSpeedKmh = 5f;
        [SerializeField, Min(0f)] private float _speedTaperBand = 0.5f;

        [Header("Torque per wheel (N·m)")]
        [SerializeField, Min(0f)] private float _motorTorque = 650f;
        [SerializeField, Min(0f)] private float _brakeTorque = 1300f;
        [SerializeField, Min(0f)] private float _coastTorque = 150f;
        [SerializeField, Min(0f)] private float _holdTorque = 1500f;
        [SerializeField, Min(0f)] private float _holdSpeed = 0.1f;

        [Header("Steering")]
        [SerializeField, Range(1f, 80f)] private float _maxRearSteerAngle = 60f;
        [SerializeField, Min(1f)] private float _rearSteerRate = 120f;

        [Header("Stability")]
        [SerializeField] private Vector3 _centerOfMass = new Vector3(0f, 0.5f, -0.5f);

        private Rigidbody _rigidbody;
        private Quaternion _frontWheelsRestRotation;
        private Quaternion _rearLeftRestRotation;
        private Quaternion _rearRightRestRotation;
        private float _frontSpin;
        private float _rearLeftSpin;
        private float _rearRightSpin;
        private float _accelerator;
        private float _brake;
        private float _steer;

        /// <summary>Current direction lever position.</summary>
        public ForkliftGear Gear { get; private set; } = ForkliftGear.Forward;

        /// <summary>Speed along the forklift's forward axis in m/s; negative when rolling backwards.</summary>
        public float ForwardSpeed { get; private set; }

        /// <summary>Current angle of the rear wheels in degrees.</summary>
        public float RearSteerAngle { get; private set; }

        /// <summary>Raised when the direction lever position changes.</summary>
        public event Action<ForkliftGear> GearChanged;

        /// <summary>Sets the driver input applied on the next physics step.</summary>
        /// <param name="accelerator">Accelerator input from 0 to 1.</param>
        /// <param name="brake">Brake input from 0 to 1.</param>
        /// <param name="steer">Steer value from -1 (full left) to 1 (full right).</param>
        public void SetDriverInput(float accelerator, float brake, float steer)
        {
            _accelerator = accelerator;
            _brake = brake;
            _steer = steer;
        }

        /// <summary>Moves the direction lever one step towards Forward.</summary>
        public void ShiftUp() => SetGear(ForkliftDriveMath.ShiftUp(Gear));

        /// <summary>Moves the direction lever one step towards Reverse.</summary>
        public void ShiftDown() => SetGear(ForkliftDriveMath.ShiftDown(Gear));

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.centerOfMass = _centerOfMass;
            _frontLeft.ConfigureVehicleSubsteps(5f, 12, 15);

            _frontWheelsRestRotation = _frontWheelsVisual.localRotation;
            _rearLeftRestRotation = _rearLeftVisual.localRotation;
            _rearRightRestRotation = _rearRightVisual.localRotation;
        }

        private void FixedUpdate()
        {
            ForwardSpeed = Vector3.Dot(_rigidbody.linearVelocity, transform.forward);

            var topSpeed = (Gear == ForkliftGear.Reverse ? _reverseTopSpeedKmh : _forwardTopSpeedKmh) / 3.6f;
            var motor = ForkliftDriveMath.MotorTorque(_accelerator, Gear, ForwardSpeed, _motorTorque, topSpeed, _speedTaperBand);
            var brake = ForkliftDriveMath.BrakeTorque(_accelerator, _brake, Gear, ForwardSpeed, _brakeTorque, _coastTorque, _holdSpeed, _holdTorque);

            _frontLeft.motorTorque = motor;
            _frontRight.motorTorque = motor;
            _frontLeft.brakeTorque = brake;
            _frontRight.brakeTorque = brake;
            _rearLeft.brakeTorque = brake;
            _rearRight.brakeTorque = brake;

            var targetAngle = ForkliftDriveMath.RearSteerAngle(_steer, _maxRearSteerAngle);
            RearSteerAngle = Mathf.MoveTowards(RearSteerAngle, targetAngle, _rearSteerRate * Time.fixedDeltaTime);
            _rearLeft.steerAngle = RearSteerAngle;
            _rearRight.steerAngle = RearSteerAngle;
        }

        private void LateUpdate()
        {
            var degreesPerRpm = k_RpmToDegreesPerSecond * Time.deltaTime;
            _frontSpin = Mathf.Repeat(_frontSpin + (_frontLeft.rpm + _frontRight.rpm) * 0.5f * degreesPerRpm, 360f);
            _rearLeftSpin = Mathf.Repeat(_rearLeftSpin + _rearLeft.rpm * degreesPerRpm, 360f);
            _rearRightSpin = Mathf.Repeat(_rearRightSpin + _rearRight.rpm * degreesPerRpm, 360f);

            _frontWheelsVisual.localRotation = _frontWheelsRestRotation * Quaternion.AngleAxis(_frontSpin, Vector3.right);

            var steerRotation = Quaternion.AngleAxis(RearSteerAngle, Vector3.up);
            _rearLeftVisual.localRotation = steerRotation * _rearLeftRestRotation * Quaternion.AngleAxis(_rearLeftSpin, Vector3.right);
            _rearRightVisual.localRotation = steerRotation * _rearRightRestRotation * Quaternion.AngleAxis(_rearRightSpin, Vector3.right);
        }

        private void SetGear(ForkliftGear gear)
        {
            if (gear == Gear)
                return;

            Gear = gear;
            GearChanged?.Invoke(gear);
        }
    }
}
