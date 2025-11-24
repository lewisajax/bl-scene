using K4os.Compression.LZ4;
using System.IO.Compression;
using System.Text;
using System.Buffers.Binary;
using System.Windows.Media.Imaging;
using JetBrains.Annotations;
using Scene.Chunks;

namespace Scene;

public class Terrain
{
    [UsedImplicitly]
    private BinaryReader _reader = null!;
    private FileStream _stream { get; set; }
    private int _version { get; set; }

    public int Version
    {
        get => this._version;
    }

    public FileInfo WorkingFile { get; init; }
    public XScene? XScene { get; init; }

    public long? SceneID { get; set; }


    public BinaryReader Reader
    {
        get { return this._reader; }
        private set
        {
            if (value.GetType() != typeof(BinaryReader))
                throw new NotSupportedException();

            this._reader = value;
        }
    }

    public TerrainChunk? MIDX { get; private set; }
    public TerrainChunk? HGHT { get; private set; }
    public TerrainChunk? NRML { get; private set; }
    public TerrainChunk? WGHT { get; private set; }
    public TerrainChunk? PHYM { get; private set; }

    public Terrain(FileInfo file, XScene? xscene = null)
    {
        if (xscene != null)
            this.XScene = xscene;

        if (!file.Exists)
            throw new FileNotFoundException();

        this.WorkingFile = file;

        FileStream stream = file.Open(FileMode.Open, FileAccess.Read);

        if (stream.CanSeek == false)
            throw new IOException();

        this._stream = stream;
    }

    public Terrain ReadFile()
    {
        using (this._reader = new BinaryReader(this._stream, Encoding.UTF8, false))
        {
            uint magic = this._reader.ReadUInt32();

            if (magic != 0x3652475A)
                throw new NotSupportedException();

            uint fourcc = this._reader.ReadUInt32(); // Always RTRN
            this._version = this._reader.ReadInt32();

            this.MIDX = this.ReadChunkHeader(new MidxChunk()); // Material Index
            this.HGHT = this.ReadChunkHeader(new HghtChunk());
            this.NRML = this.ReadChunkHeader(new NrmlChunk());
            this.WGHT = this.ReadChunkHeader(new WghtChunk());

            if (this._version == 2) // I don't believe that PHYM shows up in older versions. Current version is 2
                this.PHYM = this.ReadChunkHeader(new PhymChunk()); // Physics Material

            // Now that we're reading in the .xscene, we can simplify this if we want.
            // We only need to do this once. Then it's the same amount of bytes for HGHT, NRML and WGHT
            int numNodes = (int)(this.NRML.Offset - this.HGHT.PngSize - (this.MIDX.Offset + this.MIDX.PngSize)) / 4; // It's never gonna go into uint territory

            this.MIDX.ReadChunk(this._reader, 4);
            this.HGHT.ReadChunk(this._reader, numNodes);
            this.NRML.ReadChunk(this._reader, numNodes);
            this.WGHT.ReadChunk(this._reader, numNodes);

            if (this._version == 2 && this.PHYM != null)
                this.PHYM.ReadChunk(this._reader, numNodes);
        }

        return this;
    }

    private TerrainChunk ReadChunkHeader(TerrainChunk chunk)
    {
        chunk.Terrain = this;
        chunk.Type = this._reader.ReadUInt32();
        chunk.Version = this._reader.ReadInt32();
        chunk.Offset = this._reader.ReadUInt32();
        chunk.PngSize = this._reader.ReadInt32();
        chunk.DecompSize = this._reader.ReadInt32();

        return chunk;
    }

    public void Dispose()
    {
        this._stream?.Dispose();
        this._reader?.Dispose();
    }
}