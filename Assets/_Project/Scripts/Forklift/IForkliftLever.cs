namespace VRConTrain.Forklift
{
    /// <summary>A cab lever that reports how far it is moved from its center position.</summary>
    public interface IForkliftLever
    {
        /// <summary>Control input from -1 to 1, with the lever's dead zone already removed.</summary>
        float Value { get; }
    }
}
