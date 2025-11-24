using K4os.Compression.LZ4;

namespace Scene.Chunks;

public abstract class TerrainChunk
{
    private List<float>[]? _nodes;
    private List<List<float>?[]>? _jaggedNodes;

    public Terrain Terrain { get; set; } = null!;
    public abstract string Name { get; }
    public uint Type { get; set; }
    public int Version { get; set; }
    public uint Offset { get; set; }
    public int PngSize { get; set; }
    public int DecompSize { get; set; }

    public bool TryGetNodes(out List<float>[] nodes)
    {
        if (this._nodes != null)
            nodes = this._nodes;
        else
            nodes = new List<float>[0];

        return this._nodes != null;
    }
    public bool TryGetNodes(out List<List<float>?[]> nodes)
    {
        if (this._jaggedNodes != null)
            nodes = this._jaggedNodes;
        else
            nodes = new List<List<float>?[]>();

        return this._jaggedNodes != null;
    }

    public void SetNodes(List<float>[]? nodes)
    {
        this._nodes = nodes;
    }

    public void SetNodes(List<List<float>?[]>? nodes)
    {
        this._jaggedNodes = nodes;
    }

    protected virtual void ReadHead(BinaryReader reader)
    {
        // Chunk header repeated of sorts
        reader.ReadInt64(); // Count??
        reader.ReadInt64(); // Decomp Size
        reader.ReadInt64(); // PNG Size
        reader.ReadInt64(); // Reserve or Separator

        // These 8 bytes are the same set of bytes that are in the related terrain_ed.bin, so it's probably an id
        reader.ReadUInt32(); // Possibly a checksum. Maybe an ID.
        reader.ReadUInt32(); // Could be the max size of a chunk??? LZ4 tends to limit chunks to 32kb
    }

    protected virtual byte[] DecompChunk(BinaryReader reader)
    {
        int remainingLength = this.PngSize - 40; // 40 being the num bytes read in the header
        byte[] compData = reader.ReadBytes(remainingLength);
        byte[] output = new byte[this.DecompSize];
        LZ4Codec.Decode(compData, 0, compData.Count(), output, 0, this.DecompSize);
        return output;
    }

    public abstract void ReadChunk(BinaryReader reader, int numNodes);
}