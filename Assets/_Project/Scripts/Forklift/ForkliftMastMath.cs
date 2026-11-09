using UnityEngine;

namespace VRConTrain.Forklift
{
    /// <summary>
    /// Pure calculations for the cab levers and the mast. Holds no Unity object state, so it can be covered by
    /// EditMode tests.
    /// </summary>
    public static class ForkliftMastMath
    {
        /// <summary>
        /// Converts a lever position to a control input. Positions inside the dead zone give zero, and the rest of
        /// the travel is rescaled so the input still reaches ±1 at the end stop.
        /// </summary>
        /// <param name="leverPosition">Lever position from -1 (fully pushed) to 1 (fully pulled).</param>
        /// <param name="deadZone">Fraction of the travel each side of center that is ignored, from 0 to just under 1.</param>
        public static float LeverToInput(float leverPosition, float deadZone)
        {
            var position = Mathf.Clamp(leverPosition, -1f, 1f);
            var magnitude = Mathf.Abs(position);
            deadZone = Mathf.Clamp(deadZone, 0f, 0.99f);

            if (magnitude <= deadZone)
                return 0f;

            return Mathf.Sign(position) * (magnitude - deadZone) / (1f - deadZone);
        }

        /// <summary>
        /// Moves a mast value (height, tilt or side-shift) at a speed set by the input, and keeps it within its limits.
        /// </summary>
        /// <param name="current">Current value.</param>
        /// <param name="input">Control input from -1 to 1.</param>
        /// <param name="maxRate">Change per second at full input.</param>
        /// <param name="deltaTime">Time step in seconds.</param>
        /// <param name="min">Lowest allowed value.</param>
        /// <param name="max">Highest allowed value.</param>
        public static float Integrate(float current, float input, float maxRate, float deltaTime, float min, float max) =>
            Mathf.Clamp(current + Mathf.Clamp(input, -1f, 1f) * maxRate * deltaTime, min, max);
    }
}
