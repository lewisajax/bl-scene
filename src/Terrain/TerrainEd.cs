using K4os.Compression.LZ4;
using System.IO.Compression;
using System.Text;
using System.Buffers.Binary;
using System.Windows.Media.Imaging;
using JetBrains.Annotations;
using K4os.Compression.LZ4.Internal;
using K4os.Compression.LZ4.Encoders;

namespace Scene;

public class TerrainEd
{
    [UsedImplicitly]
    private BinaryReader _reader = null!; 
    private FileStream? _stream { get; set; }
    private int _version { get; set; }

    public FileInfo? WorkingFile { get; init; }

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

    public static int[] DetailLevels { get; } = new int[] { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024 };

    public Terrain? Terrain { get; init; }
    public XScene? XScene { get; init; }

    public TerrainEd(FileInfo file)
    {
        if (!file.Exists)
            throw new FileNotFoundException();

        this.WorkingFile = file;

        FileStream stream = file.Open(FileMode.Open, FileAccess.Read);

        if (stream.CanSeek == false)
            throw new IOException();

        this._stream = stream;
    }

    public TerrainEd(Terrain terrain, XScene xscene)
    {
        this.Terrain = terrain;
        this.XScene = xscene;
    }

    public bool WriteFile(string outPath)
    {
        if (this.Terrain == null || this.XScene == null)
            throw new ArgumentNullException("Need the terrain and xscene data");

        if (this.XScene.SceneName == null)
            throw new ArgumentNullException("No scene name");

        DirectoryInfo edDir = Directory.CreateDirectory(Path.Combine(outPath, this.XScene.SceneName));

        int storageSize = this.GetStorageSize();
        byte[] compBytes = null!;

        using (MemoryStream stream = new MemoryStream(storageSize))
        {
            BinaryWriter binWriter = new BinaryWriter(stream);

            try
            {
                this.BuildEditData(binWriter);
                binWriter.Flush();

                stream.Seek(0, SeekOrigin.Begin);

                // using (FileStream target = File.Create(Path.Combine(edDir.FullName + "/terrain_ed_decomp.bin")))
                // {
                //     stream.CopyTo(target);
                // }

                compBytes = this.CompressDecomp(stream);
            }
            finally
            {
                binWriter.Dispose();
            }
        }


        using (BinaryWriter binWriter = new BinaryWriter(File.Create(Path.Combine(edDir.FullName, "terrain_ed.bin"))))
        {
            this.WriteHeader(binWriter, compBytes.Count(), storageSize);
            binWriter.Write(compBytes);
        }

        return true;
    }

    private void WriteHeader(BinaryWriter writer, long compSize, long actualSize)
    {
        writer.Write(0x31304B4F); // Magic
        writer.Write(compSize + 40);
        writer.Write((long)2);
        writer.Write(actualSize);
        writer.Write(compSize + 40);
        writer.Write((long)0);

        if (this.Terrain == null || this.Terrain?.SceneID == null)
            throw new ArgumentNullException("The scene id is null");

        writer.Write((long)this.Terrain.SceneID);
    }

    private int GetStorageSize()
    {
        int size = 0;

        // Terrain dimensions
        size += 2;

        int? numNodes = (int?)(this.XScene?.TerrainX * this.XScene?.TerrainY);

        if (numNodes == null)
            throw new ArgumentNullException();

        // All the other filler stuff
        size += 43 * (int)numNodes;

        // if (this.Terrain?.MIDX != null && this.Terrain.MIDX.TryGetNodes(out List<float>?[] midxNodes))
        //     midxNodes.All((x) => { size += x.Count; return true; });

        if (this.Terrain?.HGHT != null && this.Terrain.HGHT.TryGetNodes(out List<float>?[] hghtNodes))
            hghtNodes.All((x) => { size += x != null ? x.Count : 0; return true; });

        if (this.Terrain?.NRML != null && this.Terrain.NRML.TryGetNodes(out List<float>?[] nrmlNodes))
            nrmlNodes.All((x) => { size += x != null ? x.Count : 0; return true; });

        if (this.Terrain?.WGHT != null && this.Terrain.WGHT.TryGetNodes(out List<List<float>?[]> wghtNodes))
        {
            wghtNodes.All((x) =>
            {
                foreach (List<float>? wghtMap in x)
                {
                    if (wghtMap != null)
                        size += wghtMap.Count + 1;
                }
                return true;
            });
        }

        if (this.Terrain?.PHYM != null && this.Terrain.PHYM.TryGetNodes(out List<List<float>?[]> phymNodes))
        {
            phymNodes.All((x) =>
            {
                foreach (List<float>? phym in x)
                {
                    if (phym != null)
                        size += phym.Count;
                }
                return true;
            });
        }

        return size * 4;
    }

    private void BuildEditData(BinaryWriter bw)
    {
        if (this.XScene == null)
            throw new ArgumentNullException();

        // Terrain Dimensions
        if (this.XScene.TerrainX == null || this.XScene.TerrainY == null)
            throw new ArgumentNullException("No terrain dimensions defined in xscene");

        bw.Write((int)this.XScene.TerrainX);
        bw.Write((int)this.XScene.TerrainY);

        if (this.XScene.Nodes == null)
            throw new ArgumentNullException("We need the nodes from the xscene");

        int numNodes = this.XScene.Nodes.Count();

        int currNode = 0;
        for (int i = 0; i < this.XScene.TerrainX; i++)
        {
            for (int j = 0; j < this.XScene.TerrainY; j++)
            {
                XSceneNode node = this.XScene.Nodes[currNode];

                // Node x and y index
                bw.Write(i);
                bw.Write(j);

                // I don't know the order yet, I'm shooting blind. Need to compare a node in terrain_ed with the node in xscene
                bw.Write(node.SummerMask);
                bw.Write(node.FallMask);
                bw.Write(node.WinterMask);
                bw.Write(node.SpringMask);

                int normRes = TerrainEd.DetailLevels[node.NormalRes] + 1;

                // Normalmap res
                bw.Write(normRes);
                bw.Write(normRes);

                if (this.Terrain?.NRML == null)
                    throw new ArgumentNullException();

                if (this.Terrain.NRML.TryGetNodes(out List<float>?[] nrmlNodes))
                {
                    List<float>? nrmlNode = nrmlNodes[currNode];

                    if (nrmlNode != null)
                    {
                        for (int k = 0; k < ((normRes * normRes)); k++)
                        {
                            bw.Write(nrmlNode[k]);
                            bw.Write(nrmlNode[k+1]);
                            bw.Write(nrmlNode[k+2]);
                            bw.Write(nrmlNode[k+3]);
                        }
                    }
                }
                else
                {
                    throw new ArgumentNullException("No nodes in NRML");
                }

                // // There are always 4 sets of values in MIDX
                // // It could be the same amount of bytes as the normals
                // foreach (List<float> midxNode in this.Terrain.MIDX.Nodes)
                // {
                //     for (int k = 0; k < (heightRes * heightRes); k++)
                //     {
                //         bw.Write(midxNode[k]);
                //     }
                // }

                // if (this.Terrain?.MIDX == null)
                //     throw new ArgumentNullException();

                // if (this.Terrain.MIDX.TryGetNodes(out List<float>?[] midxNodes))
                // {
                //     for (int l = 0; l < 4; l++)
                //     {
                //         List<float>? midxNode = midxNodes[l];

                //         if (midxNode == null)
                //             continue;

                //         for (int k = 0; k < (heightRes * heightRes); k++)
                //         {
                //             bw.Write(midxNode[k]);
                //             size += 4;
                //         }
                //     }
                // }
                // else
                // {
                //     throw new ArgumentNullException("No nodes in PHYM");
                // }

                int heightRes = TerrainEd.DetailLevels[node.HeightRes] + 1; // If there's no xscene to be had, we can fallback to the png's ihdr/phys. Cant remember which 1 has the res

                bw.Write(heightRes);
                bw.Write(heightRes);

                // Temporary
                for (int k = 0; k < (heightRes * heightRes); k++)
                {
                    bw.Write((int)0);
                }

                // if (this.Terrain?.PHYM == null)
                //     throw new ArgumentNullException();

                // if (this.Terrain.PHYM.TryGetNodes(out List<List<float>?[]> phymNodes))
                // {
                //     for (int l = 0; l < 4; l++)
                //     {
                //         List<float>? phymNode = phymNodes[currNode][l];

                //         if (phymNode == null)
                //             continue;

                //         for (int k = 0; k < (heightRes * heightRes); k++)
                //         {
                //             bw.Write(phymNode[k]);
                //         }
                //     }
                // }
                // else
                // {
                //     throw new ArgumentNullException("No nodes in PHYM");
                // }

                // I'm still not sure where the 32 is coming from
                // It could be the weights as there can only be 16 paint layers
                // Maybe it's the resolutions for the different nodes and their weight maps
                for (int k = 0; k < 32; k++)
                {
                    // We will need to change this but for now it dont matter
                    bw.Write(heightRes);
                }

                if (this.Terrain?.HGHT == null)
                    throw new ArgumentNullException();

                if (this.Terrain.HGHT.TryGetNodes(out List<float>?[] hghtNodes))
                {
                    List<float>? hghtNode = hghtNodes[currNode];

                    if (hghtNode != null)
                    {
                        //  float[] extraHghtValues = new float[];
                        for (int k = 0; k < (heightRes * heightRes); k++)
                        {
                            bw.Write(hghtNode[k]);
                        }
                    }
                }
                else
                {
                    throw new ArgumentNullException("No nodes in HGHT");
                }

                if (this.Terrain?.WGHT == null)
                    throw new ArgumentNullException();

                if (this.Terrain.WGHT.TryGetNodes(out List<List<float>?[]> wghtNodes))
                {
                    int numMaterials = 0;
                    wghtNodes[currNode].All((x) =>
                    {
                        if (x != null)
                            numMaterials++;

                        return true;
                    });

                    bw.Write(numMaterials);
                    
                    // There's always at least 1 material
                    // TODO: It looks like weightmaps can have different resolutions in the xscene
                    for (int y = 0; y < 15; y++)
                    {
                        List<float>? wghtNode = wghtNodes[currNode][y];

                        if (wghtNode == null)
                            continue;

                        // Layer index
                        bw.Write(y + 1); // 1 based indexing

                        // Will need to use the res for the weightmap instead
                        for (int k = 0; k < (heightRes * heightRes); k++)
                        {
                            bw.Write(wghtNode[k]);
                        }
                    }
                }
                else
                {
                    throw new ArgumentNullException("No nodes in WGHT");
                }

                bw.Flush();
                currNode++;
            }
        }
    }

    public TerrainEd ReadFile(string outPath)
    {
        MemoryStream decompStream;

        if (this._stream == null)
            throw new ArgumentNullException();

        using (this._reader = new BinaryReader(this._stream, Encoding.UTF8, false))
        {
            uint magic = this._reader.ReadUInt32();

            uint compressedSize = this._reader.ReadUInt32();
            this._reader.ReadSingle(); // Maybe reserve for if they wanted to save files larger than 4GB

            this._version = this._reader.ReadInt32();
            this._reader.ReadSingle();

            uint actualSize = this._reader.ReadUInt32();
            this._reader.ReadSingle(); // Prob reserve again

            this._reader.ReadUInt32(); // Compressed size again
            this._reader.ReadSingle();

            // This could be a checksum or maybe an id to another file, some terrain_eds just have 0s. I'm not sure what it could be
            this._reader.ReadByte(); // IDK
            this._reader.ReadBytes(3); // IDK
            this._reader.ReadUInt32(); // Not sure if it's an offset

            // This might be an id that gets shared between terrain_ed and terrain.bin
            this._reader.ReadUInt32(); // Might be a checksum, not sure yet
            this._reader.ReadUInt32(); // Could be a max size for the LZ4 sequences. LZ4 encoders tend to do 32KB. But then again, this changes by an extra 1 or so between files

            byte[] decompArr = new byte[actualSize];

            MemoryStream _ = new MemoryStream();
            this._reader.BaseStream.CopyTo(_);
            byte[] compData = _.ToArray(); // Might cause issues with terrain_ed files over 2GB, would need to only use streams

            LZ4Codec.Decode(compData, decompArr);

            decompStream = new MemoryStream(decompArr);

            using (FileStream target = File.Create(Path.Combine(outPath + "/terrain_ed_decomp.bin")))
            {
                decompStream.CopyTo(target);
            }
        }

        this.ReadDecomp(decompStream);

        return this;
    }

    private void ReadDecomp(MemoryStream decompStream)
    {
        using (this.Reader = new BinaryReader(decompStream, Encoding.UTF8, false))
        {
            uint xAxis = this.Reader.ReadUInt32();
            uint yAxis = this.Reader.ReadUInt32();
            uint nodeCount = xAxis * yAxis; // Don't think the game will let u save a high number of nodes, so we shouldnt worry.

            for (int i = 0; i < nodeCount; i++)
            {
                uint nodeXIndex = this.Reader.ReadUInt32();   
                uint nodeYIndex = this.Reader.ReadUInt32();   

                this.Reader.ReadBytes(16); // 4Bytes * 4. It's the layer masks for each season, look in .xscene layer_is_used_mask_summer="3039"

                uint resX = this.Reader.ReadUInt32(); // ResolutionX + 1 (4x4 will be 5x5) so each node with a res of 4x4, will have 25 verts 
                uint resY = this.Reader.ReadUInt32();
                uint numVerts = resX * resY;

                // Normals maybe???
                for (int j = 0; j < numVerts; j++)
                {
                    this.Reader.ReadSingle(); // X - This could be a separator and we shift XYZ down
                    this.Reader.ReadSingle(); // Y
                    this.Reader.ReadSingle(); // Z
                    this.Reader.ReadSingle(); // W or separator
                }

                this.Reader.ReadUInt32(); // ResX again
                this.Reader.ReadUInt32(); // ResY again

                // Might be weights
                for (int j = 0; j < numVerts; j++)
                {
                    this.Reader.ReadSingle(); // ???
                }

                // Idk where 32 is coming from
                // Could be for the weights. 16 paints layer
                // It's usually the full 32 res values, unless the bordering nodes are a higher/lower res
                // Then it's only like 4 or 8 values and the rest 0s
                for (int j = 0; j < 32; j++)
                {
                    // Res repeated, so it could be 16 * 2 4Bytes but where is the 16 coming from. 
                    // I don't think its 4x4 nodes * 2, since its still 32 for jagged sizes and larger numbers
                    // It could be separating the node into quadrants ready for connecting to a higher res node
                    // Which would mean the below "heights" loop wouldnt work
                    this.Reader.ReadSingle();
                }

                IEnumerable<byte> heights = new List<byte>();

                // Heights
                for (int j = 0; j < numVerts; j++)
                {
                    // this.Reader.ReadSingle()
                    heights = heights.Concat(this.Reader.ReadBytes(4));
                }

                // if (i == 0)
                // {
                //     using (var target = File.Create(this.WorkingFile.FullName + ".hght.bin"))
                //     {
                //         new MemoryStream(heights.ToArray()).CopyTo(target);
                //     }
                // }

                uint numMaterials = this.Reader.ReadUInt32();

                // Materal weights
                for (int j = 0; j < numMaterials; j++)
                {
                    uint layerIndex = this.Reader.ReadUInt32(); // index starts at 1

                    for (int k = 0; k < numVerts; k++)
                    {
                        this.Reader.ReadSingle(); // Weight
                    }
                }
            }
        }
    }

    // When going from terrain.bin to terrain_ed.bin
    public byte[] CompressDecomp(MemoryStream stream)
    {
        if (this.Terrain == null || this.Terrain?.WorkingFile == null)
            throw new ArgumentNullException("The working file is null.");

        byte[] decompBytes = stream.ToArray();

        long terrainSize = 0;
        if (this.Terrain != null && this.Terrain.WorkingFile != null)
            terrainSize = this.Terrain.WorkingFile.Length; // Large files will always have their compressed data be smaller

        long compLength = 0;
        if (terrainSize < UInt16.MaxValue)
            compLength = UInt16.MaxValue; // For the case of small files having their compressed output be larger.

        // Need to figure out how Kaos works with streams rather than do this.
        if (compLength == 0 && terrainSize < 0x3FFFFFFF)
            compLength = 0x3FFFFFFF; // Roughly 1gb

        if (compLength == 0 && terrainSize < int.MaxValue)
            compLength = int.MaxValue; // Roughly 2gb. Not sure if the game uses uint or int for it's max file size.

        byte[] compBytes = new byte[compLength];

        compLength = LZ4Codec.Encode(decompBytes, compBytes);
        if (compLength < 0)
            throw new OutOfMemoryException("The compressed buffer could not fit the encoded data.");

        Array.Resize(ref compBytes, (int)compLength); // Cut off the extraneous digits
        return compBytes;
    } 

    // Quick test method. Compress an already inflated terrain_ed.bin
    public void CompressDecomp(string outPath)
    {
        string newOutPath = outPath;

        // Sets the path to the scene's folder if we're coming from terrain.bin. Creates one if it doesn't exist
        if (this.XScene != null && this.XScene.SceneName != null)
        {
            newOutPath = Path.Combine(newOutPath, this.XScene.SceneName);
            if (!Directory.Exists(newOutPath))
            {
                Directory.CreateDirectory(newOutPath);
            }
        }

        if (this._stream == null)
            throw new ArgumentNullException("The stream is null.");

        if (this.WorkingFile == null)
            throw new ArgumentNullException("The working file is null.");

        byte[] decompBytes = null!;

        using (this._reader = new BinaryReader(this._stream, Encoding.UTF8, false))
        {
            MemoryStream tempStream = new MemoryStream();
            using (tempStream)
            {
                this._reader.BaseStream.CopyTo(tempStream);
                decompBytes = tempStream.ToArray();
            }
        }

        long compLength = 0;
        if (this.WorkingFile != null)
            compLength = this.WorkingFile.Length; // Large files will always have their compressed data be smaller

        if (compLength < UInt16.MaxValue)
            compLength = UInt16.MaxValue; // For the case of small files having their compressed output be larger.

        if (compLength > int.MaxValue)
            compLength = int.MaxValue;

        byte[] compBytes = new byte[compLength];

        compLength = LZ4Codec.Encode(decompBytes, compBytes);
        if (compLength < 0)
            throw new OutOfMemoryException("The compressed buffer size could not fit the encoded data.");

        Array.Resize(ref compBytes, (int)compLength); // Cut off the extraneous digits
        MemoryStream compStream = new MemoryStream(compBytes.ToArray());

        using (FileStream target = File.Create(Path.Combine(newOutPath + "/terrain_ed_compressed.bin")))
        {
            compStream.CopyTo(target);
        }
    }

    public void Dispose()
    {
        this._stream?.Dispose();
        this._reader?.Dispose();
    }
}