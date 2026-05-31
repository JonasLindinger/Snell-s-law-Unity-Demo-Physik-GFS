using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace _Project.Scripts
{
    [ExecuteAlways]
    public class SceneSetup : MonoBehaviour
    {
        public Color backgroundColor = new Color(0.05f, 0.05f, 0.08f, 1f);
        public bool setupCamera = true;
        public bool addBloom = true;

        private void Start()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        public void Apply()
        {
            // Background
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.backgroundColor = backgroundColor;
                mainCam.clearFlags = CameraClearFlags.SolidColor;
                if (setupCamera)
                {
                    mainCam.orthographic = true;
                    mainCam.orthographicSize = 6;
                    mainCam.transform.position = new Vector3(0, 0, -10);
                }
            }

            // Global Volume for Bloom
            if (addBloom && Application.isPlaying)
            {
                Volume volume = FindFirstObjectByType<Volume>();
                if (volume == null)
                {
                    GameObject volObj = new GameObject("Global Volume");
                    volume = volObj.AddComponent<Volume>();
                    volume.isGlobal = true;
                }

                if (volume.profile == null)
                {
                    volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
                }

                if (!volume.profile.Has<Bloom>())
                {
                    Bloom bloom = volume.profile.Add<Bloom>(true);
                    bloom.intensity.Override(1.5f);
                    bloom.threshold.Override(0.8f);
                    bloom.scatter.Override(0.7f);
                }
            }
        }
    }
}