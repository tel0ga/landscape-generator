using UnityEngine;

namespace SnowTracks
{
    public sealed class SnowfallAdapter : MonoBehaviour
    {
        public ParticleSystem source;
        [Min(.01f)] public float normalEmissionRate = 50;
        [Range(0,1)] public float manualIntensity;
        public float SnowfallIntensity
        {
            get
            {
                if (source == null) return Mathf.Clamp01(manualIntensity);
                if (!source.isPlaying || !source.gameObject.activeInHierarchy || !source.emission.enabled) return 0;
                var emission = source.emission;
                float t = source.main.duration > 0 ? source.time / source.main.duration : 0;
                return Mathf.Clamp01(emission.rateOverTime.Evaluate(Mathf.Repeat(t,1), .5f) / Mathf.Max(.01f, normalEmissionRate));
            }
        }
    }
}
