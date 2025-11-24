using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Windows.Media.Imaging;

namespace Scene.Chunks;

public class PhymChunk : TerrainChunk
{
    public override string Name { get; } = "phym";
    public uint[] NodeOffsets { get; private set; } = null!;

    public override void ReadChunk(BinaryReader reader, int numNodes)
    {
        /*
            PHYM has 4 sets of data for each node
            starts off with the index, ranging from 0-3
            then the size, which looks to be the sum of the res
            And for the data, it looks like it's single byte values for each vertex
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
                List<float>?[] data = this.ReadPhyms(miniReader);
                nodes.Add(data);
            }

            this.SetNodes(nodes);
        }

    }

    private List<float>?[] ReadPhyms(BinaryReader reader)
    {
        List<float>[] lists = new List<float>[4];

        for (int i = 0; i < 4; i++)
        {
            List<float> values = new List<float>();

            uint index = reader.ReadUInt32();
            uint size = reader.ReadUInt32();

            for (uint j = 0; j < size; j++)
            {
                // Need to do something with this.
                float val = reader.ReadByte();
                values.Add(val);
            }

            lists[i] = values;
        }

        return lists;
    }
}