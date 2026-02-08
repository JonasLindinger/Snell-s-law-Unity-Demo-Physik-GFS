using UnityEngine;

namespace _Project.Scripts
{
    public static class Circle
    {
        public static Vector2 PointOnCircle(Vector2 center, float radius, float angle)
        {
            return center + new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            ) * radius;
        }
        
        public static Vector2 CircleNormal(Vector2 center, Vector2 pointOnCircle)
        {
            return (pointOnCircle - center).normalized;
        }
    }
}