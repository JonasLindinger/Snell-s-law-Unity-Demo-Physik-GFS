using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace _Project.Scripts
{
    public class SimulationUI : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private CircleController controller;
        [SerializeField] private Light singleLight;

        [Header("UI Elements")]
        [SerializeField] private Toggle rainbowToggle;
        [SerializeField] private Toggle animateToggle;
        [SerializeField] private Slider hdrSlider;
        [SerializeField] private Slider bounceSlider;
        [SerializeField] private Slider wavelengthSlider;
        [SerializeField] private TextMeshProUGUI wavelengthText;

        private void Start()
        {
            // Auto-find references if not set
            if (!controller) controller = FindFirstObjectByType<CircleController>();
            if (!singleLight) singleLight = FindFirstObjectByType<Light>();

            // Cleanup: Ensure only one light exists
            Light[] allLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in allLights)
            {
                if (l != singleLight) 
                {
                    // Safety check: Don't destroy the object if it has other important components
                    Component[] comps = l.GetComponents<Component>();
                    if (comps.Length <= 2) // Transform + Light
                        Destroy(l.gameObject);
                    else
                        Destroy(l); // Only remove the component
                }
            }

            // Setup UI Events with Null Checks
            if (controller != null)
            {
                if (rainbowToggle) rainbowToggle.onValueChanged.AddListener(controller.SetRainbowMode);
                if (animateToggle) animateToggle.onValueChanged.AddListener(controller.SetAutoAnimate);
                if (hdrSlider) hdrSlider.onValueChanged.AddListener(controller.SetHdrExposure);
                if (bounceSlider) bounceSlider.onValueChanged.AddListener(controller.SetBounces);
                if (wavelengthSlider) wavelengthSlider.onValueChanged.AddListener(OnWavelengthChanged);
            }
            
            // Initialize UI values
            if (hdrSlider) hdrSlider.value = 2.0f;
            if (bounceSlider) bounceSlider.value = 5;
            if (wavelengthSlider) wavelengthSlider.value = 500;
        }

        private void OnWavelengthChanged(float val)
        {
            controller.SetWavelength(val);
            if (wavelengthText) wavelengthText.text = $"Wellenlänge: {Mathf.RoundToInt(val)} nm";
            
            // Hide wavelength slider if rainbow mode is on (it doesn't affect it)
            if (rainbowToggle && rainbowToggle.isOn)
            {
                // Optionally dim it or show it's inactive
            }
        }
    }
}