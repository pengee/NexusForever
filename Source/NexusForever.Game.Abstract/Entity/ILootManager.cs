using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Loot;
using NexusForever.Game.Static.Quest;
using NexusForever.Network.World.Message.Model.Loot;

namespace NexusForever.Game.Abstract.Entity
{
    public interface ILootManager
    {
        void AddLoot(uint ownerGuid, List<LootItem> lootItems);
        List<LootItem> GetLoot(uint ownerGuid);
        void RemoveLoot(uint ownerGuid);
        void VacuumLoot();
        void LootItem(uint ownerGuid, uint lootUnitId);
    }
}