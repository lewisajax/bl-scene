using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Windows.Media.Imaging;
using Scene.Helpers;

namespace Scene.Chunks;

public class NrmlChunk : TerrainChunk
{
    public override string Name { get; } = "nrml";
    public uint[] NodeOffsets { get; private set; } = null!;

    public override void ReadChunk(BinaryReader reader, int numNodes)
    {
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
            List<float>[] nodes = new List<float>[numNodes];
            for (int i = 0; i < numNodes; i++)
            {
                List<float> data = this.ReadPNG(miniReader);
                nodes[i] = data;
            }

            this.SetNodes(nodes);
        }
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

        // MemoryStream compStream = new MemoryStream(pixels);

        // using (FileStream target = File.Create(this.Terrain.WorkingFile.FullName + this.Name + ".terrain.bin"))
        // {
        //     compStream.CopyTo(target);
        // }

        List<float> values = new List<float>();

        // R32F
        using (BinaryReader subReader = new BinaryReader(new MemoryStream(pixels), Encoding.UTF8, false))
        {
            int denom = (bitmapSource.Format.BitsPerPixel / 8);
            int range = pixels.Length / denom;

            // Normals seem to go up to 32bit pngs, so we'll need to do a subReader.ReadSingle(). Not sure if they can be lower or not yet.

            if (this.Terrain?.XScene?.TerrainY == null || this.Terrain?.XScene?.TerrainX == null)
                throw new ArgumentNullException();

            List<float>[] initialValues = new List<float>[(int)this.Terrain.XScene.TerrainY]; // terrain.bin is either row or column based and terrain_ed is the opposite

            // Even if the normals are messed up in the recompiled terrain.bin, it seems the editor doesn't care.
            // So it might be using the normals from terrain.bin instead of the normals in _ed, not 100% on this.
            for (int j = 0; j < range; j++)
            {
                int listInd = range % (int)this.Terrain.XScene.TerrainX;

                if (initialValues[listInd] == null)
                    initialValues[listInd] = new List<float>();

                // I've yet to get the normals working
                float[] tempVals = new float[4];
                for (int k = 0; k < 4; k++)
                {
                    int val = subReader.ReadByte();

                    // tempVals[k] = ((float)val / 127.5f) - 1.0f;
                    // values2.Add(((Convert.ToDouble(val) / 2) - 0.5) / 127);

                    // Half ramp is not the right way to do this and it's definitely gonna break in some cases
                    // val = val == 63 ? 0 : val;
                    // val = val >= 127 ? val % 127 : val;
                    // tempVals[k] = val / 127;

                    // float normalised = SNORM.Decode8(val);
                    // val = ((Math.Abs(max) + Math.Abs(min)) * normalised) - Math.Abs(min);
                    // tempVals[k] = val / 127;
                    tempVals[k] = ((float)val / 255) + 0.5f;
                }

                initialValues[listInd].AddRange(tempVals);
                // initialValues[listInd].AddRange(new float[] { tempVals[1], tempVals[2], tempVals[0], 0f });
            }

            values = initialValues.Aggregate(new List<float>(), (curr, x) =>
            {
                if (x is List<float>)
                    curr.AddRange(x);
                return curr;
            });
        }

        return values;
    }
}