using System;

namespace VRConTrain.Forklift
{
    /// <summary>Supplies driver input for accelerating, braking and changing direction.</summary>
    public interface IForkliftInputSource
    {
        /// <summary>Accelerator input from 0 to 1.</summary>
        float Accelerator { get; }

        /// <summary>Brake input from 0 to 1.</summary>
        float Brake { get; }

        /// <summary>Keyboard steering for Editor testing, from -1 to 1. Zero on the headset.</summary>
        float DebugSteer { get; }

        /// <summary>Raised when the driver shifts towards Forward.</summary>
        event Action ShiftUpPressed;

        /// <summary>Raised when the driver shifts towards Reverse.</summary>
        event Action ShiftDownPressed;

        /// <summary>Raised when the driver asks to recenter their view on the seat.</summary>
        event Action RecenterPressed;
    }
}
