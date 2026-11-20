using MarchingBytes;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EndlessTerain : MonoBehaviour
{
    [SerializeField]
    public static float maxViewDst = 45;

    [SerializeField]
    public static float existDst = 1000;

    public Transform viewer;

    const float updateThreshold = 10f;
    const float sqrUpdateThreshold = updateThreshold * updateThreshold;

    const float deleteThreshold = 500f;
    const float sqrDeleteThreshold = deleteThreshold * deleteThreshold;

    public Vector2 viewerPosition;
    Vector2 viewerPositionOld;
    Vector2 viewerPositionOldForDelete;

    MapGenerator mapGenerator;
    SnowTracks.SnowTrackWorld snowTracks;
    public SnowTracks.SnowTrackMode snowTrackMode = SnowTracks.SnowTrackMode.FullSnow;
    int chunkSize;
    int chunksVisibleX, chunksVisibleY;
    float viewRadius;
    float canopyMargin = 12f;

    public Material mapMaterial;         // для PixelColours
    public Material shaderMapMaterial;   // для ShaderTextures

    public static int treeSpacing = 30;

    readonly Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    readonly List<TerrainChunk> terrainChunksVisibleLastUpdate = new List<TerrainChunk>();

    [SerializeField]
    public static bool snowFootprints = false;
    void Start()
    {
        mapGenerator = FindAnyObjectByType<MapGenerator>();
        if (mapGenerator.renderMode == MapGenerator.RenderMode.ShaderTextures && shaderMapMaterial != null && snowTrackMode != SnowTracks.SnowTrackMode.Off)
        {
            snowTracks = GetComponent<SnowTracks.SnowTrackWorld>();
            if (snowTracks == null) snowTracks = gameObject.AddComponent<SnowTracks.SnowTrackWorld>();
            snowTracks.SetMode(snowTrackMode);
            snowTracks.Initialize(shaderMapMaterial);
        }
        chunkSize = Mathf.RoundToInt(MapGenerator.worldChunkSize);
        var pool = FindAnyObjectByType<EasyObjectPool>();
        if (pool != null && pool.poolInfo != null)
            foreach (var entry in pool.poolInfo)
                if (entry.prefab != null)
                    foreach (var renderer in entry.prefab.GetComponentsInChildren<SpriteRenderer>(true))
                        canopyMargin = Mathf.Max(canopyMargin, ((Vector2)(renderer.bounds.center - entry.prefab.transform.position)).magnitude + ((Vector2)renderer.bounds.extents).magnitude);
        UpdateViewRadius();
        viewerPosition = new Vector2(viewer.position.x, viewer.position.y);
        viewerPositionOld = viewerPosition;
        viewerPositionOldForDelete = viewerPosition;
        UpdateVisibleChunks();
    }

    void OnDestroy()
    {
        foreach (TerrainChunk chunk in terrainChunkDictionary.Values)
            chunk.Delete();
        terrainChunkDictionary.Clear();
        terrainChunksVisibleLastUpdate.Clear();
    }

    private void Update()
    {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.y);
        float previousRadius = viewRadius;
        UpdateViewRadius();
        if ((viewerPosition - viewerPositionOld).sqrMagnitude > sqrUpdateThreshold || Mathf.Abs(previousRadius - viewRadius) > .1f)
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

    void UpdateViewRadius()
    {
        var camera = Camera.main;
        viewRadius = maxViewDst;
        if (camera != null && camera.orthographic)
            viewRadius = Mathf.Max(viewRadius, camera.orthographicSize * Mathf.Sqrt(1 + camera.aspect * camera.aspect));
        viewRadius += canopyMargin + updateThreshold;
        chunksVisibleX = Mathf.CeilToInt(viewRadius / chunkSize) + 1;
        chunksVisibleY = Mathf.CeilToInt(viewRadius / (chunkSize * WorldProjection.VerticalScale)) + 1;
    }

    void UpdateVisibleChunks()
    {
        Dictionary<Vector2, TerrainChunk> chunksForDelete = new Dictionary<Vector2, TerrainChunk>();
        for (int i = 0; i < terrainChunksVisibleLastUpdate.Count; i++)
        {
            chunksForDelete.Add(terrainChunksVisibleLastUpdate[i].coord, terrainChunksVisibleLastUpdate[i]);
        }
        terrainChunksVisibleLastUpdate.Clear();
        int currentChunkCoordX = Mathf.RoundToInt(WorldProjection.ToMap(viewerPosition).x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(WorldProjection.ToMap(viewerPosition).y / chunkSize);

        for (int yOffset = -chunksVisibleY; yOffset <= chunksVisibleY; yOffset++)
        {
            for (int xOffset = -chunksVisibleX; xOffset <= chunksVisibleX; xOffset++)
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
                    if (WorldProjection.ProjectBounds(viewedChunkCoord * chunkSize, Vector2.one * chunkSize).SqrDistance(viewerPosition) > viewRadius * viewRadius)
                        continue;
                    Material mat = (mapGenerator.renderMode == MapGenerator.RenderMode.PixelColours)
                        ? mapMaterial
                        : shaderMapMaterial;

                    terrainChunkDictionary.Add(
                        viewedChunkCoord,
                        new TerrainChunk(viewedChunkCoord, chunkSize, transform, mat, this)
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
        public readonly Vector2Int coord;
        public readonly Vector2 mapCentre;
        public Vector2 position => WorldProjection.ToWorld(mapCentre);
        Bounds bounds;
        MeshRenderer meshRenderer;
        MeshFilter meshFilter;
        readonly EndlessTerain owner;
        NatureObject[] natureObjects = System.Array.Empty<NatureObject>();
        GameObject[] natureGameObjects = System.Array.Empty<GameObject>();
        EasyObjectPool naturePool;
        Texture2D colourTexture;
        Texture2D noiseTexture;
        Texture2D snowTexture;
        Mesh mesh;
        bool hasMeshData;
        bool isDeleted;
        public bool hasMapData = false;

        public TerrainChunk(Vector2 coord, int size, Transform parent, Material material, EndlessTerain owner)
        {
            this.owner = owner;
            this.coord = Vector2Int.RoundToInt(coord);
            mapCentre = coord * size;
            bounds = WorldProjection.ProjectBounds(mapCentre, Vector2.one * size);
            Vector3 positionV3 = new Vector3(position.x, position.y, 0);

            meshObject = new GameObject("Terrain Chunk");
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            // Reserve the bottom order for terrain; ground effects occupy negative orders above it.
            meshRenderer.sortingOrder = short.MinValue;
            meshFilter = meshObject.AddComponent<MeshFilter>();

            meshRenderer.sharedMaterial = material;

            meshObject.transform.position = positionV3;
            meshObject.transform.parent = parent;
            SetVisible(false);

            owner.mapGenerator.RequestMapData(mapCentre, OnMapDataRecieved);
        }

        void OnMapDataRecieved(MapData mapData)
        {
            if (isDeleted || owner == null || meshObject == null) return;
            if (hasMapData) return;

            owner.mapGenerator.RequestMeshData(mapData, OnMeshDataRecieved);

            if (owner.mapGenerator.renderMode == MapGenerator.RenderMode.PixelColours)
            {
                // ===== PixelColours =====
                colourTexture = TextureGenerator.TextureFromColourMap(
                    mapData.colourMap,
                    MapGenerator.mapChunkSize,
                    MapGenerator.mapChunkSize
                );

                MaterialPropertyBlock props = new MaterialPropertyBlock();
                meshRenderer.GetPropertyBlock(props);
                props.SetTexture("_BaseMap", colourTexture);
                meshRenderer.SetPropertyBlock(props);
            }
            else
            {
                // ===== ShaderTextures =====
                noiseTexture = TextureGenerator.TextureFromNoiseMap(
                    mapData.heightMap,
                    mapData.minNoise,
                    mapData.maxNoise
                );

                snowTexture = TextureGenerator.TextureFromFloatMap(mapData.snowMap);

                MaterialPropertyBlock props = new MaterialPropertyBlock();
                meshRenderer.GetPropertyBlock(props);
                props.SetTexture("_NoiseMap", noiseTexture);
                props.SetTexture("_SnowMap", snowTexture);
                props.SetFloat("_NoiseMin", mapData.minNoise);
                props.SetFloat("_NoiseMax", mapData.maxNoise);

                // ⬅️ padding снега
                props.SetFloat("_SnowTexelSize", 1.0f / mapData.snowRes);
                props.SetFloat("_SnowPadding", (float)owner.mapGenerator.snowPadding / mapData.snowRes);

                float halfSize = MapGenerator.worldChunkSize / 2f;
                Vector4 chunkOrigin = new Vector4(
                    mapCentre.x - halfSize,
                    mapCentre.y - halfSize, 0, 0);
                props.SetVector("_ChunkOrigin", chunkOrigin);
                props.SetFloat("_ChunkSize", MapGenerator.worldChunkSize);

                meshRenderer.SetPropertyBlock(props);
                owner.snowTracks?.Register(coord, mapData, owner.mapGenerator.snowPadding, meshRenderer);
            }

            // Деревья
            natureObjects = ObjectsGenerator.GenerateObjects(mapData.heightMap, 2, mapCentre, treeSpacing, MapGenerator.PixelsPerUnit, owner.mapGenerator.seed);
            natureGameObjects = new GameObject[natureObjects.Length];

            hasMapData = true;
            UpdateTerrainChunk();
        }

        void OnMeshDataRecieved(MeshData meshData)
        {
            if (isDeleted || owner == null || meshObject == null || hasMeshData) return;
            mesh = meshData.CreateMesh();
            meshFilter.sharedMesh = mesh;
            hasMeshData = true;
            UpdateTerrainChunk();
        }

        public void UpdateTerrainChunk()
        {
            if (isDeleted || owner == null || meshObject == null) return;
            float viewerDstFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(owner.viewerPosition));
            bool visible = hasMapData && hasMeshData && viewerDstFromNearestEdge <= owner.viewRadius;

            if (natureGameObjects.Length != 0)
            {
                if (visible)
                {
                    if (naturePool == null) naturePool = EasyObjectPool.instance;
                    for (int i = 0; i < natureObjects.Length; i++)
                    {
                        if (natureGameObjects[i] != null || naturePool == null) continue;
                        var world = SurvivalCombat.CombatWorld.Current;
                        if (world != null && world.IsDestroyed(natureObjects[i].stableId)) continue;
                        natureGameObjects[i] = naturePool.GetObjectFromPool(
                            natureObjects[i].objectName,
                            WorldProjection.ToWorld(natureObjects[i].position),
                            Quaternion.identity
                        );
                        if (natureGameObjects[i] != null)
                        {
                            if (snowFootprints)
                            {
                                ProjectedFootprint.Apply(natureGameObjects[i]);
                            }
                            GroundSorting.Apply(natureGameObjects[i]);
                        }
                        if (natureGameObjects[i] != null && world != null)
                        {
                            var persistent = natureGameObjects[i].GetComponent<SurvivalCombat.PersistentDestructible>() ?? natureGameObjects[i].AddComponent<SurvivalCombat.PersistentDestructible>();
                            persistent.Bind(natureObjects[i].stableId);
                        }
                    }
                }
                else
                {
                    ReturnNatureObjects();
                }
            }

            if (visible)
            {
                if (!owner.terrainChunksVisibleLastUpdate.Contains(this))
                {
                    owner.terrainChunksVisibleLastUpdate.Add(this);
                }
            }
            SetVisible(visible);
        }

        public bool Exists()
        {
            if (isDeleted || owner == null) return false;
            float viewerDstFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(owner.viewerPosition));
            return viewerDstFromNearestEdge <= existDst;
        }

        public void Delete()
        {
            if (isDeleted) return;
            isDeleted = true;
            owner.snowTracks?.Unregister(coord);
            owner.terrainChunksVisibleLastUpdate.Remove(this);
            ReturnNatureObjects();
            if (meshObject != null) meshObject.SetActive(false);
            if (meshFilter != null) meshFilter.sharedMesh = null;
            if (meshRenderer != null) meshRenderer.SetPropertyBlock(null);
            Destroy(colourTexture);
            Destroy(noiseTexture);
            Destroy(snowTexture);
            Destroy(mesh);
            Destroy(meshObject);
            colourTexture = null;
            noiseTexture = null;
            snowTexture = null;
            mesh = null;
            meshObject = null;
            natureObjects = System.Array.Empty<NatureObject>();
            natureGameObjects = System.Array.Empty<GameObject>();
        }

        void ReturnNatureObjects()
        {
            for (int i = 0; i < natureGameObjects.Length; i++)
            {
                if (natureGameObjects[i] == null) continue;
                if (naturePool != null)
                    naturePool.ReturnObjectToPool(natureGameObjects[i]);
                else
                    Destroy(natureGameObjects[i]);
                natureGameObjects[i] = null;
            }
        }

        public void SetVisible(bool visible)
        {
            if (isDeleted || meshObject == null) return;
            meshObject.SetActive(visible && hasMapData && hasMeshData);
            owner.snowTracks?.SetVisible(coord, visible && hasMapData && hasMeshData);
        }

        public bool IsVisible()
        {
            return !isDeleted && meshObject != null && meshObject.activeSelf;
        }
    }
}
