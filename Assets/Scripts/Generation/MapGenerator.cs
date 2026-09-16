using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public enum DrawMode { NoiseMap, ColourMap, Mesh };
    public DrawMode drawMode;

    // ⬅️ НОВОЕ: переключатель рендера
    public enum RenderMode { PixelColours, ShaderTextures }
    [Header("Render Mode")]
    public RenderMode renderMode = RenderMode.PixelColours;

    public const int mapChunkSize = 121;
    int levelOfDetail = (mapChunkSize - 1) / 2;

    public const float worldChunkSize = 15f; // размер чанка в юнитах
    public static float PixelsPerUnit => (mapChunkSize - 1) / worldChunkSize;

    public float noiseScale;

    public int octaves;
    [Range(0, 1)]
    public float persistance;
    public float lacunarity;

    public float riverDepth = 1f;

    public int seed;
    public Vector2 offset;
    public float upperHeight = 0f;

    public bool autoUpdate;

    public TerrainType[] regions;

    Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
    Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

    public void DrawMapInEditor()
    {
        MapData mapData = GenerateMapData(Vector2.zero);
        MapDisplay display = FindAnyObjectByType<MapDisplay>();
        if (drawMode == DrawMode.NoiseMap)
        {
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
        }
        else if (drawMode == DrawMode.ColourMap)
        {
            display.DrawTexture(TextureGenerator.TextureFromColourMap(mapData.colourMap, mapChunkSize, mapChunkSize));
        }
        else if (drawMode == DrawMode.Mesh)
        {
            display.DrawMesh(MeshGenerator.GenerateTerrainMesh(mapData.heightMap, levelOfDetail, MapGenerator.PixelsPerUnit), TextureGenerator.TextureFromColourMap(mapData.colourMap, mapChunkSize, mapChunkSize));
        }
    }

    public void RequestMapData(Vector2 centre, System.Action<MapData> callback)
    {
        ThreadStart threadStart = delegate
        {
            MapDataThread(centre, callback);
        };
        new Thread(threadStart).Start();
    }

    void MapDataThread(Vector2 centre, System.Action<MapData> callback)
    {
        MapData mapData = GenerateMapData(centre);
        lock (mapDataThreadInfoQueue)
        {
            mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
        }
    }

    public void RequestMeshData(MapData mapData, System.Action<MeshData> callback)
    {
        ThreadStart threadStart = delegate
        {
            MeshDataThread(mapData, callback);
        };
        new Thread(threadStart).Start();
    }

    void MeshDataThread(MapData mapData, System.Action<MeshData> callback)
    {
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(mapData.heightMap, levelOfDetail, MapGenerator.PixelsPerUnit);
        lock (meshDataThreadInfoQueue)
        {
            meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }
        ;
    }

    private void Update()
    {
        if (mapDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < mapDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }
        if (meshDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < meshDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }
    }

    public MapData GenerateMapData(Vector2 centre)
    {
        // ⬅️ НОВОЕ: получаем min/max через out-параметры
        float[,] noiseMap = Noise.GenerateNoiseMap(
            mapChunkSize, mapChunkSize, seed, noiseScale, octaves, persistance, lacunarity,
            centre + offset, upperHeight,
            out float minNoise, out float maxNoise,
            MapGenerator.PixelsPerUnit        // ⬅️ НОВОЕ
        );
        /*float[,] riverMap = Noise.GenerateNoiseMap(mapChunkSize, mapChunkSize, seed + 999,                        // РЕКИИИ
            noiseScale * 1, 4, persistance, lacunarity, centre + offset,0f,out _, out _);

        for (int y = 0; y < mapChunkSize; y++)
        {
            for (int x = 0; x < mapChunkSize; x++)
            {
                float riverNoise = Mathf.Abs(riverMap[x, y] - 0.5f) * 2f;
                riverNoise = 1f - riverNoise;
                riverNoise = Mathf.Pow(riverNoise, 8f);
                noiseMap[x, y] -= riverNoise * riverDepth;
            }
        }
        */
        Color[] colourMap = new Color[mapChunkSize * mapChunkSize];

        for (int y = 0; y < mapChunkSize; y++)
        {
            for (int x = 0; x < mapChunkSize; x++)
            {
                float currentHeight = noiseMap[x, y];
                for (int i = 0; i < regions.Length; i++)
                {
                    if (currentHeight <= regions[i].height)
                    {
                        colourMap[y * mapChunkSize + x] = regions[i].colour;
                        break;
                    }
                }
            }
        }

        return new MapData(noiseMap, colourMap, minNoise, maxNoise);  // ⬅️ добавили min/max
    }


    public MapData GenerateMapData(Vector2 centre, int scale = 1)
    {
        // ⬅️ НОВОЕ: получаем min/max через out-параметры
        float[,] noiseMap = Noise.GenerateNoiseMap(
            mapChunkSize * scale, mapChunkSize * scale, seed, noiseScale, octaves, persistance, lacunarity,
            centre + offset, upperHeight,
            out float minNoise, out float maxNoise,
            MapGenerator.PixelsPerUnit        // ⬅️ НОВОЕ
        );
        /*float[,] riverMap = Noise.GenerateNoiseMap(mapChunkSize, mapChunkSize, seed + 999,                        // РЕКИИИ
            noiseScale * 1, 4, persistance, lacunarity, centre + offset,0f,out _, out _);

        for (int y = 0; y < mapChunkSize; y++)
        {
            for (int x = 0; x < mapChunkSize; x++)
            {
                float riverNoise = Mathf.Abs(riverMap[x, y] - 0.5f) * 2f;
                riverNoise = 1f - riverNoise;
                riverNoise = Mathf.Pow(riverNoise, 8f);
                noiseMap[x, y] -= riverNoise * riverDepth;
            }
        }
        */
        Color[] colourMap = new Color[mapChunkSize * mapChunkSize * scale * scale];

        for (int y = 0; y < mapChunkSize * scale; y++)
        {
            for (int x = 0; x < mapChunkSize * scale; x++)
            {
                float currentHeight = noiseMap[x, y];
                for (int i = 0; i < regions.Length; i++)
                {
                    if (currentHeight <= regions[i].height)
                    {
                        colourMap[y * mapChunkSize * scale + x] = regions[i].colour;
                        break;
                    }
                }
            }
        }

        return new MapData(noiseMap, colourMap, minNoise, maxNoise);  // ⬅️ добавили min/max
    }

    private void OnValidate()
    {
        if (lacunarity < 1) lacunarity = 1;
        if (octaves < 0) octaves = 0;
    }

    struct MapThreadInfo<T>
    {
        public readonly System.Action<T> callback;
        public readonly T parameter;
        public MapThreadInfo(System.Action<T> callback, T parameter)
        {
            this.callback = callback;
            this.parameter = parameter;
        }
    }
}

[System.Serializable]
public struct TerrainType
{
    public string name;
    public float height;
    public Color colour;
}

public struct MapData
{
    public float[,] heightMap;
    public Color[] colourMap;
    public float minNoise;   // ⬅️ НОВОЕ
    public float maxNoise;   // ⬅️ НОВОЕ

    public MapData(float[,] heightMap, Color[] colourMap, float minNoise, float maxNoise)
    {
        this.heightMap = heightMap;
        this.colourMap = colourMap;
        this.minNoise = minNoise;
        this.maxNoise = maxNoise;
    }
}