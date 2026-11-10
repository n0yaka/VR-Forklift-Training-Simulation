using NUnit.Framework;
using UnityEngine;
using VRConTrain.Interaction;

namespace VRConTrain.Tests
{
    public sealed class LocomotionMathTests
    {
        const float k_Tolerance = 1e-4f;

        [Test]
        public void ArcPoint_AtTimeZero_IsStart()
        {
            var start = new Vector3(1f, 2f, 3f);
            var point = LocomotionMath.ArcPoint(start, new Vector3(0f, 0f, 7f), 9.81f, 0f);
            Assert.That(Vector3.Distance(point, start), Is.LessThan(k_Tolerance));
        }

        [Test]
        public void ArcPoint_Horizontal_FallsUnderGravity()
        {
            // After 1 s at 5 m/s forward: 5 m along, and 0.5 * 9.81 m down.
            var point = LocomotionMath.ArcPoint(Vector3.zero, new Vector3(0f, 0f, 5f), 9.81f, 1f);
            Assert.That(point.z, Is.EqualTo(5f).Within(k_Tolerance));
            Assert.That(point.y, Is.EqualTo(-4.905f).Within(k_Tolerance));
            Assert.That(point.x, Is.EqualTo(0f).Within(k_Tolerance));
        }

        [Test]
        public void ArcPoint_Upward_PeaksThenFalls()
        {
            var velocity = new Vector3(0f, 4.905f, 1f);
            var peak = LocomotionMath.ArcPoint(Vector3.zero, velocity, 9.81f, 0.5f);
            var landing = LocomotionMath.ArcPoint(Vector3.zero, velocity, 9.81f, 1f);
            Assert.That(peak.y, Is.EqualTo(1.22625f).Within(k_Tolerance));
            Assert.That(landing.y, Is.EqualTo(0f).Within(k_Tolerance));
        }

        [TestCase(0f, true)]
        [TestCase(29f, true)]
        [TestCase(31f, false)]
        [TestCase(90f, false)]
        public void IsWalkable_ComparesSlopeToLimit(float slopeDegrees, bool expected)
        {
            var normal = Quaternion.AngleAxis(slopeDegrees, Vector3.right) * Vector3.up;
            Assert.That(LocomotionMath.IsWalkable(normal, 30f), Is.EqualTo(expected));
        }

        [Test]
        public void IsWalkable_Ceiling_IsFalse()
        {
            Assert.That(LocomotionMath.IsWalkable(Vector3.down, 30f), Is.False);
        }

        [TestCase(0.0f, true)]
        [TestCase(0.4f, true)]
        [TestCase(-0.4f, true)]
        [TestCase(0.6f, false)]
        [TestCase(-0.6f, false)]
        [TestCase(1.5f, false)]
        public void IsWithinStep_AllowsSmallHeightChangesOnly(float heightAboveFeet, bool expected)
        {
            Assert.That(LocomotionMath.IsWithinStep(10f + heightAboveFeet, 10f, 0.5f), Is.EqualTo(expected));
        }

        [Test]
        public void HorizontalDistance_IgnoresHeight()
        {
            var distance = LocomotionMath.HorizontalDistance(new Vector3(0f, 5f, 0f), new Vector3(3f, -2f, 4f));
            Assert.That(distance, Is.EqualTo(5f).Within(k_Tolerance));
        }

        [TestCase(0f, true)]
        [TestCase(0.3f, true)]
        [TestCase(-0.3f, true)]
        [TestCase(0.5f, false)]
        [TestCase(-1.4f, false)]
        public void IsStoppedForExit_AllowsCrawlInEitherDirection(float forwardSpeed, bool expected)
        {
            Assert.That(LocomotionMath.IsStoppedForExit(forwardSpeed, 0.3f), Is.EqualTo(expected));
        }
    }
}
