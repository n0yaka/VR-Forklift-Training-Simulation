using TMPro;
using UnityEngine;

namespace VRConTrain.Forklift
{
    /// <summary>Shows the direction lever position (F, N or R) on the dashboard.</summary>
    [RequireComponent(typeof(TextMeshPro))]
    public sealed class ForkliftGearReadout : MonoBehaviour
    {
        [SerializeField] private ForkliftController _controller;

        private TextMeshPro _text;

        private void Awake() => _text = GetComponent<TextMeshPro>();

        private void OnEnable()
        {
            if (_controller == null)
                return;

            _controller.GearChanged += ShowGear;
            ShowGear(_controller.Gear);
        }

        private void OnDisable()
        {
            if (_controller != null)
                _controller.GearChanged -= ShowGear;
        }

        private void ShowGear(ForkliftGear gear)
        {
            switch (gear)
            {
                case ForkliftGear.Forward:
                    _text.text = "F";
                    break;
                case ForkliftGear.Reverse:
                    _text.text = "R";
                    break;
                default:
                    _text.text = "N";
                    break;
            }
        }
    }
}
