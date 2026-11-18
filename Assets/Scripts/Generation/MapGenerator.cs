using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public enum DrawMode { NoiseMap, ColourMap, Mesh };
    public DrawMode drawMode;

    public enum RenderMode { PixelColours, ShaderTextures }
    [Header("Render Mode")]
    public RenderMode renderMode = RenderMode.PixelColours;

    public const int mapChunkSize = 121;
    int levelOfDetail = (mapChunkSize - 1) / 2;

    public const float worldChunkSize = 15f;
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

    [Header("Snow")]
    public float snowScale = 0.03f;
    public int snowSeedOffset = 3000;
    [Range(0, 64)]
    public int snowPadding = 20;   // ⬅️ запас по краям

    const int maxCallbacksPerQueuePerFrame = 8;
    readonly object queueLock = new object();
    bool isDestroyed;
    readonly Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
    readonly Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

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
            display.DrawMesh(
                MeshGenerator.GenerateTerrainMesh(mapData.heightMap, levelOfDetail, MapGenerator.PixelsPerUnit),
                TextureGenerator.TextureFromColourMap(mapData.colourMap, mapChunkSize, mapChunkSize)
            );
        }
    }

    public void RequestMapData(Vector2 centre, System.Action<MapData> callback)
    {
        lock (queueLock)
        {
            if (isDestroyed) return;
        }
        ThreadStart threadStart = delegate { MapDataThread(centre, callback); };
        new Thread(threadStart) { IsBackground = true }.Start();
    }

    void MapDataThread(Vector2 centre, System.Action<MapData> callback)
    {
        MapData mapData = GenerateMapData(centre);
        lock (queueLock)
        {
            if (isDestroyed) return;
            mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
        }
    }

    public void RequestMeshData(MapData mapData, System.Action<MeshData> callback)
    {
        lock (queueLock)
        {
            if (isDestroyed) return;
        }
        ThreadStart threadStart = delegate { MeshDataThread(mapData, callback); };
        new Thread(threadStart) { IsBackground = true }.Start();
    }

    void MeshDataThread(MapData mapData, System.Action<MeshData> callback)
    {
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(
            mapData.heightMap, levelOfDetail, MapGenerator.PixelsPerUnit);
        lock (queueLock)
        {
            if (isDestroyed) return;
            meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }
    }

    private void Update()
    {
        ProcessQueue(mapDataThreadInfoQueue);
        ProcessQueue(meshDataThreadInfoQueue);
    }

    void ProcessQueue<T>(Queue<MapThreadInfo<T>> queue)
    {
        for (int i = 0; i < maxCallbacksPerQueuePerFrame; i++)
        {
            MapThreadInfo<T> threadInfo;
            lock (queueLock)
            {
                if (isDestroyed || queue.Count == 0) return;
                threadInfo = queue.Dequeue();
            }

            // Callbacks create Unity objects and must run on the main thread, outside the lock.
            try
            {
                threadInfo.callback(threadInfo.parameter);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    void OnDestroy()
    {
        lock (queueLock)
        {
            isDestroyed = true;
            mapDataThreadInfoQueue.Clear();
            meshDataThreadInfoQueue.Clear();
        }
    }

    public MapData GenerateMapData(Vector2 centre)
    {
        return GenerateMapData(centre, 1);
    }

    public MapData GenerateMapData(Vector2 centre, int scale = 1)
    {
        int res = mapChunkSize * scale;
        int snowRes = res + snowPadding * 2;   // ⬅️ снег с запасом

        // --- 1. Высоты ---
        float[,] noiseMap = Noise.GenerateNoiseMap(
            res, res,
            seed, noiseScale, octaves, persistance, lacunarity,
            centre + offset, upperHeight,
            out float minNoise, out float maxNoise,
            MapGenerator.PixelsPerUnit
        );

        // --- 2. Снег с padding ---
        // Генерируем больше, чтобы на границах чанков сэмплы тени/нормалей
        // не упирались в Clamp-край текстуры.
        float[,] snowMap = Noise.GenerateNoiseMap(
            snowRes, snowRes,
            seed + snowSeedOffset, snowScale, 3, 0.5f, 2f,
            centre + offset, 0f,
            out _, out _,
            MapGenerator.PixelsPerUnit
        );

        for (int y = 0; y < snowRes; y++)
            for (int x = 0; x < snowRes; x++)
                snowMap[x, y] = Mathf.Clamp01(snowMap[x, y] * 0.5f + 0.5f);

        // --- 3. Colour map ---
        Color[] colourMap = new Color[res * res];

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float currentHeight = noiseMap[x, y];
                for (int i = 0; i < regions.Length; i++)
                {
                    if (currentHeight <= regions[i].height)
                    {
                        colourMap[y * res + x] = regions[i].colour;
                        break;
                    }
                }
            }
        }

        return new MapData(noiseMap, snowMap, colourMap, minNoise, maxNoise, snowRes);
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
    public float[,] snowMap;
    public Color[] colourMap;
    public float minNoise;
    public float maxNoise;
    public int snowRes;   // ⬅️ новое

    public MapData(float[,] heightMap, float[,] snowMap, Color[] colourMap, float minNoise, float maxNoise, int snowRes)
    {
        this.heightMap = heightMap;
        this.snowMap = snowMap;
        this.colourMap = colourMap;
        this.minNoise = minNoise;
        this.maxNoise = maxNoise;
        this.snowRes = snowRes;
    }
}
