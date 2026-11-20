#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using SurvivalCombat;
using Object = UnityEngine.Object;

public class ProjectionRuntimeTests
{
    [UnityTest] public IEnumerator BothScenesStartAndMoveWithDomainReloadDisabled()
    {
        bool enabled = EditorSettings.enterPlayModeOptionsEnabled;
        var options = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        try
        {
            for (int pass = 0; pass < 2; pass++)
            {
                yield return new EnterPlayMode(false);
                foreach (string scene in new[] { "CombatArena", "MAIN SCENE" })
                {
                    yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                    yield return null;
                    var player = Object.FindAnyObjectByType<PlayerMovement>();
                    Assert.IsNotNull(player, scene);
                    var actor = player.GetComponent<CombatActor>();
                    Assert.IsNotNull(actor); Assert.IsNotNull(actor.Inventory); Assert.IsFalse(actor.Blocked);
                    Assert.AreEqual(1, player.GetComponents<CombatActor>().Length);
                    var body = player.GetComponent<Rigidbody2D>();
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    typeof(PlayerMovement).GetField("walkSpeed", flags).SetValue(player, 4f);
                    typeof(PlayerMovement).GetField("smoothing", flags).SetValue(player, 1f);
                    typeof(PlayerMovement).GetField("moveInput", flags).SetValue(player, Vector2.up);
                    typeof(PlayerMovement).GetField("sprintHeld", flags).SetValue(player, false);
                    player.SendMessage("FixedUpdate"); Assert.AreEqual(2f, body.linearVelocity.y, .001f);
                    typeof(PlayerMovement).GetField("moveInput", flags).SetValue(player, Vector2.right);
                    player.SendMessage("FixedUpdate"); Assert.AreEqual(4f, body.linearVelocity.x, .001f);
                    body.linearVelocity = Vector2.zero;
                    if (scene == "MAIN SCENE")
                    {
                        float until = Time.realtimeSinceStartup + 20;
                        while (Time.realtimeSinceStartup < until)
                        {
                            bool ready = false;
                            foreach (var mesh in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude))
                                if (mesh.name == "Terrain Chunk" && mesh.sharedMesh != null) { ready = true; break; }
                            if (ready) break;
                            yield return null;
                        }
                        bool found = false;
                        foreach (var mesh in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude))
                            if (mesh.name == "Terrain Chunk" && mesh.sharedMesh != null)
                            { Assert.AreEqual(7.5f, mesh.sharedMesh.bounds.size.y, .001f); found = true; }
                        Assert.IsTrue(found, "Terrain must finish generating in the real main scene.");
                    }
                    yield return new WaitForSeconds(.5f);
                    if (pass == 0 && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                        CaptureWorld(Camera.main, scene);
                    if (scene == "MAIN SCENE")
                    {
                        MarchingBytes.PoolObject nature = null;
                        float until = Time.realtimeSinceStartup + 20;
                        while (nature == null && Time.realtimeSinceStartup < until)
                        {
                            foreach (var candidate in Object.FindObjectsByType<MarchingBytes.PoolObject>(FindObjectsInactive.Exclude))
                                if (!candidate.isPooled && candidate.poolName.StartsWith("tree ")) { nature = candidate; break; }
                            if (nature == null) yield return null;
                        }
                        Assert.IsNotNull(nature, "Projected world must spawn trees from the existing pool.");
                        Assert.IsNotNull(nature.GetComponent<ProjectedFootprint>());
                        Assert.IsNotNull(nature.GetComponent<PolygonCollider2D>());
                        player.transform.position = nature.transform.position + Vector3.right * 2;
                        body.position = player.transform.position;
                        Camera.main.transform.position = player.transform.position - Vector3.forward * 10;
                        yield return new WaitForSeconds(.2f);
                        if (pass == 0 && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                            CaptureWorld(Camera.main, "Nature");
                    }
                }
                yield return new ExitPlayMode();
            }
        }
        finally
        {
            EditorSettings.enterPlayModeOptionsEnabled = enabled;
            EditorSettings.enterPlayModeOptions = options;
        }
    }
    static void CaptureWorld(Camera camera, string scene)
    {
        var target = RenderTexture.GetTemporary(1280,720,24);
        var previous = camera.targetTexture; var active = RenderTexture.active;
        var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
            Directory.CreateDirectory("Logs/CombatChecks");
            File.WriteAllBytes("Logs/CombatChecks/projection-" + scene + ".png", image.EncodeToPNG());
        }
        finally { camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); Object.Destroy(image); }
    }
}
#endif
