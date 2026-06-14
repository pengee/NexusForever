using CommandLine;

namespace NexusForever.MapGenerator
{
    public class Parameters
    {
        [Option('i', "patchPath", Required = true,
            HelpText = "The location of the WildStar client patch folder.")]
        public string PatchPath { get; set; }

        [Option('e', "extract",
            HelpText = "Extract all game tables (.tbl) and localisation files (.bin).")]
        public bool Extract { get; set; }

        [Option('g', "generate",
            HelpText = "Generate base map files (.nfmap) from the area files.")]
        public bool Generate { get; set; }

        [Option('o', "output",
            HelpText = "The base directory where output files will be created.", Default = "")]
        public string OutputDir { get; set; }

        [Option("debug")]
        public bool Debug { get; set; }

        // generation specific options
        [Option("worldId")]
        public ushort? WorldId { get; set; }

        [Option("gridX")]
        public byte? GridX { get; set; }

        [Option("gridY")]
        public byte? GridY { get; set; }

        // diagnostic / entity export options
        [Option("hex", Default = false,
            HelpText = "Dump hex of CellProp sub-chunks for a single grid cell (use with --worldId/--gridX/--gridY).")]
        public bool Hex { get; set; }

        [Option("dump-world", Default = false,
            HelpText = "Dump all world IDs, names, and asset paths from World.tbl.")]
        public bool DumpWorld { get; set; }

        [Option("list-grids", Default = false,
            HelpText = "List all .area grid files found for a given world (use with --worldId).")]
        public bool ListGrids { get; set; }
    }
}
