using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VRConTrain.Forklift
{
    /// <summary>
    /// Reads driver input from the Forklift action map in ForkliftControls.inputactions. The map binds both the VR
    /// controllers and the keyboard, so this one source also covers testing in the Editor.
    /// </summary>
    public sealed class ControllerButtonInput : MonoBehaviour, IForkliftInputSource
    {
        const string k_MapName = "Forklift";

        [SerializeField] private InputActionAsset _controls;

        private InputActionMap _map;
        private InputAction _accelerate;
        private InputAction _brake;
        private InputAction _shiftUp;
        private InputAction _shiftDown;
        private InputAction _recenter;
        private InputAction _debugSteer;

        /// <inheritdoc />
        public float Accelerator => _accelerate != null ? _accelerate.ReadValue<float>() : 0f;

        /// <inheritdoc />
        public float Brake => _brake != null ? _brake.ReadValue<float>() : 0f;

        /// <inheritdoc />
        public float DebugSteer => _debugSteer != null ? _debugSteer.ReadValue<float>() : 0f;

        /// <inheritdoc />
        public event Action ShiftUpPressed;

        /// <inheritdoc />
        public event Action ShiftDownPressed;

        /// <inheritdoc />
        public event Action RecenterPressed;

        private void OnEnable()
        {
            if (_controls == null)
            {
                Debug.LogError($"{nameof(ControllerButtonInput)} on {name} has no input actions assigned.", this);
                return;
            }

            _map = _controls.FindActionMap(k_MapName, true);
            _accelerate = _map.FindAction("Accelerate", true);
            _brake = _map.FindAction("Brake", true);
            _shiftUp = _map.FindAction("ShiftUp", true);
            _shiftDown = _map.FindAction("ShiftDown", true);
            _recenter = _map.FindAction("Recenter", true);
            _debugSteer = _map.FindAction("DebugSteer", true);

            _shiftUp.performed += OnShiftUp;
            _shiftDown.performed += OnShiftDown;
            _recenter.performed += OnRecenter;
            _map.Enable();
        }

        private void OnDisable()
        {
            if (_map == null)
                return;

            _shiftUp.performed -= OnShiftUp;
            _shiftDown.performed -= OnShiftDown;
            _recenter.performed -= OnRecenter;
            _map.Disable();
        }

        private void OnShiftUp(InputAction.CallbackContext context) => ShiftUpPressed?.Invoke();

        private void OnShiftDown(InputAction.CallbackContext context) => ShiftDownPressed?.Invoke();

        private void OnRecenter(InputAction.CallbackContext context) => RecenterPressed?.Invoke();
    }
}
