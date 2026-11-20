using UnityEngine;

namespace SnowTracks
{
    // A bounded, temporary alternative. It never allocates chunk masks or retains unloaded tracks.
    [DisallowMultipleComponent]
    public sealed class SnowTrackParticles : MonoBehaviour
    {
        [Range(16,1024)] public int maxFootprints=256;
        [Min(1)] public float lifetime=20;
        public Color tint=new Color(.35f,.44f,.52f,.45f);
        [Tooltip("Above terrain (-32768), below character SortingGroups (0). Legacy values are clamped into this interval.")]
        [Range(-32767,-1)] public int sortingOrder=-1;
        ParticleSystem system;
        Material material;
        Texture2D texture;
        ParticleSystem.Particle[] buffer;
        float nextCheck;
        SnowTrackWorld world;
        public bool Available => system!=null && material!=null && material.shader.isSupported;
        public int Count => system!=null ? system.particleCount : 0;
        public void Initialize()
        {
            if(system!=null){system.Play();return;}
            var shader=Shader.Find("Sprites/Default");if(shader==null||!shader.isSupported)return;
            texture=new Texture2D(32,32,TextureFormat.RGBA32,false) { name="Snow particle footprint",wrapMode=TextureWrapMode.Clamp };
            var pixels=new Color[32*32];
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)
            {
                float u=(x+.5f)/16-1,v=(y+.5f)/16-1;
                pixels[y*32+x]=new Color(1,1,1,Mathf.Clamp01((1-u*u-v*v)*5));
            }
            texture.SetPixels(pixels);texture.Apply(false,true);
            material=new Material(shader) { name="Snow particle footprint",mainTexture=texture };
            var go=new GameObject("Mobile snow footprints");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,gameObject.scene);
            go.transform.localScale=new Vector3(1,WorldProjection.VerticalScale,1);
            system=go.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=system.main;main.playOnAwake=false;main.loop=true;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            main.scalingMode=ParticleSystemScalingMode.Hierarchy;main.maxParticles=Mathf.Clamp(maxFootprints,16,1024);
            main.startSpeed=0;main.startSize3D=true;main.useUnscaledTime=false;
            var emission=system.emission;emission.enabled=false;
            var shape=system.shape;shape.enabled=false;
            var fade=system.colorOverLifetime;fade.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,.65f),new GradientAlphaKey(0,1)});fade.color=gradient;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;
            renderer.alignment=ParticleSystemRenderSpace.World;renderer.sortingOrder=Mathf.Clamp(sortingOrder,-32767,-1);
            buffer=new ParticleSystem.Particle[main.maxParticles];system.Play();
        }
        public void Stamp(SnowTrackWorld owner,Vector2 point,Vector2 direction,Vector2 size)
        {
            if(!Available || !owner.IsSnow(point))return;
            direction=direction.sqrMagnitude>.0001f?direction.normalized:Vector2.up;
            Vector2 right=new Vector2(direction.y,-direction.x);
            // Conservative footprint-sized grid: reject shoreline footprints rather than draw across dry ground.
            for(int y=-2;y<=2;y++)for(int x=-2;x<=2;x++)
                if(!owner.IsSnow(point+right*(x*size.x*.25f)+direction*(y*size.y*.25f)))return;
            world=owner;
            var emit=new ParticleSystem.EmitParams { position=new Vector3(point.x,point.y,-.01f),velocity=Vector3.zero,
                startLifetime=Mathf.Max(1,lifetime),startSize3D=new Vector3(size.x,size.y,1),startColor=tint,
                rotation=-Mathf.Atan2(-direction.x,direction.y)*Mathf.Rad2Deg };
            system.Emit(emit,1);
        }
        void LateUpdate()
        {
            if(!Available||world==null||Time.timeScale<=0||Time.time<nextCheck)return;
            nextCheck=Time.time+.1f;
            int count=system.GetParticles(buffer);
            for(int i=0;i<count;i++)
                if(!world.TracksAvailable||!world.IsSnow(buffer[i].position))buffer[i].remainingLifetime=0;
            system.SetParticles(buffer,count);
        }
        public void Clear(){if(system!=null)system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
        void OnDisable(){Clear();}
        void OnDestroy()
        {
            if(system!=null)Release(system.gameObject);
            if(material!=null)Release(material);
            if(texture!=null)Release(texture);
        }
        static void Release(Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
