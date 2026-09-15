using UnityEngine;

public class TextureGenerator
{
    // ===== СТАРЫЕ (не трогаем) =====
    public static Texture2D TextureFromColourMap(Color[] colourMap, int width, int height)
    {
        Texture2D texture = new Texture2D(width, height);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(colourMap);
        texture.Apply();
        return texture;
    }

    public static Texture2D TextureFromHeightMap(float[,] heightMap)
    {
        int width = heightMap.GetLength(0);
        int height = heightMap.GetLength(1);

        Texture2D texture = new Texture2D(width, height);

        Color[] colourMap = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                colourMap[y * width + x] = Color.Lerp(Color.black, Color.white, heightMap[x, y]);
            }
        }
        return TextureFromColourMap(colourMap, width, height);
    }

    // ===== НОВЫЙ =====
    public static Texture2D TextureFromNoiseMap(float[,] noiseMap, float minNoise, float maxNoise)
    {
        int width = noiseMap.GetLength(0);
        int height = noiseMap.GetLength(1);

        Color[] colourMap = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float h = Mathf.InverseLerp(minNoise, maxNoise, noiseMap[x, y]);
                colourMap[y * width + x] = new Color(h, h, h, 1f);
            }
        }

        Texture2D tex = new Texture2D(width, height, TextureFormat.RFloat, false);
        tex.filterMode = FilterMode.Point;      // ⬅️ ИСПРАВЛЕНО
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels(colourMap);
        tex.Apply();
        return tex;
    }
}