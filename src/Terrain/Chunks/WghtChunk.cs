using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Windows.Media.Imaging;

namespace Scene.Chunks;

public class WghtChunk : TerrainChunk
{
    public override string Name { get; } = "wght";
    public uint[] NodeOffsets { get; private set; } = null!;

    public override void ReadChunk(BinaryReader reader, int numNodes)
    {
        /* WGHT:
            We might have to go by the weightmap levels in the xscene
            We'll find out soon enough
        */

        this.NodeOffsets = new uint[numNodes];
        for (int i = 0; i < numNodes; i++)
        {
            uint offset = reader.ReadUInt32();
            this.NodeOffsets[i] = offset;
        }

        this.ReadHead(reader);
        byte[] chunkData = this.DecompChunk(reader);

        using (BinaryReader miniReader = new BinaryReader(new MemoryStream(chunkData), Encoding.UTF8, false))
        {
            List<List<float>?[]>? nodes = new List<List<float>?[]>();
            for (int i = 0; i < numNodes; i++)
            {
                List<float>?[] data = this.ReadWghts(miniReader);
                nodes.Add(data);
            }

            int? x = this.Terrain?.XScene?.TerrainX;
            int? y = this.Terrain?.XScene?.TerrainY;
            if (x == null || y == null)
                throw new ArgumentNullException("The node dimensions are null");
                
            List<List<float>?[]> list = this.RotateList(nodes, (int)x, (int)y);
            this.SetNodes(list);
        }
    }

    private List<float>?[] ReadWghts(BinaryReader reader)
    {
        List<float>?[] weightMaps = new List<float>[16];

        for (int i = 0; i < 16; i++)
        {
            // It goes through each of the 15 paint layers. 
            // If a paint layer is used on the node, then it will have a 1 before the png size and the png itself, else it will be 0 and no png
            uint hasPaint = reader.ReadUInt32();

            if (hasPaint == 1)
                weightMaps[i] = this.ReadPNG(reader);
            else
                weightMaps[i] = null;
        }

        return weightMaps;
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

            for (int j = 0; j < range; j++)
            {
                float val;
                switch (denom)
                {
                    case 4:
                        val = subReader.ReadSingle();
                        break;
                    case 2:
                        val = subReader.ReadUInt16();
                        break;
                    case 1:
                    default:
                        val = subReader.ReadByte();
                        break;
                }

                values.Add((val / 255)); // Byte values
                // values.Add(val);
            }
        }

        List<float> list = this.RotateList<float>(values, height, width);
        return list;
    }
}