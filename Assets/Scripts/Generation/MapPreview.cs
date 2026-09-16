using UnityEngine;

[ExecuteAlways]
public class MapPreview : MonoBehaviour
{
    public MapGenerator generator;
    public Material targetMaterial;
    public int previewSize = 1;

    private float lastNoiseScale;
    private int lastSeed;
    private Vector2 lastOffset;
    private int lastOctaves;
    private float lastPersistance;
    private float lastLacunarity;
    private float lastUpperHeight;
    private int lastPreviewSize;
    private int lastMapChunkSize;
    private float lastWorldChunkSize;

    private Texture2D noiseTex;

    void OnEnable() => Regenerate();

    void Update()
    {
        if (generator == null || targetMaterial == null) return;

        if (generator.noiseScale != lastNoiseScale ||
            generator.seed != lastSeed ||
            generator.offset != lastOffset ||
            generator.octaves != lastOctaves ||
            generator.persistance != lastPersistance ||
            generator.lacunarity != lastLacunarity ||
            generator.upperHeight != lastUpperHeight ||
            previewSize != lastPreviewSize ||
            MapGenerator.mapChunkSize != lastMapChunkSize ||
            MapGenerator.worldChunkSize != lastWorldChunkSize)
        {
            Regenerate();
        }
    }

    void Regenerate()
    {
        if (generator == null || targetMaterial == null) return;

        lastNoiseScale = generator.noiseScale;
        lastSeed = generator.seed;
        lastOffset = generator.offset;
        lastOctaves = generator.octaves;
        lastPersistance = generator.persistance;
        lastLacunarity = generator.lacunarity;
        lastUpperHeight = generator.upperHeight;
        lastPreviewSize = previewSize;
        lastMapChunkSize = MapGenerator.mapChunkSize;
        lastWorldChunkSize = MapGenerator.worldChunkSize;

        int scale = Mathf.Max(1, previewSize);
        int res = MapGenerator.mapChunkSize * scale;

        MapData mapData = generator.GenerateMapData(Vector2.zero, scale);

        if (noiseTex == null || noiseTex.width != res)
        {
            if (noiseTex != null) DestroyImmediate(noiseTex);
            noiseTex = new Texture2D(res, res, TextureFormat.RFloat, false);
            noiseTex.filterMode = FilterMode.Point;
            noiseTex.wrapMode = TextureWrapMode.Clamp;
        }

        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float h = mapData.heightMap[x, y];
                float norm = Mathf.InverseLerp(mapData.minNoise, mapData.maxNoise, h);
                noiseTex.SetPixel(x, y, new Color(norm, 0f, 0f, 1f));
            }
        noiseTex.Apply();

        float worldSize = MapGenerator.worldChunkSize * scale;
        float halfWorld = worldSize * 0.5f;

        targetMaterial.SetTexture("_NoiseMap", noiseTex);
        targetMaterial.SetFloat("_NoiseMin", mapData.minNoise);
        targetMaterial.SetFloat("_NoiseMax", mapData.maxNoise);
        targetMaterial.SetFloat("_ChunkSize", worldSize);
        targetMaterial.SetVector("_ChunkOrigin", new Vector4(-halfWorld, -halfWorld, 0, 0));

        transform.localScale = new Vector3(worldSize, worldSize, 1f);

#if UNITY_EDITOR
        UnityEditor.SceneView.RepaintAll();
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
    }

    void OnDisable()
    {
        if (noiseTex != null)
        {
            if (Application.isPlaying)
                Destroy(noiseTex);
            else
                DestroyImmediate(noiseTex);
            noiseTex = null;
        }
    }
}