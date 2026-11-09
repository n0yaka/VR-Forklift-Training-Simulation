using UnityEngine;

namespace VRConTrain.Forklift
{
    /// <summary>
    /// Pure drive and steering calculations for the forklift. Holds no Unity object state, so it can be
    /// covered by EditMode tests.
    /// </summary>
    public static class ForkliftDriveMath
    {
        const float k_InputThreshold = 0.01f;
        const float k_OpposingSpeedThreshold = 0.05f;

        /// <summary>Moves the direction lever one step towards Forward, stopping at Forward.</summary>
        public static ForkliftGear ShiftUp(ForkliftGear gear) =>
            gear == ForkliftGear.Forward ? ForkliftGear.Forward : gear + 1;

        /// <summary>Moves the direction lever one step towards Reverse, stopping at Reverse.</summary>
        public static ForkliftGear ShiftDown(ForkliftGear gear) =>
            gear == ForkliftGear.Reverse ? ForkliftGear.Reverse : gear - 1;

        /// <summary>Limits a steering wheel angle in degrees to the steering lock.</summary>
        public static float ClampWheelAngle(float angleDegrees, float lockDegrees) =>
            Mathf.Clamp(angleDegrees, -lockDegrees, lockDegrees);

        /// <summary>Converts a steering wheel angle to a steer value from -1 (full left) to 1 (full right).</summary>
        public static float WheelAngleToSteer(float angleDegrees, float lockDegrees) =>
            lockDegrees <= 0f ? 0f : Mathf.Clamp(angleDegrees / lockDegrees, -1f, 1f);

        /// <summary>
        /// Converts a steer value to the rear wheel angle. The result has the opposite sign to the steer value:
        /// the rear wheels point left to turn the forklift right, which is what makes the tail swing out.
        /// </summary>
        public static float RearSteerAngle(float steer, float maxRearAngleDegrees) =>
            -Mathf.Clamp(steer, -1f, 1f) * maxRearAngleDegrees;

        /// <summary>
        /// Calculates the drive torque for one front wheel, fading to zero as the top speed for the gear is reached.
        /// </summary>
        /// <param name="accelerator">Accelerator input from 0 to 1.</param>
        /// <param name="gear">Current direction lever position.</param>
        /// <param name="forwardSpeed">Speed along the forklift's forward axis in m/s; negative when rolling backwards.</param>
        /// <param name="maxTorque">Torque at full accelerator in N·m.</param>
        /// <param name="topSpeed">Top speed for this gear in m/s.</param>
        /// <param name="taperBand">Speed range below the top speed over which torque fades out, in m/s.</param>
        public static float MotorTorque(float accelerator, ForkliftGear gear, float forwardSpeed, float maxTorque, float topSpeed, float taperBand)
        {
            if (gear == ForkliftGear.Neutral)
                return 0f;

            var sign = (float)(int)gear;
            var speedInGearDirection = forwardSpeed * sign;
            var headroom = taperBand <= 0f
                ? (speedInGearDirection < topSpeed ? 1f : 0f)
                : Mathf.Clamp01((topSpeed - speedInGearDirection) / taperBand);
            return sign * Mathf.Clamp01(accelerator) * maxTorque * headroom;
        }

        /// <summary>
        /// Calculates the brake torque for one wheel. Covers the brake input, engine braking when coasting, holding
        /// the forklift still when stopped, and slowing down when accelerating against the current direction of travel.
        /// </summary>
        /// <param name="accelerator">Accelerator input from 0 to 1.</param>
        /// <param name="brake">Brake input from 0 to 1.</param>
        /// <param name="gear">Current direction lever position.</param>
        /// <param name="forwardSpeed">Speed along the forklift's forward axis in m/s; negative when rolling backwards.</param>
        /// <param name="maxBrakeTorque">Torque at full brake in N·m.</param>
        /// <param name="coastTorque">Torque applied when coasting in N·m.</param>
        /// <param name="holdSpeed">Speed in m/s below which the forklift is held still when not accelerating.</param>
        /// <param name="holdTorque">Torque used to hold the forklift still in N·m.</param>
        public static float BrakeTorque(float accelerator, float brake, ForkliftGear gear, float forwardSpeed, float maxBrakeTorque, float coastTorque, float holdSpeed, float holdTorque)
        {
            var torque = Mathf.Clamp01(brake) * maxBrakeTorque;
            var driving = gear != ForkliftGear.Neutral && accelerator > k_InputThreshold;

            if (driving)
            {
                var speedInGearDirection = forwardSpeed * (int)gear;
                return speedInGearDirection < -k_OpposingSpeedThreshold
                    ? Mathf.Max(torque, Mathf.Clamp01(accelerator) * maxBrakeTorque)
                    : torque;
            }

            return Mathf.Max(torque, Mathf.Abs(forwardSpeed) < holdSpeed ? holdTorque : coastTorque);
        }
    }
}
