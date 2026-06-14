using System.IO;
using Nexus.Archive;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Shared;

namespace NexusForever.MapGenerator.GameTable
{
    public sealed class GameTableManager : Singleton<GameTableManager>
    {
        public GameTable<WorldEntry> World { get; private set; }
        public GameTable<MapZoneEntry> MapZone { get; private set; }
        public GameTable<MapZoneHexEntry> MapZoneHex { get; private set; }
        public GameTable<MapZoneHexGroupEntry> MapZoneHexGroup { get; private set; }
        public GameTable<MapZoneHexGroupEntryEntry> MapZoneHexGroupEntry { get; private set; }
        public GameTable<MapZoneWorldJoinEntry> MapZoneWorldJoin { get; private set; }

        public GameTableManager()
        {
        }

        public void Initialise()
        {
            World = LoadGameTable<WorldEntry>("World.tbl");
            MapZone = LoadGameTable<MapZoneEntry>("MapZone.tbl");
            MapZoneHex = LoadGameTable<MapZoneHexEntry>("MapZoneHex.tbl");
            MapZoneHexGroup = LoadGameTable<MapZoneHexGroupEntry>("MapZoneHexGroup.tbl");
            MapZoneHexGroupEntry = LoadGameTable<MapZoneHexGroupEntryEntry>("MapZoneHexGroupEntry.tbl");
            MapZoneWorldJoin = LoadGameTable<MapZoneWorldJoinEntry>("MapZoneWorldJoin.tbl");
        }

        /// <summary>
        /// Return <see cref="GameTable{T}"/> for supplied table name found in the main client archive.
        /// </summary>
        private GameTable<T> LoadGameTable<T>(string name) where T : class, new()
        {
            string filePath = Path.Combine("DB", name);
            if (!(ArchiveManager.Instance.MainArchive.IndexFile.FindEntry(filePath) is IArchiveFileEntry file))
                throw new FileNotFoundException();

            using (Stream archiveStream = ArchiveManager.Instance.MainArchive.OpenFileStream(file))
            using (var memoryStream = new MemoryStream())
            {
                archiveStream.CopyTo(memoryStream);
                memoryStream.Position = 0;
                return new GameTable<T>(memoryStream);
            }
        }
    }
}
