namespace VRConTrain.Forklift
{
    /// <summary>Position of the forklift's direction lever (F/N/R).</summary>
    public enum ForkliftGear
    {
        /// <summary>Drive backwards.</summary>
        Reverse = -1,

        /// <summary>No drive to the wheels.</summary>
        Neutral = 0,

        /// <summary>Drive forwards.</summary>
        Forward = 1
    }
}
