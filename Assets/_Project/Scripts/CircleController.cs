using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.Scripts
{
    public class CircleController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Light _lightToUse;
        [SerializeField] private Slider slider;
        
        [Header("Settings")]
        [SerializeField] private float radius = 2f;
        [SerializeField] private float absLaserStartXPos = 5f;
        [SerializeField] private bool StartFromLeft = true;
        private bool directionLeft => !StartFromLeft;
        [SerializeField] private int circleResolution = 100;
        [SerializeField] private float outgoingLaserLength = 15f;
        [SerializeField] private int maxBounces = 5;
        [SerializeField] private float lineWidth = 0.05f;
        [Space(5)]
        [SerializeField] private float startingSliderValue = 0.25f;
        
        [Header("Dispersion")]
        [SerializeField] private bool useRainbowMode = true;
        [SerializeField] private int rainbowRays = 12;
        [Range(0, 1)] [SerializeField] private float rainbowIntensity = 0.8f;
        [SerializeField] private float hdrExposure = 2.0f;

        [Header("Animation")]
        [SerializeField] private bool autoAnimate = true;
        [SerializeField] private float animationSpeed = 0.5f;
        [SerializeField] private float animationAmplitude = 0.8f;

        private List<LineRenderer> _linePool = new List<LineRenderer>();
        private int _poolIndex = 0;
        
        private LineRenderer _circleRenderer;
        private Vector2 _circleCenter = Vector2.zero;
        private Material _lineMaterial;

        private void Start()
        {
            if (slider)
            {
                slider.maxValue = radius * 0.99f;
                slider.minValue = -radius * 0.99f;
                slider.value = startingSliderValue;
            }

            // Robust material initialization
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
            if (shader == null) shader = Shader.Find("Standard");
            
            _lineMaterial = new Material(shader);
            
            // Clean up any existing generated objects if Start is called again
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("Ray_") || child.name == "CircleBorder")
                {
                    Destroy(child.gameObject);
                }
            }
            _linePool.Clear();
            
            InitCircle();
        }

        private void InitCircle()
        {
            GameObject circleObj = new GameObject("CircleBorder");
            circleObj.transform.SetParent(transform);
            _circleRenderer = circleObj.AddComponent<LineRenderer>();
            _circleRenderer.startWidth = 0.08f;
            _circleRenderer.endWidth = 0.08f;
            _circleRenderer.loop = true;
            _circleRenderer.material = _lineMaterial;
            _circleRenderer.startColor = new Color(0.5f, 0.8f, 1f, 0.5f);
            _circleRenderer.endColor = new Color(0.5f, 0.8f, 1f, 0.5f);
            
            Vector3[] positions = new Vector3[circleResolution];
            for (int i = 0; i < circleResolution; i++)
            {
                float angle = i * 2 * Mathf.PI / circleResolution;
                positions[i] = Circle.PointOnCircle(_circleCenter, radius, angle);
            }
            _circleRenderer.positionCount = circleResolution;
            _circleRenderer.SetPositions(positions);
        }

        private void Update()
        {
            if (!_lightToUse) return;

            _poolIndex = 0;

            float yOffset = slider ? slider.value : 0;
            if (autoAnimate && !Input.GetMouseButton(0))
            {
                yOffset = Mathf.Sin(Time.time * animationSpeed) * (radius * animationAmplitude);
                if (slider) slider.value = yOffset;
            }

            float startX = directionLeft ? absLaserStartXPos : -absLaserStartXPos;
            
            Vector2 laserHitPosition = new Vector2(
                Mathf.Sqrt(Mathf.Max(0, radius * radius - yOffset * yOffset)) * (directionLeft ? 1 : -1),
                yOffset
            );
            Vector2 laserStartPosition = new Vector2(startX, yOffset);

            if (useRainbowMode)
            {
                // 1. White incoming ray
                DrawLine(laserStartPosition, laserHitPosition, Color.white * hdrExposure);

                // 2. White initial reflection
                Vector2 incident = (laserHitPosition - laserStartPosition).normalized;
                Vector2 normal = Circle.CircleNormal(_circleCenter, laserHitPosition);
                Vector2 reflectionDir = Reflect(incident, normal);
                DrawLine(laserHitPosition, laserHitPosition + reflectionDir * outgoingLaserLength, Color.white * hdrExposure * 0.2f);

                // 3. Dispersed internal rays
                for (int i = 0; i < rainbowRays; i++)
                {
                    float t = (float)i / (rainbowRays - 1);
                    float wavelength = Mathf.Lerp(380, 750, t);
                    Color color = Light.WavelengthToRGB(wavelength);
                    color *= hdrExposure;
                    color.a = rainbowIntensity;
                    float nWater = _lightToUse.GetWaterRefractiveIndex(wavelength);
                    
                    if (Refract(incident, normal, _lightToUse.airRefractiveIndex, nWater, out Vector2 refractedDir))
                    {
                        TraceInside(laserHitPosition, refractedDir, color, nWater, _lightToUse.airRefractiveIndex, 0);
                    }
                }
            }
            else
            {
                Color color = _lightToUse.GetColor() * hdrExposure;
                color.a = rainbowIntensity;
                float nWater = _lightToUse.GetWaterRefractiveIndex(_lightToUse.waveLength);
                SimulateRay(laserStartPosition, laserHitPosition, color, _lightToUse.airRefractiveIndex, nWater);
            }

            // Deactivate unused lines in pool
            for (int i = _poolIndex; i < _linePool.Count; i++)
            {
                _linePool[i].gameObject.SetActive(false);
            }
        }

        // UI Accessors
        public void SetRainbowMode(bool enabled) => useRainbowMode = enabled;
        public void SetAutoAnimate(bool enabled) => autoAnimate = enabled;
        public void SetHdrExposure(float value) => hdrExposure = value;
        public void SetBounces(float value) => maxBounces = Mathf.RoundToInt(value);
        public void SetWavelength(float value) { if(_lightToUse) _lightToUse.waveLength = value; }

        private void SimulateRay(Vector2 start, Vector2 hit, Color color, float nAir, float nWater)
        {
            // 1. Incoming Laser
            DrawLine(start, hit, color);

            Vector2 incident = (hit - start).normalized;
            Vector2 normal = Circle.CircleNormal(_circleCenter, hit);
            
            // 2. Initial Reflection (Air)
            Vector2 reflectionDir = Reflect(incident, normal);
            DrawLine(hit, hit + reflectionDir * outgoingLaserLength, color * 0.3f);

            // 3. Initial Refraction (Into Water)
            if (Refract(incident, normal, nAir, nWater, out Vector2 refractedDir))
            {
                TraceInside(hit, refractedDir, color, nWater, nAir, 0);
            }
        }

        private void TraceInside(Vector2 start, Vector2 direction, Color color, float nIn, float nOut, int bounce)
        {
            if (bounce >= maxBounces) return;

            Vector2 hit = RaycastInTheCircle(start, direction);
            DrawLine(start, hit, color);

            Vector2 incident = (hit - start).normalized;
            Vector2 normal = Circle.CircleNormal(_circleCenter, hit);
            if (Vector2.Dot(incident, normal) > 0) normal = -normal;

            // Reflection Inside
            Vector2 reflectDir = Reflect(incident, normal);
            TraceInside(hit, reflectDir, color * 0.8f, nIn, nOut, bounce + 1);

            // Refraction Outside
            if (Refract(incident, normal, nIn, nOut, out Vector2 refractOutDir))
            {
                DrawLine(hit, hit + refractOutDir * outgoingLaserLength, color);
            }
        }

        private void DrawLine(Vector2 start, Vector2 end, Color color)
        {
            LineRenderer lr;
            if (_poolIndex < _linePool.Count)
            {
                lr = _linePool[_poolIndex];
                lr.gameObject.SetActive(true);
            }
            else
            {
                GameObject go = new GameObject("Ray_" + _poolIndex);
                go.transform.SetParent(transform);
                lr = go.AddComponent<LineRenderer>();
                lr.material = _lineMaterial;
                _linePool.Add(lr);
            }

            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.startColor = color;
            lr.endColor = color;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
            
            _poolIndex++;
        }

        private Vector2 Reflect(Vector2 v, Vector2 normal)
        {
            return v - 2f * Vector2.Dot(v, normal) * normal;
        }

        private bool Refract(Vector2 incident, Vector2 normal, float n1, float n2, out Vector2 refracted)
        {
            float eta = n1 / n2;
            float cosI = -Vector2.Dot(normal, incident);
            float sinT2 = eta * eta * (1f - cosI * cosI);

            if (sinT2 > 1f)
            {
                refracted = Vector2.zero;
                return false;
            }

            float cosT = Mathf.Sqrt(1f - sinT2);
            refracted = eta * incident + (eta * cosI - cosT) * normal;
            return true;
        }

        private Vector2 RaycastInTheCircle(Vector2 startPoint, Vector2 direction)
        {
            float b = 2f * Vector2.Dot(startPoint - _circleCenter, direction);
            float c = (startPoint - _circleCenter).sqrMagnitude - radius * radius;
            float discriminant = b * b - 4f * c;
            
            if (discriminant < 0) return startPoint;
            
            float t = (-b + Mathf.Sqrt(discriminant)) / 2f;
            return startPoint + direction * t;
        }
    }
}