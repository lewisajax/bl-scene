using System.Xml;
using JetBrains.Annotations;

namespace Scene;

public class XSceneNode
{
    public int IndX { get; set; }
    public int IndY { get; set; }
    public int SummerMask { get; set; }
    public int FallMask { get; set; }
    public int SpringMask { get; set; }
    public int WinterMask { get; set; }
    public float MinHeight { get; set; }
    public float MaxHeight { get; set; }
    public int HeightRes { get; set; }
    public int NormalRes { get; set; }
    public int[]? WeightLevels { get; set; }
}

public class XScene
{
    [UsedImplicitly]
    private XmlReader _reader = null!;
    private FileStream _stream { get; set; }

    public FileInfo WorkingFile { get; init; }

    public string? SceneName { get; set; }

    public float? MaxTerrainHeight { get; set; }
    public float? MinTerrainHeight { get; set; }

    public int? TerrainX { get; set; }
    public int? TerrainY { get; set; }

    public XSceneNode[]? Nodes { get; set; }

    public XmlReader Reader
    {
        get { return this._reader; }
        private set
        {
            if (value.GetType() != typeof(XmlReader))
                throw new NotSupportedException();

            this._reader = value;
        }
    }

    public XScene(FileInfo file)
    {
        if (!file.Exists)
            throw new FileNotFoundException();

        this.WorkingFile = file;

        FileStream stream = file.Open(FileMode.Open, FileAccess.Read);

        if (stream.CanSeek == false)
            throw new IOException();

        this._stream = stream;
    }

    public XScene ReadFile()
    {
        XmlReaderSettings settings = new XmlReaderSettings();
        using (this._reader = XmlReader.Create(this._stream, settings))
        {
            this._reader.Read(); // file declaration
            this._reader.ReadToFollowing("scene");

            this.SceneName = this._reader.GetAttribute("name");

            this._reader.ReadToDescendant("terrain");

            if (this._reader.HasAttributes)
            {
                if (int.TryParse(this._reader.GetAttribute("node_dimension_x"), out int x) &&
                    int.TryParse(this._reader.GetAttribute("node_dimension_y"), out int y))
                {
                    this.TerrainX = x;
                    this.TerrainY = y;
                }

                if (float.TryParse(this._reader.GetAttribute("min_height"), out float min) &&
                    float.TryParse(this._reader.GetAttribute("max_height"), out float max))
                {
                    this.MinTerrainHeight = min;
                    this.MaxTerrainHeight = max;
                }
            }

            this._reader.ReadToDescendant("nodes");

            if (this.TerrainX != null && this.TerrainY != null)
            {
                int numNodes = (int)(this.TerrainX * this.TerrainY); // Why tf do I need to cast
                this.Nodes = new XSceneNode[numNodes];

                this._reader.ReadToDescendant("node");
                for (int i = 0; i < numNodes; i++)
                {
                    XSceneNode node = new XSceneNode();

                    if (int.TryParse(this._reader.GetAttribute("idx"), out int x) &&
                        int.TryParse(this._reader.GetAttribute("idy"), out int y))
                    {
                        node.IndX = x;
                        node.IndY = y;
                    }

                    if (int.TryParse(this._reader.GetAttribute("layer_is_used_mask_summer"), out int summer) &&
                        int.TryParse(this._reader.GetAttribute("layer_is_used_mask_winter"), out int winter) &&
                        int.TryParse(this._reader.GetAttribute("layer_is_used_mask_spring"), out int spring) &&
                        int.TryParse(this._reader.GetAttribute("layer_is_used_mask_fall"), out int fall))
                    {
                        node.SummerMask = summer;
                        node.WinterMask = winter;
                        node.SpringMask = spring;
                        node.FallMask = fall;
                    }

                    if (float.TryParse(this._reader.GetAttribute("min_height"), out float min) &&
                        float.TryParse(this._reader.GetAttribute("max_height"), out float max))
                    {
                        node.MinHeight = min;
                        node.MaxHeight = max;
                    }

                    XmlReader subReader = this._reader.ReadSubtree();

                    subReader.Read();
                    subReader.ReadToDescendant("texture_levels");
                    subReader.ReadToDescendant("variable");


                    if (subReader.GetAttribute("name") == "heightmap_level" &&
                        int.TryParse(subReader.GetAttribute("value"), out int heightRes))
                    {       
                        node.HeightRes = heightRes;
                    }

                    subReader.ReadToNextSibling("variable");
                    subReader.ReadToNextSibling("variable");
                    
                    if (subReader.GetAttribute("name") == "normalmap_level" &&
                        int.TryParse(subReader.GetAttribute("value"), out int normRes))
                    {       
                        node.NormalRes = normRes;
                    }

                    subReader.ReadToNextSibling("variable");

                    if (subReader.GetAttribute("name") == "weightmap_levels")
                    {
                        node.WeightLevels = new int[16];

                        subReader.ReadToDescendant("variable");
                        for (int j = 0; j < 16; j++)
                        {
                            int.TryParse(subReader.GetAttribute("value"), out int weightRes);
                            node.WeightLevels[j] = weightRes;
                            subReader.ReadToNextSibling("variable");
                        }
                    }

                    subReader.Dispose();
                    subReader.Close();

                    this.Nodes[i] = node;
                    this._reader.ReadToNextSibling("node");
                }
            }
        }

        return this;
    }

    public void Dispose()
    {
        this._stream?.Dispose();
        this._reader?.Dispose();
    }
}