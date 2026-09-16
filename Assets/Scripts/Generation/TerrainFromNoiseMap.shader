Shader "Custom/TerrainFromNoiseMap_PixelPerfect"
{
    Properties
    {
        _NoiseMap ("Noise Map", 2D) = "white" {}
        _NoiseMin ("Noise Min", Float) = -2.335
        _NoiseMax ("Noise Max", Float) = 3.335
        _ChunkOrigin ("Chunk Origin", Vector) = (0,0,0,0)
        _ChunkSize ("Chunk Size", Float) = 60

        _WaterTex ("Sky Reflection (Fullscreen)", 2D) = "white" {}
        _WaterLocalTex ("Water Local (Tiled)", 2D) = "white" {}
        _WaterLocalScale ("Water Local Scale", Float) = 0.3
        _FresnelBias ("Fresnel Bias", Range(-1, 1)) = 0.3
        _FresnelStrength ("Fresnel Strength", Range(0.1, 8)) = 2.0
        _FresnelMin ("Fresnel Min (near)", Range(0, 1)) = 0.15

        _WetSandTex ("Wet Sand", 2D) = "white" {}
        _SandTex ("Sand", 2D) = "white" {}
        _GrassTex ("Grass", 2D) = "white" {}
        _DarkGrassTex ("Dark Grass", 2D) = "white" {}
        _Rock1Tex ("Rock 1", 2D) = "white" {}
        _Rock2Tex ("Rock 2", 2D) = "white" {}
        _SnowTex ("Snow", 2D) = "white" {}

        _TexScale ("Texture Scale (Land Only)", Float) = 0.1
        _PPU ("Pixels Per Unit", Float) = 3.0

        // --- НАСТРОЙКИ ВОЛН ---
        _WaveSpeed ("Wave Speed", Float) = 1.5
        _WaveFrequency ("Wave Frequency", Float) = 2.5
        _WaveAmplitude ("Wave Amplitude", Float) = 1.0

        // --- ШУМ ПЕРЛИНА ДЛЯ ОТРАЖЕНИЯ ---
        _NoiseScale ("Noise Scale", Float) = 0.15
        _NoiseStrength ("Noise Strength", Float) = 8.0
        _NoiseOctaves ("Noise Octaves", Range(1,4)) = 3
        _NoisePersistence ("Noise Persistence", Range(0,1)) = 0.5
        _NoiseLacunarity ("Noise Lacunarity", Float) = 2.0

        // --- РЯБЬ (ВАРИАЦИЯ ЯРКОСТИ ВОДЫ) ---
        _ShimmerAmount ("Shimmer Amount", Range(0,1)) = 0.15
        _ShimmerColor ("Shimmer Color", Color) = (0.9, 0.97, 1.0, 1.0)

        // --- КАУСТИКА ---
        _CausticsTex ("Caustics Pattern", 2D) = "black" {}
        _CausticsColor ("Caustics Color", Color) = (0.85, 1.0, 0.95, 1.0)
        _CausticsScale ("Caustics Scale", Float) = 0.4
        _CausticsSpeed ("Caustics Speed", Float) = 0.6
        _CausticsIntensity ("Caustics Intensity", Range(0, 2)) = 0.6
        _CausticsSharpness ("Caustics Sharpness", Range(0.1, 4)) = 0.25
        _CausticsDepthFade ("Caustics Depth Fade", Float) = 1.5

        // --- НАСТРОЙКИ ПЕНЫ ---
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _FoamWidth ("Foam Width", Range(0, 0.5)) = 0.05
        _FoamAnimSpeed ("Foam Anim Speed", Float) = 4.0
        _FoamAnimAmount ("Foam Anim Amount", Range(0, 0.2)) = 0.02

        // --- НАСТРОЙКИ БЛИКОВ ---
        _GlintColor ("Glint Color", Color) = (0.8, 0.95, 1.0, 1.0)
        _GlintChance ("Glint Chance", Range(0, 1)) = 0.02
        _GlintSpeed ("Glint Speed", Float) = 3.0

        _WaterThreshold ("Water -> Wet Sand", Float) = 0.0
        _WetSandThreshold ("Wet Sand -> Sand", Float) = 0.04
        _SandThreshold ("Sand -> Grass", Float) = 0.17
        _GrassThreshold ("Grass -> Dark Grass", Float) = 0.35
        _DarkGrassThreshold ("Dark Grass -> Rock 1", Float) = 0.55
        _Rock1Threshold ("Rock 1 -> Rock 2", Float) = 0.75
        _Rock2Threshold ("Rock 2 -> Snow", Float) = 0.9

        _BlendWidth ("Blend Width", Range(0, 1)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
                float4 screenPos : TEXCOORD4;
                float4 vertex : SV_POSITION;
            };

            sampler2D _NoiseMap;
            float _NoiseMin, _NoiseMax;
            float4 _ChunkOrigin;
            float _ChunkSize;

            sampler2D _WaterTex, _WetSandTex, _SandTex, _GrassTex;
            sampler2D _DarkGrassTex, _Rock1Tex, _Rock2Tex, _SnowTex;

            sampler2D _WaterLocalTex;
            float _WaterLocalScale;
            float _FresnelBias;
            float _FresnelStrength;
            float _FresnelMin;

            float _TexScale;
            float _PPU;

            float _WaveSpeed;
            float _WaveFrequency;
            float _WaveAmplitude;

            float _NoiseScale;
            float _NoiseStrength;
            float _NoiseOctaves;
            float _NoisePersistence;
            float _NoiseLacunarity;

            float _ShimmerAmount;
            float4 _ShimmerColor;

            sampler2D _CausticsTex;
            float4 _CausticsColor;
            float _CausticsScale;
            float _CausticsSpeed;
            float _CausticsIntensity;
            float _CausticsSharpness;
            float _CausticsDepthFade;

            float4 _FoamColor;
            float _FoamWidth;
            float _FoamAnimSpeed;
            float _FoamAnimAmount;

            float _WaterThreshold, _WetSandThreshold, _SandThreshold;
            float _GrassThreshold, _DarkGrassThreshold, _Rock1Threshold, _Rock2Threshold;
            float _BlendWidth;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            float smoothWeight(float h, float threshold, float width)
            {
                return smoothstep(threshold - width, threshold + width, h);
            }

            float hash2D(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            // --- Градиентный шум Перлина (2D) ---
            float2 fade2(float2 t)
            {
                return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
            }

            float perlin2D(float2 P)
            {
                float2 Pi = floor(P);
                float2 Pf = P - Pi;

                float2 g00 = float2(hash2D(Pi + float2(0,0)), hash2D(Pi + float2(0,0) + 17.0)) * 2.0 - 1.0;
                float2 g10 = float2(hash2D(Pi + float2(1,0)), hash2D(Pi + float2(1,0) + 17.0)) * 2.0 - 1.0;
                float2 g01 = float2(hash2D(Pi + float2(0,1)), hash2D(Pi + float2(0,1) + 17.0)) * 2.0 - 1.0;
                float2 g11 = float2(hash2D(Pi + float2(1,1)), hash2D(Pi + float2(1,1) + 17.0)) * 2.0 - 1.0;

                float n00 = dot(g00, Pf - float2(0,0));
                float n10 = dot(g10, Pf - float2(1,0));
                float n01 = dot(g01, Pf - float2(0,1));
                float n11 = dot(g11, Pf - float2(1,1));

                float2 u = fade2(Pf);

                float nx0 = lerp(n00, n10, u.x);
                float nx1 = lerp(n01, n11, u.x);
                return lerp(nx0, nx1, u.y);
            }

            // --- Фрактальный шум (fBm) ---
            float fbm2D(float2 p, int octaves, float persistence, float lacunarity)
            {
                float total = 0.0;
                float amplitude = 1.0;
                float frequency = 1.0;
                float maxValue = 0.0;

                [unroll(4)]
                for (int i = 0; i < octaves; i++)
                {
                    total += perlin2D(p * frequency) * amplitude;
                    maxValue += amplitude;
                    amplitude *= persistence;
                    frequency *= lacunarity;
                }
                return total / max(maxValue, 0.0001);
            }

            // --- Каустика: две пересекающиеся fBm-выборки ---
            float caustics(float2 uv, float time, float scale, float sharpness)
            {
                float2 uv1 = uv * scale + float2(time * 0.10, time * 0.07);
                float2 uv2 = uv * scale + float2(-time * 0.13, time * 0.09);
                float2 uv2r = float2(uv2.x * 0.8 - uv2.y * 0.6, uv2.x * 0.6 + uv2.y * 0.8);

                float n1 = fbm2D(uv1, 2, 0.5, 2.0);
                float n2 = fbm2D(uv2r, 2, 0.5, 2.0);

                float c = 1.0 - abs(n1 * n2);
                c = pow(saturate(c), sharpness * 16.0);
                return c;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 snappedWorldPos = floor(i.worldPos.xy * _PPU) / _PPU;

                // 1. Шум ландшафта
                float2 noiseUV = (snappedWorldPos - _ChunkOrigin.xy) / _ChunkSize;
                float noiseRaw = tex2D(_NoiseMap, noiseUV).r;
                float hRaw = lerp(_NoiseMin, _NoiseMax, noiseRaw);

                // 2. Веса слоёв
                float t0 = smoothWeight(hRaw, _WaterThreshold,      _BlendWidth);
                float t1 = smoothWeight(hRaw, _WetSandThreshold,    _BlendWidth);
                float t2 = smoothWeight(hRaw, _SandThreshold,       _BlendWidth);
                float t3 = smoothWeight(hRaw, _GrassThreshold,      _BlendWidth);
                float t4 = smoothWeight(hRaw, _DarkGrassThreshold,  _BlendWidth);
                float t5 = smoothWeight(hRaw, _Rock1Threshold,      _BlendWidth);
                float t6 = smoothWeight(hRaw, _Rock2Threshold,      _BlendWidth);

                float wWater     = 1 - t0;
                float wWetSand   = t0 * (1 - t1);
                float wSand      = t1 * (1 - t2);
                float wGrass     = t2 * (1 - t3);
                float wDarkGrass = t3 * (1 - t4);
                float wRock1     = t4 * (1 - t5);
                float wRock2     = t5 * (1 - t6);
                float wSnow      = t6;

                // 3. UV обычных текстур суши
                float2 texUV = snappedWorldPos * _TexScale;

                // 4. ЭКРАННЫЕ UV ДЛЯ ОТРАЖЕНИЯ НЕБА
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float2 targetResolution = float2(640.0, 360.0);
                float2 pixelScreenUV = floor(screenUV * targetResolution) / targetResolution;

                bool isDeepWater = (wWater >= 1.0);
                float shimmerMask = 0.0;

                if (isDeepWater)
                {
                    float fps = 12.0;
                    float t = floor(_Time.y * fps) / fps;

                    float2 worldXY = snappedWorldPos;

                    // Слой 1: медленный поток
                    float2 flowUV = worldXY * _NoiseScale * 0.5 + float2(t * 0.05, t * 0.03);
                    float noiseSlow = fbm2D(flowUV, (int)_NoiseOctaves, _NoisePersistence, _NoiseLacunarity);

                    // Слой 2: быстрая мелкая рябь
                    float2 rippleUV = worldXY * _NoiseScale * 3.0 + float2(-t * 0.15, t * 0.12);
                    float noiseFast = fbm2D(rippleUV, 2, 0.5, 2.0);

                    // Синусоида как базовое "дыхание" воды
                    float2 sineOffset = float2(
                        sin(worldXY.y * _WaveFrequency + t * _WaveSpeed),
                        cos(worldXY.x * _WaveFrequency + t * _WaveSpeed)
                    ) * _WaveAmplitude;

                    float2 noiseOffset = float2(noiseSlow, noiseFast) * _NoiseStrength;

                    // Округление до целых пикселей экрана
                    float2 pixelOffset = floor(sineOffset + noiseOffset);
                    pixelScreenUV += pixelOffset / targetResolution;

                    shimmerMask = noiseSlow * 0.6 + noiseFast * 0.4;
                }

                // 5. Сэмплирование всех текстур
                // ---- Локальная вода (тайлится по миру) ----
                float2 localWaterUV = snappedWorldPos * _WaterLocalScale;
                float waterFps = 8.0;
                float wTime = floor(_Time.y * waterFps) / waterFps;
                localWaterUV += float2(wTime * 0.02, wTime * 0.015);
                float4 localWater = tex2D(_WaterLocalTex, localWaterUV);

                // ---- Отражение неба (экранное) ----
                float4 skyReflection = tex2D(_WaterTex, pixelScreenUV);

                // ---- Маска Френеля (по экранной Y: дальше = сильнее отражение) ----
                float fresnel = saturate((screenUV.y - _FresnelBias) * _FresnelStrength);
                fresnel = lerp(_FresnelMin, 1.0, fresnel);

                // ---- Смешиваем ----
                float4 water = lerp(localWater, skyReflection, fresnel);

                float4 wetSand   = tex2D(_WetSandTex,   texUV);
                float4 sand      = tex2D(_SandTex,      texUV);
                float4 grass     = tex2D(_GrassTex,     texUV);
                float4 darkGrass = tex2D(_DarkGrassTex, texUV);
                float4 rock1     = tex2D(_Rock1Tex,     texUV);
                float4 rock2     = tex2D(_Rock2Tex,     texUV);
                float4 snow      = tex2D(_SnowTex,      texUV);

                // ---- Рябь: вариация яркости воды ----
                if (isDeepWater)
                {
                    float shimmer = shimmerMask * _ShimmerAmount;
                    water.rgb = lerp(water.rgb, _ShimmerColor.rgb, saturate(shimmer));
                    water.rgb *= (1.0 - saturate(-shimmerMask) * _ShimmerAmount * 0.5);
                }

                // ---- КАУСТИКА ----
                float causticsMask = wWater + wWetSand * 0.5;
                if (causticsMask > 0.001)
                {
                    float cFps = 10.0;
                    float cTime = floor(_Time.y * cFps) / cFps;

                    float c = caustics(snappedWorldPos, cTime, _CausticsScale, _CausticsSharpness);

                    // Затухание у берега
                    float depthFade = saturate(wWater * _CausticsDepthFade);
                    float causticsAmount = c * _CausticsIntensity * depthFade * causticsMask;

                    // Аддитивно
                    water.rgb   += _CausticsColor.rgb * causticsAmount;
                    wetSand.rgb += _CausticsColor.rgb * causticsAmount * 0.4;
                }

                // 6. ПЕНА У БЕРЕГА
                if (wWater > 0.0)
                {
                    float foamTime = floor(_Time.y * _FoamAnimSpeed) / _FoamAnimSpeed;
                    float waveFactor = sin(snappedWorldPos.x * 0.5 + snappedWorldPos.y * 0.5 + foamTime * 2.0);
                    float pixelatedWave = floor(waveFactor * 2.0) / 2.0;
                    float dynamicFoamWidth = _FoamWidth + (pixelatedWave * _FoamAnimAmount);

                    float foamMask = step(_WaterThreshold - dynamicFoamWidth, hRaw);
                    if (foamMask > 0.5)
                    {
                        water = lerp(water, _FoamColor, foamMask);
                    }
                }

                // 8. Финальное смешивание слоёв
                float4 result =
                      water     * wWater
                    + wetSand   * wWetSand
                    + sand      * wSand
                    + grass     * wGrass
                    + darkGrass * wDarkGrass
                    + rock1     * wRock1
                    + rock2     * wRock2
                    + snow      * wSnow;

                return result;
            }
            ENDCG
        }
    }
}