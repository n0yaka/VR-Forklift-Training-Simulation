using UnityEngine;

namespace VRConTrain.Training
{
    /// <summary>
    /// A guidance marker that hovers above a spot and points down at it. The visual bobs up and down and spins
    /// slowly so it catches the eye. The marker's own position is the lowest point the arrow tip reaches.
    /// </summary>
    public sealed class FloatingMarker : MonoBehaviour
    {
        [Tooltip("The child that bobs and spins. Its pivot should be at the arrow tip.")]
        [SerializeField] private Transform _visual;

        [Tooltip("How far the tip rises above the marker's position at the top of each bob, in metres.")]
        [SerializeField, Min(0f)] private float _bobHeight = 0.15f;

        [Tooltip("Time for one full bob, in seconds.")]
        [SerializeField, Min(0.1f)] private float _bobPeriod = 2f;

        [Tooltip("Spin speed around the vertical axis, in degrees per second.")]
        [SerializeField] private float _spinSpeed = 60f;

        private Vector3 _restPosition;
        private Quaternion _restRotation;

        private void Awake()
        {
            if (_visual == null)
            {
                Debug.LogError($"{nameof(FloatingMarker)} on {name} has no visual assigned.", this);
                enabled = false;
                return;
            }

            _restPosition = _visual.localPosition;
            _restRotation = _visual.localRotation;
        }

        private void Update()
        {
            var time = Time.time;
            var bob = (Mathf.Sin(time * 2f * Mathf.PI / _bobPeriod) + 1f) * 0.5f * _bobHeight;
            _visual.localPosition = _restPosition + Vector3.up * bob;
            _visual.localRotation = Quaternion.AngleAxis(time * _spinSpeed, Vector3.up) * _restRotation;
        }
    }
}
