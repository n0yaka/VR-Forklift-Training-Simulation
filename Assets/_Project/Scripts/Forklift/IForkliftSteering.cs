namespace VRConTrain.Forklift
{
    /// <summary>A steering control that reports how far it is turned.</summary>
    public interface IForkliftSteering
    {
        /// <summary>Steer value from -1 (full left) to 1 (full right).</summary>
        float Steer { get; }

        /// <summary>Turns the control by an angle in degrees; positive turns right.</summary>
        void Turn(float degrees);
    }
}
