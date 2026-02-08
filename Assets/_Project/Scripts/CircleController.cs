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
        [SerializeField] private float radius = 1;
        [SerializeField] private float absLaserStartXPos = 3;
        [SerializeField] private bool directionLeft = true;
        [SerializeField] private float circleResolution = 36;
        [SerializeField] private float outgoingLaserLength = 10;

        private List<LineRenderer> _reflections = new List<LineRenderer>();        
        private List<LineRenderer> _refractions = new List<LineRenderer>();        
        
        private LineRenderer _laser;
        private LineRenderer _circle;
        
        private Vector2 _circleCenter = Vector2.zero;

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                foreach (var reflection in _reflections)
                    Destroy(reflection.gameObject);
                foreach (var refraction in _refractions)
                    Destroy(refraction.gameObject);   
            }
            
            _reflections.Clear();
            _refractions.Clear();
        }

        private void Start()
        {
            slider.maxValue = radius;
            slider.minValue = -radius;
            slider.value = 0;
            
            _laser = new GameObject("Laser").AddComponent<LineRenderer>();
            _laser.startWidth = .1f;
            _laser.endWidth = .1f;
            
            _circle = new GameObject("Circle").AddComponent<LineRenderer>();
            _circle.startWidth = .1f;
            _circle.endWidth = .1f;
            _circle.loop = true;
            
            // Draw circle
            List<Vector3> circlePositions = new List<Vector3>();
            float step = 2 * Mathf.PI / circleResolution;
            float angle = 0;
            for (int i = 0; i < circleResolution; i++)
            {
                circlePositions.Add(Circle.PointOnCircle(_circleCenter, radius, angle));
                angle += step;
            }
            _circle.positionCount = circlePositions.Count;
            _circle.SetPositions(circlePositions.ToArray());
        }
        
        private void Update()
        {
            if (!_lightToUse) return;
            
            // Initial calculations
            Vector2 laserHitPosition = Circle.PointOnCircle(_circleCenter, radius, Mathf.Tan(slider.value / radius));
            if (directionLeft) laserHitPosition.x *= -1;
            Vector2 laserStartPosition = new Vector2(directionLeft ? -absLaserStartXPos : absLaserStartXPos, laserHitPosition.y);

            DrawLaser(laserStartPosition, laserHitPosition);
            
            // Use the *actual* incident direction
            Vector2 incident = (laserHitPosition - laserStartPosition).normalized;
            
            // Iteration 0 (air -> water)
            Vector2 reflectionDir = Reflect(incident, Circle.CircleNormal(_circleCenter, laserHitPosition));
            Vector2 refractionDir = Refract(laserHitPosition, incident, _lightToUse.airRefractiveIndex, _lightToUse.waterRefractiveIndex);
            DrawReflectionOutside(0, laserHitPosition, reflectionDir);
            
            laserStartPosition = laserHitPosition;
            laserHitPosition = RaycastInTheCircle(laserStartPosition, refractionDir);
            DrawRefractionInside(0, laserStartPosition, laserHitPosition);
            
            // Iteration 1 (water -> air)
            incident = (laserHitPosition - laserStartPosition).normalized;

            reflectionDir = Reflect(incident, Circle.CircleNormal(_circleCenter, laserHitPosition)); // In the circle
            refractionDir = Refract(laserHitPosition, incident, _lightToUse.waterRefractiveIndex, _lightToUse.airRefractiveIndex); // Out of the circle
            DrawRefractionOutside(1, laserHitPosition, refractionDir);
            
            laserStartPosition = laserHitPosition;
            laserHitPosition = RaycastInTheCircle(laserStartPosition, reflectionDir);
            DrawReflectionInside(1, laserStartPosition, laserHitPosition);
            
            // Iteration 2 (water -> air)
            incident = (laserHitPosition - laserStartPosition).normalized;

            reflectionDir = Reflect(incident, Circle.CircleNormal(_circleCenter, laserHitPosition)); // In the circle
            refractionDir = Refract(laserHitPosition, incident, _lightToUse.waterRefractiveIndex, _lightToUse.airRefractiveIndex); // Out of the circle
            DrawRefractionOutside(2, laserHitPosition, refractionDir);
            
            laserStartPosition = laserHitPosition;
            laserHitPosition = RaycastInTheCircle(laserStartPosition, reflectionDir);
            DrawReflectionInside(2, laserStartPosition, laserHitPosition);
            
            // Analyze results
            var angle = Vector2.Angle(directionLeft ? Vector2.left : Vector2.right, refractionDir);
            Debug.Log(Mathf.RoundToInt(angle) + " degrees");
        }
        
        private Vector2 Refract(Vector2 laserHitPosition, Vector2 incident, float n1, float n2)
        {
            Vector2 normal = Circle.CircleNormal(_circleCenter, laserHitPosition).normalized;

            // Make sure normal faces against the incident ray
            if (Vector2.Dot(incident, normal) > 0f)
                normal = -normal;

            if (Refract(incident, normal, n1, n2, out var refracted))
                return refracted.normalized;

            // Total internal reflection fallback
            return Reflect(incident, normal).normalized;
        }
        
        private bool Refract(
            Vector2 incident,
            Vector2 normal,
            float n1,
            float n2,
            out Vector2 refracted)
        {
            incident = incident.normalized;
            normal = normal.normalized;

            float eta = n1 / n2;
            float cosI = -Vector2.Dot(normal, incident);
            float sinT2 = eta * eta * (1f - cosI * cosI);

            // Total internal reflection
            if (sinT2 > 1f)
            {
                refracted = Vector2.zero;
                return false;
            }

            float cosT = Mathf.Sqrt(1f - sinT2);
            refracted = eta * incident + (eta * cosI - cosT) * normal;
            return true;
        }
        
        private static Vector2 Reflect(Vector2 v, Vector2 normal)
        {
            normal = normal.normalized;
            return v - 2f * Vector2.Dot(v, normal) * normal;
        }
        
        private Vector2 RaycastInTheCircle(Vector2 startPoint, Vector2 direction)
                {
                    // Robust ray-circle intersection (find the EXIT point).
                    direction = direction.normalized;
        
                    Vector2 center = _circleCenter;
                    Vector2 f = startPoint - center;
        
                    // Solve: |f + t*d|^2 = r^2
                    float a = 1f; // d is normalized
                    float b = 2f * Vector2.Dot(f, direction);
                    float c = f.sqrMagnitude - radius * radius;
        
                    float discriminant = b * b - 4f * a * c;
                    if (discriminant < 0f)
                    {
                        // No intersection (shouldn't happen if startPoint is on/inside and direction is valid)
                        return startPoint;
                    }
        
                    float sqrtDisc = Mathf.Sqrt(discriminant);
                    float t1 = (-b - sqrtDisc) / (2f * a);
                    float t2 = (-b + sqrtDisc) / (2f * a);
        
                    // If startPoint is on the circle, one solution is ~0; we want the other one (exit).
                    const float epsilon = 1e-4f;
        
                    float t = float.NegativeInfinity;
        
                    if (t1 > epsilon) t = t1;
                    if (t2 > epsilon) t = Mathf.Max(t, t2);
        
                    if (float.IsNegativeInfinity(t))
                    {
                        // Both intersections are behind or too close (ray points outward or numerical edge case)
                        return startPoint;
                    }
        
                    Vector2 hit = startPoint + direction * t;
        
                    return hit;
                }
        
        private void DrawLaser(Vector2 start, Vector2 end)
        {
            // Set points of Line Renderer
            Vector3[] laserPositions =
            {
                start, 
                end
            };
            _laser?.SetPositions(laserPositions);
        }
        
        private void DrawReflectionOutside(int iteration, Vector2 laserHitPosition, Vector2 reflectionVector)
        {
            LineRenderer reflection = null;
            
            // Create or Get Line Renderer
            if (iteration >= _reflections.Count)
            {
                reflection = new GameObject("Reflection " + iteration).AddComponent<LineRenderer>();
                reflection.transform.SetParent(transform);
                reflection.startWidth = .1f;
                reflection.endWidth = .1f;
                _reflections.Add(reflection);
            }
            else
                reflection = _reflections[iteration];

            // Set points of Line Renderer
            Vector3[] reflectionPositions =
            {
                laserHitPosition, 
                laserHitPosition + reflectionVector * outgoingLaserLength
            };
            reflection?.SetPositions(reflectionPositions);
        }

        private void DrawReflectionInside(int iteration, Vector2 start, Vector2 end)
        {
            LineRenderer reflection = null;
            
            // Create or Get Line Renderer
            if (iteration >= _reflections.Count)
            {
                reflection = new GameObject("Reflection " + iteration).AddComponent<LineRenderer>();
                reflection.transform.SetParent(transform);
                reflection.startWidth = .1f;
                reflection.endWidth = .1f;
                _reflections.Add(reflection);
            }
            else
                reflection = _reflections[iteration];

            // Set points of Line Renderer
            Vector3[] reflectionPositions =
            {
                start, 
                end
            };
            reflection?.SetPositions(reflectionPositions);
        }
        
        private void DrawRefractionOutside(int iteration, Vector2 laserHitPosition, Vector2 refractionVector)
        {
            LineRenderer refraction = null;
            
            // Create or Get Line Renderer
            if (iteration >= _refractions.Count)
            {
                refraction = new GameObject("Refraction " + iteration).AddComponent<LineRenderer>();
                refraction.transform.SetParent(transform);
                refraction.startWidth = .1f;
                refraction.endWidth = .1f;
                _refractions.Add(refraction);
            }
            else
                refraction = _refractions[iteration];

            // Set points of Line Renderer
            Vector3[] refractionPositions =
            {
                laserHitPosition,
                laserHitPosition + refractionVector * outgoingLaserLength
            };
            refraction?.SetPositions(refractionPositions);
        }
        
        private void DrawRefractionInside(int iteration, Vector2 start, Vector2 end)
        {
            LineRenderer refraction = null;
            
            // Create or Get Line Renderer
            if (iteration >= _refractions.Count)
            {
                refraction = new GameObject("Refraction " + iteration).AddComponent<LineRenderer>();
                refraction.transform.SetParent(transform);
                refraction.startWidth = .1f;
                refraction.endWidth = .1f;
                _refractions.Add(refraction);
            }
            else
                refraction = _refractions[iteration];

            // Set points of Line Renderer
            Vector3[] refractionPositions =
            {
                start,
                end
            };
            refraction?.SetPositions(refractionPositions);
        }
    }
}