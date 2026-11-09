using NUnit.Framework;
using VRConTrain.Forklift;

namespace VRConTrain.Tests
{
    /// <summary>Tests the forklift's pure drive and steering calculations.</summary>
    public class ForkliftDriveMathTests
    {
        const float k_MaxTorque = 600f;
        const float k_TopSpeed = 2f;
        const float k_Taper = 0.5f;
        const float k_MaxBrake = 1000f;
        const float k_Coast = 100f;
        const float k_HoldSpeed = 0.1f;
        const float k_Hold = 1500f;

        [TestCase(ForkliftGear.Reverse, ForkliftGear.Neutral)]
        [TestCase(ForkliftGear.Neutral, ForkliftGear.Forward)]
        [TestCase(ForkliftGear.Forward, ForkliftGear.Forward)]
        public void ShiftUp_MovesOneStepAndStopsAtForward(ForkliftGear from, ForkliftGear expected) =>
            Assert.AreEqual(expected, ForkliftDriveMath.ShiftUp(from));

        [TestCase(ForkliftGear.Forward, ForkliftGear.Neutral)]
        [TestCase(ForkliftGear.Neutral, ForkliftGear.Reverse)]
        [TestCase(ForkliftGear.Reverse, ForkliftGear.Reverse)]
        public void ShiftDown_MovesOneStepAndStopsAtReverse(ForkliftGear from, ForkliftGear expected) =>
            Assert.AreEqual(expected, ForkliftDriveMath.ShiftDown(from));

        [TestCase(180f, 0.5f)]
        [TestCase(-360f, -1f)]
        [TestCase(720f, 1f)]
        public void WheelAngleToSteer_ScalesAndClamps(float angle, float expected) =>
            Assert.AreEqual(expected, ForkliftDriveMath.WheelAngleToSteer(angle, 360f), 0.0001f);

        [Test]
        public void ClampWheelAngle_StopsAtTheLock()
        {
            Assert.AreEqual(360f, ForkliftDriveMath.ClampWheelAngle(500f, 360f));
            Assert.AreEqual(-360f, ForkliftDriveMath.ClampWheelAngle(-500f, 360f));
        }

        [Test]
        public void RearSteerAngle_PointsOppositeToTheTurn()
        {
            Assert.AreEqual(-60f, ForkliftDriveMath.RearSteerAngle(1f, 60f), 0.0001f);
            Assert.AreEqual(30f, ForkliftDriveMath.RearSteerAngle(-0.5f, 60f), 0.0001f);
        }

        [Test]
        public void MotorTorque_IsZeroInNeutral() =>
            Assert.AreEqual(0f, ForkliftDriveMath.MotorTorque(1f, ForkliftGear.Neutral, 0f, k_MaxTorque, k_TopSpeed, k_Taper));

        [Test]
        public void MotorTorque_SignFollowsTheGear()
        {
            Assert.AreEqual(k_MaxTorque, ForkliftDriveMath.MotorTorque(1f, ForkliftGear.Forward, 0f, k_MaxTorque, k_TopSpeed, k_Taper), 0.0001f);
            Assert.AreEqual(-k_MaxTorque, ForkliftDriveMath.MotorTorque(1f, ForkliftGear.Reverse, 0f, k_MaxTorque, k_TopSpeed, k_Taper), 0.0001f);
        }

        [Test]
        public void MotorTorque_FadesOutAtTopSpeed()
        {
            Assert.AreEqual(k_MaxTorque * 0.5f, ForkliftDriveMath.MotorTorque(1f, ForkliftGear.Forward, 1.75f, k_MaxTorque, k_TopSpeed, k_Taper), 0.0001f);
            Assert.AreEqual(0f, ForkliftDriveMath.MotorTorque(1f, ForkliftGear.Forward, 3f, k_MaxTorque, k_TopSpeed, k_Taper), 0.0001f);
            Assert.AreEqual(0f, ForkliftDriveMath.MotorTorque(1f, ForkliftGear.Reverse, -3f, k_MaxTorque, k_TopSpeed, k_Taper), 0.0001f);
        }

        [Test]
        public void BrakeTorque_FreeWhenDrivingInGearDirection() =>
            Assert.AreEqual(0f, Brake(accelerator: 1f, brake: 0f, ForkliftGear.Forward, forwardSpeed: 1f));

        [Test]
        public void BrakeTorque_FollowsTheBrakeInput() =>
            Assert.AreEqual(k_MaxBrake * 0.5f, Brake(accelerator: 0f, brake: 0.5f, ForkliftGear.Forward, forwardSpeed: 1f), 0.0001f);

        [Test]
        public void BrakeTorque_CoastsWhenMovingWithNoInput() =>
            Assert.AreEqual(k_Coast, Brake(accelerator: 0f, brake: 0f, ForkliftGear.Forward, forwardSpeed: 1f));

        [Test]
        public void BrakeTorque_HoldsWhenStoppedWithNoInput() =>
            Assert.AreEqual(k_Hold, Brake(accelerator: 0f, brake: 0f, ForkliftGear.Forward, forwardSpeed: 0f));

        [Test]
        public void BrakeTorque_SlowsDownWhenAcceleratingAgainstTravel() =>
            Assert.AreEqual(k_MaxBrake, Brake(accelerator: 1f, brake: 0f, ForkliftGear.Reverse, forwardSpeed: 1f));

        [Test]
        public void BrakeTorque_AcceleratorInNeutralDoesNotReleaseTheHold() =>
            Assert.AreEqual(k_Hold, Brake(accelerator: 1f, brake: 0f, ForkliftGear.Neutral, forwardSpeed: 0f));

        static float Brake(float accelerator, float brake, ForkliftGear gear, float forwardSpeed) =>
            ForkliftDriveMath.BrakeTorque(accelerator, brake, gear, forwardSpeed, k_MaxBrake, k_Coast, k_HoldSpeed, k_Hold);
    }
}
