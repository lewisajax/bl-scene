namespace Scene;

public class Program
{
    private static void Main(string[] args)
    {
        string srcPath = null!;
        string outPath = null!;

        if (args.Length > 0)
        {
            // Throws an error if path is invalid
            FileAttributes attr = File.GetAttributes(args[0]);
            if (!attr.HasFlag(FileAttributes.Directory))
                throw new DirectoryNotFoundException("The src folder is not a valid folder.");

            srcPath = args[0];
        } 
        else
        {
            srcPath = Program.PromptDirectory("src");
        }

        if (args.Length > 1)
        {
            FileAttributes attr = File.GetAttributes(args[1]);
            if (!attr.HasFlag(FileAttributes.Directory))
                throw new DirectoryNotFoundException("The output folder is not a valid folder.");

            outPath = args[1];
        } 
        else
        {
            outPath = Program.PromptDirectory("output");
        }

        if (srcPath == null || outPath == null)
            throw new ArgumentNullException("The src or the output path is null");

        // Program.CompTerrainEd(srcPath, outPath);
        // Program.ReadTerrainEd(srcPath, outPath);

        // Check if we're in a scene's folder.
        bool isSceneFolder = Directory.EnumerateFiles(srcPath).Any(x => x == Path.Combine(srcPath, "scene.xscene"));

        // If false, assume we're in a parent folder that contains multiple scene folders
        if (!isSceneFolder)
        {
            Directory.EnumerateDirectories(srcPath).All((string x) =>
            {
                try
                {
                    return Program.CreateTerrainEd(x, outPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex + x);
                    return false;
                }
            });
        }
        else
        {
            Program.CreateTerrainEd(srcPath, outPath);
        }
    }

    private static string PromptDirectory(string name)
    {
        string dirName = "";
        bool hasDir = false;
        Console.WriteLine($"Input the {name} folder path:");
        while (hasDir == false)
        {
            string? input = Console.ReadLine();
            if (input != null)
            {
                try
                {
                    FileAttributes attr = File.GetAttributes(input);
                    if (!attr.HasFlag(FileAttributes.Directory))
                        throw new DirectoryNotFoundException($"The {name} folder is not a valid folder.");

                    dirName = input;
                    hasDir = true;
                }
                catch (Exception ex)
                {
                    if (ex is ArgumentNullException)
                        Console.WriteLine("The input was null");
                    
                    if (ex is UnauthorizedAccessException)
                        Console.WriteLine("Not authorized to access that folder");
                }
            }
        }

        return dirName;
    }

    private static bool CreateTerrainEd(string srcPath, string outPath)
    {
        string xscenePath = Path.Combine(srcPath, "scene.xscene");
        string terrainPath = Path.Combine(srcPath, "terrain.bin");

        bool isSceneFolder = Directory.EnumerateFiles(srcPath).Any(x => x == xscenePath);

        if (!isSceneFolder)
            throw new FileNotFoundException(srcPath + " is not a valid scene folder");

        XScene xscene = new XScene(new FileInfo(xscenePath)).ReadFile();
        Terrain terrain = new Terrain(new FileInfo(terrainPath), xscene).ReadFile();
        TerrainEd terrainEd = new TerrainEd(terrain, xscene);
        return terrainEd.WriteFile(outPath);
    }

    // Quick test methods
    private static void ReadTerrainEd(string srcPath, string outPath)
    {
        string edPath = Path.Combine(srcPath, "terrain_ed.bin");
        TerrainEd terrainEd = new TerrainEd(new FileInfo(edPath));
        terrainEd.ReadFile(outPath);
    }

    private static void CompTerrainEd(string srcPath, string outPath)
    {
        string decompPath = Path.Combine(srcPath, "terrain_ed.bin");
        TerrainEd terrainEd = new TerrainEd(new FileInfo(decompPath));
        terrainEd.CompressDecomp(outPath);
    }
}