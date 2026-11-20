#if UNITY_INCLUDE_TESTS
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using SurvivalCombat;
using Object = UnityEngine.Object;

public class ProjectionTests
{
    [TestCase(0, 0)] [TestCase(15, 30)] [TestCase(-45, -60)]
    public void CoordinatesRoundTrip(float x, float y)
    {
        var p = new Vector2(x, y);
        Assert.AreEqual(p, WorldProjection.ToMap(WorldProjection.ToWorld(p)));
        Assert.AreEqual(y * .5f, WorldProjection.ToWorld(p).y);
    }
    [Test] public void VelocityAndDiagonalUseMapMetric()
    {
        Assert.AreEqual(new Vector2(4, 0), WorldProjection.ToWorld(Vector2.right * 4));
        Assert.AreEqual(new Vector2(0, 2), WorldProjection.ToWorld(Vector2.up * 4));
        var diagonal = WorldProjection.ToWorld(Vector2.one.normalized * 4);
        Assert.AreEqual(2.828427f, diagonal.x, .0001f);
        Assert.AreEqual(1.414214f, diagonal.y, .0001f);
        Assert.AreEqual(4, WorldProjection.Distance(Vector2.zero, diagonal), .0001f);
    }
    [TestCase(-2)] [TestCase(0)] [TestCase(3)]
    public void MeshAndAdjacentChunkBoundsAgree(int row)
    {
        var data = MeshGenerator.GenerateTerrainMesh(new float[121,121], 0, 8);
        Assert.AreEqual(15, data.vertices[1].x - data.vertices[0].x);
        Assert.AreEqual(7.5f, data.vertices[0].y - data.vertices[2].y);
        var a = WorldProjection.ProjectBounds(new Vector2(15, row * 15), Vector2.one * 15);
        var b = WorldProjection.ProjectBounds(new Vector2(15, (row + 1) * 15), Vector2.one * 15);
        Assert.AreEqual(a.max.y, b.min.y);
    }
    [Test] public void NatureIdentityAndMapPositionsAreNotChangedByProjection()
    {
        var heights = new float[121,121];
        for (int y = 0; y < 121; y++) for (int x = 0; x < 121; x++) heights[x,y] = .5f;
        var a = ObjectsGenerator.GenerateObjects(heights, 25, new Vector2(-15,30), 30, 8, 42);
        var b = ObjectsGenerator.GenerateObjects(heights, 25, new Vector2(-15,30), 30, 8, 42);
        Assert.IsNotEmpty(a); Assert.AreEqual(a.Length, b.Length);
        for (int i = 0; i < a.Length; i++)
        {
            Assert.AreEqual(a[i].stableId, b[i].stableId);
            Assert.AreEqual(a[i].objectName, b[i].objectName);
            Assert.AreEqual(a[i].position, WorldProjection.ToMap(WorldProjection.ToWorld(b[i].position)));
        }
    }
    [Test] public void FootprintIsCompressedOnceWithoutScalingVisual()
    {
        var go = new GameObject("Tree");
        try
        {
            var circle = go.AddComponent<CircleCollider2D>(); circle.radius = 2; circle.offset = Vector2.up;
            ProjectedFootprint.Apply(go);
            var polygon = go.GetComponent<PolygonCollider2D>(); var points = polygon.points;
            Assert.AreEqual(32, points.Length); Assert.AreEqual(Vector3.one, go.transform.localScale);
            float min = float.MaxValue, max = float.MinValue;
            foreach (var point in points) { min = Mathf.Min(min, point.y); max = Mathf.Max(max, point.y); }
            Assert.AreEqual(2, max - min, .02f); Assert.AreEqual(.5f, (max + min) / 2, .001f);
            ProjectedFootprint.Apply(go); CollectionAssert.AreEqual(points, polygon.points);
            Assert.AreEqual(1, go.GetComponents<Collider2D>().Length);
        }
        finally { Object.DestroyImmediate(go); }
    }
    [Test] public void SceneMigrationDoesNotMoveVisualChildrenOrCompressTwice()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var go = new GameObject("Placed tree"); SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.position = new Vector3(4, 12, 0); go.AddComponent<BoxCollider2D>();
        var child = new GameObject("Visual"); child.transform.SetParent(go.transform, false); child.transform.localPosition = Vector3.up * 3; child.AddComponent<SpriteRenderer>();
        try
        {
            SceneProjectionState.Apply(scene);
            Assert.AreEqual(new Vector3(4,6,0), go.transform.position);
            Assert.AreEqual(Vector3.up * 3, child.transform.localPosition);
            SceneProjectionState.Apply(scene);
            Assert.AreEqual(new Vector3(4,6,0), go.transform.position);
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
    }
    [Test] public void ThickProjectedCastStopsAtThinVerticalAndHorizontalWalls()
    {
        var wall = new GameObject("Thin wall");
        try
        {
            var box = wall.AddComponent<BoxCollider2D>(); box.size = new Vector2(.01f, 4); wall.transform.position = new Vector2(2,0); Physics2D.SyncTransforms();
            Assert.IsTrue(ProjectedCast.Cast(Vector2.zero, Vector2.right, 10, .1f, null, out var hit));
            Assert.AreSame(box, hit.collider);
            box.size = new Vector2(4,.01f); wall.transform.position = new Vector2(0,2); Physics2D.SyncTransforms();
            Assert.IsTrue(ProjectedCast.Cast(Vector2.zero, Vector2.up, 10, .1f, null, out hit));
            Assert.AreSame(box, hit.collider);
        }
        finally { Object.DestroyImmediate(wall); }
    }
    [Test] public void RadialDamageUsesEllipticalGroundDistance()
    {
        var near = new GameObject("Near"); var far = new GameObject("Far");
        try
        {
            near.transform.position = new Vector2(0,1); far.transform.position = new Vector2(0,2);
            near.AddComponent<CircleCollider2D>().radius = .1f; far.AddComponent<CircleCollider2D>().radius = .1f;
            var a = near.AddComponent<Health>(); var b = far.AddComponent<Health>(); a.ResetHealth(); b.ResetHealth(); Physics2D.SyncTransforms();
            CombatPhysics.Radial(Vector2.zero, 3, new DamageInfo { damage = 30 }, true);
            Assert.AreEqual(90, a.Current, .001f); Assert.AreEqual(100,b.Current);
        }
        finally { Object.DestroyImmediate(near); Object.DestroyImmediate(far); }
    }
    [Test] public void VersionOneSaveMigratesOnceAndKeepsBackup()
    {
        var catalog = CombatDefaults.CreateCatalog(); var root = new GameObject("Save test");
        string path = Path.Combine(Application.temporaryCachePath, "projection-" + Guid.NewGuid() + ".json");
        try
        {
            var pool = root.AddComponent<CombatPool>(); var world = root.AddComponent<CombatWorld>(); world.Initialize(catalog,pool,"projection-test");
            var actor = root.AddComponent<CombatActor>(); actor.Initialize(catalog,pool,world); actor.Health.ResetHealth(); world.Player = actor;
            var save = new CombatSaveService(world,path); var legacy = save.Capture(); legacy.version = 1; legacy.playerPosition = new Vector2(4,20);
            var item = new ItemEntry { definitionId = "ammo.light", count = 5 };
            legacy.world.items.Add(new WorldItemRecord { item = item, position = new Vector2(6,12) });
            string original = JsonUtility.ToJson(legacy); File.WriteAllText(path, original);
            Assert.IsTrue(save.TryLoad()); Assert.AreEqual(new Vector3(4,10,0), root.transform.position);
            Assert.AreEqual(new Vector2(6,12), world.Data.items[0].position);
            Assert.IsTrue(save.TrySave()); Assert.AreEqual(original,File.ReadAllText(path + ".v1.bak"));
            Assert.AreEqual(2,save.Capture().version); Assert.IsTrue(save.TryLoad());
            Assert.AreEqual(new Vector3(4,10,0), root.transform.position);
            Assert.AreEqual(item.instanceId, world.Data.items[0].item.instanceId);
        }
        finally
        {
            Object.DestroyImmediate(root); foreach (var item in catalog.items) Object.DestroyImmediate(item); Object.DestroyImmediate(catalog);
            foreach (string suffix in new[] { "", ".bak", ".v1.bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
        }
    }
}
#endif
