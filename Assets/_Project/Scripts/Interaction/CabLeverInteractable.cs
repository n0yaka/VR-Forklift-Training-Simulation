using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VRConTrain.Forklift;

namespace VRConTrain.Interaction
{
    /// <summary>
    /// A cab lever that a hand grabs and pulls or pushes around its hinge. Like a hydraulic valve lever, it springs
    /// back to center when released. The lever follows the hand's movement, so it never jumps when grabbed, and the
    /// model's pivot doesn't need to be at the hinge.
    /// </summary>
    public sealed class CabLeverInteractable : XRBaseInteractable, IForkliftLever
    {
        [Tooltip("The hinge point in the parent's space.")]
        [SerializeField] private Vector3 _hinge;

        [Tooltip("The hinge axis in the parent's space. A positive angle pulls the lever towards the driver.")]
        [SerializeField] private Vector3 _axis = Vector3.left;

        [Tooltip("How far the lever moves each way from center, in degrees.")]
        [SerializeField, Min(1f)] private float _maxAngle = 15f;

        [Tooltip("Fraction of the travel each side of center that does nothing.")]
        [SerializeField, Range(0f, 0.9f)] private float _deadZone = 0.1f;

        [Tooltip("How fast the lever springs back to center when released, in degrees per second.")]
        [SerializeField, Min(0f)] private float _returnSpeed = 120f;

        [Tooltip("Flips the output so that pulling gives a negative value.")]
        [SerializeField] private bool _invertValue;

        [Tooltip("A hand closer to the hinge axis than this is ignored, because its angle would jump around.")]
        [SerializeField, Min(0f)] private float _minHandRadius = 0.05f;

        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private Vector3 _lastHandDirection;
        private bool _hasLastHandDirection;
        private float _angle;

        /// <summary>Lever angle in degrees; positive is pulled towards the driver.</summary>
        public float Angle => _angle;

        /// <inheritdoc />
        public float Value
        {
            get
            {
                var value = ForkliftMastMath.LeverToInput(_angle / _maxAngle, _deadZone);
                return _invertValue ? -value : value;
            }
        }

        /// <inheritdoc />
        protected override void Awake()
        {
            // XRI skips trigger colliders when it collects them itself, and the grab volume is a trigger so that it
            // can't collide with anything. Registering it here keeps the lever grabbable.
            if (colliders.Count == 0 && TryGetComponent<Collider>(out var grabCollider))
                colliders.Add(grabCollider);

            base.Awake();
            _axis = _axis.normalized;
            _restPosition = transform.localPosition;
            _restRotation = transform.localRotation;
        }

        /// <summary>
        /// Measures the distance to the knob. The model's pivot is not on the lever, so XRI's default measurement
        /// would rank all the levers the same and let a neighbouring lever or the steering wheel take the grab.
        /// </summary>
        public override DistanceInfo GetDistance(Vector3 position)
        {
            var knob = colliders.Count > 0 && colliders[0] is SphereCollider sphere
                ? sphere.transform.TransformPoint(sphere.center)
                : transform.position;

            return new DistanceInfo { point = knob, distanceSqr = (knob - position).sqrMagnitude };
        }

        /// <inheritdoc />
        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            _hasLastHandDirection = TryGetHandDirection(args.interactorObject, out _lastHandDirection);
        }

        /// <inheritdoc />
        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);

            if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic)
                return;

            if (isSelected)
            {
                if (TryGetHandDirection(firstInteractorSelecting, out var direction))
                {
                    if (_hasLastHandDirection)
                        SetAngle(_angle + Vector3.SignedAngle(_lastHandDirection, direction, _axis));

                    _lastHandDirection = direction;
                    _hasLastHandDirection = true;
                }
            }
            else if (_angle != 0f)
            {
                SetAngle(Mathf.MoveTowards(_angle, 0f, _returnSpeed * Time.deltaTime));
            }
        }

        private void SetAngle(float angle)
        {
            _angle = Mathf.Clamp(angle, -_maxAngle, _maxAngle);
            var rotation = Quaternion.AngleAxis(_angle, _axis);
            transform.localPosition = _hinge + rotation * (_restPosition - _hinge);
            transform.localRotation = rotation * _restRotation;
        }

        /// <summary>
        /// Gets the hand's direction from the hinge, flattened onto the plane the lever swings in, in the parent's
        /// space. Using the parent's space keeps the result independent of how the forklift is moving.
        /// </summary>
        private bool TryGetHandDirection(IXRSelectInteractor interactor, out Vector3 direction)
        {
            var handPosition = interactor.GetAttachTransform(this).position;
            var offset = transform.parent.InverseTransformPoint(handPosition) - _hinge;
            direction = Vector3.ProjectOnPlane(offset, _axis);

            if (direction.magnitude < _minHandRadius)
            {
                direction = Vector3.zero;
                return false;
            }

            return true;
        }
    }
}
