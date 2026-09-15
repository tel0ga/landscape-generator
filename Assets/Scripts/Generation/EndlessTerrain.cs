using MarchingBytes;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EndlessTerain : MonoBehaviour
{
    [SerializeField]
    public static float maxViewDst = 150;

    [SerializeField]
    public static float existDst = 2000;

    public Transform viewer;

    const float updateThreshold = 100f;
    const float sqrUpdateThreshold = updateThreshold * updateThreshold;

    const float deleteThreshold = 500f;
    const float sqrDeleteThreshold = deleteThreshold * deleteThreshold;

    public static Vector2 viewerPosition;
    static Vector2 viewerPositionOld;
    static Vector2 viewerPositionOldForDelete;

    static MapGenerator mapGenerator;
    int chunkSize;
    int chunksVisibleInViewDst;

    // ⬅️ ДВА материала вместо одного
    public Material mapMaterial;         // для PixelColours
    public Material shaderMapMaterial;   // ⬅️ НОВОЕ: для ShaderTextures

    public static int treeSpacing = 15;

    static Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    static List<TerrainChunk> terrainChunksVisibleLastUpdate = new List<TerrainChunk>();

    private void Start()
    {
        mapGenerator = FindAnyObjectByType<MapGenerator>();
        chunkSize = MapGenerator.mapChunkSize - 1;
        chunksVisibleInViewDst = Mathf.RoundToInt(maxViewDst / chunkSize);
        UpdateVisibleChunks();
    }

    private void Update()
    {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.y);
        if ((viewerPosition - viewerPositionOld).sqrMagnitude > sqrUpdateThreshold)
        {
            viewerPositionOld = viewerPosition;
            UpdateVisibleChunks();
        }
        if ((viewerPosition - viewerPositionOldForDelete).sqrMagnitude > sqrDeleteThreshold)
        {
            viewerPositionOldForDelete = viewerPosition;
            UpdateExistingChunks();
        }
    }

    void UpdateVisibleChunks()
    {
        Dictionary<Vector2, TerrainChunk> chunksForDelete = new Dictionary<Vector2, TerrainChunk>();
        for (int i = 0; i < terrainChunksVisibleLastUpdate.Count; i++)
        {
            chunksForDelete.Add(terrainChunksVisibleLastUpdate[i].position, terrainChunksVisibleLastUpdate[i]);
        }
        terrainChunksVisibleLastUpdate.Clear();
        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

        for (int yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++)
        {
            for (int xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++)
            {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                if (terrainChunkDictionary.ContainsKey(viewedChunkCoord))
                {
                    terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
                    if (chunksForDelete.ContainsKey(viewedChunkCoord))
                    {
                        chunksForDelete.Remove(viewedChunkCoord);
                    }
                }
                else
                {
                    // ⬅️ Выбираем материал по режиму
                    Material mat = (mapGenerator.renderMode == MapGenerator.RenderMode.PixelColours)
                        ? mapMaterial
                        : shaderMapMaterial;

                    terrainChunkDictionary.Add(
                        viewedChunkCoord,
                        new TerrainChunk(viewedChunkCoord, chunkSize, transform, mat)
                    );
                }
            }
        }
        foreach (Vector2 v in chunksForDelete.Keys.ToArray())
        {
            chunksForDelete[v].UpdateTerrainChunk();
        }
        chunksForDelete.Clear();
    }

    void UpdateExistingChunks()
    {
        foreach (Vector2 chunkPosition in terrainChunkDictionary.Keys.ToList())
        {
            TerrainChunk chunk = terrainChunkDictionary[chunkPosition];
            if (!chunk.Exists())
            {
                terrainChunkDictionary.Remove(chunkPosition);
                chunk.Delete();
            }
        }
    }

    public class TerrainChunk
    {
        GameObject meshObject;
        public Vector2 position;
        Bounds bounds;
        MeshRenderer meshRenderer;
        MeshFilter meshFilter;
        NatureObject[] natureObjects;
        GameObject[] natureGameObjects;
        public bool hasMapData = false;

        public TerrainChunk(Vector2 coord, int size, Transform parent, Material material)
        {
            position = coord * size;
            bounds = new Bounds(position, Vector2.one * size);
            Vector3 positionV3 = new Vector3(position.x, position.y, 0);

            meshObject = new GameObject("Terrain Chunk");
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();

            // ⬅️ sharedMaterial вместо material — работает с MaterialPropertyBlock
            meshRenderer.sharedMaterial = material;

            meshObject.transform.position = positionV3;
            meshObject.transform.parent = parent;
            SetVisible(false);

            mapGenerator.RequestMapData(position, OnMapDataRecieved);
        }

        void OnMapDataRecieved(MapData mapData)
        {
            mapGenerator.RequestMeshData(mapData, OnMeshDataRecieved);

            // ⬅️ РАЗВИЛКА по режиму
            if (mapGenerator.renderMode == MapGenerator.RenderMode.PixelColours)
            {
                // ===== СТАРАЯ СИСТЕМА =====
                Texture2D texture = TextureGenerator.TextureFromColourMap(
                    mapData.colourMap,
                    MapGenerator.mapChunkSize,
                    MapGenerator.mapChunkSize
                );

                MaterialPropertyBlock props = new MaterialPropertyBlock();
                meshRenderer.GetPropertyBlock(props);
                props.SetTexture("_BaseMap", texture); // URP Lit
                // Если Built-in — "_MainTex"
                meshRenderer.SetPropertyBlock(props);
            }
            else
            {
                // ===== НОВАЯ СИСТЕМА =====
                Texture2D noiseTex = TextureGenerator.TextureFromNoiseMap(
                    mapData.heightMap,
                    mapData.minNoise,
                    mapData.maxNoise
                );

                MaterialPropertyBlock props = new MaterialPropertyBlock();
                meshRenderer.GetPropertyBlock(props);
                props.SetTexture("_NoiseMap", noiseTex);
                props.SetFloat("_NoiseMin", mapData.minNoise);
                props.SetFloat("_NoiseMax", mapData.maxNoise);

                float halfSize = (MapGenerator.mapChunkSize - 1) / 2f;
                Vector4 chunkOrigin = new Vector4(position.x - halfSize, position.y - halfSize, 0, 0);
                props.SetVector("_ChunkOrigin", chunkOrigin);
                props.SetFloat("_ChunkSize", MapGenerator.mapChunkSize - 1);

                meshRenderer.SetPropertyBlock(props);
            }

            // Деревья
            natureObjects = ObjectsGenerator.GenerateObjects(mapData.heightMap, 20, position, treeSpacing);
            natureGameObjects = new GameObject[natureObjects.Length];

            hasMapData = true;
            UpdateTerrainChunk();
        }

        void OnMeshDataRecieved(MeshData meshData)
        {
            meshFilter.mesh = meshData.CreateMesh();
        }

        public void UpdateTerrainChunk()
        {
            float viewerDstFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
            bool visible = viewerDstFromNearestEdge <= maxViewDst;

            if (natureGameObjects.Length != 0)
            {
                if (visible && natureGameObjects[0] == null)
                {
                    for (int i = 0; i < natureObjects.Length; i++)
                    {
                        natureGameObjects[i] = EasyObjectPool.instance.GetObjectFromPool(
                            natureObjects[i].objectName,
                            natureObjects[i].position,
                            Quaternion.identity
                        );
                    }
                }
                else if (!visible && natureGameObjects[0] != null)
                {
                    for (int i = 0; i < natureGameObjects.Length; i++)
                    {
                        if (natureGameObjects[i] != null)
                        {
                            EasyObjectPool.instance.ReturnObjectToPool(natureGameObjects[i]);
                            natureGameObjects[i] = null;
                        }
                    }
                }
            }

            if (visible)
            {
                if (!terrainChunksVisibleLastUpdate.Contains(this))
                {
                    terrainChunksVisibleLastUpdate.Add(this);
                }
            }
            SetVisible(visible);
        }

        public bool Exists()
        {
            float viewerDstFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
            return viewerDstFromNearestEdge <= existDst;
        }

        public void Delete()
        {
            Destroy(meshObject);
        }

        public void SetVisible(bool visible)
        {
            meshObject.SetActive(visible);
        }

        public bool IsVisible()
        {
            return meshObject.activeSelf;
        }
    }
}