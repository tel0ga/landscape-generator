using System.Collections.Generic;
using UnityEngine;

namespace SnowTracks
{
    public enum SnowTrackMode { Off, MobileParticles, FullSnow }
    [DisallowMultipleComponent, DefaultExecutionOrder(1100)]
    public sealed class SnowTrackWorld : MonoBehaviour
    {
        sealed class Tile
        {
            public SnowSurfaceQuery surface;
            public Renderer renderer;
            public bool visible;
            public SnowTrackMask mask;
            public Texture2D texture;
            public byte[] upload;
        }
        readonly Dictionary<Vector2Int,Tile> tiles = new Dictionary<Vector2Int,Tile>();
        readonly List<Vector2Int> scratch = new List<Vector2Int>();
        MaterialPropertyBlock properties;
        public Material terrainMaterial;
        public SnowfallAdapter snowfall;
        public double SnowfallTotal { get; private set; }
        public int MaskCount { get { int n=0; foreach(var t in tiles.Values) if(t.mask!=null)n++; return n; } }
        public int TextureCount { get { int n=0; foreach(var t in tiles.Values) if(t.texture!=null)n++; return n; } }
        float nextUpload;
        SnowSurfaceSettings settings;
        bool initialized;
        [SerializeField] SnowTrackMode mode = SnowTrackMode.FullSnow;
        SnowTrackMode appliedMode;
        SnowTrackParticles particles;
        public SnowTrackMode Mode => mode;
        public bool SurfaceAvailable => initialized && terrainMaterial != null && terrainMaterial.HasProperty("_SnowMap");
        public bool FullSnowAvailable => SurfaceAvailable && terrainMaterial.HasProperty("_SnowTracks");
        public bool TracksAvailable => isActiveAndEnabled && SurfaceAvailable && mode != SnowTrackMode.Off &&
            (mode == SnowTrackMode.FullSnow ? FullSnowAvailable : particles != null && particles.Available);
        public string Status => mode == SnowTrackMode.Off ? "Disabled" : !SurfaceAvailable ? "Missing snow terrain material" :
            mode == SnowTrackMode.FullSnow ? (FullSnowAvailable ? "Full snow trails" : "Shader does not support track masks") :
            particles != null && particles.Available ? "Mobile particle footprints" : "Particle renderer unavailable";
        public int ParticleCount => particles != null ? particles.Count : 0;
        public void SetMode(SnowTrackMode value)
        {
            mode=value;
            if(!initialized || appliedMode==mode) return;
            foreach(var tile in tiles.Values) { ReleaseTexture(tile);tile.mask=null; }
            if(particles!=null) particles.Clear();
            appliedMode=mode;
            if(mode==SnowTrackMode.MobileParticles)
            {
                if(particles==null) particles=GetComponent<SnowTrackParticles>() ?? gameObject.AddComponent<SnowTrackParticles>();
                particles.Initialize();
            }
        }
        public bool IsSnow(Vector2 mapPoint)
        {
            return SurfaceAvailable && tiles.TryGetValue(ChunkAt(mapPoint),out var tile) && tile.renderer!=null &&
                tile.surface.Weight(mapPoint,SnowSurfaceSettings.Read(terrainMaterial))>=.05f;
        }
        public void Initialize(Material material)
        {
            if(initialized) return;
            if(material==null || !material.HasProperty("_SnowMap")) return;
            initialized=true; properties=new MaterialPropertyBlock(); terrainMaterial=material; settings=SnowSurfaceSettings.Read(material);
            appliedMode=mode==SnowTrackMode.Off ? SnowTrackMode.FullSnow : SnowTrackMode.Off;
            SetMode(mode);
            if(snowfall==null) snowfall=GetComponent<SnowfallAdapter>();
            if(snowfall==null) snowfall=gameObject.AddComponent<SnowfallAdapter>();
            // An unambiguous camera-following snow emitter is wired once, never inferred from snowAmount.
            if(snowfall.source==null)
            {
                ParticleSystem candidate=null; int count=0;
                foreach(var follower in FindObjectsByType<SnowFollowCamera>(FindObjectsInactive.Exclude))
                {
                    if(follower.gameObject.scene!=gameObject.scene) continue;
                    var particles=follower.GetComponentInChildren<ParticleSystem>();
                    if(particles!=null) { candidate=particles;count++; }
                }
                if(count==1)
                {
                    snowfall.source=candidate;
                    snowfall.normalEmissionRate=Mathf.Max(.01f,candidate.emission.rateOverTime.Evaluate(0,.5f));
                }
            }
            foreach(var player in FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude))
            {
                if(player.gameObject.scene!=gameObject.scene) continue;
                var emitter=player.GetComponent<SnowFootprintEmitter>();
                if(emitter==null) emitter=player.gameObject.AddComponent<SnowFootprintEmitter>();
                emitter.world=this; emitter.ResetPath();
            }
        }
        public static Vector2Int ChunkAt(Vector2 map) => new Vector2Int(Mathf.FloorToInt(map.x/MapGenerator.worldChunkSize+.5f),Mathf.FloorToInt(map.y/MapGenerator.worldChunkSize+.5f));
        static Vector2 Origin(Vector2Int coord) => (Vector2)coord*MapGenerator.worldChunkSize-Vector2.one*(MapGenerator.worldChunkSize*.5f);
        public void Register(Vector2Int coord, MapData data, int padding, Renderer renderer)
        {
            if(!initialized) return;
            if(!tiles.TryGetValue(coord,out var tile)) { tile=new Tile();tiles.Add(coord,tile); }
            tile.surface=new SnowSurfaceQuery(data,(Vector2)coord*MapGenerator.worldChunkSize,MapGenerator.worldChunkSize,padding);
            tile.renderer=renderer;
            BindTexture(tile,null);
        }
        public void SetVisible(Vector2Int coord,bool visible)
        {
            if(!tiles.TryGetValue(coord,out var tile)||tile.visible==visible) return;
            tile.visible=visible;
            if(!visible) { ReleaseTexture(tile);tile.mask?.Compress(); }
            else if(tile.mask!=null) { tile.mask.Dirty=true;DirtyNeighbours(coord); }
        }
        public void Unregister(Vector2Int coord)
        {
            if(!tiles.TryGetValue(coord,out var tile)) return;
            ReleaseTexture(tile);tile.renderer=null;tile.visible=false;
            if(tile.mask==null) tiles.Remove(coord);
            else { tile.mask.Compress(); tile.surface.Compress(); }
        }
        public void Stamp(Vector2 mapPoint,Vector2 mapDirection,Vector2 size,float trailWidth,float trailStrength=1,bool trailOnly=false)
        {
            if(!TracksAvailable || Time.timeScale<=0) return;
            if(mode==SnowTrackMode.MobileParticles) { if(!trailOnly)particles.Stamp(this,mapPoint,mapDirection,size);return; }
            ObserveWeather();
            mapDirection=mapDirection.sqrMagnitude>.0001f?mapDirection.normalized:Vector2.up;
            float radius=Mathf.Max(size.magnitude*.5f,trailWidth*.5f);
            var min=ChunkAt(mapPoint-Vector2.one*radius); var max=ChunkAt(mapPoint+Vector2.one*radius);
            for(int y=min.y;y<=max.y;y++) for(int x=min.x;x<=max.x;x++)
            {
                var coord=new Vector2Int(x,y);
                // No replay on future snow: an ungenerated surface is deliberately not stamped.
                if(!tiles.TryGetValue(coord,out var tile)||tile.surface==null||tile.renderer==null) continue;
                Age(coord,tile);
                bool created=tile.mask==null;
                if(created&&!SnowTrackMask.HasSnow(mapPoint,radius,tile.surface,Origin(coord),MapGenerator.worldChunkSize,settings))continue;
                if(created) tile.mask=new SnowTrackMask(SnowfallTotal,settings.threshold);
                if(tile.mask.Stamp(mapPoint,mapDirection,size,trailWidth,tile.surface,Origin(coord),MapGenerator.worldChunkSize,settings,trailStrength,trailOnly)) DirtyNeighbours(coord);
                else if(created) tile.mask=null;
            }
        }
        void Awake()
        {
            // Выведет в консоль всю цепочку вызовов, которая создала этот скрипт
            Debug.Log("Меня создали отсюда:\n" + System.Environment.StackTrace);
        }
        void ObserveWeather()
        {
            var current=SnowSurfaceSettings.Read(terrainMaterial);
            if(current.threshold!=settings.threshold)
                foreach(var tile in tiles.Values)
                    if(tile.mask!=null) tile.mask.PendingMeltThreshold=Mathf.Max(tile.mask.PendingMeltThreshold,current.threshold);
            // With fixed blend/height settings, erasing at the maximum threshold is equivalent to
            // applying every intervening melt threshold in order. This bounds history memory for distant chunks.
            settings=current;
        }
        void LateUpdate()
        {
            if(initialized && appliedMode!=mode) SetMode(mode);
            if(mode!=SnowTrackMode.FullSnow) return;
            if(!initialized||terrainMaterial==null||Time.timeScale<=0||Time.deltaTime<=0) return;
            ObserveWeather(); SnowfallTotal+=Time.deltaTime*(snowfall!=null?snowfall.SnowfallIntensity:0);
            if(Time.time<nextUpload) return;
            nextUpload=Time.time+.1f; Flush();
        }
        public void AdvanceSnowfall(float secondsAtNormalIntensity)
        { SnowfallTotal+=Mathf.Max(0,secondsAtNormalIntensity); ObserveWeather(); }
        public void Flush()
        {
            if(!initialized || mode!=SnowTrackMode.FullSnow) return;
            ObserveWeather();scratch.Clear();
            foreach(var pair in tiles)
            {
                var tile=pair.Value;
                if(tile.mask==null) continue;
                if(SnowfallTotal-tile.mask.LastSnowfall>=300)
                { tile.mask=null; ReleaseTexture(tile);DirtyNeighbours(pair.Key);if(tile.renderer==null)scratch.Add(pair.Key); }
                else if(tile.visible) Age(pair.Key,tile);
            }
            foreach(var key in scratch) tiles.Remove(key);
            foreach(var pair in tiles)
                if(pair.Value.visible&&pair.Value.mask!=null&&pair.Value.mask.Dirty) Upload(pair.Key,pair.Value);
            // Leave dirty flags until all neighbour borders have consumed the same generation.
            foreach(var tile in tiles.Values) if(tile.visible&&tile.mask!=null) tile.mask.Dirty=false;
        }
        void Age(Vector2Int coord,Tile tile)
        {
            if(tile.mask==null) return;
            if(!tile.mask.Advance(SnowfallTotal,tile.surface,Origin(coord),MapGenerator.worldChunkSize,settings))
            { tile.mask=null;ReleaseTexture(tile);DirtyNeighbours(coord); }
            else if(tile.mask.Dirty) DirtyNeighbours(coord);
        }
        void DirtyNeighbours(Vector2Int coord)
        {
            for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
                if(tiles.TryGetValue(coord+new Vector2Int(x,y),out var tile)&&tile.mask!=null)tile.mask.Dirty=true;
        }
        void Upload(Vector2Int coord,Tile tile)
        {
            int n=SnowTrackMask.TextureSize, res=SnowTrackMask.Resolution, border=SnowTrackMask.Border;
            for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
                if((x!=0||y!=0)&&tiles.TryGetValue(coord+new Vector2Int(x,y),out var neighbour)&&neighbour.mask!=null)
                    Age(coord+new Vector2Int(x,y),neighbour);
            if(tile.texture==null)
            {
                var format=SystemInfo.SupportsTextureFormat(TextureFormat.RG16)?TextureFormat.RG16:TextureFormat.RGBA32;
                tile.texture=new Texture2D(n,n,format,false,true) { name="Snow tracks "+coord,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp };
                tile.upload=new byte[n*n*(format==TextureFormat.RG16?2:4)];
            }
            int channels=tile.texture.format==TextureFormat.RG16?2:4;
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                int px=x-border,py=y-border,dx=px<0?-1:px>=res?1:0,dy=py<0?-1:py>=res?1:0;
                int dst=(y*n+x)*channels;byte r=0,g=0;
                if(tiles.TryGetValue(coord+new Vector2Int(dx,dy),out var other)&&other.mask!=null)
                {
                    if(other.mask!=null)
                    { var pixels=other.mask.Pixels;int src=(((py+res)%res)*res+(px+res)%res)*2;r=pixels[src];g=pixels[src+1]; }
                }
                tile.upload[dst]=r;tile.upload[dst+1]=g;
                if(channels==4){tile.upload[dst+2]=0;tile.upload[dst+3]=255;}
            }
            tile.texture.LoadRawTextureData(tile.upload);tile.texture.Apply(false,false);BindTexture(tile,tile.texture);
            // Border sampling may have unpacked an invisible neighbour. Return it to compact storage.
            for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
                if(tiles.TryGetValue(coord+new Vector2Int(x,y),out var other)&&!other.visible)
                { other.mask?.Compress();if(other.renderer==null)other.surface.Compress(); }
        }
        void BindTexture(Tile tile,Texture texture)
        {
            if(tile.renderer==null)return;
            tile.renderer.GetPropertyBlock(properties);
            properties.SetTexture("_SnowTracks",texture!=null?texture:Texture2D.blackTexture);
            properties.SetFloat("_SnowTracksEnabled",texture!=null?1:0);
            properties.SetVector("_SnowTrackUV",new Vector4((float)SnowTrackMask.Resolution/SnowTrackMask.TextureSize,(float)SnowTrackMask.Border/SnowTrackMask.TextureSize,1f/SnowTrackMask.TextureSize,MapGenerator.worldChunkSize/SnowTrackMask.Resolution));
            tile.renderer.SetPropertyBlock(properties);
        }
        void ReleaseTexture(Tile tile)
        {
            BindTexture(tile,null);
            if(tile.texture!=null) { if(Application.isPlaying) Destroy(tile.texture);else DestroyImmediate(tile.texture); }
            tile.texture=null;tile.upload=null;
        }
        void OnDisable()
        {
            if(particles!=null) particles.Clear();
            if(properties!=null)foreach(var tile in tiles.Values)ReleaseTexture(tile);
        }
        void OnEnable()
        {
            foreach(var tile in tiles.Values)if(tile.mask!=null)tile.mask.Dirty=true;
            if(initialized && mode==SnowTrackMode.MobileParticles && particles!=null)particles.Initialize();
        }
        void OnDestroy() { if(particles!=null) particles.Clear();foreach(var tile in tiles.Values) ReleaseTexture(tile);tiles.Clear(); }
    }
}
