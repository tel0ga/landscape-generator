using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;


public class WeatherController : MonoBehaviour
{
    [SerializeField] public Material terrainMaterial;
    [SerializeField] public float snowAmount = 50f;
    [SerializeField] public float snowCoverage = 0.65f;
    [SerializeField] public int dayLength = 720;
    [SerializeField] private Gradient dayGradient;
    [SerializeField] private AnimationCurve dayLight;
    [SerializeField] private Volume volume;
    [SerializeField] private ParticleSystem snowParticles;
    private ColorAdjustments colorAdjustments;
    [SerializeField] public float lightingNoiseSpeed;
    [SerializeField] public float lightingNoiseAmount;

    [SerializeField] public float snowNoiseSpeed;
    [SerializeField] public float snowNoiseAmount;
    [SerializeField] public float seed = 42f;


    [Range(0f, 1f)]
    public float dayMoment;
    int startTime = 0;
    [SerializeField] float currentTime = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        startTime = Random.Range(0, dayLength);
        // startTime = 360;
    }
    private void Awake()
    {
        if (volume.profile.TryGet(out colorAdjustments))
        {
            colorAdjustments.colorFilter.overrideState = true;
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        float noise = Mathf.PerlinNoise1D(
            Time.time * snowNoiseSpeed + seed
        );

        float snowChange = (noise * 2f - 1f) * snowNoiseAmount;

        snowAmount += snowChange;
        snowAmount = Mathf.Clamp(snowAmount, 0f, 99999f);
    }
    private void Update()
    {
        currentTime = Time.time + (float)startTime;
        currentTime = currentTime - (dayLength) * ((int)currentTime / dayLength);
        dayMoment = currentTime / dayLength;
        if(colorAdjustments == null)
        {
            return;
        }
        float noise = Mathf.PerlinNoise1D(
            Time.time * lightingNoiseSpeed + seed
        );

        float offset = (noise * 2f - 1f) * lightingNoiseAmount;

        colorAdjustments.colorFilter.value = dayGradient.Evaluate(dayMoment);
        colorAdjustments.postExposure.value = dayLight.Evaluate(dayMoment) + offset;
        snowCoverage = Mathf.Clamp01(snowAmount / 100);
        terrainMaterial.SetFloat("_SnowCoverageThreshold", 1f-snowCoverage);

    }
}
