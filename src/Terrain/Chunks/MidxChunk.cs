using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Windows.Media.Imaging;

namespace Scene.Chunks;

public class MidxChunk : TerrainChunk
{
    public override string Name { get; } = "midx";

    public override void ReadChunk(BinaryReader reader, int numNodes)
    {
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
                List<float> pngData = this.ReadPNG(miniReader);
                nodes[i] = pngData;
            }

            this.SetNodes(nodes);
        }
    }

    protected override void ReadHead(BinaryReader reader)
    {
        reader.ReadInt64();
        reader.ReadInt64();
        reader.ReadInt64();
        reader.ReadInt64();

        long sceneId = reader.ReadInt64();
        if (this.Terrain.SceneID == null)
            this.Terrain.SceneID = sceneId;
    }

    private List<float> ReadPNG(BinaryReader reader)
    {
        int pngSize = reader.ReadInt32();
        byte[] pngBytes = reader.ReadBytes(pngSize);

        // MIDX doesn't have the res in it's ihdr chunk. Instead it has the node x and y values shifted to the left. 
        // The midx pngs change size based on how many nodes there are. 
        // The res doesnt make a difference, so there's probably a specific res value that all midx pngs use

        // Tried changing the values before we inflated the png
        // if (chunk.Name == "midx")
        // {
        //     byte[] ihdrWidth = BitConverter.GetBytes(BinaryPrimitives.ReadInt32LittleEndian(pngBytes.Take<byte>(new Range(16, 20)).ToArray()) << 8);
        //     byte[] ihdrHeight = BitConverter.GetBytes(BinaryPrimitives.ReadInt32LittleEndian(pngBytes.Take<byte>(new Range(20, 24)).ToArray()) << 8);

        //     byte[] newBytes = ihdrWidth.Concat<byte>(ihdrHeight).ToArray();
        //     for (int i = 16; i < 24; i++)
        //     {
        //         pngBytes[i] = newBytes[i % 16];
        //     }
        // }

        PngBitmapDecoder decoder = new PngBitmapDecoder(new MemoryStream(pngBytes), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
        BitmapSource bitmapSource = decoder.Frames[0];

        int width = bitmapSource.PixelWidth;
        int height = bitmapSource.PixelHeight;

        // The 4 midx pngs have large numbers in the ihdr, instead of the res. It makes sense, since there's only 4 midx pngs regardless of how many nodes there are.
        // These values are shifted to the left for some reason but move them back and we get the terrain dimensions not the res
        // if (chunk.Name == "midx")
        // {
        //     width >>= 8;
        //     height >>= 8;
        // }

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

                // values.Add(min + ((val - 0) * (max - min) / ((1 << bitmapSource.Format.BitsPerPixel) - 1 - 0)));
                values.Add(val / 255);
            }
        }

        return values;
    }

    private void ReadIDAT(BinaryReader reader)
    {
        int pngSize = reader.ReadInt32();

        reader.ReadUInt32(); // Magic
        reader.ReadUInt32(); // DOS stuff

        int idhrLength = BinaryPrimitives.ReadInt32BigEndian(reader.ReadBytes(4)); // IHDR length
        reader.ReadUInt32(); // Type
        reader.ReadBytes(idhrLength); // Data
        reader.ReadUInt32(); // CRC

        // Ancillary chunk
        int physLength = BinaryPrimitives.ReadInt32BigEndian(reader.ReadBytes(4)); // pHYs length
        reader.ReadUInt32(); // Type
        reader.ReadBytes(physLength); // Data
        reader.ReadUInt32(); // CRC

        int idatLength = BinaryPrimitives.ReadInt32BigEndian(reader.ReadBytes(4)); // IDAT length
        reader.ReadUInt32(); // Type
        byte[] idatData = reader.ReadBytes(idatLength); // Data
        reader.ReadUInt32(); // CRC

        ZLibStream zlibStream = new ZLibStream(new MemoryStream(idatData), CompressionMode.Decompress);

        using (var target = File.Create(Terrain.WorkingFile.FullName + this.Name + ".bin"))
        {
            zlibStream.CopyTo(target);
        }

        int iendLength = BinaryPrimitives.ReadInt32BigEndian(reader.ReadBytes(4)); // IEND length
        reader.ReadUInt32(); // Type
        reader.ReadUInt32(); // CRC
    }
}