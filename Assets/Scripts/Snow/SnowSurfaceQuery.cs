using UnityEngine;

namespace SnowTracks
{
    public struct SnowSurfaceSettings
    {
        public float threshold, coverageBlend, valleys, rockThreshold, terrainBlend, waterThreshold, ppu;
        public static SnowSurfaceSettings Read(Material material) => new SnowSurfaceSettings
        {
            threshold = material.GetFloat("_SnowCoverageThreshold"), coverageBlend = material.GetFloat("_SnowCoverageBlend"),
            valleys = material.GetFloat("_SnowInValleys"), rockThreshold = material.GetFloat("_Rock2Threshold"),
            terrainBlend = material.GetFloat("_BlendWidth"), waterThreshold = material.GetFloat("_WaterThreshold"),
            ppu = Mathf.Max(1, material.GetFloat("_PPU"))
        };
    }

    // Samples the same raw height and padded snow data as the terrain shader; never regenerates noise.
    public sealed class SnowSurfaceQuery
    {
        float[,] heights, snow;
        byte[] archive;
        readonly int heightWidth,heightHeight,snowWidth,snowHeight;
        readonly Vector2 origin;
        readonly float size, padding;
        public SnowSurfaceQuery(MapData data, Vector2 mapCentre, float chunkSize, int snowPadding)
        {
            heights = data.heightMap; snow = data.snowMap; origin = mapCentre - Vector2.one * chunkSize * .5f; size = chunkSize; padding = (float)snowPadding / data.snowRes;
            heightWidth=heights.GetLength(0);heightHeight=heights.GetLength(1);snowWidth=snow.GetLength(0);snowHeight=snow.GetLength(1);
        }
        public void Compress()
        {
            if(heights==null)return;
            byte[] raw=new byte[(heights.Length+snow.Length)*4];
            System.Buffer.BlockCopy(heights,0,raw,0,heights.Length*4);System.Buffer.BlockCopy(snow,0,raw,heights.Length*4,snow.Length*4);
            using(var output=new System.IO.MemoryStream())
            {
                using(var stream=new System.IO.Compression.DeflateStream(output,System.IO.Compression.CompressionLevel.Fastest,true))stream.Write(raw,0,raw.Length);
                archive=output.ToArray();
            }
            heights=null;snow=null;
        }
        void Unpack()
        {
            if(heights!=null)return;
            byte[] raw=new byte[(heightWidth*heightHeight+snowWidth*snowHeight)*4];
            using(var input=new System.IO.MemoryStream(archive))
            using(var stream=new System.IO.Compression.DeflateStream(input,System.IO.Compression.CompressionMode.Decompress))
            {
                int offset=0,read;
                while(offset<raw.Length&&(read=stream.Read(raw,offset,raw.Length-offset))>0)offset+=read;
                if(offset!=raw.Length)throw new System.IO.InvalidDataException("Incomplete snow surface archive");
            }
            heights=new float[heightWidth,heightHeight];snow=new float[snowWidth,snowHeight];
            System.Buffer.BlockCopy(raw,0,heights,0,heights.Length*4);System.Buffer.BlockCopy(raw,heights.Length*4,snow,0,snow.Length*4);archive=null;
        }
        public float Weight(Vector2 mapPoint, SnowSurfaceSettings settings)
        {
            Unpack();
            mapPoint = new Vector2(Mathf.Floor(mapPoint.x * settings.ppu), Mathf.Floor(mapPoint.y * settings.ppu)) / settings.ppu;
            Vector2 uv = (mapPoint - origin) / size;
            float height = heights[Mathf.Clamp(Mathf.FloorToInt(uv.x * heights.GetLength(0)), 0, heights.GetLength(0)-1),
                                   Mathf.Clamp(Mathf.FloorToInt(uv.y * heights.GetLength(1)), 0, heights.GetLength(1)-1)];
            // No tracks on water, including the terrain's blended shoreline.
            float land = Smooth(settings.waterThreshold - settings.terrainBlend, settings.waterThreshold + settings.terrainBlend, height);
            if (land < .99f) return 0;
            Vector2 snowUV = uv * (1 - 2 * padding) + Vector2.one * padding;
            float coverage = Bilinear(snow, snowUV);
            float high = Smooth(settings.rockThreshold - settings.terrainBlend, settings.rockThreshold + settings.terrainBlend, height);
            return Mathf.Clamp01(high + Smooth(settings.threshold - settings.coverageBlend, settings.threshold + settings.coverageBlend, coverage) * settings.valleys);
        }
        public static float Smooth(float low, float high, float value)
        { if (high <= low) return value >= low ? 1 : 0; float t = Mathf.Clamp01((value - low) / (high - low)); return t*t*(3-2*t); }
        static float Bilinear(float[,] map, Vector2 uv)
        {
            float x = uv.x * map.GetLength(0) - .5f, y = uv.y * map.GetLength(1) - .5f;
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            int x0 = Mathf.Clamp(ix,0,map.GetLength(0)-1), x1 = Mathf.Clamp(ix+1,0,map.GetLength(0)-1);
            int y0 = Mathf.Clamp(iy,0,map.GetLength(1)-1), y1 = Mathf.Clamp(iy+1,0,map.GetLength(1)-1);
            return Mathf.Lerp(Mathf.Lerp(map[x0,y0], map[x1,y0], x-ix), Mathf.Lerp(map[x0,y1], map[x1,y1], x-ix), y-iy);
        }
    }
}
