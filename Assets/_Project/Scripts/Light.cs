using UnityEngine;

namespace _Project.Scripts
{
    public class Light : MonoBehaviour
    {
        public string lightName;
        [Range(380, 750)]
        public float waveLength = 500;
        public float airRefractiveIndex = 1f;
        
        // Cauchy's equation constants for water
        private const float WaterA = 1.324f;
        private const float WaterB = 0.00312f;

        public float GetWaterRefractiveIndex(float lambdaNm)
        {
            // lambda in micrometers for the formula
            float lambdaUm = lambdaNm / 1000f;
            return WaterA + (WaterB / (lambdaUm * lambdaUm));
        }

        public Color GetColor()
        {
            return WavelengthToRGB(waveLength);
        }

        public static Color WavelengthToRGB(float wavelength)
        {
            float r, g, b, factor;

            if (wavelength >= 380 && wavelength < 440)
            {
                r = -(wavelength - 440) / (440 - 380);
                g = 0.0f;
                b = 1.0f;
            }
            else if (wavelength >= 440 && wavelength < 490)
            {
                r = 0.0f;
                g = (wavelength - 440) / (490 - 440);
                b = 1.0f;
            }
            else if (wavelength >= 490 && wavelength < 510)
            {
                r = 0.0f;
                g = 1.0f;
                b = -(wavelength - 510) / (510 - 490);
            }
            else if (wavelength >= 510 && wavelength < 580)
            {
                r = (wavelength - 510) / (580 - 510);
                g = 1.0f;
                b = 0.0f;
            }
            else if (wavelength >= 580 && wavelength < 645)
            {
                r = 1.0f;
                g = -(wavelength - 645) / (645 - 580);
                b = 0.0f;
            }
            else if (wavelength >= 645 && wavelength <= 780)
            {
                r = 1.0f;
                g = 0.0f;
                b = 0.0f;
            }
            else
            {
                r = g = b = 0.0f;
            }

            // Let intensity fall off near the vision limits
            if (wavelength >= 380 && wavelength < 420) factor = 0.3f + 0.7f * (wavelength - 380) / (420 - 380);
            else if (wavelength >= 420 && wavelength < 701) factor = 1.0f;
            else if (wavelength >= 701 && wavelength <= 780) factor = 0.3f + 0.7f * (780 - wavelength) / (780 - 701);
            else factor = 0.0f;

            return new Color(r * factor, g * factor, b * factor);
        }
    }
}