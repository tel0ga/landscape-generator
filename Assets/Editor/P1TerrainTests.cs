#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using MarchingBytes;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class P1TerrainTests
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    static object Get(object target, string name) => target.GetType().GetField(name, Fields).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
    static void Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Fields).Invoke(target, args);

    static EnterPlayMode EnterRuntime() => new EnterPlayMode(
        !EditorSettings.enterPlayModeOptionsEnabled ||
        (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0);

    [Test]
    public void ConcurrentResultsRespectBudgetAndStopAfterShutdown()
    {
        var go = new GameObject("Queue test");
        go.SetActive(false);
        var generator = go.AddComponent<MapGenerator>();
        var workers = new List<Thread>();
        try
        {
            object gate = Get(generator, "queueLock");
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            int maps = 0, meshes = 0;
            Action<MapData> mapCallback = _ =>
            {
                Assert.AreEqual(mainThread, Thread.CurrentThread.ManagedThreadId);
                Assert.IsFalse(Monitor.IsEntered(gate));
                maps++;
            };
            Action<MeshData> meshCallback = _ =>
            {
                Assert.AreEqual(mainThread, Thread.CurrentThread.ManagedThreadId);
                Assert.IsFalse(Monitor.IsEntered(gate));
                meshes++;
            };
            // Preload enough results to catch the shrinking-Count loop regression.
            for (int i = 0; i < 10; i++)
            {
                Enqueue(generator, "mapDataThreadInfoQueue", mapCallback, default(MapData));
                Enqueue(generator, "meshDataThreadInfoQueue", meshCallback, new MeshData(2, 2));
            }
            Call(generator, "Update");
            Assert.AreEqual(8, maps);
            Assert.AreEqual(8, meshes);

            for (int worker = 0; worker < 4; worker++)
            {
                var thread = new Thread(() =>
                {
                    for (int i = 0; i < 32; i++)
                    {
                        Enqueue(generator, "mapDataThreadInfoQueue", mapCallback, default(MapData));
                        Enqueue(generator, "meshDataThreadInfoQueue", meshCallback, new MeshData(2, 2));
                    }
                });
                workers.Add(thread);
                thread.Start();
            }
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (maps < 138 || meshes < 138)
            {
                int oldMaps = maps, oldMeshes = meshes;
                Call(generator, "Update");
                Assert.LessOrEqual(maps - oldMaps, 8);
                Assert.LessOrEqual(meshes - oldMeshes, 8);
                Assert.Less(timeout.Elapsed.TotalSeconds, 10);
                Thread.Yield();
            }
            foreach (var worker in workers) worker.Join();
            Call(generator, "Update");
            Assert.AreEqual(138, maps);
            Assert.AreEqual(138, meshes);

            Enqueue(generator, "mapDataThreadInfoQueue", mapCallback, default(MapData));
            Enqueue(generator, "meshDataThreadInfoQueue", meshCallback, new MeshData(2, 2));
            Call(generator, "OnDestroy");
            generator.regions = Array.Empty<TerrainType>();
            generator.snowPadding = 0;
            // Simulate workers finishing after shutdown through the real producer methods.
            Call(generator, "MapDataThread", Vector2.zero, mapCallback);
            Call(generator, "MeshDataThread", new MapData { heightMap = new float[2, 2] }, meshCallback);
            Call(generator, "Update");
            Assert.AreEqual(0, ((ICollection)Get(generator, "mapDataThreadInfoQueue")).Count);
            Assert.AreEqual(0, ((ICollection)Get(generator, "meshDataThreadInfoQueue")).Count);
            Assert.AreEqual(138, maps);
            Assert.AreEqual(138, meshes);
        }
        finally
        {
            foreach (var worker in workers) worker.Join();
            Object.DestroyImmediate(go);
        }
    }

    static void Enqueue<T>(MapGenerator generator, string name, Action<T> callback, T data)
    {
        object queue = Get(generator, name);
        Type itemType = queue.GetType().GetGenericArguments()[0];
        object item = Activator.CreateInstance(itemType, new object[] { callback, data });
        lock (Get(generator, "queueLock"))
            queue.GetType().GetMethod("Enqueue").Invoke(queue, new[] { item });
    }

    [UnityTest]
    public IEnumerator ChunkLifecycleReleasesResourcesAndIgnoresLateResults()
    {
        yield return EnterRuntime();
        var root = new GameObject("Chunk lifecycle test");
        root.SetActive(false);
        var generator = root.AddComponent<MapGenerator>();
        var owner = root.AddComponent<EndlessTerain>();
        Set(owner, "mapGenerator", generator);
        // Deliver results manually, so readiness and deletion races are deterministic.
        Call(generator, "OnDestroy");
        var chunks = (Dictionary<Vector2, EndlessTerain.TerrainChunk>)Get(owner, "terrainChunkDictionary");
        var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
        var previousPool = EasyObjectPool.instance;
        var poolObject = new GameObject("Test pool");
        poolObject.SetActive(false);
        var pool = poolObject.AddComponent<EasyObjectPool>();
        var prefab = new GameObject("Test nature prefab");
        prefab.SetActive(false);
        pool.poolInfo = new[] { new PoolInfo { poolName = "test", prefab = prefab, poolSize = 1 } };
        Call(pool, "Start");
        GameObject pooled = null;
        try
        {
            foreach (MapGenerator.RenderMode mode in Enum.GetValues(typeof(MapGenerator.RenderMode)))
            {
                generator.renderMode = mode;
                var chunk = new EndlessTerain.TerrainChunk(Vector2.zero, 15, root.transform, material, owner);
                chunks.Add(Vector2.zero, chunk);
                chunk.UpdateTerrainChunk();
                chunk.SetVisible(true);
                Assert.IsFalse(chunk.IsVisible());

                int size = MapGenerator.mapChunkSize;
                var data = new MapData(new float[size, size], new float[size, size],
                    new Color[size * size], -1, 1, size);
                Call(chunk, "OnMapDataRecieved", data);
                Assert.IsFalse(chunk.IsVisible(), "Map data alone must not show a chunk.");
                Call(chunk, "OnMeshDataRecieved", MeshGenerator.GenerateTerrainMesh(data.heightMap, 0, 8));
                Assert.IsTrue(chunk.IsVisible());
                var mesh = (Mesh)Get(chunk, "mesh");
                var textures = new[] { (Texture2D)Get(chunk, "colourTexture"),
                    (Texture2D)Get(chunk, "noiseTexture"), (Texture2D)Get(chunk, "snowTexture") };
                Assert.AreEqual(mode == MapGenerator.RenderMode.PixelColours ? 1 : 2,
                    Array.FindAll(textures, texture => texture != null).Length);
                pooled = pool.GetObjectFromPool("test", Vector3.zero, Quaternion.identity);
                Set(chunk, "naturePool", pool);
                Set(chunk, "natureGameObjects", new[] { pooled });
                chunk.Delete();
                chunk.Delete();
                Call(chunk, "OnMapDataRecieved", data);
                Call(chunk, "OnMeshDataRecieved", new MeshData(2, 2));
                chunk.UpdateTerrainChunk();
                Assert.IsFalse(chunk.IsVisible());
                Assert.IsFalse(pooled.activeSelf);
                Assert.IsTrue(pooled.GetComponent<PoolObject>().isPooled);
                Assert.AreEqual(0, ((ICollection)Get(owner, "terrainChunksVisibleLastUpdate")).Count);
                yield return null;
                Assert.IsTrue(mesh == null);
                foreach (var texture in textures) Assert.IsTrue(texture == null);
                chunks.Clear();

                // Delete before either result and between the map and mesh results.
                for (int stage = 0; stage < 2; stage++)
                {
                    var pending = new EndlessTerain.TerrainChunk(Vector2.zero, 15, root.transform, material, owner);
                    chunks.Add(Vector2.zero, pending);
                    if (stage == 1) Call(pending, "OnMapDataRecieved", data);
                    Call(owner, "OnDestroy");
                    Call(pending, "OnMapDataRecieved", data);
                    Call(pending, "OnMeshDataRecieved", new MeshData(2, 2));
                    Assert.IsFalse(pending.IsVisible());
                    Assert.AreEqual(0, chunks.Count);
                }
            }

            var second = new GameObject("Second terrain");
            second.SetActive(false);
            var secondOwner = second.AddComponent<EndlessTerain>();
            Assert.AreNotSame(Get(owner, "terrainChunkDictionary"), Get(secondOwner, "terrainChunkDictionary"));
            Assert.AreNotSame(Get(owner, "terrainChunksVisibleLastUpdate"), Get(secondOwner, "terrainChunksVisibleLastUpdate"));
            Object.Destroy(second);
        }
        finally
        {
            Object.Destroy(root);
            Object.Destroy(material);
            Object.Destroy(pooled);
            Object.Destroy(prefab);
            Object.Destroy(poolObject);
            EasyObjectPool.instance = previousPool;
        }
        yield return null;
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator SceneUnloadClearsChunksAcrossPlaySessions()
    {
        yield return EnterRuntime();
        for (int reload = 0; reload < 2; reload++)
        {
            var scene = SceneManager.CreateScene("P1 scene lifecycle");
            var root = new GameObject("Terrain scene lifecycle");
            SceneManager.MoveGameObjectToScene(root, scene);
            var generator = root.AddComponent<MapGenerator>();
            generator.enabled = false;
            Call(generator, "OnDestroy");
            var owner = root.AddComponent<EndlessTerain>();
            owner.enabled = false;
            Set(owner, "mapGenerator", generator);
            var chunks = (Dictionary<Vector2, EndlessTerain.TerrainChunk>)Get(owner, "terrainChunkDictionary");
            Assert.AreEqual(0, chunks.Count);
            var chunk = new EndlessTerain.TerrainChunk(Vector2.zero, 15, root.transform, null, owner);
            chunks.Add(Vector2.zero, chunk);
            var chunkObject = (GameObject)Get(chunk, "meshObject");
            yield return SceneManager.UnloadSceneAsync(scene);
            Assert.IsTrue(owner == null);
            Assert.IsTrue(chunkObject == null);
            Assert.AreEqual(0, chunks.Count);
            // Results still in flight during scene unload must be harmless.
            Call(chunk, "OnMapDataRecieved", default(MapData));
            Call(chunk, "OnMeshDataRecieved", new MeshData(2, 2));
            Assert.IsFalse(chunk.IsVisible());
        }
        yield return new ExitPlayMode();
        // Keep reload instructions outside loops: Unity restores the coroutine's
        // program counter after domain reload, but not its local loop counters.
        yield return EnterRuntime();
        var freshRoot = new GameObject("Fresh terrain session");
        freshRoot.SetActive(false);
        var freshOwner = freshRoot.AddComponent<EndlessTerain>();
        Assert.AreEqual(0, ((ICollection)Get(freshOwner, "terrainChunkDictionary")).Count);
        Assert.AreEqual(0, ((ICollection)Get(freshOwner, "terrainChunksVisibleLastUpdate")).Count);
        Object.Destroy(freshRoot);
        yield return null;
        yield return new ExitPlayMode();
    }
}
#endif
