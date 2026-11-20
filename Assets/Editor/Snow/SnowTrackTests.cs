#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SnowTracks;
using UnityEngine;
using Object=UnityEngine.Object;

public class SnowTrackTests
{
    GameObject root;
    Material material;
    SnowTrackWorld world;
    [SetUp] public void Setup()
    {
        Time.timeScale=1;
        root=new GameObject("Snow test");
        material=new Material(Shader.Find("Custom/TerrainFromNoiseMap_PixelPerfect"));
        material.SetFloat("_PPU",16);material.SetFloat("_SnowCoverageThreshold",.5f);material.SetFloat("_SnowCoverageBlend",0);
        material.SetFloat("_SnowInValleys",1);material.SetFloat("_Rock2Threshold",.9f);material.SetFloat("_BlendWidth",.01f);material.SetFloat("_WaterThreshold",0);
        world=root.AddComponent<SnowTrackWorld>();world.Initialize(material);
    }
    [TearDown] public void Cleanup(){Object.DestroyImmediate(root);Object.DestroyImmediate(material);Time.timeScale=1;}
    static MapData Data(float height=.5f,float coverage=.8f)
    {
        var h=new float[121,121];var s=new float[125,125];
        for(int y=0;y<121;y++)for(int x=0;x<121;x++)h[x,y]=height;
        for(int y=0;y<125;y++)for(int x=0;x<125;x++)s[x,y]=coverage;
        return new MapData(h,s,new Color[121*121],0,1,125);
    }
    Renderer Register(Vector2Int coord,MapData data)
    {
        var go=new GameObject("Chunk "+coord);go.transform.SetParent(root.transform);var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
        var block=new MaterialPropertyBlock();block.SetFloat("_NoiseMin",.123f);renderer.SetPropertyBlock(block);
        world.Register(coord,data,2,renderer);world.SetVisible(coord,true);return renderer;
    }
    static Texture2D Texture(Renderer renderer)
    {var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);return block.GetTexture("_SnowTracks") as Texture2D;}
    [Test] public void ModesClearResourcesAndMissingSystemsAreSafe()
    {
        Register(Vector2Int.zero,Data());
        world.Stamp(Vector2.zero,Vector2.up,new Vector2(.22f,.4f),.55f);world.Flush();
        Assert.IsTrue(world.FullSnowAvailable);Assert.AreEqual(1,world.MaskCount);
        world.SetMode(SnowTrackMode.Off);
        Assert.IsFalse(world.TracksAvailable);Assert.AreEqual(0,world.MaskCount);Assert.AreEqual(0,world.TextureCount);
        world.Stamp(Vector2.zero,Vector2.up,Vector2.one,.55f);Assert.AreEqual(0,world.MaskCount);
        world.SetMode(SnowTrackMode.MobileParticles);Assert.IsTrue(world.TracksAvailable,world.Status);
        world.Stamp(Vector2.zero,Vector2.up,new Vector2(.22f,.4f),.55f);Assert.AreEqual(0,world.MaskCount);
        world.SetMode(SnowTrackMode.FullSnow);Assert.AreEqual(0,world.ParticleCount);
        var missing=new GameObject("Missing surface");
        try {var empty=missing.AddComponent<SnowTrackWorld>();empty.Initialize(null);Assert.IsFalse(empty.TracksAvailable);empty.Stamp(Vector2.zero,Vector2.up,Vector2.one,1);}
        finally{Object.DestroyImmediate(missing);}
    }
    [Test] public void SurfaceMatchesZeroBlendWaterAndHighAltitudeSnow()
    {
        var settings=SnowSurfaceSettings.Read(material);
        Assert.AreEqual(1,new SnowSurfaceQuery(Data(),Vector2.zero,15,2).Weight(Vector2.zero,settings));
        Assert.AreEqual(0,new SnowSurfaceQuery(Data(.5f,.2f),Vector2.zero,15,2).Weight(Vector2.zero,settings));
        Assert.AreEqual(0,new SnowSurfaceQuery(Data(-.2f,.9f),Vector2.zero,15,2).Weight(Vector2.zero,settings));
        Assert.AreEqual(1,new SnowSurfaceQuery(Data(1,.1f),Vector2.zero,15,2).Weight(Vector2.zero,settings));
        Assert.AreEqual(1,SnowSurfaceQuery.Smooth(.5f,.5f,.5f));
    }
    [Test] public void BilinearCoverageAndPaddingAgreeWithTextureSampling()
    {
        var data=Data();for(int y=0;y<125;y++)for(int x=0;x<125;x++)data.snowMap[x,y]=(float)x/124;
        var query=new SnowSurfaceQuery(data,Vector2.zero,15,2);var settings=SnowSurfaceSettings.Read(material);settings.coverageBlend=.2f;
            foreach(float x in new[]{-7.5f,-4f,0f,3f,7.49f})
            {
                float snapped=Mathf.Floor(x*16)/16;float uv=(snapped+7.5f)/15;
                // GPU tex2D addresses pixel centres at (index+.5)/size. GetPixelBilinear's
                // CPU convention differs, so use the analytic value of this linear ramp.
                float sample=Mathf.Clamp01(((uv*(1-4f/125)+2f/125)*125-.5f)/124);
                float expected=SnowSurfaceQuery.Smooth(.3f,.7f,sample);
                Assert.AreEqual(expected,query.Weight(new Vector2(x,0),settings),.001f);
            }
    }
    [Test] public void NoMaskIsAllocatedOnGrassOrWater()
    {
        Register(Vector2Int.zero,Data(.5f,.1f));world.Stamp(Vector2.zero,Vector2.up,new Vector2(.22f,.4f),.55f);
        Assert.AreEqual(0,world.MaskCount);Assert.AreEqual(0,world.TextureCount);
        world.Unregister(Vector2Int.zero);Register(Vector2Int.zero,Data(-1,.9f));
        world.Stamp(Vector2.zero,Vector2.up,new Vector2(.22f,.4f),.55f);Assert.AreEqual(0,world.MaskCount);
    }
    [Test] public void FourChunkCornerAndBordersSurviveUnloadWithoutPropertyLoss()
    {
        var coordinates=new[]{Vector2Int.zero,Vector2Int.right,Vector2Int.up,Vector2Int.one};
        var renderers=new Renderer[4];for(int i=0;i<4;i++)renderers[i]=Register(coordinates[i],Data());
        world.Stamp(new Vector2(7.5f,7.5f),Vector2.up,new Vector2(.22f,.4f),.55f);
        world.Stamp(new Vector2(7.5f,0),Vector2.up,new Vector2(.22f,.4f),.55f);world.Flush();
        Assert.AreEqual(4,world.MaskCount);Assert.AreEqual(4,world.TextureCount);
        var left=Texture(renderers[0]);var right=Texture(renderers[1]);
        for(int y=122;y<138;y++)Assert.AreEqual(right.GetPixel(2,y),left.GetPixel(258,y));
        var block=new MaterialPropertyBlock();renderers[0].GetPropertyBlock(block);Assert.AreEqual(.123f,block.GetFloat("_NoiseMin"));
        Color saved=right.GetPixel(2,130);
        foreach(var coord in coordinates)world.Unregister(coord);
        Assert.AreEqual(4,world.MaskCount);Assert.AreEqual(0,world.TextureCount);
        var restored=Register(Vector2Int.right,Data());world.Flush();Assert.AreEqual(saved,Texture(restored).GetPixel(2,130));
    }
    [Test] public void TrailStrengthControlsCompactionWithoutChangingFootprints()
    {
        var surface=new SnowSurfaceQuery(Data(),Vector2.zero,15,2);
        var settings=SnowSurfaceSettings.Read(material);var origin=Vector2.one*-7.5f;
        var none=new SnowTrackMask(0,.5f);var normal=new SnowTrackMask(0,.5f);var strong=new SnowTrackMask(0,.5f);
        none.Stamp(Vector2.zero,Vector2.up,new Vector2(.22f,.4f),.55f,surface,origin,15,settings,0);
        normal.Stamp(Vector2.zero,Vector2.up,new Vector2(.22f,.4f),.55f,surface,origin,15,settings,1);
        strong.Stamp(Vector2.zero,Vector2.up,new Vector2(.22f,.4f),.55f,surface,origin,15,settings,10);
        int n=0,s=0;
        for(int i=0;i<none.Pixels.Length;i+=2)
        {
            Assert.AreEqual(0,none.Pixels[i+1]);Assert.AreEqual(none.Pixels[i],strong.Pixels[i]);
            n+=normal.Pixels[i+1];s+=strong.Pixels[i+1];
        }
        Assert.Greater(n,0);Assert.Greater(s,n*8);
        var path=new SnowTrackMask(0,.5f);
        for(float y=-1;y<=1;y+=.1f)path.Stamp(new Vector2(0,y),Vector2.up,Vector2.zero,.55f,surface,origin,15,settings,10,true);
        for(int i=0;i<path.Pixels.Length;i+=2)Assert.AreEqual(0,path.Pixels[i],"Trail strokes must not create extra footprints");
        for(int y=114;y<=141;y++)Assert.Greater(path.Pixels[(y*256+128)*2+1],200,"Continuous path must have no gaps");
    }
    [Test] public void RepeatedPassesBuildTrailAndCompressionIsLossless()
    {
        var surface=new SnowSurfaceQuery(Data(),Vector2.zero,15,2);var settings=SnowSurfaceSettings.Read(material);
        var mask=new SnowTrackMask(0,.5f);var origin=Vector2.one*-7.5f;
        mask.Stamp(Vector2.zero,Vector2.up,new Vector2(.22f,.4f),.55f,surface,origin,15,settings);
        byte[] once=(byte[])mask.Pixels.Clone();mask.Compress();Assert.IsTrue(mask.IsCompressed);CollectionAssert.AreEqual(once,mask.Pixels);
        mask.Stamp(Vector2.zero,Vector2.up,new Vector2(.22f,.4f),.55f,surface,origin,15,settings);
        int i=(128*256+128)*2;Assert.Greater(mask.Pixels[i+1],once[i+1]);
        Assert.AreEqual(once[i],mask.Pixels[i]);
        for(int n=0;n<30;n++)mask.Stamp(Vector2.zero,Vector2.up,new Vector2(.22f,.4f),.55f,surface,origin,15,settings);
        mask.Advance(120,surface,origin,15,settings);Assert.AreEqual(0,mask.Pixels[i]);Assert.Greater(mask.Pixels[i+1],0);
        Assert.IsFalse(mask.Advance(300,surface,origin,15,settings));
    }
    [Test] public void MeltThenNewSnowDoesNotRestoreUnloadedTracks()
    {
        Register(Vector2Int.zero,Data());world.Stamp(Vector2.zero,Vector2.up,new Vector2(.22f,.4f),.55f);world.Flush();world.Unregister(Vector2Int.zero);
        material.SetFloat("_SnowCoverageThreshold",1);world.Flush();
        material.SetFloat("_SnowCoverageThreshold",0);world.Flush();
        Register(Vector2Int.zero,Data());world.Flush();Assert.AreEqual(0,world.MaskCount);
        world.Stamp(Vector2.zero,Vector2.up,new Vector2(.22f,.4f),.55f);world.Flush();Assert.AreEqual(1,world.MaskCount);
        world.Unregister(Vector2Int.zero);world.AdvanceSnowfall(300);world.Flush();Assert.AreEqual(0,world.MaskCount);
    }
    [Test] public void StepSpacingIsIndependentOfFrameRateAndProjectedDirection()
    {
        foreach(var direction in new[]{Vector2.up,Vector2.right,Vector2.one.normalized})
        {
            var a=Steps(direction,60);var b=Steps(direction,240);Assert.AreEqual(a.Count,b.Count);
            for(int i=0;i<a.Count;i++)Assert.Less(Vector2.Distance(a[i],b[i]),.0001f);
        }
    }
    static List<Vector2> Steps(Vector2 direction,int frames)
    {
        var result=new List<Vector2>();var steps=new SnowStepSampler();
        for(int i=0;i<=frames;i++)steps.Sample(WorldProjection.ToMap(WorldProjection.ToWorld(direction*(9f*i/frames))),.45f,.1f,(p,d)=>result.Add(p));
        return result;
    }
    [Test] public void StationaryTeleportAndResetDoNotDrawConnectingTracks()
    {
        var sampler=new SnowStepSampler();int count=0;Action<Vector2,Vector2> stamp=(p,d)=>count++;
        sampler.Sample(Vector2.zero,.45f,.1f,stamp);for(int i=0;i<10;i++)sampler.Sample(Vector2.zero,.45f,.1f,stamp);
        sampler.Sample(Vector2.one*30,.45f,.1f,stamp);Assert.AreEqual(0,count);
        sampler.Reset();sampler.Sample(Vector2.one*31,.45f,.1f,stamp);Assert.AreEqual(0,count);
        sampler.Sample(Vector2.one*31+Vector2.up*.9f,.45f,.1f,stamp);Assert.AreEqual(2,count);
    }
    [Test] public void SnowfallAdapterUsesExplicitSourceAndPauseStopsWorldClock()
    {
        var adapter=root.GetComponent<SnowfallAdapter>();adapter.manualIntensity=.3f;Assert.AreEqual(.3f,adapter.SnowfallIntensity);
        var particles=root.AddComponent<ParticleSystem>();adapter.source=particles;adapter.normalEmissionRate=50;
        var emission=particles.emission;emission.rateOverTime=25;particles.Play();Assert.AreEqual(.5f,adapter.SnowfallIntensity);
        emission.enabled=false;Assert.AreEqual(0,adapter.SnowfallIntensity);emission.enabled=true;particles.Stop();Assert.AreEqual(0,adapter.SnowfallIntensity);
        Time.timeScale=0;double clock=world.SnowfallTotal;
        typeof(SnowTrackWorld).GetMethod("LateUpdate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(world,null);
        Assert.AreEqual(clock,world.SnowfallTotal);
    }
}
#endif
