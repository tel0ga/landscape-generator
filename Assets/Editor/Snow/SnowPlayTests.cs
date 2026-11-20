#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.IO;
using NUnit.Framework;
using SnowTracks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public class SnowPlayTests
{
    [UnityTest] public IEnumerator RenderingEmitterAndSessionCleanupWithoutDomainReload()
    {
        bool enabled=EditorSettings.enterPlayModeOptionsEnabled;var options=EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        try
        {
            for(int session=0;session<2;session++)
            {
                yield return new EnterPlayMode(false);
                var root=new GameObject("Snow play validation");
                var material=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Scripts/Generation/TerrainShaderMaterial.mat"));
                material.SetFloat("_PPU",16);material.SetFloat("_SnowCoverageThreshold",.2f);material.SetFloat("_SnowCoverageBlend",0);material.SetFloat("_SnowInValleys",1);
                var h=new float[121,121];var s=new float[125,125];for(int y=0;y<121;y++)for(int x=0;x<121;x++)h[x,y]=.5f;
                for(int y=0;y<125;y++)for(int x=0;x<125;x++)s[x,y]=.8f;
                var data=new MapData(h,s,new Color[121*121],0,1,125);
                var mesh=MeshGenerator.GenerateTerrainMesh(h,0,8).CreateMesh();var renderer=root.AddComponent<MeshRenderer>();renderer.sortingOrder=short.MinValue;renderer.sharedMaterial=material;root.AddComponent<MeshFilter>().sharedMesh=mesh;
                var noise=TextureGenerator.TextureFromNoiseMap(h,0,1);var snow=TextureGenerator.TextureFromFloatMap(s);
                var props=new MaterialPropertyBlock();props.SetTexture("_NoiseMap",noise);props.SetTexture("_SnowMap",snow);props.SetFloat("_NoiseMin",0);props.SetFloat("_NoiseMax",1);
                props.SetFloat("_SnowPadding",2f/125);props.SetFloat("_SnowTexelSize",1f/125);props.SetFloat("_ChunkSize",15);props.SetVector("_ChunkOrigin",new Vector4(-7.5f,-7.5f,0,0));renderer.SetPropertyBlock(props);
                var world=root.AddComponent<SnowTrackWorld>();world.Initialize(material);world.Register(Vector2Int.zero,data,2,renderer);world.SetVisible(Vector2Int.zero,true);
                Assert.AreEqual(0,world.MaskCount);Assert.AreEqual(0,world.TextureCount);
                var cameraGo=new GameObject("Snow camera");cameraGo.transform.SetParent(root.transform);cameraGo.transform.position=new Vector3(0,0,-10);
                var camera=cameraGo.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=2;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.gray;
                try
                {
                    Texture2D before=null;
                    if(session==0&&SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)before=Capture(camera,"before");
                    var player=new GameObject("Emitter test");player.transform.SetParent(root.transform);
                    var body=player.AddComponent<Rigidbody2D>();body.bodyType=RigidbodyType2D.Kinematic;body.gravityScale=0;body.position=new Vector2(-4,0);
                    var emitter=player.AddComponent<SnowFootprintEmitter>();emitter.world=world;emitter.footAnchor=player.transform;
                    float readyAt=Time.fixedTime+.08f;
                    while(Time.fixedTime<readyAt)yield return null;
                    Assert.IsTrue(world.TracksAvailable,world.Status);
                    Assert.IsTrue(world.IsSnow(new Vector2(-3,0)),"Synthetic walking surface must be snowy");
                    for(int i=1;i<=4;i++)
                    {
                        body.position=new Vector2(-4+i*.3f,0);
                        float next=Time.fixedTime+.06f;while(Time.fixedTime<next)yield return null;
                    }
                    world.Flush();Assert.AreEqual(1,world.MaskCount,"Actual Rigidbody motion must leave snow tracks. "+JsonUtility.ToJson(emitter)+" body="+body.position+" time="+Time.time+" playing="+Application.isPlaying);
                    emitter.enabled=false;
                    for(int pass=0;pass<12;pass++)
                        for(float x=-5;x<=5;x+=.45f)
                        {
                            world.Stamp(new Vector2(x,-1.6f+((Mathf.RoundToInt((x+5)/.45f)%2)==0?.1f:-.1f)),Vector2.right,new Vector2(.22f,.4f),.55f);
                            if(pass==0)world.Stamp(new Vector2(x,1.6f+((Mathf.RoundToInt((x+5)/.45f)%2)==0?.1f:-.1f)),Vector2.right,new Vector2(.22f,.4f),.55f);
                        }
                    world.Flush();Assert.AreEqual(1,world.TextureCount);
                    Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
                    if(before!=null)
                    {
                        var after=Capture(camera,"after");var a=before.GetPixels32();var b=after.GetPixels32();int changed=0;
                        for(int i=0;i<a.Length;i++)if(Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>6)changed++;
                        Assert.Greater(changed,100,"Terrain shader must visibly render the track mask.");Object.Destroy(before);Object.Destroy(after);
                    }
                    world.Unregister(Vector2Int.zero);Assert.AreEqual(0,world.TextureCount);Assert.AreEqual(1,world.MaskCount);
                    world.AdvanceSnowfall(300);world.Flush();Assert.AreEqual(0,world.MaskCount);
                    world.Register(Vector2Int.zero,data,2,renderer);world.SetVisible(Vector2Int.zero,true);
                    world.SetMode(SnowTrackMode.MobileParticles);
                    Assert.IsTrue(world.TracksAvailable,world.Status);
                    world.Stamp(Vector2.zero,Vector2.up,new Vector2(.22f,.4f),.55f);
                    Assert.AreEqual(1,world.ParticleCount);Assert.AreEqual(0,world.MaskCount);Assert.AreEqual(0,world.TextureCount);
                    world.Stamp(new Vector2(30,30),Vector2.up,new Vector2(.22f,.4f),.55f);Assert.AreEqual(1,world.ParticleCount,"Unknown terrain must not emit particles");
                    var lightweight=root.GetComponent<SnowTrackParticles>();
                    var footprintRenderer=GameObject.Find("Mobile snow footprints").GetComponent<ParticleSystemRenderer>();
                    Assert.Greater(footprintRenderer.sortingOrder,renderer.sortingOrder,"Footprints must render above terrain");
                    Assert.Less(footprintRenderer.sortingOrder,0,"Footprints must render below character groups");
                    for(int i=0;i<400;i++)world.Stamp(new Vector2((i%20)*.3f-3,(i/20%8)*.4f-1.6f),Vector2.right,new Vector2(.22f,.4f),.55f);
                    Assert.LessOrEqual(world.ParticleCount,lightweight.maxFootprints);
                    if(session==0&&SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                    {
                        var shown=Capture(camera,"particles");
                        world.SetMode(SnowTrackMode.Off);
                        var hidden=Capture(camera,"particles-off");int changes=0;var a=shown.GetPixels32();var b=hidden.GetPixels32();
                        for(int i=0;i<a.Length;i++)if(Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>6)changes++;
                        Assert.Greater(changes,100,"Mobile footprints must actually render");Object.Destroy(shown);Object.Destroy(hidden);
                    }
                    world.SetMode(SnowTrackMode.Off);Assert.AreEqual(0,world.ParticleCount);Assert.IsFalse(world.TracksAvailable);
                }
                finally{Object.Destroy(root);Object.Destroy(mesh);Object.Destroy(noise);Object.Destroy(snow);Object.Destroy(material);}
                yield return null;
                foreach(var texture in Resources.FindObjectsOfTypeAll<Texture2D>())Assert.IsFalse(texture.name.StartsWith("Snow tracks "),"No track textures may survive owner destruction.");
                Assert.IsNull(GameObject.Find("Mobile snow footprints"),"Particle host must be released with its owner");
                yield return new ExitPlayMode();
            }
        }
        finally{Time.timeScale=1;EditorSettings.enterPlayModeOptionsEnabled=enabled;EditorSettings.enterPlayModeOptions=options;}
    }
    static Texture2D Capture(Camera camera,string suffix)
    {
        var rt=RenderTexture.GetTemporary(1280,720,24);var old=camera.targetTexture;var active=RenderTexture.active;
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
            Directory.CreateDirectory("Logs/SnowChecks");File.WriteAllBytes("Logs/SnowChecks/snow-"+suffix+".png",image.EncodeToPNG());return image;
        }
        finally{camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);}
    }
}
#endif
