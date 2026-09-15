Shader "Custom/TerrainFromNoiseMap"
{
    Properties
    {
        _NoiseMap ("Noise Map", 2D) = "white" {}
        _NoiseMin ("Noise Min", Float) = -2.335
        _NoiseMax ("Noise Max", Float) = 3.335
        _ChunkOrigin ("Chunk Origin", Vector) = (0,0,0,0)
        _ChunkSize ("Chunk Size", Float) = 60

        _WaterTex ("Water", 2D) = "white" {}
        _WetSandTex ("Wet Sand", 2D) = "white" {}
        _SandTex ("Sand", 2D) = "white" {}
        _GrassTex ("Grass", 2D) = "white" {}
        _DarkGrassTex ("Dark Grass", 2D) = "white" {}
        _Rock1Tex ("Rock 1", 2D) = "white" {}
        _Rock2Tex ("Rock 2", 2D) = "white" {}
        _SnowTex ("Snow", 2D) = "white" {}

        _TexScale ("Texture Scale", Float) = 0.1

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
                float4 vertex : SV_POSITION;
            };

            sampler2D _NoiseMap;
            float _NoiseMin, _NoiseMax;
            float4 _ChunkOrigin;
            float _ChunkSize;

            sampler2D _WaterTex, _WetSandTex, _SandTex, _GrassTex;
            sampler2D _DarkGrassTex, _Rock1Tex, _Rock2Tex, _SnowTex;
            float _TexScale;

            float _WaterThreshold, _WetSandThreshold, _SandThreshold;
            float _GrassThreshold, _DarkGrassThreshold, _Rock1Threshold, _Rock2Threshold;
            float _BlendWidth;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                return o;
            }

            float smoothWeight(float h, float threshold, float width)
            {
                return smoothstep(threshold - width, threshold + width, h);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 1. Шум по мировым UV (бесшовно между чанками)
                float2 noiseUV = (i.worldPos.xy - _ChunkOrigin.xy) / _ChunkSize;
                float noiseRaw = tex2D(_NoiseMap, noiseUV).r;

                // 2. Денормализация в сырой диапазон
                float hRaw = lerp(_NoiseMin, _NoiseMax, noiseRaw);

                // 3. Веса переходов между слоями
                float t0 = smoothWeight(hRaw, _WaterThreshold,      _BlendWidth);
                float t1 = smoothWeight(hRaw, _WetSandThreshold,    _BlendWidth);
                float t2 = smoothWeight(hRaw, _SandThreshold,       _BlendWidth);
                float t3 = smoothWeight(hRaw, _GrassThreshold,      _BlendWidth);
                float t4 = smoothWeight(hRaw, _DarkGrassThreshold,  _BlendWidth);
                float t5 = smoothWeight(hRaw, _Rock1Threshold,      _BlendWidth);
                float t6 = smoothWeight(hRaw, _Rock2Threshold,      _BlendWidth);

                // 4. Итоговые веса слоёв (цепочка "ступенек")
                float wWater     = 1 - t0;
                float wWetSand   = t0 * (1 - t1);
                float wSand      = t1 * (1 - t2);
                float wGrass     = t2 * (1 - t3);
                float wDarkGrass = t3 * (1 - t4);
                float wRock1     = t4 * (1 - t5);
                float wRock2     = t5 * (1 - t6);
                float wSnow      = t6;

                // 5. UV текстур из мировых координат
                float2 texUV = i.worldPos.xy * _TexScale;

                // 6. Сэмплируем 8 текстур
                float4 water     = tex2D(_WaterTex,     texUV);
                float4 wetSand   = tex2D(_WetSandTex,   texUV);
                float4 sand      = tex2D(_SandTex,      texUV);
                float4 grass     = tex2D(_GrassTex,     texUV);
                float4 darkGrass = tex2D(_DarkGrassTex, texUV);
                float4 rock1     = tex2D(_Rock1Tex,     texUV);
                float4 rock2     = tex2D(_Rock2Tex,     texUV);
                float4 snow      = tex2D(_SnowTex,      texUV);

                // 7. Смешиваем
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