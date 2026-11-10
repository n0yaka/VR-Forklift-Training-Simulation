using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using VRConTrain.Forklift;

namespace VRConTrain.Interaction
{
    /// <summary>
    /// Keeps the driver's head at the seat's eye point. Recenters once when headset tracking starts, and again
    /// whenever the driver presses the recenter control. <see cref="ForkliftBoarding"/> enables it only while the
    /// player is seated.
    /// </summary>
    public sealed class SeatedRigRecenter : MonoBehaviour
    {
        [SerializeField] private XROrigin _origin;

        [Tooltip("Where the driver's eyes should be; its forward is the direction they should face.")]
        [SerializeField] private Transform _eyePoint;

        [Tooltip("A component implementing IForkliftInputSource.")]
        [SerializeField] private MonoBehaviour _inputSource;

        [Tooltip("Recenter anyway after this long if no headset tracking arrives, for example in the Editor.")]
        [SerializeField, Min(0f)] private float _trackingTimeout = 1f;

        private IForkliftInputSource _input;

        private void Awake() => _input = _inputSource as IForkliftInputSource;

        private void OnEnable()
        {
            if (_input != null)
                _input.RecenterPressed += Recenter;
        }

        private void OnDisable()
        {
            if (_input != null)
                _input.RecenterPressed -= Recenter;
        }

        private IEnumerator Start()
        {
            var startPosition = _origin.Camera.transform.localPosition;
            var elapsed = 0f;

            while (_origin.Camera.transform.localPosition == startPosition && elapsed < _trackingTimeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Disabling the component doesn't stop this coroutine, so skip it if the driver got out meanwhile.
            if (enabled)
                Recenter();
        }

        /// <summary>Moves the rig so the camera sits at the eye point, facing the same way.</summary>
        public void Recenter()
        {
            _origin.MatchOriginUpCameraForward(_eyePoint.up, _eyePoint.forward);
            _origin.MoveCameraToWorldLocation(_eyePoint.position);
        }
    }
}
