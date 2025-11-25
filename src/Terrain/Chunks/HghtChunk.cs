using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Windows.Media.Imaging;
using Scene.Helpers;

namespace Scene.Chunks;

public class HghtChunk : TerrainChunk
{
    public override string Name { get; } = "hght";
    public uint[] NodeOffsets { get; private set; } = null!;

    public override void ReadChunk(BinaryReader reader, int numNodes)
    {
        /* HGHT:
            The pngs are heightmaps for EACH node.
            Most will be 16-bit grayscale heightmaps
            Go into the .xscene to find each node's min/max height and then we should be able to use the pixel data with that to get the height.

            heightmap_level, normalmap_level etc
            There's the minimum detail level and the ideal level
            Delta heightmaps??? Wtf is a delta heightmap 
        */

        // Each of the chunks except for MIDX, has the offsets for each node's png above the compressed png data 
        this.NodeOffsets = new uint[numNodes];
        for (int i = 0; i < numNodes; i++)
        {
            uint offset = reader.ReadUInt32();
            this.NodeOffsets[i] = offset;
        }

        this.ReadHead(reader);
        byte[] chunkData = this.DecompChunk(reader);

        // MemoryStream compStream = new MemoryStream(chunkData);

        // using (FileStream target = File.Create(this.Terrain.WorkingFile.FullName + this.Name + ".terrain.bin"))
        // {
        //     compStream.CopyTo(target);
        // }

        using (BinaryReader miniReader = new BinaryReader(new MemoryStream(chunkData), Encoding.UTF8, false))
        {
            List<float>[] nodes = new List<float>[numNodes];
            for (int i = 0; i < numNodes; i++)
            {
                List<float> data = this.ReadPNG(miniReader);
                nodes[i] = data;
            }

            int? x = this.Terrain?.XScene?.TerrainX;
            int? y = this.Terrain?.XScene?.TerrainY;
            if (x == null || y == null)
                throw new ArgumentNullException("The node dimensions are null");

            List<float>[] list = this.RotateList<List<float>>(nodes.ToList(), (int)x, (int)y).ToArray();
            this.SetNodes(list);
        }
    }

    public List<T> RotateList<T>(List<T> list, int height, int width)
    {
        T[][] arr = new T[height][];

        for (int h = 0; h < height; h++)
            arr[h] = new T[width];

        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                arr[i][j] = list[(j * height) + i];
            }
        }

        return arr.SelectMany(o => o).ToList();
    }

    private List<float> ReadPNG(BinaryReader reader)
    {
        int pngSize = reader.ReadInt32();
        byte[] pngBytes = reader.ReadBytes(pngSize);

        PngBitmapDecoder decoder = new PngBitmapDecoder(new MemoryStream(pngBytes), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
        BitmapSource bitmapSource = decoder.Frames[0];

        int width = bitmapSource.PixelWidth;
        int height = bitmapSource.PixelHeight;

        int stride = width * (bitmapSource.Format.BitsPerPixel / 8);
        byte[] pixels = new byte[height * stride];
        bitmapSource.CopyPixels(pixels, stride, 0);

        List<float> values = new List<float>();

        using (BinaryReader subReader = new BinaryReader(new MemoryStream(pixels), Encoding.UTF8, false))
        {
            int denom = (bitmapSource.Format.BitsPerPixel / 8);
            int range = pixels.Length / denom;

            // L16_UNORM
            for (int j = 0; j < range; j++)
            {
                // https://en.wikipedia.org/wiki/Feature_scaling

                // 1.3.X Scenes don't seem to have an arbitrary min/max value anymore. It now goes through the nodes to get the min/max
                // But they still use 500/-100 as the upper/lower bound even when it's not declared in the xscene
                float max = 500f;
                float min = -100f;

                // We do need to revisit this since scenes can change the min/max height of the scene.
                if (Terrain.XScene != null)
                {
                    max = Terrain.XScene.MaxTerrainHeight ?? max;
                    min = Terrain.XScene.MinTerrainHeight ?? min;
                }

                float val = subReader.ReadUInt16();
                float norm = UNORM.Decode16((uint)val); // Handles edge cases

                if (min > 0)
                {
                    // Feels like a hacky way to go about this
                    val = ((Math.Abs(max) - Math.Abs(min)) * norm);
                    if (min > 0) val += Math.Abs(min);
                }
                else
                {
                    val = ((Math.Abs(max) + Math.Abs(min)) * norm) - Math.Abs(min);
                }

                // We lose the same amount of precision as if we were just getting the percentage
                // values.Add(min + ((val - 0) * (max - min) / ((1 << bitmapSource.Format.BitsPerPixel) - 1 - 0)));
                // val = (val / 65535) * (max + Math.Abs(min)) - Math.Abs(min);
                // values.Add(((val / (1 << bitmapSource.Format.BitsPerPixel)) * (max + Math.Abs(min)) - Math.Abs(min)));

                values.Add(val);
            }
        }

        List<float> list = this.RotateList<float>(values, height, width);
        return list;
    }
}