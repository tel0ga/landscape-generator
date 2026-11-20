using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace SnowTracks
{
    public sealed class SnowTrackMask
    {
        public const int Resolution = 256;
        public const int Border = 2;
        public const int TextureSize = Resolution + Border * 2;
        byte[] pixels, compressed;
        public double LastSnowfall { get; private set; }
        public float PendingMeltThreshold;
        public bool Dirty = true;
        public SnowTrackMask(double snowfall, float threshold)
        { LastSnowfall = snowfall; PendingMeltThreshold = threshold; pixels = new byte[Resolution * Resolution * 2]; }
        public byte[] Pixels
        {
            get
            {
                if (pixels == null)
                {
                    pixels = new byte[Resolution * Resolution * 2];
                    using (var input = new MemoryStream(compressed))
                    using (var stream = new DeflateStream(input, CompressionMode.Decompress))
                    {
                        int offset = 0, read;
                        while (offset < pixels.Length && (read = stream.Read(pixels, offset, pixels.Length-offset)) > 0) offset += read;
                        if (offset != pixels.Length) throw new InvalidDataException("Incomplete snow mask");
                    }
                    compressed = null;
                }
                return pixels;
            }
        }
        public bool IsCompressed => pixels == null;
        public static bool HasSnow(Vector2 centre,float radius,SnowSurfaceQuery surface,Vector2 origin,float size,SnowSurfaceSettings settings)
        {
            Vector2 lower=(centre-Vector2.one*radius-origin)*(Resolution/size), upper=(centre+Vector2.one*radius-origin)*(Resolution/size);
            for(int y=Mathf.Max(0,Mathf.FloorToInt(lower.y));y<=Mathf.Min(Resolution-1,Mathf.CeilToInt(upper.y));y++)
                for(int x=Mathf.Max(0,Mathf.FloorToInt(lower.x));x<=Mathf.Min(Resolution-1,Mathf.CeilToInt(upper.x));x++)
                {
                    Vector2 point=origin+new Vector2(x+.5f,y+.5f)*(size/Resolution);
                    if((point-centre).sqrMagnitude<=radius*radius&&surface.Weight(point,settings)>=.05f)return true;
                }
            return false;
        }
        public void Compress()
        {
            if (pixels == null) return;
            using (var output = new MemoryStream())
            {
                using (var stream = new DeflateStream(output, System.IO.Compression.CompressionLevel.Fastest, true)) stream.Write(pixels,0,pixels.Length);
                compressed = output.ToArray();
            }
            pixels = null;
        }
        public bool Advance(double snowfall, SnowSurfaceQuery surface, Vector2 origin, float size, SnowSurfaceSettings settings)
        {
            int depthLoss = (int)Math.Min(255, Math.Floor(snowfall * 255 / 120) - Math.Floor(LastSnowfall * 255 / 120));
            int trailLoss = (int)Math.Min(255, Math.Floor(snowfall * 255 / 300) - Math.Floor(LastSnowfall * 255 / 300));
            float currentThreshold=settings.threshold;
            settings.threshold = Mathf.Max(settings.threshold, PendingMeltThreshold);
            bool any = false;
            var data = Pixels;
            for (int y=0; y<Resolution; y++) for (int x=0; x<Resolution; x++)
            {
                int i = (y*Resolution+x)*2;
                int r = Math.Max(0, data[i]-depthLoss), g = Math.Max(0,data[i+1]-trailLoss);
                if ((r != 0 || g != 0) && surface.Weight(origin + new Vector2(x+.5f,y+.5f)*(size/Resolution), settings) < .05f) r=g=0;
                if (r != data[i] || g != data[i+1]) Dirty = true;
                data[i]=(byte)r; data[i+1]=(byte)g; any |= r!=0 || g!=0;
            }
            LastSnowfall = snowfall; PendingMeltThreshold = currentThreshold;
            return any;
        }
        public bool Stamp(Vector2 centre, Vector2 direction, Vector2 footSize, float trailWidth, SnowSurfaceQuery surface,
            Vector2 origin, float size, SnowSurfaceSettings settings,float trailStrength=1,bool trailOnly=false)
        {
            float radius = Mathf.Max(footSize.magnitude*.5f, trailWidth*.5f);
            Vector2 lower = (centre-Vector2.one*radius-origin)*(Resolution/size), upper = (centre+Vector2.one*radius-origin)*(Resolution/size);
            int x0=Mathf.Clamp(Mathf.FloorToInt(lower.x),0,Resolution-1), x1=Mathf.Clamp(Mathf.CeilToInt(upper.x),0,Resolution-1);
            int y0=Mathf.Clamp(Mathf.FloorToInt(lower.y),0,Resolution-1), y1=Mathf.Clamp(Mathf.CeilToInt(upper.y),0,Resolution-1);
            Vector2 right = new Vector2(direction.y,-direction.x);
            var data = Pixels; bool changed=false;
            for (int y=y0;y<=y1;y++) for (int x=x0;x<=x1;x++)
            {
                Vector2 point=origin+new Vector2(x+.5f,y+.5f)*(size/Resolution), delta=point-centre;
                float snow=surface.Weight(point,settings); if(snow<.05f) continue;
                float u=Vector2.Dot(delta,right)/Mathf.Max(.01f,footSize.x*.5f), v=Vector2.Dot(delta,direction)/Mathf.Max(.01f,footSize.y*.5f);
                float sole=trailOnly?0:Mathf.Clamp01((1-u*u-v*v)*4);
                float broad=Mathf.Clamp01(1-delta.sqrMagnitude/Mathf.Max(.0001f,trailWidth*trailWidth*.25f));
                int i=(y*Resolution+x)*2;
                byte r=(byte)Mathf.Max(data[i],Mathf.RoundToInt(sole*snow*255));
                byte g=(byte)Mathf.Min(255,data[i+1]+Mathf.RoundToInt(broad*snow*26*Mathf.Clamp(trailStrength,0,10)));
                if(r!=data[i] || g!=data[i+1]) { data[i]=r;data[i+1]=g;changed=true; }
            }
            Dirty |= changed; return changed;
        }
    }
}
