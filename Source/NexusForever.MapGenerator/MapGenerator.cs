using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;
using CommandLine.Text;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Archive;
using NexusForever.Game.Static.Entity;
using NexusForever.GameTable.Model;
using NexusForever.IO.Area;
using NexusForever.MapGenerator.GameTable;
using NexusForever.Shared;
using NLog;

namespace NexusForever.MapGenerator
{
    internal static class MapGenerator
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private static ParserResult<Parameters> parserResult;

        #if DEBUG
        private const string Title = "NexusForever: Map Generator (DEBUG)";
        #else
        private const string Title = "NexusForever: Map Generator (RELEASE)";
        #endif

        private static void Main(string[] args)
        {
            IServiceCollection services = new ServiceCollection();
            services.AddSingleton<ArchiveManager>();
            services.AddSingleton<GameTableManager>();
            services.AddSingleton<ExtractionManager>();
            services.AddSingleton<GenerationManager>();

            LegacyServiceProvider.Provider = services.BuildServiceProvider();

            Console.Title = Title;

            parserResult = Parser.Default.ParseArguments<Parameters>(args);
            parserResult.WithParsed(ParameterOk);

            log.Info("Finished!");
        }

        private static void ParameterOk(Parameters parameters)
        {
            if (!Directory.Exists(parameters.PatchPath))
                throw new DirectoryNotFoundException();

            if (!parameters.Extract && !parameters.Generate 
                && !parameters.DumpWorld && !parameters.ListGrids && !parameters.Hex)
            {
                log.Warn("Please specify the Extract, Generate, DumpWorld, ListGrids, or Hex parameter");
                log.Info(GetHelp());
                return;
            }

            if ((parameters.Extract || parameters.Generate) && !string.IsNullOrEmpty(parameters.OutputDir))
            {
                if (!Directory.Exists(parameters.OutputDir))
                    throw new DirectoryNotFoundException(parameters.OutputDir);
            }

            ArchiveManager.Instance.Initialise(parameters.PatchPath);
            GameTableManager.Instance.Initialise();

            if (parameters.Extract)
                ExtractionManager.Instance.Initialise(parameters.OutputDir);
            if (parameters.Generate)
            {
                GenerationManager.Instance.Initialise(parameters.OutputDir);

                var start = DateTime.UtcNow;
                if (parameters.WorldId.HasValue)
                    GenerationManager.Instance.GenerateWorld(parameters.WorldId.Value, parameters.GridX, parameters.GridY);
                else
                    GenerationManager.Instance.GenerateWorlds(true);

                TimeSpan span = DateTime.UtcNow - start;
                log.Info($"Generated base maps in {span.TotalSeconds}s.");
            }

            if (parameters.DumpWorld)
                DumpWorlds();
            if (parameters.ListGrids && parameters.WorldId.HasValue)
                ListGrids(parameters.WorldId.Value);
            if (parameters.Hex && parameters.WorldId.HasValue && parameters.GridX.HasValue && parameters.GridY.HasValue)
                DumpHex(parameters.WorldId.Value, parameters.GridX.Value, parameters.GridY.Value);
        }

        private static string GetHelp()
        {
            return HelpText.AutoBuild(parserResult, h => h, e => e);
        }

        private static void DumpWorlds()
        {
            log.Info("Dumping all worlds from World.tbl...");
            foreach (var entry in GameTableManager.Instance.World.Entries.OrderBy(e => e.Id))
            {
                string nameId = entry.LocalizedTextIdName > 0
                    ? $"(NameTextId={entry.LocalizedTextIdName})"
                    : "(no name)";
                log.Info($"  WorldId={entry.Id,5}  Flags={entry.Flags,3}  Type={(int)entry.Type}  AssetPath=\"{entry.AssetPath}\"  {nameId}");
            }

            var celestion = GameTableManager.Instance.World.GetEntry(51);
            if (celestion != null)
            {
                string file = Path.Combine("/root/temp", "celestion_worldinfo.txt");
                Directory.CreateDirectory("/root/temp");
                File.WriteAllText(file, "Celestion (world 51):\n"
                    + $"\n  AssetPath             = {celestion.AssetPath}"
                    + $"\n  Flags                  = {celestion.Flags}"
                    + $"\n  Type                   = {celestion.Type}"
                    + $"\n  LocalizedTextIdName    = {celestion.LocalizedTextIdName}"
                    + $"\n  ChunkBounds            = ({celestion.ChunkBounds00}, {celestion.ChunkBounds01}, {celestion.ChunkBounds02}, {celestion.ChunkBounds03})");
                log.Info($"Celestion asset path also written to: {file}");
            }
            else
            {
                log.Warn("World 51 (Celestion) not found in World.tbl!");
            }
        }

        private static void ListGrids(ushort worldId)
        {
            WorldEntry entry = GameTableManager.Instance.World.GetEntry(worldId);
            if (entry == null)
            {
                log.Error($"World {worldId} not found!");
                return;
            }

            log.Info($"Listing grids for World {worldId} (AssetPath={entry.AssetPath})...");

            string asset = entry.AssetPath.Replace('\\', Path.DirectorySeparatorChar);
            string path = $"{asset}/*.*.area";

            var files = ArchiveManager.Instance.MainArchive.IndexFile.GetFiles(path);
            int count = 0;
            string debug = $"[ListGrids] worldId={worldId} asset={asset} pattern={path}\n";
            foreach (IArchiveFileEntry grid in files)
            {
                debug += $"  {grid.FileName}\n";
                count++;
            }
            debug += $"\nTotal: {count} grids\n";
            File.WriteAllText("/root/temp/listgrids_debug.txt", debug);
        }

        private static void DumpHex(ushort worldId, byte gridX, byte gridY)
        {
            const string tempDir = "/root/temp";
            Directory.CreateDirectory(tempDir);

            WorldEntry entry = GameTableManager.Instance.World.GetEntry(worldId);
            if (entry == null)
            {
                log.Error($"World {worldId} not found!");
                return;
            }

            string mapFileAsset = Path.GetFileName(entry.AssetPath.Replace('\\', Path.DirectorySeparatorChar));
            string areaName = $"{mapFileAsset}.{gridX:x2}{gridY:x2}.area";
            string areaPath = $"{entry.AssetPath.Replace('\\', Path.DirectorySeparatorChar)}/{areaName}";
            IArchiveFileEntry gridFile = ArchiveManager.Instance.MainArchive.GetFileInfoByPath(areaPath);

            if (gridFile == null)
            {
                log.Error($"Area file not found: {areaPath}");
                File.WriteAllText("/root/temp/dumphex_debug.txt", $"NOT FOUND: {areaPath}\n");
                return;
            }
            File.WriteAllText("/root/temp/dumphex_debug.txt", $"FOUND: {areaPath} → {gridFile.FileName}\n");

            // Build MapZoneHex flag lookup for this world: (hexX, hexY) -> hexFlags
            var hexFlagLookup = BuildHexFlagLookup(worldId);

            string outPath = Path.Combine(tempDir, $"hexdump_w{worldId}_g{gridX:x2}{gridY:x2}.txt");

            using (Stream stream = ArchiveManager.Instance.MainArchive.OpenFileStream(gridFile))
            using (var reader = new BinaryReader(stream))
            using (var writer = new StreamWriter(outPath))
            {
                writer.WriteLine($"=== AREA: {areaName} ===");
                if (hexFlagLookup != null)
                    writer.WriteLine($"  HexFlagLookup: {hexFlagLookup.Count} unique positions loaded.");
                writer.WriteLine();

                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    long chunkStart = reader.BaseStream.Position;
                    uint chunkMagic = reader.ReadUInt32();
                    uint chunkSize = reader.ReadUInt32();
                    byte[] chunkData = reader.ReadBytes((int)chunkSize);

                    string name = chunkMagic switch
                    {
                        0x44484D4F => "DHMO",
                        0x43484E4B => "CHNK",
                        0x43555254 => "CURT",
                        0x50524F70 => "PROP (top-level)",
                        _ => $"UNKNOWN(0x{chunkMagic:X8})"
                    };

                    writer.WriteLine($"--- Chunk @ 0x{chunkStart:X} ---");
                    writer.WriteLine($"Type: {name}  Size: {chunkSize:N0} bytes");
                    writer.WriteLine();

                    if (chunkMagic == 0x43484E4B) // CHNK
                    {
                        using (var cr = new BinaryReader(new MemoryStream(chunkData)))
                            HexDumpChnk(cr, writer, tempDir, worldId, gridX, gridY, hexFlagLookup);
                    }
                    else if (chunkMagic == 0x50524F70) // top-level PROP
                    {
                        writer.WriteLine("  (top-level PROP stub — already stubbed in Prop.cs)");
                        writer.WriteLine();
                    }
                }
            }

            log.Info($"Hex dump written to: {outPath}");
        }

        /// <summary>
        /// Build a lookup of (hexX, hexY) -> hexFlags for all zones that belong to the given world.
        /// Uses MapZoneWorldJoin to find which MapZoneIds belong to the world, then reads MapZoneHex entries.
        /// Returns null if no flag data is available for this world.
        /// </summary>
        private static Dictionary<(ushort X, ushort Y), uint> BuildHexFlagLookup(ushort worldId)
        {
            // Collect the MapZoneIds that belong to this world via MapZoneWorldJoin
            var zoneIds = new HashSet<uint>();
            foreach (var join in GameTableManager.Instance.MapZoneWorldJoin.Entries)
            {
                if (join.WorldId == worldId)
                    zoneIds.Add(join.MapZoneId);
            }

            if (zoneIds.Count == 0)
            {
                log.Warn($"No MapZoneWorldJoin entries found for world {worldId}; hex flag data unavailable.");
                return null;
            }

            log.Info($"World {worldId}: {zoneIds.Count} MapZoneIds from MapZoneWorldJoin; building hex flag lookup...");

            // --- Diagnostic: dump MapZoneHex entries for world 51 ---
            string hexDiagPath = Path.Combine("/root/temp", "maphz_diag.txt");
            using (var diag = new StreamWriter(hexDiagPath))
            {
                diag.WriteLine($"=== MapZoneHex diagnostic for world {worldId} ===\n");

                // Show zone IDs matched to world with hex bounds
                diag.WriteLine($"MapZoneIds for world {worldId} ({zoneIds.Count} zones):");
                foreach (uint zid in zoneIds.OrderBy(z => z))
                {
                    var ze = GameTableManager.Instance.MapZone.GetEntry(zid);
                    diag.WriteLine($"  MapZone.Id={zid,5}  NameId={ze?.LocalizedTextIdName ?? 0,5}  WorldZoneId={ze?.WorldZoneId,5}  HexMinX={ze?.HexMinX,5}  HexMinY={ze?.HexMinY,5}  HexLimX={ze?.HexLimX,5}  HexLimY={ze?.HexLimY,5}");
                }
                diag.WriteLine();

                // Show all MapZoneHex entries that match
                diag.WriteLine("MapZoneHex entries matching world's zones:");
                int matchCount = 0;
                foreach (var hexEntry in GameTableManager.Instance.MapZoneHex.Entries)
                {
                    if (zoneIds.Contains(hexEntry.MapZoneId))
                    {
                        diag.WriteLine($"  MapZoneId={hexEntry.MapZoneId,5}  Pos0={hexEntry.Pos0,6}  Pos1={hexEntry.Pos1,6}  Flags=0x{hexEntry.Flags:X8}");
                        matchCount++;
                    }
                }
                diag.WriteLine($"\nTotal matching MapZoneHex rows: {matchCount}");

                // Per-zone MapZoneHex summary
                diag.WriteLine("\nMapZoneHex per-zone position ranges:");
                foreach (uint zid in zoneIds.OrderBy(z => z))
                {
                    var zoneEntries = GameTableManager.Instance.MapZoneHex.Entries
                        .Where(h => h.MapZoneId == zid)
                        .ToList();
                    if (zoneEntries.Count > 0)
                    {
                        var minPos0 = zoneEntries.Min(h => h.Pos0);
                        var maxPos0 = zoneEntries.Max(h => h.Pos0);
                        var minPos1 = zoneEntries.Min(h => h.Pos1);
                        var maxPos1 = zoneEntries.Max(h => h.Pos1);
                        diag.WriteLine($"  ZoneId={zid,5}  entries={zoneEntries.Count,4}  Pos0=[{minPos0}-{maxPos0}]  Pos1=[{minPos1}-{maxPos1}]");
                    }
                }

                // Show MapZoneHexGroupEntry data (HexX/HexY) for the same zones
                diag.WriteLine("\nMapZoneHexGroupEntry (HexX/HexY) for world's zones:");
                int groupMatchCount = 0;
                foreach (var groupEntry in GameTableManager.Instance.MapZoneHexGroup.Entries)
                {
                    if (zoneIds.Contains(groupEntry.MapZoneId))
                    {
                        var subEntries = GameTableManager.Instance.MapZoneHexGroupEntry.Entries
                            .Where(s => s.MapZoneHexGroupId == groupEntry.Id)
                            .OrderBy(s => s.HexX);
                        foreach (var sub in subEntries)
                        {
                            diag.WriteLine($"  MapZoneId={groupEntry.MapZoneId,5}  GroupId={groupEntry.Id,5}  HexX={sub.HexX,5}  HexY={sub.HexY,5}");
                            groupMatchCount++;
                        }
                    }
                }
                diag.WriteLine($"\nTotal MapZoneHexGroupEntry rows: {groupMatchCount}");

                // Per-zone MapZoneHexGroupEntry position ranges
                diag.WriteLine("\nMapZoneHexGroupEntry per-zone position ranges:");
                foreach (uint zid in zoneIds.OrderBy(z => z))
                {
                    var groupEntries = GameTableManager.Instance.MapZoneHexGroup.Entries
                        .Where(g => g.MapZoneId == zid)
                        .ToList();
                    if (groupEntries.Count > 0)
                    {
                        var allSubs = new List<MapZoneHexGroupEntryEntry>();
                        foreach (var ge in groupEntries)
                            allSubs.AddRange(GameTableManager.Instance.MapZoneHexGroupEntry.Entries.Where(s => s.MapZoneHexGroupId == ge.Id));

                        if (allSubs.Count > 0)
                        {
                            var minHexX = allSubs.Min(s => s.HexX);
                            var maxHexX = allSubs.Max(s => s.HexX);
                            var minHexY = allSubs.Min(s => s.HexY);
                            var maxHexY = allSubs.Max(s => s.HexY);
                            diag.WriteLine($"  ZoneId={zid,5}  groups={groupEntries.Count,4}  entries={allSubs.Count,5}  HexX=[{minHexX}-{maxHexX}]  HexY=[{minHexY}-{maxHexY}]");
                        }
                    }
                }
            }
            log.Info($"MapZoneHex diagnostic written to: {hexDiagPath}");
            // --- end diagnostic ---

            var lookup = new Dictionary<(ushort X, ushort Y), uint>();
            foreach (var hexEntry in GameTableManager.Instance.MapZoneHex.Entries)
            {
                if (zoneIds.Contains(hexEntry.MapZoneId))
                {
                    var key = ((ushort)hexEntry.Pos0, (ushort)hexEntry.Pos1);
                    // If multiple entries share the same cell, the last one wins
                    lookup[key] = hexEntry.Flags;
                }
            }

            log.Info($"Hex flag lookup: {lookup.Count} unique positions for world {worldId}.");
            return lookup;
        }

        private static void HexDumpChnk(BinaryReader reader, StreamWriter writer, string tempDir, ushort worldId, byte gridX, byte gridY, Dictionary<(ushort X, ushort Y), uint> hexFlagLookup)
        {
            uint lastIndex = 0;

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                long cellStart = reader.BaseStream.Position;
                uint cellInfo = reader.ReadUInt32();
                uint index = (cellInfo >> 24) & 0xFF;
                uint size  = cellInfo & 0x00FFFFFF;

                byte[] cellData = reader.ReadBytes((int)size);
                index += lastIndex;
                lastIndex = index + 1;

                uint cellX = (uint)index % 16;
                uint cellY = (uint)index / 16;

                // Compute world-hex coordinates for this cell (MapZoneHex is in world grid space)
                ushort worldHexX = (ushort)(gridX * 16 + cellX);
                ushort worldHexY = (ushort)(gridY * 16 + cellY);

                // Look up the MapZoneHex flag for this position
                uint hexFlags = 0;
                hexFlagLookup?.TryGetValue((worldHexX, worldHexY), out hexFlags);

                writer.WriteLine($"  [Cell {index,2}] offset=0x{cellStart:X} size={size:N0}  hexPos=({worldHexX},{worldHexY})  hexFlags=0x{hexFlags:X8}");
                writer.WriteLine($"  Flags raw: 0x{cellInfo:X8}");
                writer.WriteLine();

                using (var cr = new BinaryReader(new MemoryStream(cellData)))
                {
                    uint cellFlags = cr.ReadUInt32();
                    writer.WriteLine($"    Flags: 0x{cellFlags:X8}");

                    // known data sizes per bit (from ChnkCell.cs dataSize[])
                    int[] dataSize =
                    {
                        722, 16, 8450, 8450, 8450, 4, 64, 16, 4225, 2178,
                        4,   578, 1,    4624, 2312, 8450, 4096, 2312, 2312, 2312,
                        2312, 1,  16,   16900, 8,   8450, 21316, 4096, 16, 8450, 8450, 2312
                    };

                    bool hasProp = false;

                    for (int i = 0; i < 32; i++)
                    {
                        ChnkCellFlags flag = (ChnkCellFlags)(1u << i);
                        if ((cellFlags & (uint)flag) == 0) continue;

                        string flagName = flag.ToString();
                        int sz = dataSize[i];

                        // is this flag already parsed?
                        bool parsed = flag is ChnkCellFlags.Zone or ChnkCellFlags.HeightMap or ChnkCellFlags.ZoneBound;
                        long pos = cr.BaseStream.Position;

                        if (!parsed)
                        {
                            // hex dump of the raw bytes for this flag
                            byte[] raw = cr.ReadBytes(sz);
                            writer.WriteLine($"    [{flagName}] ({sz} bytes) @ 0x{pos:X}");
                            writer.WriteLine($"    {BitConverter.ToString(raw.Take(Math.Min(64, raw.Length)).ToArray()).Replace('-', ' ')}");
                            if (sz > 64)
                                writer.WriteLine($"    ... ({sz - 64} more bytes)");
                            writer.WriteLine();
                        }
                        else
                        {
                            // already-handled flag, just skip over it
                            writer.WriteLine($"    [{flagName}] (parsed by ChnkCell) — skipping {sz} bytes");
                            cr.BaseStream.Position = pos + sz;
                        }
                    }

                    // now look for cell sub-chunks
                    long remainingStart = cr.BaseStream.Position;
                    writer.WriteLine($"    Remaining cell bytes: {cr.BaseStream.Length - remainingStart}");

                    ushort subIdx = 0;
                    while (cr.BaseStream.Position < cr.BaseStream.Length)
                    {
                        long subPos = cr.BaseStream.Position;
                        uint subMagic = cr.ReadUInt32();
                        uint subSize  = cr.ReadUInt32();
                        byte[] subData = cr.ReadBytes((int)subSize);
                        string subName = subMagic switch
                        {
                            0x77627350 => "WBSP",
                            0x63757244 => "CURD",
                            0x50524F50 => "PROP",
                            0x57417447 => "WATG",
                            _ => $"UNKNOWN(0x{subMagic:X8})"
                        };

                        writer.WriteLine($"    [Sub-chunk {subIdx}] type={subName} size={subSize:N0} @ 0x{subPos:X}");
                        if (subMagic == 0x50524F50) // CellProp
                        {
                            hasProp = true;
                            writer.WriteLine($"    === CellProp ({subSize} bytes) ===");
                            writer.WriteLine(BitConverter.ToString(subData.Take(Math.Min(256, subData.Length)).ToArray()).Replace('-', ' '));
                            if (subSize > 256)
                                writer.WriteLine($"    ... ({subSize - 256} more bytes)");
                            writer.WriteLine();

                            // Also dump per-cell CellProp to its own file for closer analysis
                            string propFile = Path.Combine(tempDir, $"cellprop_w{worldId}_g{gridX:x2}{gridY:x2}_c{index:D2}.bin");
                            File.WriteAllBytes(propFile, subData);
                        }
                        else
                        {
                            writer.WriteLine($"    {BitConverter.ToString(subData.Take(32).ToArray()).Replace('-', ' ')}");
                            if (subData.Length > 32)
                                writer.WriteLine($"    ... ({subData.Length - 32} more bytes)");
                            writer.WriteLine();
                        }

                        subIdx++;
                    }

                    if (!hasProp)
                        writer.WriteLine("    (no CellProp sub-chunk in this cell)");
                    writer.WriteLine();
                }
            }
        }
    }
}
