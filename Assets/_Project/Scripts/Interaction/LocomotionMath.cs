using UnityEngine;

namespace VRConTrain.Interaction
{
    /// <summary>
    /// Pure calculations for moving around on foot: the teleport arc, which surfaces can be landed on, and the
    /// distance checks used for getting into the forklift.
    /// </summary>
    public static class LocomotionMath
    {
        /// <summary>
        /// A point on a thrown arc: where something launched from <paramref name="start"/> at
        /// <paramref name="velocity"/> is after <paramref name="time"/> seconds, with gravity pulling straight down.
        /// </summary>
        public static Vector3 ArcPoint(Vector3 start, Vector3 velocity, float gravity, float time)
        {
            return start + velocity * time + Vector3.down * (0.5f * gravity * time * time);
        }

        /// <summary>True when a surface with this normal is flat enough to stand on.</summary>
        public static bool IsWalkable(Vector3 surfaceNormal, float maxSlopeDegrees)
        {
            return Vector3.Angle(surfaceNormal, Vector3.up) <= maxSlopeDegrees;
        }

        /// <summary>
        /// True when a surface is within <paramref name="maxStep"/> metres above or below the player's feet, so the
        /// player can't teleport onto shelves, the forklift roof or down a drop.
        /// </summary>
        public static bool IsWithinStep(float surfaceHeight, float feetHeight, float maxStep)
        {
            return Mathf.Abs(surfaceHeight - feetHeight) <= maxStep;
        }

        /// <summary>Distance between two points ignoring height.</summary>
        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>True when the forklift is slow enough to get out of safely.</summary>
        public static bool IsStoppedForExit(float forwardSpeed, float maxExitSpeed)
        {
            return Mathf.Abs(forwardSpeed) <= maxExitSpeed;
        }
    }
}
