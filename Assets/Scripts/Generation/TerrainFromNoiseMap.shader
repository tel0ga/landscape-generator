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

        _TexScale ("Texture Scale", Float) = 0.1

        _WaterThreshold ("Water -> Wet Sand", Float) = 0.0
        _WetSandThreshold ("Wet Sand -> Sand", Float) = 0.04
        _SandThreshold ("Sand -> Grass", Float) = 0.17

        _BlendWidth ("Blend Width", Range(0.01, 1)) = 0.1
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
            float _TexScale;

            float _WaterThreshold, _WetSandThreshold, _SandThreshold;
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

                // 3. Веса слоёв
                float wWater_to_WetSand = smoothWeight(hRaw, _WaterThreshold,   _BlendWidth);
                float wWetSand_to_Sand  = smoothWeight(hRaw, _WetSandThreshold, _BlendWidth);
                float wSand_to_Grass    = smoothWeight(hRaw, _SandThreshold,    _BlendWidth);

                float wWater   = 1 - wWater_to_WetSand;
                float wWetSand = wWater_to_WetSand * (1 - wWetSand_to_Sand);
                float wSand    = wWetSand_to_Sand * (1 - wSand_to_Grass);
                float wGrass   = wSand_to_Grass;

                // 4. UV текстур из мировых координат
                float2 texUV = i.worldPos.xy * _TexScale;

                // 5. Сэмплируем 4 текстуры
                float4 water   = tex2D(_WaterTex,   texUV);
                float4 wetSand = tex2D(_WetSandTex, texUV);
                float4 sand    = tex2D(_SandTex,    texUV);
                float4 grass   = tex2D(_GrassTex,   texUV);

                // 6. Смешиваем
                float4 result = water * wWater + wetSand * wWetSand + sand * wSand + grass * wGrass;

                return result;
            }
            ENDCG
        }
    }
}