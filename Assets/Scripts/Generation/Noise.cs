using UnityEngine;
using System.Collections;

public static class Noise
{
    public static float[,] GenerateNoiseMap(
    int mapWidth, int mapHeight, int seed, float scale,
    int octaves, float persistance, float lacunarity,
    Vector2 offset, float upperHeight,
    out float outMin, out float outMax,
    float pixelsPerUnit = 1f,
    float warpStrength = 30f,
    float warpScale = 1500f,
    int warpOctaves = 3,
    float warpPersistance = 0.5f,
    float warpLacunarity = 2f)
    {
        float[,] noiseMap = new float[mapWidth, mapHeight];

        System.Random prng = new System.Random(seed);
        Vector2[] octaveOffsets = new Vector2[octaves];
        for (int i = 0; i < octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000) + offset.x;
            float offsetY = prng.Next(-100000, 100000) + offset.y;
            octaveOffsets[i] = new Vector2(offsetX, offsetY);
        }

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

        // ⬅️ ПОЛУРАЗМЕРЫ В МИРОВЫХ ЮНИТАХ, а не в индексах
        float worldHalfWidth = (mapWidth - 1) / pixelsPerUnit / 2f;
        float worldHalfHeight = (mapHeight - 1) / pixelsPerUnit / 2f;

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                // ⬅️ МИРОВЫЕ координаты точки относительно центра чанка
                float worldX = x / pixelsPerUnit - worldHalfWidth;
                float worldY = y / pixelsPerUnit - worldHalfHeight;

                float warpX = 0f;
                float warpY = 0f;
                if (warpStrength > 0f)
                {
                    float warpAmp = 1f;
                    float warpFreq = 1f;

                    for (int i = 0; i < warpOctaves; i++)
                    {
                        // ⬅️ используем worldX/worldY (заодно исправлен баг: x/y были перепутаны)
                        float wx = (worldX + warpOctaveOffsets[i].x) / warpScale * warpFreq;
                        float wy = (worldY + warpOctaveOffsets[i].y) / warpScale * warpFreq;

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

                float amplitude = 1;
                float frequency = 1;
                float noiseHeight = 0;

                for (int i = 0; i < octaves; i++)
                {
                    // ⬅️ worldX/worldY вместо (x - halfWidth)
                    float sampleX = (worldX + octaveOffsets[i].x + warpX) / scale * frequency;
                    float sampleY = (worldY + octaveOffsets[i].y + warpY) / scale * frequency;

                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= persistance;
                    frequency *= lacunarity;
                }
                noiseMap[x, y] = noiseHeight + upperHeight;
            }
        }

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