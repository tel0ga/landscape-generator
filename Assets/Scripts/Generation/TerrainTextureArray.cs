using UnityEngine;

public class TerrainTextureArray : MonoBehaviour
{
    [Header("Слои (порядок важен!)")]
    [Tooltip("0 = Deep Water, 1 = Water, 2 = Wet Sand, 3 = Sand, 4 = Grass, 5 = Dark Grass, 6 = Stone, 7 = Dark Stone, 8 = Ice")]
    public Texture2D[] layers = new Texture2D[9];

    [Header("Размер текстур (все должны быть одного размера)")]
    public int textureSize = 128;

    private Texture2DArray textureArray;

    void Awake()
    {
        CreateTextureArray();
    }

    void CreateTextureArray()
    {
        // Проверка размеров
        foreach (var tex in layers)
        {
            if (tex == null)
            {
                Debug.LogError($"Слой не назначен!");
                return;
            }
            if (tex.width != layers[0].width || tex.height != layers[0].height)
            {
                Debug.LogError($"Текстура {tex.name} имеет размер {tex.width}×{tex.height}, а должна {layers[0].width}×{layers[0].height}");
                return;
            }
        }

        int size = layers[0].width;

        // Создаём Texture2DArray
        textureArray = new Texture2DArray(
            size, size, layers.Length,
            TextureFormat.RGBA32,  // можно RGBA32 или DXT5 для сжатия
            false,                 // mipmaps
            false                  // linear
        );

        textureArray.filterMode = FilterMode.Bilinear;
        textureArray.wrapMode = TextureWrapMode.Repeat;

        // Копируем пиксели
        for (int i = 0; i < layers.Length; i++)
        {
            // Нужно Read/Write Enabled!
            Color[] pixels = layers[i].GetPixels();
            textureArray.SetPixels(pixels, i, 0);
        }

        textureArray.Apply();
    }

    public Texture2DArray GetArray() => textureArray;
    public int LayerCount => layers.Length;
}