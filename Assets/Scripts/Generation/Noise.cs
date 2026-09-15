using UnityEngine;
using System.Collections;

public static class Noise
{
    public static float[,] GenerateNoiseMap(
        int mapWidth, int mapHeight, int seed, float scale,
        int octaves, float persistance, float lacunarity,
        Vector2 offset, float upperHeight,
        out float outMin, out float outMax,
        // ⬅️ НОВЫЕ ПАРАМЕТРЫ domain warping
        float warpStrength = 30f,
        float warpScale = 1500f,
        int warpOctaves = 3,
        float warpPersistance = 0.5f,
        float warpLacunarity = 2f)
    {
        float[,] noiseMap = new float[mapWidth, mapHeight];

        System.Random prng = new System.Random(seed);

        // Смещения для основного шума
        Vector2[] octaveOffsets = new Vector2[octaves];
        for (int i = 0; i < octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000) + offset.x;
            float offsetY = prng.Next(-100000, 100000) + offset.y;
            octaveOffsets[i] = new Vector2(offsetX, offsetY);
        }

        // ⬅️ НОВОЕ: смещения для warp-шума (отдельный PRNG, чтобы не зависеть от octaves)
        System.Random warpPrng = new System.Random(seed + 1337);
        Vector2[] warpOctaveOffsets = new Vector2[warpOctaves];
        for (int i = 0; i < warpOctaves; i++)
        {
            float offsetX = warpPrng.Next(-100000, 100000) + offset.x;
            float offsetY = warpPrng.Next(-100000, 100000) + offset.y;
            warpOctaveOffsets[i] = new Vector2(offsetX, offsetY);
        }

        if (scale <= 0) scale = 0.0001f;
        if (warpScale <= 0) warpScale = 0.0001f;

        float halfWidth = mapWidth / 2f;
        float halfHeight = mapHeight / 2f;

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                // ⬅️ НОВОЕ: вычисляем warp-смещение для текущей точки
                float warpX = 0f;
                float warpY = 0f;
                if (warpStrength > 0f)
                {
                    float warpAmp = 1f;
                    float warpFreq = 1f;

                    for (int i = 0; i < warpOctaves; i++)
                    {
                        float wx = (x - halfHeight + warpOctaveOffsets[i].x) / warpScale * warpFreq;
                        float wy = (y - halfWidth + warpOctaveOffsets[i].y) / warpScale * warpFreq;

                        // Два независимых канала шума для X и Y
                        float noiseX = Mathf.PerlinNoise(wx, wy) * 2f - 1f;
                        float noiseY = Mathf.PerlinNoise(wx + 5.2f, wy + 1.3f) * 2f - 1f;

                        warpX += noiseX * warpAmp;
                        warpY += noiseY * warpAmp;

                        warpAmp *= warpPersistance;
                        warpFreq *= warpLacunarity;
                    }

                    warpX *= warpStrength;
                    warpY *= warpStrength;
                }

                // Основной шум (с применённым warp-смещением)
                float amplitude = 1;
                float frequency = 1;
                float noiseHeight = 0;

                for (int i = 0; i < octaves; i++)
                {
                    float sampleX = (x - halfHeight + octaveOffsets[i].x + warpX) / scale * frequency;
                    float sampleY = (y - halfWidth + octaveOffsets[i].y + warpY) / scale * frequency;

                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= persistance;
                    frequency *= lacunarity;
                }
                noiseMap[x, y] = noiseHeight + upperHeight;
            }
        }

        // Глобальный диапазон шума — одинаковый для всех чанков
        float maxAmp = 0f;
        float amp = 1f;
        for (int i = 0; i < octaves; i++)
        {
            maxAmp += amp;
            amp *= persistance;
        }
        outMin = -maxAmp + upperHeight;
        outMax = +maxAmp + upperHeight;

        return noiseMap;
    }
}