using NUnit.Framework;
using VRConTrain.Forklift;

namespace VRConTrain.Tests
{
    /// <summary>Tests the pure lever and mast calculations.</summary>
    public class ForkliftMastMathTests
    {
        const float k_Tolerance = 1e-5f;

        [TestCase(0f)]
        [TestCase(0.05f)]
        [TestCase(-0.1f)]
        public void LeverToInput_InsideDeadZone_IsZero(float position) =>
            Assert.AreEqual(0f, ForkliftMastMath.LeverToInput(position, 0.1f), k_Tolerance);

        [TestCase(1f, 1f)]
        [TestCase(-1f, -1f)]
        [TestCase(0.55f, 0.5f)]
        [TestCase(-0.55f, -0.5f)]
        public void LeverToInput_OutsideDeadZone_RescalesToFullRange(float position, float expected) =>
            Assert.AreEqual(expected, ForkliftMastMath.LeverToInput(position, 0.1f), k_Tolerance);

        [Test]
        public void LeverToInput_PastEndStop_IsClampedToOne() =>
            Assert.AreEqual(1f, ForkliftMastMath.LeverToInput(1.5f, 0.1f), k_Tolerance);

        [Test]
        public void LeverToInput_NoDeadZone_PassesThrough() =>
            Assert.AreEqual(0.3f, ForkliftMastMath.LeverToInput(0.3f, 0f), k_Tolerance);

        [Test]
        public void Integrate_FullInput_MovesAtMaxRate() =>
            Assert.AreEqual(0.35f, ForkliftMastMath.Integrate(0f, 1f, 0.35f, 1f, -1f, 1f), k_Tolerance);

        [Test]
        public void Integrate_HalfInputBackwards_MovesAtHalfRate() =>
            Assert.AreEqual(-0.1f, ForkliftMastMath.Integrate(0f, -0.5f, 0.2f, 1f, -1f, 1f), k_Tolerance);

        [Test]
        public void Integrate_StopsAtMaximum() =>
            Assert.AreEqual(0.9f, ForkliftMastMath.Integrate(0.85f, 1f, 0.35f, 1f, -0.55f, 0.9f), k_Tolerance);

        [Test]
        public void Integrate_StopsAtMinimum() =>
            Assert.AreEqual(-6f, ForkliftMastMath.Integrate(-5f, -1f, 4f, 1f, -6f, 10f), k_Tolerance);

        [Test]
        public void Integrate_ZeroInput_HoldsPosition() =>
            Assert.AreEqual(0.4f, ForkliftMastMath.Integrate(0.4f, 0f, 0.35f, 1f, -0.55f, 0.9f), k_Tolerance);

        [Test]
        public void Integrate_InputAboveOne_IsClamped() =>
            Assert.AreEqual(0.35f, ForkliftMastMath.Integrate(0f, 3f, 0.35f, 1f, -1f, 1f), k_Tolerance);
    }
}
