using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VRConTrain.Forklift;

namespace VRConTrain.Interaction
{
    /// <summary>
    /// A steering wheel that one or both hands can grab and turn. The wheel follows each hand's movement around its
    /// axis, so it never jumps when grabbed, and it can turn more than a half rotation up to the steering lock.
    /// </summary>
    public sealed class SteeringWheelInteractable : XRBaseInteractable, IForkliftSteering
    {
        [Tooltip("The wheel's rotation axis in the parent's space, pointing from the wheel towards the driver.")]
        [SerializeField] private Vector3 _axis = Vector3.back;

        [Tooltip("How far the wheel turns each way from center, in degrees.")]
        [SerializeField, Min(1f)] private float _lockDegrees = 360f;

        [Tooltip("Radius of the rim's centerline, in metres. Used to tell a hand on the rim from one on a nearby control.")]
        [SerializeField, Min(0.01f)] private float _rimRadius = 0.21f;

        [Tooltip("A hand closer to the axis than this is ignored, because its angle would jump around.")]
        [SerializeField, Min(0f)] private float _minHandRadius = 0.03f;

        private readonly Dictionary<IXRSelectInteractor, Vector3> _lastHandDirections = new Dictionary<IXRSelectInteractor, Vector3>();
        private Quaternion _restRotation;
        private float _angle;

        /// <inheritdoc />
        public float Steer => ForkliftDriveMath.WheelAngleToSteer(_angle, _lockDegrees);

        /// <inheritdoc />
        public void Turn(float degrees)
        {
            _angle = ForkliftDriveMath.ClampWheelAngle(_angle + degrees, _lockDegrees);
            transform.localRotation = Quaternion.AngleAxis(_angle, _axis) * _restRotation;
        }

        /// <inheritdoc />
        protected override void Awake()
        {
            // XRI skips trigger colliders when it collects them itself, and the wheel's grab volume is a trigger so
            // that it can't collide with anything. Registering it here keeps the wheel grabbable.
            if (colliders.Count == 0 && TryGetComponent<Collider>(out var grabCollider))
                colliders.Add(grabCollider);

            base.Awake();
            selectMode = InteractableSelectMode.Multiple;
            _axis = _axis.normalized;
            _restRotation = transform.localRotation;
        }

        /// <summary>
        /// Measures the distance to the nearest point on the rim, so a hand on the rim picks the wheel and a hand on a
        /// lever next to the wheel picks the lever, even though both are inside the wheel's grab volume.
        /// </summary>
        public override DistanceInfo GetDistance(Vector3 position)
        {
            var center = transform.position;
            var axis = transform.parent.TransformDirection(_axis);
            var radial = Vector3.ProjectOnPlane(position - center, axis);
            if (radial.sqrMagnitude < 1e-6f)
                radial = transform.parent.up;

            var rimPoint = center + radial.normalized * _rimRadius;
            return new DistanceInfo { point = rimPoint, distanceSqr = (rimPoint - position).sqrMagnitude };
        }

        /// <inheritdoc />
        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            TryGetHandDirection(args.interactorObject, out var direction);
            _lastHandDirections[args.interactorObject] = direction;
        }

        /// <inheritdoc />
        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            _lastHandDirections.Remove(args.interactorObject);
        }

        /// <inheritdoc />
        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);

            if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic || !isSelected)
                return;

            var totalDelta = 0f;
            var handCount = 0;
            for (var i = 0; i < interactorsSelecting.Count; i++)
            {
                var interactor = interactorsSelecting[i];
                if (!TryGetHandDirection(interactor, out var direction))
                    continue;

                if (_lastHandDirections.TryGetValue(interactor, out var lastDirection) && lastDirection != Vector3.zero)
                {
                    totalDelta += Vector3.SignedAngle(lastDirection, direction, _axis);
                    handCount++;
                }

                _lastHandDirections[interactor] = direction;
            }

            if (handCount > 0)
                Turn(totalDelta / handCount);
        }

        /// <summary>
        /// Gets the hand's direction from the wheel's center, flattened onto the wheel's plane, in the parent's space.
        /// Using the parent's space keeps the result independent of how the forklift is moving and of the wheel's
        /// own rotation.
        /// </summary>
        private bool TryGetHandDirection(IXRSelectInteractor interactor, out Vector3 direction)
        {
            var parent = transform.parent;
            var handPosition = interactor.GetAttachTransform(this).position;
            var offset = parent.InverseTransformPoint(handPosition) - transform.localPosition;
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
